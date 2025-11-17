using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Genes;
using VREAndroids;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(VEF.Genes.VanillaExpandedFramework_Pawn_HealthTracker_DropBloodSmear_Patch), "TryChangeBloodSmear")]
    public static class MurderRimCore_TryChangeBloodSmear_Patch
    {
        public static bool Prefix(ThingDef thingDef, Pawn pawn, ref ThingDef __result)
        {
            if (pawn?.genes != null &&
                pawn.genes.HasActiveGene(VREA_DefOf.VREA_NeutroCirculation) &&
                pawn.genes.HasActiveGene(MRC_DefOf.MRC_OilBlood))
            {
                Gene oilGene = pawn.genes.GetGene(MRC_DefOf.MRC_OilBlood);
                var ext = oilGene?.def.GetModExtension<GeneExtension>();
                if (ext?.customBloodSmearThingDef != null)
                {
                    __result = ext.customBloodSmearThingDef;
                    return false; // Use oil blood smear, skip original
                }
            }
            return true; // Run original
        }
    }
}

