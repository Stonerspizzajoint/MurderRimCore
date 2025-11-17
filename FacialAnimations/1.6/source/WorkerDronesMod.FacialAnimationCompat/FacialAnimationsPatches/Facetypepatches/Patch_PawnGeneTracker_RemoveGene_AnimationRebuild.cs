using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System;
using FacialAnimation;
using System.Reflection;

namespace MurderRimCore.FacialAnimationCompat
{
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.RemoveGene))]
    public static class FacialAnimationCompat_PawnGeneTracker_RemoveGene_AnimationRebuild_Patch
    {
        static void Postfix(Gene gene, Pawn_GeneTracker __instance)
        {
            var pawn = __instance.pawn;
            if (pawn == null) return;

            // Queue animation dictionary rebuild for this pawn
            FacialAnimationBatcher.QueueAnimationRebuild(pawn);
        }
    }
}



