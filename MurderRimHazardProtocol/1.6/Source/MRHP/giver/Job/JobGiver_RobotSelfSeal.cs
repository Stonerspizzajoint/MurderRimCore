using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MRHP
{
    public class JobGiver_RobotSelfSeal : ThinkNode_JobGiver
    {
        // Thresholds
        private const int CriticalBleedTicks = 15000; // Approx 6 in-game hours remaining
        private const float EnemyScanRadius = 20f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. Basic Checks
            if (pawn.Downed || pawn.Drafted || pawn.InAggroMentalState) return null;
            if (pawn.Map == null) return null;

            // 2. Manipulation Check (Need hands/head to fix self)
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return null;
            }

            // 3. Bleeding Analysis
            // Trigger sealing if ANY bleeding exists at all (even very small amounts).
            if (pawn.health.hediffSet.BleedRateTotal <= 0f)
            {
                return null;
            }

            // 4. Urgency Calculation
            int ticksToDeath = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            bool isCritical = ticksToDeath < CriticalBleedTicks;

            // 5. Threat Analysis
            // We need a List<Thing> for the Flee algorithm, so we scan manually.
            List<Thing> threats = new List<Thing>();

            foreach (Thing t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, EnemyScanRadius, true))
            {
                // FIX for CS1061: Cast to Pawn before checking Downed
                Pawn enemy = t as Pawn;
                if (enemy != null)
                {
                    if (enemy.HostileTo(pawn) && !enemy.Downed && !enemy.Dead)
                    {
                        threats.Add(enemy);
                    }
                }
                else
                {
                    // Handle turrets or other hostile non-pawns
                    if (t.HostileTo(pawn) && t.def.building != null && t.def.building.IsTurret)
                    {
                        threats.Add(t);
                    }
                }
            }

            bool enemiesNearby = threats.Count > 0;

            // -----------------------------------------------------------------
            // DECISION MATRIX
            // -----------------------------------------------------------------

            // CASE A: Light bleeding + Enemies. Fight on.
            if (enemiesNearby && !isCritical)
            {
                return null;
            }

            // CASE B: Critical bleeding + Enemies. FLEE.
            if (enemiesNearby && isCritical)
            {
                // FIX for CS0117: Use CellFinderLoose instead of RCellFinder
                IntVec3 fleeDest = CellFinderLoose.GetFleeDest(pawn, threats, 24f);

                if (fleeDest.IsValid && fleeDest != pawn.Position)
                {
                    Job fleeJob = JobMaker.MakeJob(JobDefOf.Flee, fleeDest, threats[0]);
                    fleeJob.expiryInterval = 120; // Run for 2 seconds then re-evaluate
                    return fleeJob;
                }

                // If trapped, fall through to Seal.
            }

            // CASE C: Safe (or Desperate). Seal.
            return JobMaker.MakeJob(MRHP_DefOf.MRHP_RobotSelfSeal, pawn);
        }
    }
}