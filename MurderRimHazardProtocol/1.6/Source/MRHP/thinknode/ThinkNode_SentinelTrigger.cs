using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MRHP
{
    public class ThinkNode_SentinelTrigger : ThinkNode
    {
        public float searchRadius = 50f;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            // 1. Don't trigger if already raging
            if (pawn.MentalStateDef == MRHP_DefOf.MRHP_AndroidRage)
                return ThinkResult.NoJob;

            // 2. CHECK FREQUENCY
            // Changed from 90 ticks (1.5s) to 10 ticks (0.16s).
            // This is perceptually instant but technically efficient.
            if (!pawn.IsHashIntervalTick(10))
                return ThinkResult.NoJob;

            // 3. FIND VICTIM
            Pawn victim = (Pawn)GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(pawn), searchRadius,
                (Thing t) => {
                    Pawn p = t as Pawn;
                    if (p == null || p.Dead ) return false;

                    // Must be Android
                    if (!Utils.IsAndroid(p)) return false;

                    // Must be Reachable
                    if (!pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly)) return false;

                    // USER REQUEST: "CanSee" check (Line of Sight)
                    // They only trigger if they actually SEE the target.
                    if (!GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map)) return false;
                     
                    return true;
                }
            );

            // 4. TRIGGER RAGE
            if (victim != null)
            {
                bool started = pawn.mindState.mentalStateHandler.TryStartMentalState(
                    MRHP_DefOf.MRHP_AndroidRage,
                    "Android Detected",
                    true,
                    false,
                    false,
                    null,
                    false,
                    false
                );

                if (started)
                {
                    MentalState_AndroidRage rage = pawn.MentalState as MentalState_AndroidRage;
                    if (rage != null) rage.target = victim;

                    // Visual feedback
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "!", UnityEngine.Color.red);
                }
            }

            return ThinkResult.NoJob;
        }
    }
}