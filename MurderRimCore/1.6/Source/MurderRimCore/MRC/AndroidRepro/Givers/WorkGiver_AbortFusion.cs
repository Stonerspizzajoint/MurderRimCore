using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    // Abort fusion (Gestation or Assembly): open-job style. Uses the BasicWorker work type you provided.
    public class WorkGiver_AbortFusion : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn.Map == null) return true;
            foreach (var _ in AndroidFusionRuntime.StationsWithAbortQueued(pawn.Map))
                return false;
            return true;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null) yield break;
            foreach (var station in AndroidFusionRuntime.StationsWithAbortQueued(map))
            {
                if (station != null) yield return station;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var station = t as VREAndroids.Building_AndroidCreationStation;
            if (station == null || station.Destroyed) return false;

            // Must be queued to abort
            if (!AndroidFusionRuntime.IsAbortQueued(station)) return false;

            // Allow abort during Gestation OR Assembly
            if (!AndroidFusionRuntime.TryGetProcess(station, out var proc) || proc == null)
                return false;
            if (proc.Stage != FusionStage.Gestation && proc.Stage != FusionStage.Assembly)
                return false;

            if (t.IsForbidden(pawn)) return false;
            if (!pawn.CanReserve(station, 1, -1, null, forced)) return false;
            if (!pawn.CanReach(station.InteractionCell, PathEndMode.OnCell, Danger.Some)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!AndroidFusionRuntime.IsAbortQueued(t as VREAndroids.Building_AndroidCreationStation))
                return null;
            return JobMaker.MakeJob(MRC_AndroidRepro_DefOf.MRC_AbortFusion, t);
        }
    }
}