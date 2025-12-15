using RimWorld;
using Verse;
using Verse.AI;
using System;
using VREAndroids;

namespace MRHP
{
    public class JobGiver_SentinelFinishOff : ThinkNode_JobGiver
    {
        public float searchRadius = 30f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Downed || !pawn.Awake() || pawn.Map == null) return null;

            Predicate<Thing> validator = (Thing t) =>
            {
                Pawn victim = t as Pawn;
                if (victim == null) return false;
                if (!Utils.IsAndroid(victim)) return false;

                bool isPinned = victim.health.hediffSet.HasHediff(HediffDef.Named("MRHP_Pinned"));
                if (!victim.Downed && !isPinned) return false;

                if (victim.Faction == pawn.Faction) return false;
                if (pawn.Faction != null && pawn.Faction.IsPlayer && !pawn.HostileTo(victim)) return false;
                if (!pawn.CanReserve(victim)) return false;

                // CROWD CONTROL CHECK
                if (SentinelAIUtils.IsTargetOvercrowded(victim, pawn)) return false;

                return true;
            };

            Pawn victimToKill = (Pawn)GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(pawn), searchRadius, validator
            );

            if (victimToKill != null)
            {
                bool hasPinHediff = victimToKill.health.hediffSet.HasHediff(HediffDef.Named("MRHP_Pinned"));

                if (hasPinHediff)
                {
                    if (victimToKill.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.2f)
                    {
                        if (!SentinelAIUtils.IsSomeoneExecuting(victimToKill, pawn))
                            return JobMaker.MakeJob(MRHP_DefOf.MRHP_ExecuteAndroid, victimToKill);
                    }

                    if (pawn.Position.DistanceTo(victimToKill.Position) < 2.5f)
                    {
                        JobDef maulDef = DefDatabase<JobDef>.GetNamedSilentFail("MRHP_SentinelMaul");
                        if (maulDef != null) return JobMaker.MakeJob(maulDef, victimToKill);
                    }
                    else
                    {
                        return JobMaker.MakeJob(JobDefOf.Goto, victimToKill);
                    }
                }
                else if (victimToKill.Downed)
                {
                    if (!SentinelAIUtils.IsSomeoneExecuting(victimToKill, pawn))
                        return JobMaker.MakeJob(MRHP_DefOf.MRHP_ExecuteAndroid, victimToKill);
                }
            }

            return null;
        }
    }
}