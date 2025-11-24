using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public class WorkGiver_BuildAndroidBody : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn.Map == null) return true;

            // If there are no assembly-stage stations that are not abort-queued, skip.
            foreach (var st in AndroidFusionRuntime.StationsAwaitingAssembly(pawn.Map))
            {
                if (st != null && !AndroidFusionRuntime.IsAbortQueued(st))
                    return false;
            }
            return true;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null) yield break;

            foreach (var st in AndroidFusionRuntime.StationsAwaitingAssembly(map))
            {
                if (st != null && !AndroidFusionRuntime.IsAbortQueued(st))
                    yield return st;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var station = t as VREAndroids.Building_AndroidCreationStation;
            if (station == null || station.DestroyedOrNull()) return false;

            // Only androids should assemble android bodies
            if (!Utils.IsAndroid(pawn)) return false;

            // Abort queued? The abort job should take over instead.
            if (AndroidFusionRuntime.IsAbortQueued(station)) return false;

            // Must have a valid fusion process in Assembly stage
            if (!AndroidFusionRuntime.TryGetProcess(station, out var proc) ||
                proc == null || proc.Stage != FusionStage.Assembly)
                return false;

            if (t.IsForbidden(pawn)) return false;

            // Reserve station and interaction cell like WorkGiver_DoBill
            if (!pawn.CanReserve(station, 1, -1, null, forced)) return false;
            if (station.def.hasInteractionCell &&
                !pawn.CanReserveSittableOrSpot(station.InteractionCell, station, forced))
                return false;

            if (!pawn.CanReach(station.InteractionCell, PathEndMode.OnCell, Danger.Some)) return false;

            // Expensive map scan last: verify enough reachable materials given what’s already inside
            if (!AndroidFusionRuntime.HasAllReachableAssemblyMaterials(pawn, station))
                return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var station = t as VREAndroids.Building_AndroidCreationStation;
            if (station == null) return null;

            // Guard race: abort queued after HasJobOnThing
            if (AndroidFusionRuntime.IsAbortQueued(station)) return null;

            Job job = JobMaker.MakeJob(MRC_AndroidRepro_DefOf.MRC_AssembleAndroidBody, station);

            bool okP = QueueIngredient(pawn, job, ThingDefOf.Plasteel, AndroidFusionRuntime.PlasteelReq);
            bool okU = QueueIngredientFlexible(pawn, job, AndroidFusionRuntime.UraniumDefNames, AndroidFusionRuntime.UraniumReq);
            bool okA = QueueIngredientFlexible(pawn, job, AndroidFusionRuntime.AdvancedComponentDefNames, AndroidFusionRuntime.AdvCompReq);

            // Race guard again after queue building
            if (AndroidFusionRuntime.IsAbortQueued(station)) return null;

            if (!okP || !okU || !okA)
            {
                AndroidFusionRuntime.VerboseAssemblyLog.LogIfTrue("[FusionAssembly] Queue build failed (race or missing stacks).");
                return null;
            }

            AndroidFusionRuntime.VerboseAssemblyLog.LogIfTrue("[FusionAssembly] Queue built: " +
                $"P:{AndroidFusionRuntime.PlasteelReq} U:{AndroidFusionRuntime.UraniumReq} A:{AndroidFusionRuntime.AdvCompReq} targets={job.targetQueueB.Count}");

            return job;
        }

        private bool QueueIngredient(Pawn pawn, Job job, ThingDef def, int need)
        {
            int collected = 0;
            foreach (var stack in pawn.Map.listerThings.ThingsOfDef(def))
            {
                if (!ValidStack(pawn, stack)) continue;
                int still = need - collected;
                if (still <= 0) break;
                job.AddQueuedTarget(TargetIndex.B, stack);
                collected += stack.stackCount >= still ? still : stack.stackCount;
            }
            return collected >= need;
        }

        private bool QueueIngredientFlexible(Pawn pawn, Job job, string[] defNames, int need)
        {
            int collected = 0;
            foreach (var stack in pawn.Map.listerThings.AllThings)
            {
                if (!defNames.Contains(stack.def.defName)) continue;
                if (!ValidStack(pawn, stack)) continue;
                int still = need - collected;
                if (still <= 0) break;
                job.AddQueuedTarget(TargetIndex.B, stack);
                collected += stack.stackCount >= still ? still : stack.stackCount;
            }
            return collected >= need;
        }

        private bool ValidStack(Pawn pawn, Thing stack)
        {
            if (stack.stackCount <= 0) return false;
            if (stack.IsForbidden(pawn)) return false;
            if (!pawn.CanReach(stack, PathEndMode.Touch, Danger.Some)) return false;
            return true;
        }
    }

    internal static class BoolLogExtension
    {
        public static void LogIfTrue(this bool flag, string msg)
        {
            if (flag) Log.Message(msg);
        }
    }
}