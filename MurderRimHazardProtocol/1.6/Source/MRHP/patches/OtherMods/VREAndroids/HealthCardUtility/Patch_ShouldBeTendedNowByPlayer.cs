using HarmonyLib;
using RimWorld;
using Verse;

namespace MRHP.Patches
{
    // 1. STOP DOCTORS FROM TENDING ROBOTS
    [HarmonyPatch(typeof(HealthAIUtility), "ShouldBeTendedNowByPlayer")]
    public static class Patch_ShouldBeTendedNowByPlayer
    {
        [HarmonyPriority(Priority.Last)] // Let other mods run, then override them
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (pawn.IsRobotic())
            {
                __result = false;
            }
        }
    }
}