using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using VREAndroids;

namespace MurderRimCore.MRWD.Patches
{
    [HarmonyPatch(typeof(Utils), nameof(Utils.TryAssignBackstory), new System.Type[] { typeof(Pawn), typeof(string) })]
    public static class Utils_TryAssignBackstory_ColonyAwakeningEnforced
    {
        public static bool DebugLog = false;

        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn pawn, ref string spawnCategory)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
                return true;

            bool isDrone = DroneHelper.IsWorkerDrone(pawn);
            bool isAndroid = IsAndroidSafe(pawn);
            if (!isDrone && !isAndroid)
                return true;

            bool awakened = IsAwakenedSafe(pawn);
            var settingsDef = WorkerDroneSettingsDef.Current;
            var settings = settingsDef?.BackstorySettings;

            if (!awakened)
            {
                if (isDrone)
                {
                    string colonyCat = settings?.colonyCreatedCategory ?? "ColonyDrone";
                    return AssignCategoryFirst(pawn, ref spawnCategory, new[] { colonyCat }, settings);
                }
                else
                {
                    return AssignCategoryFirst(pawn, ref spawnCategory, new[] { "ColonyAndroid" }, settings);
                }
            }

            if (isDrone)
            {
                List<string> allowed = new List<string> { "WorkerDrone" };

                var kindCats = pawn.kindDef?.backstoryCategories;
                if (kindCats != null && kindCats.Count > 0)
                {
                    var gated = settings?.pawnkindGatedCategories;
                    if (gated != null && gated.Count > 0)
                    {
                        for (int i = 0; i < gated.Count; i++)
                        {
                            string cat = gated[i];
                            if (kindCats.Contains(cat))
                                allowed.Add(cat);
                        }
                    }
                }
                else
                {
                    var gated = settings?.factionGatedCategories;
                    if (gated != null && gated.Count > 0)
                    {
                        for (int i = 0; i < gated.Count; i++)
                        {
                            string cat = gated[i];
                            if (BackstoryCategoryResolver.FactionAllowsCategory(pawn, cat))
                                allowed.Add(cat);
                        }
                    }
                }

                return AssignCategoryFirst(pawn, ref spawnCategory, allowed, settings);
            }
            else
            {
                return AssignCategoryFirst(pawn, ref spawnCategory, new[] { "AwakenedAndroid" }, settings);
            }
        }

        // CATEGORY-FIRST selection with an option to prefer unique-to-category backstories.
        private static bool AssignCategoryFirst(Pawn pawn, ref string spawnCategory, IEnumerable<string> allowedCatsEnum, WorkerDroneBackstorySettings settings)
        {
            var allowedCats = allowedCatsEnum?.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            if (allowedCats == null || allowedCats.Count == 0) return true;

            // Build per-category candidate lists (childhood only)
            Dictionary<string, List<BackstoryDef>> byCat = new Dictionary<string, List<BackstoryDef>>(allowedCats.Count);
            for (int i = 0; i < allowedCats.Count; i++)
                byCat[allowedCats[i]] = new List<BackstoryDef>(16);

            var all = DefDatabase<BackstoryDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.slot != BackstorySlot.Childhood || b.spawnCategories == null) continue;
                for (int c = 0; c < allowedCats.Count; c++)
                {
                    string cat = allowedCats[c];
                    if (b.spawnCategories.Contains(cat))
                        byCat[cat].Add(b);
                }
            }

            // Remove empty categories
            var catsWithAny = allowedCats.Where(cat => byCat.TryGetValue(cat, out var list) && list.Count > 0).ToList();
            if (catsWithAny.Count == 0) return true;

            // Roll category by multiplier
            float totalCatW = 0f;
            float[] catWeights = new float[catsWithAny.Count];
            for (int i = 0; i < catsWithAny.Count; i++)
            {
                string cat = catsWithAny[i];
                float w = settings?.GetCategoryMultiplier(cat, 1f) ?? 1f;
                if (w < 0f) w = 0f;
                catWeights[i] = w;
                totalCatW += w;
            }
            if (totalCatW <= 0f) return true;

            float rollCat = Rand.Value * totalCatW;
            float accCat = 0f;
            int chosenCatIndex = catsWithAny.Count - 1;
            for (int i = 0; i < catsWithAny.Count; i++)
            {
                accCat += catWeights[i];
                if (rollCat <= accCat) { chosenCatIndex = i; break; }
            }
            string chosenCat = catsWithAny[chosenCatIndex];
            spawnCategory = chosenCat;

            // Prefer backstories unique to the chosen category if requested
            var candidates = byCat[chosenCat];
            if (settings?.exclusiveChosenCategory == true)
            {
                var unique = candidates.Where(bs =>
                    bs.spawnCategories != null &&
                    bs.spawnCategories.Contains(chosenCat) &&
                    !bs.spawnCategories.Any(other => other != chosenCat && allowedCats.Contains(other))
                ).ToList();

                if (unique.Count > 0)
                    candidates = unique;
            }

            // Weight by per-backstory commonality
            List<(BackstoryDef def, float w)> weighted = new List<(BackstoryDef, float)>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var bs = candidates[i];
                float w = 1f;
                var ext = bs.GetModExtension<BackstoryExtension>();
                if (ext != null) w *= ext.commonality;
                if (w < 0f) w = 0f;
                if (w > 0f) weighted.Add((bs, w));
            }
            if (weighted.Count == 0)
            {
                for (int i = 0; i < candidates.Count; i++)
                    weighted.Add((candidates[i], 1f));
            }

            float total = 0f;
            for (int i = 0; i < weighted.Count; i++) total += weighted[i].w;
            if (total <= 0f) return true;

            float roll = Rand.Value * total;
            float acc = 0f;
            BackstoryDef chosen = weighted[weighted.Count - 1].def;
            for (int i = 0; i < weighted.Count; i++)
            {
                acc += weighted[i].w;
                if (roll <= acc) { chosen = weighted[i].def; break; }
            }

            pawn.story.Childhood = chosen;

            if ((settings?.applyForcedTraits ?? true) && chosen.forcedTraits != null)
            {
                for (int i = 0; i < chosen.forcedTraits.Count; i++)
                {
                    var ft = chosen.forcedTraits[i];
                    if (ft?.def == null) continue;

                    var existing = pawn.story.traits.allTraits.FirstOrDefault(t => t.def == ft.def);
                    if (existing != null)
                    {
                        if (existing.Degree != ft.degree)
                        {
                            // Remove then add with desired degree
                            pawn.story.traits.allTraits.Remove(existing);
                            AddTraitSafe(pawn, ft.def, ft.degree);
                        }
                    }
                    else
                    {
                        AddTraitSafe(pawn, ft.def, ft.degree);
                    }
                }
            }

            if (DebugLog)
            {
                string cats = string.Join(",", catsWithAny.Select((c, idx) => c + ":" + catWeights[idx].ToString("0.##")));
                Log.Message($"[MRC] Category-first: allowed={string.Join(",", allowedCats)} weights={cats} chosenCat={chosenCat} chosenStory={chosen.defName}");
            }

            return false;
        }

        private static bool IsAwakenedSafe(Pawn pawn)
        {
            try { return Utils.IsAwakened(pawn); } catch { return false; }
        }
        private static bool IsAndroidSafe(Pawn pawn)
        {
            try { return pawn.IsAndroid(); } catch { return false; }
        }
        private static void AddTraitSafe(Pawn pawn, TraitDef def, int degree)
        {
            try { pawn.story.traits.GainTrait(new Trait(def, degree, forced: true), true); }
            catch { pawn.story.traits.allTraits.Add(new Trait(def, degree, forced: true)); }
        }
    }
}