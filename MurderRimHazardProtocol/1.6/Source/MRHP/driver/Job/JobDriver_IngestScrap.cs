using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using VEF.AnimalBehaviours;

namespace MRHP
{
    public class JobDriver_IngestScrap : JobDriver_IngestWeird
    {
        // Cache the thing being eaten BEFORE finalize
        private Thing eatenThing;
        private const bool debugMode = false; // set true while testing

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (Toil toil in base.MakeNewToils())
            {
                // Capture the ingestible just before finalize runs
                if (toil.debugName == "FinalizeIngestAnything")
                {
                    yield return CacheEatenThingToil();
                }

                yield return toil;

                // Inject AFTER finalize
                if (toil.debugName == "FinalizeIngestAnything")
                {
                    yield return PostFinalizeScrapToil();
                }
            }
        }

        private Toil CacheEatenThingToil()
        {
            Toil toil = ToilMaker.MakeToil("MRHP_CacheEatenThing");
            toil.initAction = () =>
            {
                eatenThing = job?.GetTarget(TargetIndex.A).Thing;
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil PostFinalizeScrapToil()
        {
            Toil toil = ToilMaker.MakeToil("MRHP_PostFinalizeScrap");
            toil.initAction = () =>
            {
                Pawn pawn = toil.actor;
                if (pawn == null || pawn.Dead) return;

                // Only spawn scrap if the thing was actually destroyed
                if (eatenThing == null || !eatenThing.Destroyed) return;

                // Comp is on the THING, not the pawn
                CompScrapEater scrapComp = eatenThing.TryGetComp<CompScrapEater>();
                if (scrapComp == null) return;

                try
                {
                    // Spawn scrap, reduce hediff, attempt duplication as before
                    scrapComp.TrySpawnScrap(eatenThing);
                    scrapComp.TryReduceHediffByOneStage(pawn);
                    scrapComp.TryDuplicateSelfIfReady(pawn);

                    // --- Apply nutrition from our scrap comp props ---
                    if (!pawn.Dead && pawn.needs?.food != null && scrapComp.Props != null)
                    {
                        float compNutrition = scrapComp.Props.nutrition;

                        // If the thing was a plant and the comp marks food-as-plant, scale by growth
                        if (scrapComp.Props.areFoodSourcesPlants && eatenThing is Plant eatenPlant)
                            compNutrition *= eatenPlant.Growth;

                        if (compNutrition > 0f)
                        {
                            pawn.needs.food.CurLevel += compNutrition;
                            pawn.records.AddTo(RecordDefOf.NutritionEaten, compNutrition);

                            if (debugMode)
                                Log.Message($"[MRHP DEBUG] Applied {compNutrition} nutrition from CompScrapEater to {pawn.LabelShort}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[MRHP] Scrap post-ingest failed: {ex}");
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}
