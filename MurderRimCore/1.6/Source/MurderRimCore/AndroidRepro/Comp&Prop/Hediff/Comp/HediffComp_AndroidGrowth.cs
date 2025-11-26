using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    /// <summary>
    /// HediffComp that tracks android growth readiness.
    /// Readiness accumulates passively over time. When it reaches 100%, 
    /// the android is eligible for a manual upgrade via medical bill.
    /// The severity of the parent hediff determines current life stage.
    /// </summary>
    public class HediffComp_AndroidGrowth : HediffComp
    {
        // Current readiness toward next upgrade (0.0 to 1.0)
        private float upgradeReadiness = 0f;

        public HediffCompProperties_AndroidGrowth Props => (HediffCompProperties_AndroidGrowth)props;

        /// <summary>
        /// Returns the current readiness percentage (0-100%).
        /// </summary>
        public float UpgradeReadinessPercent => upgradeReadiness;

        /// <summary>
        /// Returns true if the android has accumulated enough readiness for an upgrade.
        /// </summary>
        public bool ReadyForUpgrade => upgradeReadiness >= 1.0f;

        /// <summary>
        /// Returns the current stage index based on hediff severity.
        /// 0 = Baby, 1 = Child, 2 = Teen, 3 = Adult (no more marker)
        /// </summary>
        public int CurrentStageIndex
        {
            get
            {
                float severity = parent.Severity;
                if (severity >= Props.adultThreshold) return 3;
                if (severity >= Props.teenThreshold) return 2;
                if (severity >= Props.childThreshold) return 1;
                return 0;
            }
        }

        /// <summary>
        /// Returns true if this is the final upgrade that will remove the hediff.
        /// </summary>
        public bool IsAtFinalStage => parent.Severity >= Props.teenThreshold && parent.Severity < Props.adultThreshold;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Don't accumulate readiness if already at adult stage (hediff should be removed)
            if (parent.Severity >= Props.adultThreshold) return;

            // Don't accumulate if already ready for upgrade
            if (upgradeReadiness >= 1.0f) return;

            // Accumulate readiness based on days threshold
            // 60000 ticks = 1 day
            float dailyIncrement = 1f / Props.daysPerStageThreshold;
            float tickIncrement = dailyIncrement / 60000f;

            upgradeReadiness += tickIncrement;
            if (upgradeReadiness > 1.0f) upgradeReadiness = 1.0f;
        }

        /// <summary>
        /// Called when the player performs an upgrade via medical bill.
        /// Advances to the next stage and resets readiness.
        /// </summary>
        public void PerformUpgrade()
        {
            int currentStage = CurrentStageIndex;

            // Advance to next stage threshold
            float newSeverity;
            switch (currentStage)
            {
                case 0: // Baby -> Child
                    newSeverity = Props.childThreshold;
                    break;
                case 1: // Child -> Teen
                    newSeverity = Props.teenThreshold;
                    break;
                case 2: // Teen -> Adult (final)
                    newSeverity = Props.adultThreshold;
                    break;
                default:
                    return; // Already adult
            }

            parent.Severity = newSeverity;
            upgradeReadiness = 0f;

            // Update backstory
            AndroidReproUtils.ApplyStageBackstory(Pawn, currentStage + 1);

            // If we just reached adult threshold, handle graduation
            if (newSeverity >= Props.adultThreshold)
            {
                GraduateToAdult();
            }

            // Refresh visuals
            if (Pawn.Drawer?.renderer != null)
            {
                Pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            PortraitsCache.SetDirty(Pawn);
        }

        /// <summary>
        /// Handles the final transition to adulthood.
        /// </summary>
        private void GraduateToAdult()
        {
            // Set the adult backstory
            AndroidReproUtils.ApplyStageBackstory(Pawn, 2);

            // Remove the childhood marker hediff since they're now an adult
            if (Pawn.health.hediffSet.HasHediff(parent.def))
            {
                Pawn.health.RemoveHediff(parent);
            }

            Messages.Message($"{Pawn.LabelShortCap} has completed their artificial growth cycle and is now a fully functional adult android.", 
                Pawn, MessageTypeDefOf.PositiveEvent);
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (ReadyForUpgrade)
                {
                    return "Ready for Upgrade";
                }
                return $"Readiness: {upgradeReadiness.ToStringPercent()}";
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                string stageName = CurrentStageIndex switch
                {
                    0 => "Baby (Pill)",
                    1 => "Child",
                    2 => "Teen",
                    _ => "Adult"
                };

                string nextStage = CurrentStageIndex switch
                {
                    0 => "Child",
                    1 => "Teen",
                    2 => "Adult (Final)",
                    _ => "None"
                };

                string readyStatus = ReadyForUpgrade 
                    ? "<color=green>READY - Apply upgrade bill to advance</color>" 
                    : $"Progress: {upgradeReadiness.ToStringPercent()}";

                return $"Current Frame: {stageName}\nNext Stage: {nextStage}\nUpgrade Status: {readyStatus}";
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref upgradeReadiness, "upgradeReadiness", 0f);
        }
    }
}
