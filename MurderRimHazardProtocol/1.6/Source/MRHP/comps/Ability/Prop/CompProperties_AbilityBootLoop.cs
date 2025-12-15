using RimWorld;
using Verse;

namespace MRHP
{
    public class CompProperties_AbilityBootLoop : CompProperties_AbilityEffect
    {
        // Cone Logic
        public float coneAngle = 60f;
        public int stunDuration = 300;
        public bool useSightBasedChance = true;

        // Hediffs
        public HediffDef hediffDef;         // Standard effect
        public HediffDef hediffDefCritical; // NEW: Effect for pawns with Critical Vulnerability gene

        // Visuals
        public ThingDef moteDef;
        public ThingDef hitMoteDef;

        // Validation
        public bool checkVerbBodyParts = false;

        public CompProperties_AbilityBootLoop()
        {
            this.compClass = typeof(CompAbilityBootLoop);
        }
    }
}