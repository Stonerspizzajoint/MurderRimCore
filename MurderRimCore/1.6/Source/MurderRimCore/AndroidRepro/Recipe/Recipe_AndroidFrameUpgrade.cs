using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    /// <summary>
    /// Recipe worker for android frame upgrades.
    /// Validates that the android is ready for upgrade before the bill can be applied.
    /// </summary>
    public class Recipe_AndroidFrameUpgrade : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;

            Pawn pawn = thing as Pawn;
            if (pawn == null) return false;

            // Must have the android childhood marker
            Hediff marker = pawn.health.hediffSet.GetFirstHediffOfDef(AndroidRep_DefOf.MRC_AndroidChildhoodMarker);
            if (marker == null) return false;

            // Must have the growth comp
            HediffComp_AndroidGrowth growthComp = marker.TryGetComp<HediffComp_AndroidGrowth>();
            if (growthComp == null) return false;

            // Must be ready for upgrade
            return growthComp.ReadyForUpgrade;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn == null) return;

            Hediff marker = pawn.health.hediffSet.GetFirstHediffOfDef(AndroidRep_DefOf.MRC_AndroidChildhoodMarker);
            if (marker == null) return;

            HediffComp_AndroidGrowth growthComp = marker.TryGetComp<HediffComp_AndroidGrowth>();
            if (growthComp == null) return;

            // Open the upgrade selection window for player choices
            Find.WindowStack.Add(new Window_AndroidUpgradeSelection(pawn, growthComp, billDoer));
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null) return base.GetLabelWhenUsedOn(pawn, part);

            Hediff marker = pawn.health.hediffSet.GetFirstHediffOfDef(AndroidRep_DefOf.MRC_AndroidChildhoodMarker);
            if (marker == null) return base.GetLabelWhenUsedOn(pawn, part);

            HediffComp_AndroidGrowth growthComp = marker.TryGetComp<HediffComp_AndroidGrowth>();
            if (growthComp == null) return base.GetLabelWhenUsedOn(pawn, part);

            string currentStage = growthComp.CurrentStageIndex switch
            {
                0 => "Baby",
                1 => "Child",
                2 => "Teen",
                _ => "Adult"
            };

            string nextStage = growthComp.CurrentStageIndex switch
            {
                0 => "Child",
                1 => "Teen",
                2 => "Adult",
                _ => "None"
            };

            return $"Upgrade Frame: {currentStage} → {nextStage}";
        }
    }
}
