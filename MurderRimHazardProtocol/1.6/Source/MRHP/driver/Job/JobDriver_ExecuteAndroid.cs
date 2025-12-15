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
                // Cache references
                Pawn victim = TargetA.Thing as Pawn;
                Map map = pawn.Map;
                Pawn self = pawn;

                if (map != null)
                {
                    MoteMaker.ThrowText(self.DrawPos, map, "INTERRUPTED!", Color.white);
                }

                // 1. TRIGGER COOLDOWN (Fixes the loop)
                TriggerCooldown();

                // 2. Force End
                self.jobs.EndCurrentJob(JobCondition.InterruptForced);

                // 3. Trigger Rage Aggro Switch
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
            tearApart.WithProgressBarToilDelay(TargetIndex.A);

            tearApart.initAction = () =>
            {
                isExecutingPhase = true;

                Pawn victim = TargetA.Thing as Pawn;
                if (victim != null)
                {
                    Hediff pin = victim.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("MRHP_Pinned"));
                    if (pin != null) victim.health.RemoveHediff(pin);
                }

                if (pawn.Map != null)
                {
                    FleckMaker.ThrowMicroSparks(TargetA.Cell.ToVector3Shifted(), pawn.Map);
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "EXECUTING...", Color.red);
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

                if (pawn.IsHashIntervalTick(60))
                {
                    if (pawn.Map != null) MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TEARING", Color.yellow);

                    pawn.Drawer.Notify_MeleeAttackOn(victim);
                    DamageInfo tearDinfo = new DamageInfo(DamageDefOf.Scratch, 1, 0, -1, pawn, null, null);
                    victim.TakeDamage(tearDinfo);

                    SoundDefOf.MetalHitImportant.PlayOneShot(pawn);
                    FleckMaker.ThrowMicroSparks(victim.DrawPos, pawn.Map);
                }
            };

            // SAFETY: If this Toil ends prematurely (Interrupt/Fail), ensure cooldown is set.
            tearApart.AddFinishAction(() =>
            {
                // If we are exiting this toil, but we haven't reached the final kill toil yet...
                // We check if the job was successful or not in the final step, but simpler:
                // If the victim is alive when this toil ends, it usually means we didn't finish.
                // However, normal flow moves to next toil. 
                // So we rely on Notify_DamageTaken for damage interrupts.
                // This is just a fallback for other random interrupts.

                if (pawn.CurJob.def != MRHP_DefOf.MRHP_ExecuteAndroid)
                {
                    // Job changed/ended unexpectedly
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

        // --- HELPER ---
        private void TriggerCooldown()
        {
            if (pawn.MentalState is MentalState_AndroidRage rage)
            {
                rage.lastFailedExecutionTick = Find.TickManager.TicksGame;
            }
        }
    }
}