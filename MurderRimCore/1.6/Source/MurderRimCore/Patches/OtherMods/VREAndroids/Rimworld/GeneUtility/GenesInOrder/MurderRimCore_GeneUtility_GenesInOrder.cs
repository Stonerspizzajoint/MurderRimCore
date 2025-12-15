using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(RimWorld.GeneUtility), "GenesInOrder", MethodType.Getter)]
    public static class MurderRimCore_GeneUtility_GenesInOrder
    {
        public static void Postfix(ref List<GeneDef> __result)
        {
            // Get all custom Android gene categories
            var androidCategories = DefDatabase<AndroidGeneCategoryDef>.AllDefs.ToHashSet();

            // Remove any genes whose displayCategory is one of our custom categories
            __result.RemoveAll(g => g.displayCategory != null && androidCategories.Contains(g.displayCategory));
        }
    }
}
