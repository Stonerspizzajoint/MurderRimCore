using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.patches
{
    [HarmonyPatch]
    static class Patch_PawnGeneTracker_AddGene
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(Pawn_GeneTracker), "AddGene", new Type[] { typeof(Gene), typeof(bool) });
        }

        static void Postfix(Pawn_GeneTracker __instance, Gene gene, bool addAsXenogene)
        {
            if (gene?.def == null) return;
            Pawn pawn = AccessTools.FieldRefAccess<Pawn_GeneTracker, Pawn>("pawn")(__instance);
            if (pawn == null) return;
            // After a gene is added, ensure any trait that should be forced by genes is present
            TraitGeneUtils.EnsureAllForcedTraitsForPawn(pawn);
        }
    }
}
