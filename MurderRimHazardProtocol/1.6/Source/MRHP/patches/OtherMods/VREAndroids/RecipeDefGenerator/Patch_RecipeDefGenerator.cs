using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(RecipeDefGenerator), "DrugAdministerDefs")]
    public static class Patch_RecipeDefGenerator
    {
        public static IEnumerable<RecipeDef> Postfix(IEnumerable<RecipeDef> __result)
        {
            // 1. Pass originals
            foreach (var r in __result) yield return r;

            // 2. DUPLICATE CHECK
            // If we ran this already (e.g. reload or multiple generators), don't add it again.
            string myDefName = "MRHP_Administer_Neutroamine";
            if (DefDatabase<RecipeDef>.GetNamedSilentFail(myDefName) != null)
            {
                // Recipe already exists in DB. Don't yield a new one, just exit.
                yield break;
            }

            ThingDef neutroamine = DefDatabase<ThingDef>.GetNamedSilentFail("Neutroamine");
            if (neutroamine == null) yield break;

            // 3. Create Recipe
            RecipeDef recipe = new RecipeDef();
            recipe.defName = myDefName;
            recipe.label = "administer " + neutroamine.label + " (Robot)";
            recipe.jobString = "administering " + neutroamine.label;
            recipe.workerClass = typeof(Recipe_AdministerNeutroamineForRobotic);
            recipe.targetsBodyPart = false;
            recipe.anesthetize = false;
            recipe.surgerySuccessChanceFactor = 9999f;
            recipe.workAmount = 150f;
            recipe.modContentPack = neutroamine.modContentPack;

            // 4. INGREDIENT BUFFER
            // We set this to 1 so the UI shows "1x Neutroamine" instead of "100x".
            // The Worker class will ask for more if needed.
            IngredientCount ing = new IngredientCount();
            ing.SetBaseCount(1f);
            ing.filter.SetAllow(neutroamine, true);
            recipe.ingredients.Add(ing);
            recipe.fixedIngredientFilter.SetAllow(neutroamine, true);

            // 5. USER DETECTION
            recipe.recipeUsers = new List<ThingDef>();
            foreach (ThingDef td in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (td.race != null && td.race.FleshType != null &&
                    td.race.FleshType.defName == "MRHP_JCJensonRobotFlesh")
                {
                    recipe.recipeUsers.Add(td);
                }
            }

            yield return recipe;
        }
    }
}