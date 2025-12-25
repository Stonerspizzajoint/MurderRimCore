using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;
using Verse.Sound;

namespace MRHP
{
    public class JobDriver_ExecuteAndroid : JobDriver
    {
        private const int ExecutionDuration = 360;
        private const int DamageThresholdToInterrupt = 5;
        private bool isExecutingPhase = false;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);

            if (isExecutingPhase && dinfo.Amount >= DamageThresholdToInterrupt)
            {
                Pawn victim = TargetA.Thing as Pawn;
                Map map = pawn.Map;
                Pawn self = pawn;

                // KEEP THIS TEXT (Feedback for failure)
                if (map != null)
                {
                    MoteMaker.ThrowText(self.DrawPos, map, "INTERRUPTED!", Color.white);
                }

                TriggerCooldown();
                self.jobs.EndCurrentJob(JobCondition.InterruptForced);
                SentinelAIUtils.TryTriggerRage(self, victim, map);
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                Pawn victim = TargetA.Thing as Pawn;
                return victim == null || !victim.Downed;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil tearApart = new Toil();
            tearApart.defaultCompleteMode = ToilCompleteMode.Delay;
            tearApart.defaultDuration = ExecutionDuration;

            // VISUAL 1: The Progress Bar (UI Indicator)
            tearApart.WithProgressBarToilDelay(TargetIndex.A);

            // AUDIO: Grinding Metal Sound
            tearApart.PlaySustainerOrSound(SoundDefOf.Recipe_ButcherCorpseMechanoid);

            tearApart.initAction = () =>
            {
                isExecutingPhase = true;

                Pawn victim = TargetA.Thing as Pawn;
                if (victim != null)
                {
                    Hediff pin = victim.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("MRHP_Pinned"));
                    if (pin != null) victim.health.RemoveHediff(pin);
                }

                // Initial Burst
                if (pawn.Map != null)
                {
                    FleckMaker.ThrowMicroSparks(TargetA.Cell.ToVector3Shifted(), pawn.Map);
                    // REMOVED: "EXECUTING..." text
                }
            };

            tearApart.tickAction = () =>
            {
                Pawn victim = TargetA.Thing as Pawn;
                if (victim == null || victim.Destroyed)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // Every 1 second (approx)
                if (pawn.IsHashIntervalTick(60))
                {
                    // REMOVED: "TEARING" text

                    // Apply Damage
                    pawn.Drawer.Notify_MeleeAttackOn(victim);
                    DamageInfo tearDinfo = new DamageInfo(DamageDefOf.Scratch, 1, 0, -1, pawn, null, null);
                    victim.TakeDamage(tearDinfo);

                    // Extra Impact Visuals
                    SoundDefOf.MetalHitImportant.PlayOneShot(pawn);
                    FleckMaker.ThrowMicroSparks(victim.DrawPos, pawn.Map);

                    // Add a small shake to the victim to visualize the violence
                    if (victim.Drawer != null && victim.Drawer.renderer != null)
                    {
                        // A slight white flash indicating impact
                        victim.Drawer.Notify_DamageApplied(tearDinfo);
                    }
                }
            };

            tearApart.AddFinishAction(() =>
            {
                if (pawn.CurJob.def != MRHP_DefOf.MRHP_ExecuteAndroid)
                {
                    TriggerCooldown();
                }
            });

            yield return tearApart;

            yield return new Toil
            {
                initAction = () =>
                {
                    isExecutingPhase = false;
                    Pawn victim = TargetA.Thing as Pawn;

                    if (victim != null && !victim.Destroyed && victim.Downed)
                    {
                        SentinelAIUtils.PerformMechanicalExecution(pawn, victim);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private void TriggerCooldown()
        {
            if (pawn.MentalState is MentalState_AndroidRage rage)
            {
                rage.lastFailedExecutionTick = Find.TickManager.TicksGame;
            }
        }
    }
}