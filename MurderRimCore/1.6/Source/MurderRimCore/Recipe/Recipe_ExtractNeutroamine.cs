using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore
{
    public class Recipe_ExtractNeutroamine : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || !pawn.IsAndroid())
                return false;

            // Check if there is ANY fuel left to extract.
            // 1.0 Severity = Empty. 
            // We check if severity is less than ~0.99 (meaning at least ~1 unit remains).
            Hediff lossHediff = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss);

            if (lossHediff != null)
            {
                if (lossHediff.Severity >= 0.99f)
                {
                    return false; // Tank is effectively empty
                }
            }

            return base.AvailableOnNow(thing, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 1. Determine Requested Amount
            int requestedAmount = 1;
            var extension = recipe.GetModExtension<NeutroamineExtractionExtension>();
            if (extension != null)
            {
                requestedAmount = extension.amount;
            }

            // 2. Calculate Available Amount
            Hediff lossHediff = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss);
            float currentSeverity = (lossHediff != null) ? lossHediff.Severity : 0f;

            // Formula: (1.0 - CurrentSeverity) * 100 = Units Remaining
            // Example: Severity 0.2 (80% full) -> (1.0 - 0.2) * 100 = 80 units.
            float severitySpaceRemaining = 1.0f - currentSeverity;
            int unitsAvailable = (int)(severitySpaceRemaining * 100f);

            // 3. Determine Actual Extraction (Don't give more than what exists)
            int actualExtract = Math.Min(requestedAmount, unitsAvailable);

            if (actualExtract <= 0)
            {
                // Should happen rarely due to AvailableOnNow, but handles edge cases
                if (billDoer != null) Messages.Message("MRWD_TankEmpty".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 4. Apply Changes
            float severityToApply = actualExtract * 0.01f;

            // AdjustSeverity creates the hediff if missing
            HealthUtility.AdjustSeverity(pawn, VREA_DefOf.VREA_NeutroLoss, severityToApply);

            // 5. Spawn Items
            Thing neutro = ThingMaker.MakeThing(VREA_DefOf.Neutroamine);
            neutro.stackCount = actualExtract;
            GenPlace.TryPlaceThing(neutro, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }
    }
}