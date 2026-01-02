using System.Linq;
using Verse;
using RimWorld;

namespace MurderRimCore
{
    public static class TraitGeneUtils
    {
        // Returns true if any gene on the pawn lists this traitDef in its forcedTraits
        public static bool AnyGeneForcesTrait(Pawn pawn, TraitDef traitDef)
        {
            if (pawn?.genes == null || traitDef == null) return false;

            var genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                var gd = genes[i].def;
                if (gd == null) continue;

                // dev-only diagnostic
                if (Prefs.DevMode)
                {
                    Log.Message($"[AnyGeneForcesTrait] Checking gene {gd.defName} forcedTraits count: {gd.forcedTraits?.Count ?? 0}");
                }

                if (gd.forcedTraits.NullOrEmpty()) continue;

                for (int j = 0; j < gd.forcedTraits.Count; j++)
                {
                    var ft = gd.forcedTraits[j]; // GeneticTraitData
                    if (ft == null) continue;

                    // direct TraitDef reference equality
                    if (ft.def == traitDef) return true;

                    // fallback by name (very defensive)
                    if (ft.def?.defName == traitDef.defName) return true;
                }
            }

            return false;
        }

        // Ensure trait exists if any gene forces it; remove it if no gene forces it.
        public static void EnsureTraitIfForcedByGene(Pawn pawn, TraitDef traitDef, int degree = 0)
        {
            if (pawn == null || traitDef == null) return;

            var ext = traitDef.GetModExtension<TraitForceOnGeneModExtension>();
            if (ext == null || !ext.ForceOnGene) return;

            bool shouldHave = AnyGeneForcesTrait(pawn, traitDef);
            bool has = pawn.story?.traits?.HasTrait(traitDef) ?? false;

            if (Prefs.DevMode)
            {
                Log.Message($"[TraitGeneUtils] Pawn={pawn} trait={traitDef.defName} shouldHave={shouldHave} has={has}");
            }

            if (shouldHave && !has)
            {
                var traitInstance = new Trait(traitDef, degree);
                pawn.story.traits.GainTrait(traitInstance, suppressConflicts: true);
                if (Prefs.DevMode) Log.Message($"[TraitGeneUtils] Added trait {traitDef.defName} to {pawn}.");
            }
            else if (!shouldHave && has)
            {
                var traitSet = pawn.story?.traits;
                if (traitSet != null)
                {
                    Trait existing = traitSet.allTraits.FirstOrDefault(t => t.def == traitDef);
                    if (existing != null)
                    {
                        traitSet.RemoveTrait(existing);
                        if (Prefs.DevMode) Log.Message($"[TraitGeneUtils] Removed trait {traitDef.defName} from {pawn}.");
                    }
                }
            }
        }

        // Ensure all TraitDefs that have the TraitForceOnGeneModExtension are correct for this pawn
        public static void EnsureAllForcedTraitsForPawn(Pawn pawn)
        {
            if (pawn == null) return;

            foreach (var traitDef in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                var ext = traitDef.GetModExtension<TraitForceOnGeneModExtension>();
                if (ext != null && ext.ForceOnGene)
                {
                    EnsureTraitIfForcedByGene(pawn, traitDef);
                }
            }
        }
    }
}