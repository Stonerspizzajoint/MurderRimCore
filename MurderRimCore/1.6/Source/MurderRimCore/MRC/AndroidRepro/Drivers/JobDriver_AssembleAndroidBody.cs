using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_AssembleAndroidBody : JobDriver
    {
        private const int WorkDurationTicks = 5000;

        private Building_AndroidCreationStation Station => TargetA.Thing as Building_AndroidCreationStation;

        // Remaining requirements tracked during the job
        private int remainingPlasteel;
        private int remainingUranium;
        private int remainingAdvComp;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Station == null) return false;

            // Reserve the station itself
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
                return false;

            // Reserve interaction cell like vanilla DoBill
            if (Station.def.hasInteractionCell &&
                !pawn.ReserveSittableOrSpot(Station.InteractionCell, job, errorOnFailed))
                return false;

            // If a queue exists (created by WorkGiver), reserve those stacks too (best effort)
            if (job.targetQueueB != null)
            {
                pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
                foreach (var targ in job.GetTargetQueue(TargetIndex.B))
                {
                    if (targ.Thing != null)
                        pawn.Map.physicalInteractionReservationManager.Reserve(pawn, job, targ.Thing);
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // End if station disappears or leaves Assembly stage
            AddEndCondition(() =>
            {
                var st = Station;
                if (st == null || !st.Spawned) return JobCondition.Incompletable;

                if (!AndroidFusionRuntime.TryGetProcess(st, out var proc) || proc == null)
                    return JobCondition.Incompletable;

                if (proc.Stage != FusionStage.Assembly)
                    return JobCondition.Incompletable;

                return JobCondition.Ongoing;
            });

            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.A);

            InitRemainingRequirements();

            // Two modes:
            // 1) Queue present (from WorkGiver): consume queue.
            // 2) No queue (e.g. started from float menu): self-collect dynamically per resource.
            bool hasQueue = job.targetQueueB != null && job.targetQueueB.Count > 0;

            if (hasQueue)
            {
                var gotoStationInteraction = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
                yield return Toils_Jump.JumpIf(gotoStationInteraction, RequirementsMet);

                foreach (var toil in ConsumeQueuedIngredientsToils(TargetIndex.B))
                    yield return toil;

                // After queue consumed, head to station and verify all delivered
                yield return gotoStationInteraction;
            }
            else
            {
                // Self-collect: Plasteel -> Uranium -> Advanced components
                foreach (var toil in SelfCollectResourceLoop(
                    needGetter: () => remainingPlasteel,
                    label: "Plasteel",
                    validator: def => def == ThingDefOf.Plasteel,
                    subtract: amt => remainingPlasteel = Math.Max(0, remainingPlasteel - amt)))
                    yield return toil;

                foreach (var toil in SelfCollectResourceLoop(
                    needGetter: () => remainingUranium,
                    label: "Uranium",
                    validator: def => Array.Exists(AndroidFusionRuntime.UraniumDefNames, dn => dn == def.defName),
                    subtract: amt => remainingUranium = Math.Max(0, remainingUranium - amt)))
                    yield return toil;

                foreach (var toil in SelfCollectResourceLoop(
                    needGetter: () => remainingAdvComp,
                    label: "Advanced components",
                    validator: def => Array.Exists(AndroidFusionRuntime.AdvancedComponentDefNames, dn => dn == def.defName),
                    subtract: amt => remainingAdvComp = Math.Max(0, remainingAdvComp - amt)))
                    yield return toil;
            }

            // Final verification before starting work
            yield return new Toil
            {
                initAction = () =>
                {
                    if (!RequirementsMet())
                    {
                        Messages.Message("Assembly failed: insufficient materials delivered inside station.", MessageTypeDefOf.RejectInput);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    job.SetTarget(TargetIndex.C, Station);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            // Work toil with progress bar
            var workToil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = WorkDurationTicks
            };
            workToil.WithProgressBar(TargetIndex.C,
                () => 1f - (float)workToil.actor.jobs.curDriver.ticksLeftThisToil / WorkDurationTicks);
            // Remove or change this effect if VREA_DefOf.ButcherMechanoid is not present in your loadout
            workToil.WithEffect(() => VREA_DefOf.ButcherMechanoid, TargetIndex.C);
            workToil.FailOn(() =>
            {
                // If station or process changes mid-work, abort
                var st = Station;
                if (st == null || st.Destroyed) return true;
                if (!AndroidFusionRuntime.TryGetProcess(st, out var proc) || proc == null) return true;
                if (proc.Stage != FusionStage.Assembly) return true;
                // If someone removed mats mid-work, abort
                if (!RequirementsMet()) return true;
                return false;
            });
            yield return workToil;

            // Finish
            yield return new Toil
            {
                initAction = () =>
                {
                    var st = Station;
                    if (st == null || st.Destroyed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (!AndroidFusionRuntime.TryGetProcess(st, out var proc) ||
                        proc == null ||
                        proc.Stage != FusionStage.Assembly)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (!AndroidFusionRuntime.TryConsumeAssemblyMaterials(st))
                    {
                        Messages.Message("Assembly failed: materials changed or missing inside station.", MessageTypeDefOf.RejectInput);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    AndroidFusionRuntime.SpawnNewbornFromAssembly(proc);
                    EndJobWith(JobCondition.Succeeded);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private void InitRemainingRequirements()
        {
            if (Station == null) return;

            remainingPlasteel = AndroidFusionRuntime.PlasteelReq - AndroidFusionRuntime.CountInFootprint(Station, ThingDefOf.Plasteel);
            remainingUranium = AndroidFusionRuntime.UraniumReq - AndroidFusionRuntime.CountInFootprintFlexible(Station, AndroidFusionRuntime.UraniumDefNames);
            remainingAdvComp = AndroidFusionRuntime.AdvCompReq - AndroidFusionRuntime.CountInFootprintFlexible(Station, AndroidFusionRuntime.AdvancedComponentDefNames);

            if (remainingPlasteel < 0) remainingPlasteel = 0;
            if (remainingUranium < 0) remainingUranium = 0;
            if (remainingAdvComp < 0) remainingAdvComp = 0;

            AndroidFusionRuntime.VerboseAssemblyLog.LogIfTrue(
                $"[FusionAssembly] InitRemaining: P={remainingPlasteel}, U={remainingUranium}, A={remainingAdvComp}");
        }

        private bool RequirementsMet()
        {
            return remainingPlasteel <= 0 &&
                   remainingUranium <= 0 &&
                   remainingAdvComp <= 0;
        }

        // Consume prebuilt queue (from WorkGiver)
        private IEnumerable<Toil> ConsumeQueuedIngredientsToils(TargetIndex ingredientQueueInd)
        {
            var extract = Toils_JobTransforms.ExtractNextTargetFromQueue(ingredientQueueInd);
            yield return extract;

            // Decide how much to take from this queued stack
            var decideNeed = new Toil
            {
                initAction = () =>
                {
                    Thing stack = job.GetTarget(ingredientQueueInd).Thing;
                    if (stack == null)
                    {
                        JumpToToil(extract);
                        return;
                    }

                    int needForDef = NeedForDef(stack.def);
                    if (needForDef <= 0)
                    {
                        JumpToToil(extract);
                        return;
                    }

                    int take = Math.Min(needForDef, stack.stackCount);
                    job.count = take;
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return decideNeed;

            var gotoIng = Toils_Goto.GotoThing(ingredientQueueInd, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(ingredientQueueInd)
                .FailOnSomeonePhysicallyInteracting(ingredientQueueInd);
            yield return gotoIng;

            var takeToil = Toils_Haul.StartCarryThing(ingredientQueueInd,
                                                      subtractNumTakenFromJobCount: false,
                                                      failIfStackCountLessThanJobCount: false);
            yield return takeToil;

            // Go to station interaction cell for adjacency
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Drop strictly inside station footprint
            var drop = new Toil
            {
                initAction = () =>
                {
                    if (pawn.carryTracker.CarriedThing == null)
                        return;

                    if (!TryDropInFootprint(pawn))
                    {
                        Messages.Message("Assembly failed: no free cell inside station footprint.", MessageTypeDefOf.RejectInput);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return drop;

            var adjustRemaining = new Toil
            {
                initAction = () =>
                {
                    Thing lastStack = job.GetTarget(ingredientQueueInd).Thing;
                    if (lastStack != null)
                    {
                        int delivered = job.count;
                        string defName = lastStack.def.defName;
                        if (defName == ThingDefOf.Plasteel.defName)
                            remainingPlasteel = Math.Max(0, remainingPlasteel - delivered);
                        else if (Array.Exists(AndroidFusionRuntime.UraniumDefNames, dn => dn == defName))
                            remainingUranium = Math.Max(0, remainingUranium - delivered);
                        else if (Array.Exists(AndroidFusionRuntime.AdvancedComponentDefNames, dn => dn == defName))
                            remainingAdvComp = Math.Max(0, remainingAdvComp - delivered);
                    }

                    if (RequirementsMet()) return;

                    if (!job.GetTargetQueue(ingredientQueueInd).NullOrEmpty())
                        JumpToToil(extract);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return adjustRemaining;
        }

        // Self-collect a specific resource type until its requirement is satisfied
        private IEnumerable<Toil> SelfCollectResourceLoop(
            Func<int> needGetter,
            string label,
            Predicate<ThingDef> validator,
            Action<int> subtract)
        {
            // Early exit if already satisfied
            if (needGetter() <= 0) yield break;

            // Find next stack
            var findNext = new Toil
            {
                initAction = () =>
                {
                    int remaining = needGetter();
                    if (remaining <= 0)
                    {
                        ReadyForNextToil();
                        return;
                    }

                    Thing next = GenClosest.ClosestThingReachable(
                        pawn.Position,
                        pawn.Map,
                        ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                        PathEndMode.Touch,
                        TraverseParms.For(pawn),
                        maxDistance: 999f,
                        validator: t =>
                            t != null &&
                            t.stackCount > 0 &&
                            t.Spawned &&
                            !t.IsForbidden(pawn) &&
                            validator(t.def) &&
                            pawn.CanReach(t, PathEndMode.Touch, Danger.Some));

                    if (next == null)
                    {
                        Messages.Message($"Assembly failed: no reachable stacks for {label}.", MessageTypeDefOf.RejectInput);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    job.SetTarget(TargetIndex.B, next);
                    int take = Math.Min(remaining, next.stackCount);
                    job.count = take;

                    // Reserve this stack
                    pawn.Reserve(next, job);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return findNext;

            // Go to stack
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            // Take exact count
            yield return Toils_Haul.StartCarryThing(TargetIndex.B,
                                                    subtractNumTakenFromJobCount: false,
                                                    failIfStackCountLessThanJobCount: false);

            // Go to station interaction cell
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Drop strictly inside station footprint
            yield return new Toil
            {
                initAction = () =>
                {
                    if (pawn.carryTracker.CarriedThing == null)
                        return;

                    if (!TryDropInFootprint(pawn))
                    {
                        Messages.Message("Assembly failed: no free cell inside station footprint.", MessageTypeDefOf.RejectInput);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            // Update remaining and loop if still needed
            var checkLoop = new Toil
            {
                initAction = () =>
                {
                    subtract(job.count);
                    if (needGetter() > 0)
                    {
                        // Loop again for the same resource
                        JumpToToil(findNext);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return checkLoop;
        }

        // Map a thing def to its remaining need
        private int NeedForDef(ThingDef def)
        {
            if (def == ThingDefOf.Plasteel) return remainingPlasteel;
            if (Array.Exists(AndroidFusionRuntime.UraniumDefNames, dn => dn == def.defName)) return remainingUranium;
            if (Array.Exists(AndroidFusionRuntime.AdvancedComponentDefNames, dn => dn == def.defName)) return remainingAdvComp;
            return 0;
        }

        // Try to drop the carried thing into one of the building's footprint cells
        private bool TryDropInFootprint(Pawn pawn)
        {
            var map = Station.Map;
            foreach (var c in AndroidFusionRuntime.FootprintCells(Station))
            {
                if (pawn.carryTracker.TryDropCarriedThing(c, ThingPlaceMode.Direct, out _, null))
                    return true;
            }
            return false;
        }
    }
}