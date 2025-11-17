using Verse;
using RimWorld;
using UnityEngine;

namespace MurderRimCore
{
    // Unified backstory extension:
    // - selection commonality (how often this backstory is picked)
    // - optional age override
    // - optional limb removal
    public class BackstoryExtension : DefModExtension
    {
        // BACKSTORY SELECTION COMMONALITY (multiplier)
        // 1.0 = vanilla frequency, >1 = more common, <1 = rarer, 0 = never picked
        public float commonality = 1f;

        // AGE OVERRIDE (optional; if false or not present -> vanilla ages)
        public bool overrideAge = false;
        public FloatRange biologicalYears = new FloatRange(30f, 55f);
        public IntRange extraChronologicalYears = new IntRange(20, 120);
        public bool chronoIsBio = false;
        public bool forceOverrideFixedAge = true;

        // LIMB REMOVAL
        public bool RanMissingPart = false;
        public IntRange missingPartCount = new IntRange(0, 0);
        public bool ensureAtLeastOneLeg = false;

        public override string ToString()
        {
            return $"BackstoryExtension(backstoryCommonality={commonality}, overrideAge={overrideAge}, chronoIsBio={chronoIsBio}, RanMissingPart={RanMissingPart}, missingPartCount={missingPartCount}, ensureAtLeastOneLeg={ensureAtLeastOneLeg})";
        }
    }
}