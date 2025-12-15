using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MRHP
{
    public class JobDriver_RobotSelfSeal : JobDriver
    {
        // 8 seconds (480 ticks)
        private const int SealDuration = 480;
        private const float TendQuality = 0.4f;

        private Hediff targetHediff;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref targetHediff, "targetHediff");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(pawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.job.SetTarget(TargetIndex.A, pawn);

            Toil findNextWound = new Toil();
            Toil finalizeTend = new Toil();

            // --- STEP 1: Find HIGHEST Bleeding Wound ---
            findNextWound.initAction = () =>
            {
                targetHediff = null;
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                float highestBleedRate = -1f;

                // Scan all wounds to find the worst bleeder
                for (int i = 0; i < hediffs.Count; i++)
                {
                    if (hediffs[i].Bleeding)
                    {
                        float currentBleed = hediffs[i].BleedRate;

                        // If this wound is bleeding faster than our current max, pick it
                        if (currentBleed > highestBleedRate)
                        {
                            highestBleedRate = currentBleed;
                            targetHediff = hediffs[i];
                        }
                        // Fallback: If we haven't picked anything yet, pick this one
                        else if (targetHediff == null)
                        {
                            targetHediff = hediffs[i];
                        }
                    }
                }

                if (targetHediff == null)
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            };
            findNextWound.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findNextWound;

            // --- STEP 2: Repair Action (Wait) ---
            Toil sealWound = Toils_General.Wait(SealDuration, TargetIndex.None);

            sealWound.WithProgressBarToilDelay(TargetIndex.A, false, 0f);

            // CHANGED: Use the traditional "bandaging" rustle sound
            sealWound.PlaySustainerOrSound(SoundDefOf.Interact_Tend);

            yield return sealWound;

            // --- STEP 3: Apply Tend ---
            finalizeTend.initAction = () =>
            {
                if (targetHediff != null && targetHediff.Bleeding)
                {
                    targetHediff.Tended(TendQuality, 1.0f, 0);
                }
            };
            finalizeTend.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalizeTend;

            // --- STEP 4: Loop ---
            yield return Toils_Jump.Jump(findNextWound);
        }
    }
}