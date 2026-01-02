using Verse;

namespace MurderRimCore
{
    public class TraitForceOnGeneModExtension : DefModExtension
    {
        // If true, this trait will be forced on pawns that have a gene
        // whose GeneDef.forcedTraits contains this trait.defName.
        public bool ForceOnGene = false;
    }
}
