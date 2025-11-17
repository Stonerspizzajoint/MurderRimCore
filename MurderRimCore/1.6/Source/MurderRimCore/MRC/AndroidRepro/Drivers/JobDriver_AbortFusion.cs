using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_AbortFusion : JobDriver
    {
        private const int AbortDurationTicks = 120; // ~2 seconds

        private VREAndroids.Building_AndroidCreationStation Station => job?.targetA.Thing as VREAndroids.Building_AndroidCreationStation;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Station == null) return false;
            return pawn.Reserve(Station, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Allow abort if queued AND stage is Gestation OR Assembly
            this.FailOn(() =>
                Station == null ||
                !AndroidFusionRuntime.TryGetProcess(Station, out var proc) ||
                proc == null ||
                !AndroidFusionRuntime.IsAbortQueued(Station) ||
                (proc.Stage != FusionStage.Gestation && proc.Stage != FusionStage.Assembly));

            // Go to interaction cell
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Short wait with a progress bar
            var wait = Toils_General.WaitWith(TargetIndex.A, AbortDurationTicks, useProgressBar: true, maintainPosture: true);
            wait.socialMode = RandomSocialMode.Off;
            yield return wait;

            // Perform the abort
            var doAbort = new Toil
            {
                initAction = () =>
                {
                    string stageLabel = "Fusion";
                    if (AndroidFusionRuntime.TryGetProcess(Station, out var p) && p != null)
                    {
                        stageLabel = p.Stage == FusionStage.Gestation ? "Gestation"
                                   : p.Stage == FusionStage.Assembly ? "Assembly"
                                   : "Fusion";
                    }

                    AndroidFusionRuntime.Abort(Station, "Aborted by colonist.");
                    // Abort() already clears any queued flag; no need to call ClearAbortQueued again.
                    Messages.Message($"{stageLabel} aborted.", Station, MessageTypeDefOf.NegativeEvent);
                    EndJobWith(JobCondition.Succeeded);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return doAbort;
        }
    }
}