using Verse;

namespace MurderRimCore.AndroidRepro
{
    /// <summary>
    /// Properties for the Android Growth hediff comp.
    /// Controls passive growth readiness accumulation for android children.
    /// When readiness reaches the threshold, the android becomes eligible for a manual upgrade via medical bill.
    /// </summary>
    public class HediffCompProperties_AndroidGrowth : HediffCompProperties
    {
        // How many days of passive accumulation needed before eligible for upgrade
        public float daysPerStageThreshold = 5f;

        // Life stage severity thresholds (matching the HediffDef stages)
        public float childThreshold = 0.35f;
        public float teenThreshold = 0.70f;
        public float adultThreshold = 1.0f;

        // Number of skill points the player can allocate per upgrade
        public int skillPointsPerUpgrade = 5;

        // Number of passions the player can select per upgrade
        public int passionsPerUpgrade = 1;

        // Number of traits the player can select on final (adult) upgrade
        public int traitsOnAdultUpgrade = 1;

        public HediffCompProperties_AndroidGrowth()
        {
            compClass = typeof(HediffComp_AndroidGrowth);
        }
    }
}
