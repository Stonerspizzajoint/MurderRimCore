using RimWorld;
using HarmonyLib;
using Verse;

namespace MRHP.patches
{
    // This stops the rot meter from increasing.
    [HarmonyPatch(typeof(CompRottable), "Active", MethodType.Getter)]
    public static class CompRottable_Active_Patch
    {
        public static bool Prefix(CompRottable __instance, ref bool __result)
        {
            // Check if parent is a Corpse with a Robotic inner pawn
            if (__instance.parent is Corpse corpse && corpse.InnerPawn.IsRobotic())
            {
                __result = false; // "Is Active?" -> No.
                return false;     // Skip original method.
            }
            return true;
        }
    }
}
