using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.Patches
{
    // Zero out opinion offset from face-dependent SITUATIONAL SOCIAL thoughts (RimWorld 1.6+: method, returns float)
    [HarmonyPatch(typeof(Thought_SituationalSocial))]
    [HarmonyPatch(nameof(Thought_SituationalSocial.OpinionOffset))]
    public static class Patch_Thought_SituationalSocial_OpinionOffset
    {
        public static void Postfix(Thought_SituationalSocial __instance, ref float __result)
        {
            if (__result == 0f) return;

            var pawn = __instance.pawn;
            if (pawn == null || !pawn.HasSilhouettePerception()) return;

            var other = __instance.OtherPawn();
            if (other == null || !other.IsSilhouetteTarget()) return;

            if (__instance.def.IsFaceDependent())
                __result = 0f;
        }
    }
}
