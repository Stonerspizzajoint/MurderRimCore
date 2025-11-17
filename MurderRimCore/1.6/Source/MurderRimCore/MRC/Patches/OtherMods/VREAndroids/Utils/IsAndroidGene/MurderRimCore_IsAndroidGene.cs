using VREAndroids;
using HarmonyLib;
using Verse;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(VREAndroids.Utils), nameof(VREAndroids.Utils.IsAndroidGene))]
    public static class MurderRimCore_IsAndroidGene
    {
        static void Postfix(GeneDef geneDef, ref bool __result)
        {
            // If already true, leave as is
            if (__result) return;

            // Consider any gene with your custom category as an android gene
            if (geneDef.displayCategory is AndroidGeneCategoryDef)
            {
                __result = true;
            }
        }
    }
}
