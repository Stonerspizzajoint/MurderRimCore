using RimWorld;
using Verse;

namespace MRHP
{
    public class CompProperties_SentinelSettings : CompProperties_AbilityEffect
    {
        public float baseDodgeChance = 0.05f;
        public float dodgeChancePerMeleeLevel = 0.03f;

        // Settings for the Hediff breakout logic
        public float breakoutBaseChancePerSkill = 0.02f;
        public float breakoutChancePerStruggle = 0.01f;

        public CompProperties_SentinelSettings()
        {
            this.compClass = typeof(CompAbility_SentinelSettings);
        }
    }

    public class CompAbility_SentinelSettings : CompAbilityEffect
    {
        public new CompProperties_SentinelSettings Props => (CompProperties_SentinelSettings)props;

        // Logic stays empty here; we just use this class to hold the data
    }
}