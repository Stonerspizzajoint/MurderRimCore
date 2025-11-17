using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids; // Utils.IsAwakened

namespace MurderRimCore
{
    [HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
    public static class MurderRimCore_Pawn_HardhatApparelPatch
    {
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            // Safety checks: only humanlike pawns
            if (pawn == null || pawn.def?.race?.Humanlike != true)
                return;

            var settings = MRC_Settings.Spawn;
            if (settings == null) return;

            // If hardhat def missing, nothing to do
            var hardhatDef = MRWD.MRWD_DefOf.MRWD_Headgear_Hardhat;
            if (hardhatDef == null) return;

            // Determine whether this pawn is a worker drone (or has a kindDef name fallback)
            bool isDrone = DroneHelper.IsWorkerDrone(pawn) ||
                           (pawn.kindDef?.defName?.IndexOf("WorkerDrone", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (!isDrone) return;

            // Check for explicit always-hardhat gene (optional)
            bool hasAlwaysHardhatGene = false;
            if (pawn.genes != null)
            {
                foreach (Gene gene in pawn.genes.GenesListForReading)
                {
                    if (gene.def == MRWD.MRWD_DefOf.MRWD_AlwaysHardhat)
                    {
                        hasAlwaysHardhatGene = true;
                        break;
                    }
                }
            }

            // Base spawn roll
            bool shouldAddHardhat = false;
            if (hasAlwaysHardhatGene)
            {
                shouldAddHardhat = true;
            }
            else
            {
                float hardHatChance = settings?.hardHatSpawnChance ?? 0f;
                if (Rand.Value < hardHatChance)
                    shouldAddHardhat = true;
            }

            // If enabled, always give hardhat to pawns that are Bald (checks final hair state)
            if (!shouldAddHardhat && settings?.alwaysHardHatWhenBald == true)
            {
                var baldDef = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
                if (baldDef != null && pawn.story?.hairDef == baldDef)
                    shouldAddHardhat = true;
            }

            if (!shouldAddHardhat) return;

            // Ensure pawn can wear apparel and doesn't already have one
            if (pawn.apparel == null) return;
            if (!ApparelUtility.HasPartsToWear(pawn, hardhatDef)) return;
            if (pawn.apparel.WornApparel.Any(a => a.def == hardhatDef)) return;

            // Create the hardhat
            Apparel hardhat = (Apparel)ThingMaker.MakeThing(hardhatDef);

            // Color logic:
            // - Unawakened drones: always white
            // - Awakened drones: favoriting color (Ideology + setting + chance); if favorite color not available, log and leave default
            bool awakened = Utils.IsAwakened(pawn);
            if (!awakened)
            {
                TryApplyColor(hardhat, Color.white);
            }
            else
            {
                if (ModsConfig.IdeologyActive && settings.hardHatUseFavoriteColor)
                {
                    float favChance = Mathf.Clamp01(settings.favHardHatColorChance);
                    if (Rand.Value <= favChance)
                    {
                        if (!TryGetPawnFavoriteColor_Strict(pawn, out Color fav))
                        {
                            Log.Error($"[MRC] hardHat favorite-color enabled but favorite color unavailable for pawn '{pawn?.LabelShort ?? "null"}'. Hard hat left default.");
                        }
                        else
                        {
                            TryApplyColor(hardhat, fav);
                        }
                    }
                }
            }

            // Wear it
            pawn.apparel.Wear(hardhat, false);
        }

        private static bool TryApplyColor(Thing thing, Color color)
        {
            var comp = thing.TryGetComp<CompColorable>();
            if (comp != null)
            {
                comp.SetColor(color);
                return true;
            }
            return false;
        }

        // Strict: requires Ideology and a non-null favorite ColorDef on story; no fallback.
        private static bool TryGetPawnFavoriteColor_Strict(Pawn pawn, out Color color)
        {
            color = default;
            if (!ModsConfig.IdeologyActive) return false;

            var story = pawn?.story;
            var colorDef = story?.favoriteColor; // ColorDef on Pawn_StoryTracker in your build
            if (colorDef == null) return false;

            color = colorDef.color;
            return true;
        }
    }
}