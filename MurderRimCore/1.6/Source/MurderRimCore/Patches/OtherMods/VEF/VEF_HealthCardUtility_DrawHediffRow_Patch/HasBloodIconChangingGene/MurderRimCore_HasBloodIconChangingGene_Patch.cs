using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Genes;
using VREAndroids;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(VEF.Genes.VanillaExpandedFramework_HealthCardUtility_DrawHediffRow_Patch), "HasBloodIconChangingGene")]
    public static class MurderRimCore_HasBloodIconChangingGene_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn?.genes != null &&
                pawn.genes.HasActiveGene(VREA_DefOf.VREA_NeutroCirculation) &&
                pawn.genes.HasActiveGene(MRC_DefOf.MRC_OilBlood))
            {
                Gene oilGene = pawn.genes.GetGene(MRC_DefOf.MRC_OilBlood);
                var ext = oilGene?.def.GetModExtension<GeneExtension>();
                if (!string.IsNullOrEmpty(ext?.customBloodIcon))
                {
                    VEF.Genes.VanillaExpandedFramework_HealthCardUtility_DrawHediffRow_Patch.bloodIcon = ext.customBloodIcon;
                    __result = true;
                    return false; // Skip original, use oil gene's icon
                }
            }
            return true; // Run original
        }
    }
}

