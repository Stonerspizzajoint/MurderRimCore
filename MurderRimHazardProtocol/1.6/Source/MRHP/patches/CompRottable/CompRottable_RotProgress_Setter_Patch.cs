using RimWorld;
using HarmonyLib;
using Verse;

namespace MRHP.patches
{
    // Safety check to ensure Stage never switches from Fresh to Rotting.
    [HarmonyPatch(typeof(CompRottable), "RotProgress", MethodType.Setter)]
    public static class CompRottable_RotProgress_Setter_Patch
    {
        public static bool Prefix(CompRottable __instance)
        {
            if (__instance.parent is Corpse corpse && corpse.InnerPawn.IsRobotic())
            {
                return false; // Prevent setting the rot progress.
            }
            // Also check if attached directly to a Pawn (rare, but possible in some mods)
            if (__instance.parent is Pawn pawn && pawn.IsRobotic())
            {
                return false;
            }
            return true;
        }
    }
}
