using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using Verse.Sound;

namespace MRHP
{
    public class JobDriver_SentinelPounce : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            Toil jump = new Toil();
            jump.initAction = () =>
            {
                if (pawn.Map == null || !TargetA.IsValid) return;

                IntVec3 dest = TargetA.Cell;

                // Create the flyer and launch
                PawnFlyer flyer = PawnFlyer.MakeFlyer(MRHP_DefOf.MRHP_SentinelLeapFlyer, pawn, dest, null, null);
                if (flyer != null)
                {
                    GenSpawn.Spawn(flyer, dest, pawn.Map);

                    // Play sound
                    SoundDef jumpSound = DefDatabase<SoundDef>.GetNamedSilentFail("Longjump_Jump");
                    jumpSound?.PlayOneShot(new TargetInfo(dest, pawn.Map));
                }
            };
            jump.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return jump;
        }
    }
}