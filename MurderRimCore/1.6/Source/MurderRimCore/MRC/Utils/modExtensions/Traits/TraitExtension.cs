using Verse;
using RimWorld;
using System.Collections.Generic;

namespace MurderRimCore
{
    // Attach to TraitDef via <modExtensions>.
    // Use it to restrict where/when a trait is allowed to appear.
    public class TraitExtension : DefModExtension
    {
        // If non-empty, the pawn must have at least one of these backstories
        // (Childhood OR Adulthood) for this trait to be allowed.
        public List<BackstoryDef> restrictToBackstories;

        // If true, the trait is only valid for worker drones:
        // pawns with the gene MRC_DefOf.MRWD_DroneBody.
        public bool onlyForWorkerDrones = false;

        // If true, and the pawn satisfies the above restrictions,
        // auto-add the trait at the end of generation if it wasn't added yet.
        // Degree handling: defaults to 0 if not specified elsewhere.
        public bool alwaysAddIfBackstoryPresent = false;

        // Optional explicit degree to add if alwaysAddIfBackstoryPresent is true.
        // If null, defaults to degree 0.
        public int? forcedDegree;

        public override string ToString()
        {
            var count = restrictToBackstories?.Count ?? 0;
            return $"TraitExtension(restrictToBackstories={count}, onlyForWorkerDrones={onlyForWorkerDrones}, alwaysAdd={alwaysAddIfBackstoryPresent}, forcedDegree={(forcedDegree.HasValue ? forcedDegree.Value.ToString() : "null")})";
        }
    }
}
