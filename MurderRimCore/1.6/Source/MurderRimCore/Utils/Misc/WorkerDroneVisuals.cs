using System;
using System.Collections.Generic;
using System.Linq; // Required for LINQ
using RimWorld;
using Verse;
using MRWD;

namespace MurderRimCore
{
    public static class WorkerDroneVisuals
    {
        // MAIN ENTRY POINT
        public static void ApplyWorkerDroneVisuals(Pawn pawn)
        {
            if (pawn == null) return;

            var settings = MurderRimCoreMod.SpawnSettings ?? WorkerDroneSettingsDef.Spawn;
            if (settings == null) return;

            // 1. Sanitize Hair (Respects Genes)
            SanitizeHair(pawn);

            // 2. Decide Baldness (Respects Genes)
            bool isNowBald = ApplyBaldness(pawn, settings);

            // 3. Apply Beard (Males only, Respects Genes)
            ApplyBeard(pawn, settings);

            // 4. Apply Helmet (Force if bald, otherwise chance)
            ApplyHelmet(pawn, settings, forceHelmet: isNowBald);
        }

        // --- HAIR LOGIC ---
        public static void SanitizeHair(Pawn pawn)
        {
            if (pawn.story?.hairDef == null || !pawn.RaceProps.Humanlike) return;

            // Step A: Check if current hair is forbidden by Genes
            bool forbiddenByGenes = ModsConfig.BiotechActive && pawn.genes != null && !pawn.genes.StyleItemAllowed(pawn.story.hairDef);

            // Step B: Check if hair is "Shaved" (which we don't want)
            bool isShavedStyle = IsShavedHair(pawn.story.hairDef);

            // If it's valid (Allowed by genes AND not shaved), do nothing.
            if (!forbiddenByGenes && !isShavedStyle) return;

            // === FIND REPLACEMENT ===
            var allHair = DefDatabase<HairDef>.AllDefsListForReading;

            // Filter: Must NOT be shaved style
            var validHair = allHair.Where(h => !IsShavedHair(h));

            // Filter: Must be allowed by Genes (if Biotech active)
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                validHair = validHair.Where(h => pawn.genes.StyleItemAllowed(h));
            }

            if (validHair.TryRandomElement(out HairDef newHair))
            {
                pawn.story.hairDef = newHair;
            }
            else
            {
                // Fallback: Bald (Only if genes allow it)
                var bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
                if (bald != null)
                {
                    // Only apply bald fallback if genes allow baldness
                    if (!ModsConfig.BiotechActive || pawn.genes == null || pawn.genes.StyleItemAllowed(bald))
                    {
                        pawn.story.hairDef = bald;
                    }
                }
            }
        }

        public static bool ApplyBaldness(Pawn pawn, WorkerDroneSpawnModSettings s)
        {
            if (pawn.story == null || !pawn.RaceProps.Humanlike) return false;

            // 1. Roll for Baldness
            bool makeBald = false;
            if (pawn.gender == Gender.Male && Rand.Value < s.baldChanceMale) makeBald = true;
            else if (pawn.gender == Gender.Female && Rand.Value < s.baldChanceFemale) makeBald = true;

            if (!makeBald) return false;

            // 2. Validate against Genes
            var bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
            if (bald != null)
            {
                // If genes exist, check if "Bald" is an allowed style.
                // (Some genes might force "Long Hair" tags only)
                if (ModsConfig.BiotechActive && pawn.genes != null)
                {
                    if (!pawn.genes.StyleItemAllowed(bald))
                    {
                        return false; // Gene forbids baldness, abort.
                    }
                }

                pawn.story.hairDef = bald;
                return true;
            }
            return false;
        }

        // --- BEARD LOGIC ---
        public static void ApplyBeard(Pawn pawn, WorkerDroneSpawnModSettings s)
        {
            if (pawn.style == null || !pawn.RaceProps.Humanlike) return;

            // 1. Females: No beard
            if (pawn.gender == Gender.Female)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            // 2. Settings check
            if (!s.allowBeards || Rand.Value >= s.beardSpawnChance)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            // 3. Prepare List
            var allBeards = DefDatabase<BeardDef>.AllDefsListForReading;
            IEnumerable<BeardDef> candidates;

            if (s.mustachesOnlyWhenBeardsAllowed)
            {
                // Filter: Mustaches only + Not Shaved
                candidates = allBeards.Where(b => b.styleTags != null && b.styleTags.Contains("MoustacheOnly") && !IsShavedBeard(b));
            }
            else
            {
                // Filter: All valid beards + Not Shaved
                candidates = allBeards.Where(b => !IsShavedBeard(b));
            }

            // 4. GENE FILTER (The Critical Update)
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                // Remove any beard style that the genes do not allow.
                // If the pawn has "Beardless" gene, this will filter everything except NoBeard.
                candidates = candidates.Where(b => pawn.genes.StyleItemAllowed(b));
            }

            // 5. Select
            if (candidates.TryRandomElement(out BeardDef chosen))
            {
                pawn.style.beardDef = chosen;
            }
            else
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
            }
        }

        // --- HELMET LOGIC ---
        public static void ApplyHelmet(Pawn pawn, WorkerDroneSpawnModSettings s, bool forceHelmet)
        {
            if (pawn.apparel == null) return;

            if (!forceHelmet && Rand.Value >= s.baseHelmetSpawnChance) return;

            var helmetDef = MRWD_DefOf.MRWD_Headgear_Hardhat;
            if (helmetDef == null) return;

            // Don't duplicate
            if (pawn.apparel.WornApparel.Any(w => w.def == helmetDef)) return;

            // Remove conflicting
            for (int i = pawn.apparel.WornApparel.Count - 1; i >= 0; i--)
            {
                Apparel existing = pawn.apparel.WornApparel[i];
                if (!ApparelUtility.CanWearTogether(helmetDef, existing.def, pawn.RaceProps.body))
                {
                    pawn.apparel.Remove(existing);
                    existing.Destroy();
                }
            }

            Apparel helmet = (Apparel)ThingMaker.MakeThing(helmetDef);

            // Color Logic
            if (ModsConfig.IdeologyActive && s.matchFavoriteColorWhenIdeology && pawn.story?.favoriteColor != null)
            {
                if (Rand.Value < s.favoriteColorHelmetChance)
                {
                    helmet.SetColor(pawn.story.favoriteColor.color);
                }
            }

            pawn.apparel.Wear(helmet, false, false);
        }

        // --- HELPERS ---
        private static bool IsShavedHair(HairDef h)
        {
            return h?.styleTags?.Contains("Shaved") ?? false;
        }

        private static bool IsShavedBeard(BeardDef b)
        {
            return b?.styleTags?.Contains("Shaved") ?? false;
        }
    }
}