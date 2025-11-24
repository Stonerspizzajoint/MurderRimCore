using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;
using MurderRimCore.MRWD;

namespace MurderRimCore.Patch
{
    [HarmonyPatch(typeof(Utils), nameof(Utils.TryAssignBackstory), new[] { typeof(Pawn), typeof(string) })]
    public static class Utils_TryAssignBackstory_WorkerDrones
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn pawn, ref string spawnCategory)
        {
            if (pawn == null || pawn.story == null)
                return true;

            if (!DroneHelper.IsWorkerDrone(pawn))
                return true; // not a worker drone, let VRE do its thing

            WorkerDroneBackstorySettings settings = WorkerDroneSettingsDef.Backstory;
            if (settings == null)
                return true;

            HashSet<string> allowedSet = WorkerDroneBackstoryResolver.GetAllowedCategoriesForWorkerDrone(pawn);
            if (allowedSet == null || allowedSet.Count == 0)
                return true;

            List<string> allowedCats = allowedSet
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            if (allowedCats.Count == 0)
                return true;

            bool success = AssignCategoryAndBackstory(pawn, ref spawnCategory, allowedCats, settings);
            return !success; // if we handled it, skip original
        }

        private static bool AssignCategoryAndBackstory(
            Pawn pawn,
            ref string spawnCategory,
            List<string> allowedCats,
            WorkerDroneBackstorySettings settings)
        {
            string thisPawnkind = pawn.kindDef != null ? pawn.kindDef.defName : null;

            // Map category -> list of candidate backstories
            var byCat = new Dictionary<string, List<BackstoryDef>>(allowedCats.Count);
            foreach (string cat in allowedCats)
                byCat[cat] = new List<BackstoryDef>();

            foreach (BackstoryDef b in DefDatabase<BackstoryDef>.AllDefsListForReading)
            {
                if (b == null || b.slot != BackstorySlot.Childhood || b.spawnCategories == null)
                    continue;

                for (int i = 0; i < allowedCats.Count; i++)
                {
                    string cat = allowedCats[i];
                    if (b.spawnCategories.Contains(cat))
                        byCat[cat].Add(b);
                }
            }

            // Categories that actually have backstories
            List<string> catsWithAny = new List<string>();
            for (int i = 0; i < allowedCats.Count; i++)
            {
                string cat = allowedCats[i];
                List<BackstoryDef> list;
                if (byCat.TryGetValue(cat, out list) && list != null && list.Count > 0)
                    catsWithAny.Add(cat);
            }

            if (catsWithAny.Count == 0)
                return false;

            // --- ENFORCE EXCLUSIVITY: only owner pawnkinds see pawnkind categories ---

            List<string> filteredCats = new List<string>();
            for (int i = 0; i < catsWithAny.Count; i++)
            {
                string cat = catsWithAny[i];

                List<string> owners = settings.GetOwnerPawnkindsForCategory(cat);
                if (owners != null && owners.Count > 0)
                {
                    bool isOwner = false;
                    if (!string.IsNullOrEmpty(thisPawnkind))
                    {
                        for (int o = 0; o < owners.Count; o++)
                        {
                            if (owners[o] == thisPawnkind)
                            {
                                isOwner = true;
                                break;
                            }
                        }
                    }

                    if (!isOwner)
                    {
                        // Debug aid: log if a non-owner would have been allowed
                        Log.Message($"[MRC] Blocking pawnkind {thisPawnkind ?? "null"} from pawnkind category {cat} owned by [{string.Join(",", owners)}]");
                        continue; // non-owner can't see this category
                    }
                }

                filteredCats.Add(cat);
            }

            catsWithAny = filteredCats;

            if (catsWithAny.Count == 0)
            {
                // After exclusivity, this pawn has no valid categories from our system.
                // Let original behavior run instead of failing hard.
                return false;
            }

            // --- CATEGORY WEIGHTING: owned categories favored for owners ---

            float[] catWeights = new float[catsWithAny.Count];
            float totalCatW = 0f;

            for (int i = 0; i < catsWithAny.Count; i++)
            {
                string cat = catsWithAny[i];

                float w = settings.GetCategoryMultiplier(cat, 1f);

                if (!string.IsNullOrEmpty(thisPawnkind) &&
                    settings.PawnkindOwnsCategory(thisPawnkind, cat))
                {
                    w *= settings.pawnkindCategoryWeightBoost;
                }

                if (w < 0f) w = 0f;
                catWeights[i] = w;
                totalCatW += w;
            }

            if (totalCatW <= 0f)
                return false;

            float rollCat = Rand.Value * totalCatW;
            float accCat = 0f;
            int chosenCatIndex = catsWithAny.Count - 1;
            for (int i = 0; i < catsWithAny.Count; i++)
            {
                accCat += catWeights[i];
                if (rollCat <= accCat)
                {
                    chosenCatIndex = i;
                    break;
                }
            }

            string chosenCat = catsWithAny[chosenCatIndex];
            spawnCategory = chosenCat;

            List<BackstoryDef> candidates = byCat[chosenCat];
            if (candidates == null || candidates.Count == 0)
                return false;

            if (settings.exclusiveChosenCategory)
            {
                List<BackstoryDef> unique = new List<BackstoryDef>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    BackstoryDef bs = candidates[i];
                    if (bs.spawnCategories == null)
                        continue;

                    bool hasOther = false;
                    for (int s = 0; s < bs.spawnCategories.Count; s++)
                    {
                        string other = bs.spawnCategories[s];
                        if (other != chosenCat && allowedCats.Contains(other))
                        {
                            hasOther = true;
                            break;
                        }
                    }

                    if (!hasOther)
                        unique.Add(bs);
                }

                if (unique.Count > 0)
                    candidates = unique;
            }

            // --- PER-BACKSTORY WEIGHTS ---

            var weighted = new List<BackstoryDef>();
            var weights = new List<float>();
            float totalW = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                BackstoryDef bs = candidates[i];
                float w = settings.GetBackstoryMultiplier(bs.defName, 1f);
                if (w <= 0f)
                    continue;

                weighted.Add(bs);
                weights.Add(w);
                totalW += w;
            }

            if (weighted.Count == 0)
            {
                weighted.AddRange(candidates);
                for (int i = 0; i < candidates.Count; i++)
                    weights.Add(1f);
                totalW = candidates.Count;
            }

            if (totalW <= 0f)
                return false;

            float roll = Rand.Value * totalW;
            float acc = 0f;
            int chosenIndex = weighted.Count - 1;

            for (int i = 0; i < weighted.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc)
                {
                    chosenIndex = i;
                    break;
                }
            }

            BackstoryDef chosen = weighted[chosenIndex];
            pawn.story.Childhood = chosen;

            if (settings.applyForcedTraits && chosen.forcedTraits != null)
            {
                for (int i = 0; i < chosen.forcedTraits.Count; i++)
                {
                    var ft = chosen.forcedTraits[i];
                    if (ft == null || ft.def == null)
                        continue;

                    Trait existing = pawn.story.traits.allTraits
                        .FirstOrDefault(t => t.def == ft.def);

                    if (existing != null)
                    {
                        if (existing.Degree != ft.degree)
                        {
                            pawn.story.traits.allTraits.Remove(existing);
                            pawn.story.traits.GainTrait(
                                new Trait(ft.def, ft.degree, forced: true), true);
                        }
                    }
                    else
                    {
                        pawn.story.traits.GainTrait(
                            new Trait(ft.def, ft.degree, forced: true), true);
                    }
                }
            }

            return true;
        }
    }
}