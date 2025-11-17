using System;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    // Re-ensure needs after any global add/remove pass.
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "AddOrRemoveNeedsAsAppropriate")]
    public static class EnsureNeeds_AfterAddOrRemove
    {
        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            if (__instance == null) return;

            try
            {
                var pawnField = AccessTools.Field(__instance.GetType(), "pawn");
                if (pawnField == null) return;
                Pawn pawn = pawnField.GetValue(__instance) as Pawn;
                if (pawn == null) return;

                // Only act for androids
                if (!Utils.IsAndroid(pawn))
                    return;

                // Only act for android babies or marked fused newborns (belt-and-suspenders)
                bool isMarked = FusedNewbornMarkerUtil.IsMarkedByHediff(pawn);
                bool isBaby = pawn.DevelopmentalStage == DevelopmentalStage.Baby;

                if (isMarked || isBaby)
                {
                    NeedEnsureUtil.EnsureAndroidBabyNeeds(pawn);
                }
            }
            catch
            {
                // Swallow exceptions; better a silent failure than a cascade of red text.
            }
        }
    }
}