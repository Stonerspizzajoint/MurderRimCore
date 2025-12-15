using HarmonyLib;
using Verse;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(HediffUtility), "CanHealNaturally")]
    public static class Patch_HediffUtility_CanHealNaturally
    {
        // We use Low Priority to ensure we run last (overriding other mods that might say "yes")
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Hediff_Injury hd, ref bool __result)
        {
            // If the pawn is our robot, force natural healing to FALSE
            if (hd.pawn.IsRobotic())
            {
                __result = false;
            }
        }
    }
}