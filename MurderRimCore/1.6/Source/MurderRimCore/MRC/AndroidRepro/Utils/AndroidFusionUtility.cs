using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public static class AndroidFusionUtility
    {
        public const string NameSentinel = "[MRC-FUSED]";

        public static bool IsEligibleParent(Pawn p, AndroidReproductionSettingsDef s)
        {
            if (p == null || s == null) return false;
            if (s.onlyHumanlikeAndroids && (p.RaceProps == null || !p.RaceProps.Humanlike)) return false;
            bool isDrone = DroneHelper.IsWorkerDrone(p);
            bool isAndroid = Utils.IsAndroid(p) && !DroneHelper.IsWorkerDrone(p);
            return isDrone || isAndroid;
        }

        public static bool ValidateParents(Pawn a, Pawn b, AndroidReproductionSettingsDef s, out string reason)
        {
            reason = null;
            if (a == null || b == null) { reason = "Select both parents."; return false; }
            if (a == b) { reason = "Parents must be different."; return false; }
            if (s.requireAwakenedBoth && !(Utils.IsAwakened(a) && Utils.IsAwakened(b))) { reason = "Both must be awakened."; return false; }
            if (!IsEligibleParent(a, s) || !IsEligibleParent(b, s)) { reason = "Invalid parent types."; return false; }
            if (s.requireLovePartners)
            {
                bool lovers =
                    (a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)) ||
                    (a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b)) ||
                    (a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b));
                if (!lovers) { reason = "Not love partners."; return false; }
            }
            if (!s.allowCrossFaction && a.Faction != b.Faction) { reason = "Different faction."; return false; }
            return true;
        }

        // New simple rule:
        // - Take union of both parents' genes (endo/xeno per settings).
        // - Drop duplicates.
        // - Drop genes listed as non-inheritable (def or tag).
        // - Do NOT drop for category conflicts or other heuristics here.
        public static CustomXenotype BuildFusedProject(Pawn parentA, Pawn parentB, AndroidReproductionSettingsDef s)
        {
            if (parentA == null || parentB == null || s == null) return null;

            if (s.enforceDeterministicFusion)
            {
                int seed = parentA.thingIDNumber ^ parentB.thingIDNumber;
                Rand.PushState(seed);
            }

            var union = CollectUnion(parentA, parentB, s);

            // Optionally enforce caps (off by default).
            if (!s.disableGeneCaps)
                ApplyCaps(union, parentA, parentB, s);

            if (s.enforceDeterministicFusion)
                Rand.PopState();

            var cx = new CustomXenotype
            {
                // Set desired xenotype name
                name = "Android Born",
                genes = union
            };

            if (!string.IsNullOrEmpty(s.fusedXenotypeIconDef))
                cx.iconDef = DefDatabase<XenotypeIconDef>.GetNamedSilentFail(s.fusedXenotypeIconDef);

            return cx;
        }

        // Collect unique set of genes across both parents, minus non-inheritable.
        public static List<GeneDef> CollectUnion(Pawn a, Pawn b, AndroidReproductionSettingsDef s)
        {
            HashSet<GeneDef> set = new HashSet<GeneDef>();

            if (s.inheritEndogenes)
            {
                if (a.genes != null) foreach (var g in a.genes.Endogenes) MaybeAdd(set, g.def, s);
                if (b.genes != null) foreach (var g in b.genes.Endogenes) MaybeAdd(set, g.def, s);
            }
            if (s.inheritXenogenes)
            {
                if (a.genes != null) foreach (var g in a.genes.Xenogenes) MaybeAdd(set, g.def, s);
                if (b.genes != null) foreach (var g in b.genes.Xenogenes) MaybeAdd(set, g.def, s);
            }

            return set.ToList();
        }

        private static void MaybeAdd(HashSet<GeneDef> set, GeneDef def, AndroidReproductionSettingsDef s)
        {
            if (def == null) return;
            if (IsNonInheritable(def, s)) return;
            // Excluded global tags (legacy; optional)
            if (HasAnyTag(def, s.excludedGeneTags)) return;
            set.Add(def);
        }

        private static bool IsNonInheritable(GeneDef d, AndroidReproductionSettingsDef s)
        {
            if (d == null) return true;
            if (s.nonInheritableGeneDefs != null && s.nonInheritableGeneDefs.Contains(d.defName)) return true;
            if (HasAnyTag(d, s.nonInheritableGeneTags)) return true;
            return false;
        }

        private static bool HasAnyTag(GeneDef d, List<string> tags)
        {
            // Placeholder – if you implement ModExtensions with tags later, check them here.
            return tags != null && tags.Count > 0 ? false : false;
        }

        private static void ApplyCaps(List<GeneDef> genes, Pawn a, Pawn b, AndroidReproductionSettingsDef s)
        {
            int maxEndo = s.maxEndogenes.max;
            int maxXeno = s.maxXenogenes.max;

            var endo = genes.Where(g => IsEndogene(g, a, b)).ToList();
            var xeno = genes.Where(g => IsXenogene(g, a, b)).ToList();

            if (endo.Count > maxEndo) TrimList(endo, maxEndo, genes, s.essentialGeneDefs);
            if (xeno.Count > maxXeno) TrimList(xeno, maxXeno, genes, s.essentialGeneDefs);
        }

        private static void TrimList(List<GeneDef> subset, int target, List<GeneDef> master, List<string> essential)
        {
            subset.Shuffle();
            int toRemove = subset.Count - target;
            for (int i = 0; i < subset.Count && toRemove > 0; i++)
            {
                var g = subset[i];
                if (essential != null && essential.Contains(g.defName)) continue;
                master.Remove(g);
                toRemove--;
            }
        }

        private static bool IsEndogene(GeneDef d, Pawn a, Pawn b)
        {
            return (a.genes?.Endogenes?.Any(g => g.def == d) ?? false) ||
                   (b.genes?.Endogenes?.Any(g => g.def == d) ?? false);
        }

        private static bool IsXenogene(GeneDef d, Pawn a, Pawn b)
        {
            return (a.genes?.Xenogenes?.Any(g => g.def == d) ?? false) ||
                   (b.genes?.Xenogenes?.Any(g => g.def == d) ?? false);
        }
    }
}