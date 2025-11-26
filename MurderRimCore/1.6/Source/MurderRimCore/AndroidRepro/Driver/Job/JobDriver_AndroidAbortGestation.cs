using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_AndroidAbortGestation : JobDriver
    {
        private const int Duration = 300; // 5 seconds to purge
        private Sustainer _sustainer;

        private CompAndroidReproduction Station => TargetA.Thing.TryGetComp<CompAndroidReproduction>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Station == null);

            // 1. Go to the machine
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // 2. Perform the Purge
            Toil purge = Toils_General.Wait(Duration, TargetIndex.A);
            purge.WithProgressBarToilDelay(TargetIndex.A);

            // --- START SOUND ---
            purge.AddPreInitAction(() =>
            {
                if (SoundDefOf.Hacking_Started != null)
                    SoundDefOf.Hacking_Started.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            });

            purge.tickAction = () =>
            {
                // --- SUSTAINER LOOP ---
                if (SoundDefOf.Hacking_InProgress != null)
                {
                    if (_sustainer == null || _sustainer.Ended)
                    {
                        _sustainer = SoundDefOf.Hacking_InProgress.TrySpawnSustainer(SoundInfo.InMap(pawn, MaintenanceType.PerTick));
                    }
                    _sustainer.Maintain();
                }
            };

            // Cleanup Sustainer
            purge.AddFinishAction(() =>
            {
                if (_sustainer != null && !_sustainer.Ended)
                {
                    _sustainer.End();
                    _sustainer = null;
                }
            });

            yield return purge;

            // 3. Trigger the Reset
            yield return new Toil
            {
                initAction = () =>
                {
                    // --- COMPLETION SOUND ---
                    if (SoundDefOf.Hacking_Suspended != null)
                        SoundDefOf.Hacking_Suspended.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

                    Station.ForceAbortGestation();
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}