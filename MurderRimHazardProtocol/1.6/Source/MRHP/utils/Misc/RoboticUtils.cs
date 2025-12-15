using RimWorld;
using Verse;

namespace MRHP
{
    public static class RoboticUtils
    {
        /// <summary>
        /// Checks if the pawn is a JCJenson Robot based on its FleshType.
        /// </summary>
        public static bool IsRobotic(this Pawn pawn)
        {
            if (pawn == null) return false;

            // Check if the pawn has a race and fleshType defined
            if (pawn.RaceProps == null || pawn.RaceProps.FleshType == null)
                return false;

            // Compare against your custom FleshTypeDef
            return pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh;
        }
    }
}