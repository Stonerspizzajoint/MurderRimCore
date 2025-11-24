using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_FuseAtAndroidStation : JobDriver
    {
        private VREAndroids.Building_AndroidCreationStation Station
            => (VREAndroids.Building_AndroidCreationStation)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // DO NOT reserve the station (targetA); both parents must be able to use it.
            // Only reserve this pawn's slot cell (targetB).
            return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Station == null);

            // Don't reserve again; TryMakePreToilReservations already handled B.
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

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