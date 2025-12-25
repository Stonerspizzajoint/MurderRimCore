using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;

namespace MRHP
{
    public class JobDriver_SentinelMaul : JobDriver
    {
        private float cumulativeDamage = 0f;
        private const int DamageThresholdToInterrupt = 30;
        private bool isMaulingPhase = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cumulativeDamage, "cumulativeDamage", 0f);
            Scribe_Values.Look(ref isMaulingPhase, "isMaulingPhase", false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            if (isMaulingPhase)
            {
                bool staggered = SentinelAIUtils.CheckStaggerOnDamage(
                    pawn,
                    cumulativeDamage,
                    dinfo.Amount,
                    DamageThresholdToInterrupt
                );

                if (!staggered)
                {
                    cumulativeDamage += dinfo.Amount;
                }
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Check for Pin Hediff
            this.FailOn(() => {
                Pawn victim = TargetA.Thing as Pawn;
                return victim == null || !victim.health.hediffSet.HasHediff(HediffDef.Named("MRHP_Pinned"));
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil maul = new Toil();
            maul.defaultCompleteMode = ToilCompleteMode.Never;

            // OPTIONAL: Add a subtle effect to show activity if desired
            // maul.WithEffect(EffecterDefOf.MeatExplosion, TargetIndex.A); // Too messy?

            maul.initAction = () =>
            {
                isMaulingPhase = true;
                cumulativeDamage = 0f;
                // Text removed
            };

            maul.tickAction = () =>
            {
                Pawn victim = TargetA.Thing as Pawn;
                SentinelAIUtils.PerformMaulAttack(pawn, victim);
            };

            yield return maul;
        }
    }
}