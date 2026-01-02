using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.patches
{
    [HarmonyPatch]
    static class Patch_PawnGeneTracker_RemoveGene
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(Pawn_GeneTracker), "RemoveGene", new Type[] { typeof(Gene) });
        }

        static void Postfix(Pawn_GeneTracker __instance, Gene gene)
        {
            if (gene?.def == null) return;
            Pawn pawn = AccessTools.FieldRefAccess<Pawn_GeneTracker, Pawn>("pawn")(__instance);
            if (pawn == null) return;

            // After a gene is removed, ensure traits are updated (removed if no longer forced)
            TraitGeneUtils.EnsureAllForcedTraitsForPawn(pawn);
        }
    }
}
