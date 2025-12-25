using RimWorld;
using HarmonyLib;
using Verse;

namespace MRHP.patches
{
    // This stops the "Rotting" or "Fresh" text from showing in the UI.
    [HarmonyPatch(typeof(CompRottable), "CompInspectStringExtra")]
    public static class CompRottable_CompInspectStringExtra_Patch
    {
        public static bool Prefix(CompRottable __instance, ref string __result)
        {
            if (__instance.parent is Corpse corpse && corpse.InnerPawn.IsRobotic())
            {
                __result = null; // No text.
                return false;    // Skip original.
            }
            return true;
        }
    }
}
