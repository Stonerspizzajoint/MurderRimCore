using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_FuseAtAndroidStation : JobDriver
    {
        private VREAndroids.Building_AndroidCreationStation Station
        {
            get { return (VREAndroids.Building_AndroidCreationStation)job.targetA.Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve ONLY the slot cell (targetB). Do NOT reserve the station.
            return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Station == null);

            // Reserve slot cell (explicit reserve toil for targetB only)
            yield return Toils_Reserve.Reserve(TargetIndex.B);

            // Go stand on the assigned slot
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Fusion work loop
            Toil fuse = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Never
            };
            fuse.initAction = delegate
            {
                FusionProcess proc;
                if (!AndroidFusionRuntime.TryGetProcess(Station, out proc))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            fuse.tickAction = delegate
            {
                FusionProcess proc;
                if (!AndroidFusionRuntime.TryGetProcess(Station, out proc))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (proc.Stage != FusionStage.Fusion)
                {
                    if (proc.Stage == FusionStage.Gestation || proc.Stage == FusionStage.Complete)
                        EndJobWith(JobCondition.Succeeded);
                    else if (proc.Stage == FusionStage.Aborted)
                        EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Contribute only if both parents are exactly in their slots.
                if (proc.ParentsInSlots)
                {
                    float speed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal, true, -1);
                    var s = AndroidReproductionSettingsDef.Current;
                    float delta = speed * (s != null ? s.fusionTickFactorPerStat : 1f);
                    AndroidFusionRuntime.NotifyFusionWork(Station, pawn, delta);
                }
            };
            yield return fuse;
        }
    }
}