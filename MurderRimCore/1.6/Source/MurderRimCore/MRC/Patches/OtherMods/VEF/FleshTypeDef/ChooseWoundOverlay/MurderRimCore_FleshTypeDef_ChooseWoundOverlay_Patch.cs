using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Genes;
using VREAndroids;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(FleshTypeDef), "ChooseWoundOverlay")]
    public static class MurderRimCore_FleshTypeDef_ChooseWoundOverlay_Patch
    {
        public static void Postfix(Hediff hediff, ref FleshTypeDef.ResolvedWound __result)
        {
            Pawn pawn = hediff?.pawn;
            if (pawn?.genes != null &&
                pawn.genes.HasActiveGene(VREA_DefOf.VREA_NeutroCirculation) &&
                pawn.genes.HasActiveGene(MRC_DefOf.MRC_OilBlood))
            {
                Gene oilGene = pawn.genes.GetGene(MRC_DefOf.MRC_OilBlood);
                var ext = oilGene?.def.GetModExtension<GeneExtension>();
                if (ext?.customWoundsFromFleshtype != null)
                {
                    // Use the same logic as the original postfix, but with the oil gene's flesh type
                    var resolvedWound = VanillaExpandedFramework__FleshTypeDef_ChooseWoundOverlay_Patch.ChooseWoundOverlay(ext.customWoundsFromFleshtype, hediff);
                    if (resolvedWound != null)
                    {
                        __result = resolvedWound;
                    }
                }
            }
        }
    }
}

