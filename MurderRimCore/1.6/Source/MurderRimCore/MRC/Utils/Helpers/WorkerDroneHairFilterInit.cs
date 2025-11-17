using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids; // Utils.IsAwakened
using UnityEngine; // Mathf, Color

namespace MurderRimCore
{
    public static class WorkerDroneHairFilterInit
    {
        public static bool DebugLog = false;

        [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
        public static class WorkerDroneHair_GeneratePawnPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
            {
                if (__result == null) return;
                ApplyHairPolicy(__result, reason: "GeneratePawn");
            }
        }

        public static void ApplyHairPolicy(Pawn pawn, string reason = null)
        {
            try
            {
                if (pawn == null || pawn.story == null || !pawn.RaceProps.Humanlike) return;

                var settings = MRC_Settings.Spawn;
                if (settings == null) return;

                bool isDrone =
                    DroneHelper.IsWorkerDrone(pawn) ||
                    (pawn.kindDef?.defName?.IndexOf("WorkerDrone", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isDrone) return;

                HairDef current = pawn.story.hairDef;
                bool awakened = Utils.IsAwakened(pawn);

                bool forcedBald =
                    (!awakened && settings.noHairForUnawakened) ||
                    (awakened && !settings.hairForAwakened);

                if (DebugLog)
                    Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: awakened={awakened}, forcedBald={forcedBald}, current='{current?.defName ?? "null"}', baldChanceMale={settings.baldChanceMale}, baldChanceFemale={settings.baldChanceFemale}");

                if (forcedBald)
                {
                    ForceBald(pawn, current, reason + " (forced)");
                    // continue to ensure hardhat if configured
                }
                else
                {
                    // Filter removed tags first (only if hair exists)
                    var removedTags = settings.removedHairTags ?? new List<string>();
                    if (current != null && removedTags.Count > 0 && HasRemovedTag(current, removedTags))
                    {
                        var replacement = PickReplacementHair(removedTags);
                        if (replacement != null && replacement != current)
                        {
                            pawn.story.hairDef = replacement;
                            current = replacement;
                            if (DebugLog)
                                Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: removed-tag hair replaced with '{replacement.defName}'.");
                        }
                        else
                        {
                            // If no replacement, enforce Bald
                            ForceBald(pawn, current, reason + " (no replacement)");
                        }
                    }

                    // Random bald chance (only when hair allowed and not forced)
                    float chance = pawn.gender == Gender.Male
                        ? Mathf.Clamp01(settings.baldChanceMale)
                        : Mathf.Clamp01(settings.baldChanceFemale);

                    if (chance > 0f && Rand.Value <= chance)
                    {
                        ForceBald(pawn, current, reason + " (rolled)");
                    }
                }

                // Ensure hardhat for Bald if enabled — run after hair has been finalized
                EnsureHardhatForBald(pawn, reason);
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] ({reason}) Hair enforcement failed for {pawn?.LabelShort ?? "null"}: {e.Message}");
            }
        }

        private static void EnsureHardhatForBald(Pawn pawn, string context)
        {
            try
            {
                var settings = MRC_Settings.Spawn;
                if (settings == null || !settings.alwaysHardHatWhenBald) return;

                var baldDef = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
                if (baldDef == null) return; // nothing we can do

                if (pawn.story?.hairDef != baldDef) return;

                // If pawn already has the hardhat, nothing to do
                if (pawn.apparel == null) return;
                if (pawn.apparel.WornApparel.Any(a => a.def == MRWD.MRWD_DefOf.MRWD_Headgear_Hardhat)) return;

                // Check wearable parts
                if (!ApparelUtility.HasPartsToWear(pawn, MRWD.MRWD_DefOf.MRWD_Headgear_Hardhat)) return;

                // Create and color the hardhat appropriately
                Apparel hardhat = (Apparel)ThingMaker.MakeThing(MRWD.MRWD_DefOf.MRWD_Headgear_Hardhat);

                bool awakened = Utils.IsAwakened(pawn);
                if (!awakened)
                {
                    // Unawakened: always white
                    TryApplyColor(hardhat, Color.white);
                }
                else
                {
                    // Awakened: apply favorite color if configured and rolled
                    if (ModsConfig.IdeologyActive && settings.hardHatUseFavoriteColor)
                    {
                        float favChance = Mathf.Clamp01(settings.favHardHatColorChance);
                        if (Rand.Value <= favChance)
                        {
                            if (!TryGetPawnFavoriteColor_Strict(pawn, out Color fav))
                            {
                                Log.Error($"[MRC] Favorite color unavailable for awakened drone '{pawn?.LabelShort ?? "null"}'. Hard hat left default.");
                            }
                            else
                            {
                                TryApplyColor(hardhat, fav);
                            }
                        }
                    }
                }

                pawn.apparel.Wear(hardhat, false);
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] ({context}) EnsureHardhatForBald failed for {pawn?.LabelShort ?? "null"}: {e.Message}");
            }
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

        private static bool HasRemovedTag(HairDef hair, List<string> removed)
        {
            if (hair?.styleTags == null) return false;
            foreach (var tag in hair.styleTags)
                if (removed.Contains(tag))
                    return true;
            return false;
        }

        private static HairDef PickReplacementHair(List<string> removedTags)
        {
            var candidates = DefDatabase<HairDef>.AllDefsListForReading
                .Where(h => h.styleTags == null || !h.styleTags.Any(t => removedTags.Contains(t)))
                .ToList();

            if (candidates.Count == 0)
                return GetBaldDef(); // If we can't find a candidate, return Bald (may be null)

            return candidates.RandomElement();
        }

        private static void ForceBald(Pawn pawn, HairDef previous, string context)
        {
            var bald = GetBaldDef();
            if (bald != null)
            {
                pawn.story.hairDef = bald;
                if (DebugLog)
                    Log.Message($"[MRC] {context} {pawn.LabelShort}: set Bald (previous='{previous?.defName ?? "null"}').");
            }
            else
            {
                Log.Warning($"[MRC] {context} {pawn.LabelShort}: 'Bald' HairDef not found; keeping '{previous?.defName ?? "null"}'.");
            }
        }

        private static HairDef GetBaldDef() =>
            DefDatabase<HairDef>.GetNamedSilentFail("Bald");
    }
}