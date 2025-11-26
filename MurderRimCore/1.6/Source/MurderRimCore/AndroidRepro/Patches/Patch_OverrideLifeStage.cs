using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace MurderRimCore.HarmonyPatches
{
    // We patch the PROPERTY GETTER. 
    // This controls every time the game asks "What stage is this pawn?"
    [HarmonyPatch(typeof(Pawn_AgeTracker), "get_CurLifeStageIndex")]
    public static class Patch_OverrideLifeStage
    {
        public static void Postfix(Pawn_AgeTracker __instance, ref int __result)
        {
            // 1. Safety Checks
            // We need to access the pawn. The AgeTracker has a private 'pawn' field.
            // But 'CurKindLifeStage' or 'pawn' property isn't exposed publicly in the tracker for easy access?
            // WAIT. In the file you sent, 'pawn' is private. 
            // But we can pass the instance to a helper or use reflection ONCE.
            // Actually, let's just use the traverse/reflection safely.

            Pawn p = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            if (p == null || p.health == null) return;

            // 2. The Check
            // Do they have the marker?
            Hediff marker = p.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("MRC_AndroidChildhoodMarker"));

            if (marker != null)
            {
                // 3. The Logic
                // We dictate the stage based on severity.
                float s = marker.Severity;

                if (s >= 1.0f)
                {
                    // Do nothing. Let the original result stand (likely Adult).
                    return;
                }
                else if (s >= 0.70f)
                {
                    __result = 2; // Teen
                }
                else if (s >= 0.35f)
                {
                    __result = 1; // Child
                }
                else
                {
                    __result = 0; // Baby (Pill)
                }
            }
        }
    }
}
