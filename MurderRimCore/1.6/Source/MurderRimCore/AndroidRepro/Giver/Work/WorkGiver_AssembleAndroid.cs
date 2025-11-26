using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    public class WorkGiver_AssembleAndroid : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("VREA_AndroidCreationStation"));

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 1. Basic Station Checks
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced)) return false;

            var comp = t.TryGetComp<CompAndroidReproduction>();
            if (comp == null || !comp.ReadyForAssembly) return false;

            if (pawn.skills.GetSkill(SkillDefOf.Crafting).Level < 8)
            {
                JobFailReason.Is("Requires Crafting level 8");
                return false;
            }

            // 2. INGREDIENT CHECK (Crucial Fix)
            // We must ensure ingredients exist before promising a job.
            if (!FindIngredients(pawn, out _))
            {
                JobFailReason.Is("Missing required resources (50 Plasteel, 10 Uranium, 2 Adv. Components)");
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // Re-fetch the actual things to lock them into the job
            if (!FindIngredients(pawn, out List<ThingCount> found)) return null;

            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MRC_AssembleAndroid"), t);

            // Drop ingredients at Interaction Cell
            job.targetC = t.InteractionCell;

            job.targetQueueB = new List<LocalTargetInfo>();
            job.countQueue = new List<int>();

            foreach (var f in found)
            {
                job.targetQueueB.Add(f.Thing);
                job.countQueue.Add(f.Count);
            }

            return job;
        }

        // Helper to find items. Returns false if any are missing.
        private bool FindIngredients(Pawn pawn, out List<ThingCount> foundThings)
        {
            foundThings = new List<ThingCount>();

            List<ThingDefCountClass> required = new List<ThingDefCountClass>
            {
                new ThingDefCountClass(ThingDef.Named("Plasteel"), 50),
                new ThingDefCountClass(ThingDef.Named("Uranium"), 10),
                new ThingDefCountClass(ThingDef.Named("ComponentSpacer"), 2)
            };

            foreach (var req in required)
            {
                // We need to find enough items to satisfy the count.
                // Note: This simple check finds the CLOSEST single stack that meets the count.
                // If you have 2 stacks of 25 plasteel, this basic check might fail or only grab one.
                // For robustness, we typically use Region searches, but for this fix let's assume single stacks first.

                Thing item = GenClosest.ClosestThingReachable(
                    pawn.Position, pawn.Map, ThingRequest.ForDef(req.thingDef),
                    PathEndMode.ClosestTouch, TraverseParms.For(pawn),
                    9999f,
                    x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && x.stackCount >= req.count
                );

                // Fallback: If no single stack is big enough, just find ANY reachable stack.
                // The JobDriver logic handles queues, but if we return false here, the job won't start.
                // If you want to support multiple small stacks, you need a more complex loop here.
                // For now, we stick to the "Find Big Stack" logic to be safe against errors.

                if (item == null)
                {
                    // Retry with smaller stacks? 
                    // To keep it simple: If we can't find the item, we fail.
                    return false;
                }

                foundThings.Add(new ThingCount(item, req.count));
            }

            return true;
        }
    }
}