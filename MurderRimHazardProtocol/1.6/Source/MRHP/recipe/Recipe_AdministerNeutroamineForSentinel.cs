using RimWorld;
using Verse;
using VREAndroids;

namespace MRHP
{
    public class Recipe_AdministerNeutroamineForSentinel : Recipe_AdministerNeutroamineForAndroid
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return false;

            // 1. Check for OUR FleshType
            if (pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
            {
                // CRITICAL: Return TRUE immediately. 
                // Do NOT call base.AvailableOnNow(), because VRE checks "IsAndroid()" 
                // which often fails for custom robot races defined outside VRE.
                return true;
            }

            return false;
        }
    }
}