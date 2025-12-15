using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VREAndroids;

namespace MurderRimCore
{
    [HarmonyPatch(typeof(PawnGenerator), "GenerateGenes", new[] { typeof(Pawn), typeof(XenotypeDef), typeof(PawnGenerationRequest) })]
    public static class MurderRimCore_Pawn_GeneSkinColorPatch
    {
        // Hardcoded values for hair color variance
        private const float HairDarknessVariance = 0.4f;      // 0 = no darkening, 1 = can be fully black
        private const float HairColorUnchangedChance = 0.2f;   // 0 = always set, 1 = never set (leave vanilla)

        public static void Postfix(Pawn pawn, XenotypeDef xenotype, PawnGenerationRequest request)
        {
            if (pawn == null || pawn.story == null || pawn.def.race?.Humanlike != true || pawn.genes == null)
                return;

            // Check if any gene has a skinColorOverride defined
            bool hasSkinColorOverrideGene = false;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene.def.skinColorOverride != null)
                {
                    hasSkinColorOverrideGene = true;
                    break;
                }
            }

            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                var ext = gene.def.GetModExtension<RandomSkinColorsExtension>();
                if (ext != null && ext.skinColors != null && ext.skinColors.Count > 0)
                {
                    List<Color> colorList = new List<Color>();
                    foreach (var ci in ext.skinColors)
                        colorList.Add(ci.ToColor);

                    // Generate the base color (no saturation)
                    Color baseColor = SkinColorUtils.GetRandomOrMixedColor(colorList, ext.AllowMixing, 0f);

                    // Only match hair color if no skinColorOverride gene is present
                    if (!hasSkinColorOverrideGene && ext.HairColorMatchChance > 0f && Random.value < ext.HairColorMatchChance)
                    {
                        Color? hairColor = SkinColorUtils.GetHairColorWithVariance(baseColor, HairDarknessVariance, HairColorUnchangedChance);
                        if (hairColor.HasValue)
                            pawn.story.HairColor = hairColor.Value;
                        // else: leave hair color unchanged (vanilla)
                    }

                    // Apply to skin color (with saturation)
                    Color chosen = SkinColorUtils.ApplySaturation(baseColor, ext.Saturation);
                    pawn.story.SkinColorBase = chosen;

                    break;
                }
            }
        }
    }
}

