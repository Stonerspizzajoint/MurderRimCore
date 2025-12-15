using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MRHP
{
    public class JobGiver_RobotSelfSeal : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Downed || pawn.Drafted || pawn.InAggroMentalState) return null;
            if (pawn.Map == null) return null;

            if (PawnUtility.EnemiesAreNearby(pawn, 15, true)) return null;

            // Bleeding Check
            bool isBleeding = false;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && injury.Bleeding)
                {
                    isBleeding = true;
                    break;
                }
            }

            if (!isBleeding) return null;

            // CAPACITY CHECK: Re-enabled!
            // Requires Manipulation.
            // Since "MRHP_SerratedMaw" has the manipulation tag, they can still do this 
            // even if both arms are destroyed, as long as the head is intact!
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return null;
            }

            return JobMaker.MakeJob(MRHP_DefOf.MRHP_RobotSelfSeal, pawn);
        }
    }
}