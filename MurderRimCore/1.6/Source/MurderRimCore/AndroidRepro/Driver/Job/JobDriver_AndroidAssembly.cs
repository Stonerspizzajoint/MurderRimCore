using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_AndroidAssembly : JobDriver
    {
        private const int WorkDuration = 2500;
        private CompAndroidReproduction Station => TargetA.Thing.TryGetComp<CompAndroidReproduction>();

        // Standard 8-way offsets for finding drop spots
        private static readonly IntVec3[] Offsets8Way = new IntVec3[]
        {
            new IntVec3(0, 0, 1), new IntVec3(0, 0, -1), new IntVec3(1, 0, 0), new IntVec3(-1, 0, 0),
            new IntVec3(1, 0, 1), new IntVec3(1, 0, -1), new IntVec3(-1, 0, 1), new IntVec3(-1, 0, -1)
        };

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed)) return false;
            if (job.targetQueueB != null)
            {
                pawn.ReserveAsManyAsPossible(job.targetQueueB, job);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Station == null || !Station.ReadyForAssembly);

            // 1. Init
            yield return Toils_General.DoAtomic(() => { });

            // 2. Fetch Loop (Get Ingredients)
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);
            yield return extract;

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true);

            // Find drop spot logic (Inside Station)
            Toil findDropSpot = Toils_General.DoAtomic(() =>
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried == null) return;

                IntVec3 interactCell = TargetA.Thing.InteractionCell;
                CellRect stationRect = TargetA.Thing.OccupiedRect();
                IntVec3 bestSpot = IntVec3.Invalid;
                bool foundStack = false;

                foreach (IntVec3 offset in Offsets8Way)
                {
                    IntVec3 c = interactCell + offset;
                    if (!stationRect.Contains(c)) continue;

                    List<Thing> things = c.GetThingList(pawn.Map);
                    bool blocked = false;
                    bool sameStack = false;

                    foreach (Thing t in things)
                    {
                        if (t.def.category == ThingCategory.Item)
                        {
                            if (t.def == carried.def && t.stackCount < t.def.stackLimit)
                                sameStack = true;
                            else
                                blocked = true;
                        }
                        if (t.def.passability == Traversability.Impassable && t != TargetA.Thing)
                            blocked = true;
                    }

                    if (sameStack)
                    {
                        bestSpot = c;
                        foundStack = true;
                        break;
                    }
                    if (!blocked && !foundStack) bestSpot = c;
                }

                if (bestSpot.IsValid) job.targetC = bestSpot;
                else job.targetC = interactCell;
            });
            yield return findDropSpot;

            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, null, false);
            yield return Toils_Jump.JumpIf(extract, () => !job.targetQueueB.NullOrEmpty());

            // ------------------------------------------------------------
            // 3. MOVEMENT: Go BACK to Interaction Cell before crafting
            // ------------------------------------------------------------
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // 4. Assembly Work
            Toil assemble = Toils_General.Wait(WorkDuration, TargetIndex.A);
            assemble.WithProgressBarToilDelay(TargetIndex.A);
            assemble.activeSkill = () => SkillDefOf.Crafting;

            // --- SOUND UPDATE: Use Recipe_Machining ---
            assemble.PlaySustainerOrSound(SoundDef.Named("Recipe_Machining"));

            assemble.tickAction = () =>
            {
                Station?.Notify_Working();

                // Kept the sparks for visual flair, removed the manual sound clicks 
                // since the sustainer handles the audio now.
                if (pawn.IsHashIntervalTick(60))
                    FleckMaker.ThrowMicroSparks(TargetA.Thing.Position.ToVector3Shifted(), pawn.Map);
            };
            yield return assemble;

            // 5. Finish
            yield return new Toil
            {
                initAction = () =>
                {
                    Station.FinishAssembly(pawn);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}