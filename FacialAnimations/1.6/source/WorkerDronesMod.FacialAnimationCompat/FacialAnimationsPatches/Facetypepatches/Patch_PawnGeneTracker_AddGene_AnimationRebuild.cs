using HarmonyLib;
using Verse;
using RimWorld;
using FacialAnimation;

namespace MurderRimCore.FacialAnimationCompat
{
    [HarmonyPatch(typeof(Pawn_GeneTracker), "AddGene", new[] { typeof(Gene), typeof(bool) })]
    public static class FacialAnimationCompat_PawnGeneTracker_AddGene_ForceTypes_Patch
    {
        static void Postfix(Gene __result, Pawn_GeneTracker __instance)
        {
            if (__result == null) return;
            var pawn = __instance.pawn;
            if (pawn == null) return;

            // Queue animation dictionary rebuild for this pawn
            FacialAnimationBatcher.QueueAnimationRebuild(pawn);

            // Reload eye graphics
            FacialAnimationGeneUtil.SafeReload(pawn.GetComp<EyeballControllerComp>());
        }
    }
}
