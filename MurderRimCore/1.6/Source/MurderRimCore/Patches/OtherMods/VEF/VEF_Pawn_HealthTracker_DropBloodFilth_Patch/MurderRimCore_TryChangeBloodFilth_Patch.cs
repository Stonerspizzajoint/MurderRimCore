using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Genes;
using VREAndroids;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(VEF.Genes.VanillaExpandedFramework_Pawn_HealthTracker_DropBloodFilth_Patch), "TryChangeBloodFilth")]
    public static class MurderRimCore_TryChangeBloodFilth_Patch
    {
        public static bool Prefix(ThingDef thingDef, Pawn pawn, ref ThingDef __result)
        {
            if (pawn?.genes != null &&
                pawn.genes.HasActiveGene(VREA_DefOf.VREA_NeutroCirculation) &&
                pawn.genes.HasActiveGene(MRC_DefOf.MRC_OilBlood))
            {
                Gene oilGene = pawn.genes.GetGene(MRC_DefOf.MRC_OilBlood);
                var ext = oilGene?.def.GetModExtension<GeneExtension>();
                if (ext?.customBloodThingDef != null)
                {
                    __result = ext.customBloodThingDef;
                    return false; // Skip original, use oil blood
                }
            }
            return true; // Run original
        }
    }
}

