using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Genes;
using VREAndroids;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(VEF.Genes.VanillaExpandedFramework__DamageWorker_AddInjury_ApplyToPawn_Patch), "GetEffecterDef")]
    public static class MurderRimCore_GetEffecterDef_Patch
    {
        public static bool Prefix(EffecterDef effecterDef, Pawn curPawn, ref EffecterDef __result)
        {
            if (curPawn?.genes != null &&
                curPawn.genes.HasActiveGene(VREA_DefOf.VREA_NeutroCirculation) &&
                curPawn.genes.HasActiveGene(MRC_DefOf.MRC_OilBlood))
            {
                Gene oilGene = curPawn.genes.GetGene(MRC_DefOf.MRC_OilBlood);
                var ext = oilGene?.def.GetModExtension<GeneExtension>();
                if (ext?.customBloodEffect != null)
                {
                    __result = ext.customBloodEffect;
                    return false; // Use oil blood effect, skip original
                }
            }
            return true; // Run original
        }
    }
}

