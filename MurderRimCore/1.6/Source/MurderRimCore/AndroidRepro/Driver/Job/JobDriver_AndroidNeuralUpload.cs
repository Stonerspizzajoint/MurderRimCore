using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MurderRimCore.AndroidRepro
{
    public class JobDriver_AndroidNeuralUpload : JobDriver
    {
        private const int UploadDuration = 600;
        private Sustainer _sustainer;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed)) return false;
            return pawn.Reserve(TargetC, job, 2, 0, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.C);
            this.FailOn(() => TargetB.Pawn == null || TargetB.Pawn.Dead || !TargetB.Pawn.Spawned);

            // 1. Go to position
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // 2. Wait for Partner
            Toil waitForPartner = new Toil();
            waitForPartner.defaultCompleteMode = ToilCompleteMode.Never;
            waitForPartner.initAction = () => pawn.pather.StopDead();
            waitForPartner.tickAction = () =>
            {
                Pawn partner = TargetB.Pawn;
                Thing station = TargetC.Thing;
                pawn.rotationTracker.FaceTarget(station);

                bool partnerReady = partner.CurJobDef == job.def &&
                                    partner.Position.InHorDistOf(station.InteractionCell, 2.9f);

                if (partnerReady) ReadyForNextToil();
                else
                {
                    if (pawn.IsHashIntervalTick(100))
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Syncing...", Color.cyan);
                }
            };
            yield return waitForPartner;

            // 3. THE UPLOAD
            Toil uploadToil = Toils_General.Wait(UploadDuration, TargetIndex.C);

            // Check if we are the Primary Source (Parent A)
            // We calculate this once here to keep the logic clean
            var comp = TargetC.Thing.TryGetComp<CompAndroidReproduction>();
            bool isParentA = (comp != null && comp.ParentA == pawn);

            // --- START SOUND (Only Parent A) ---
            uploadToil.AddPreInitAction(() =>
            {
                if (isParentA && SoundDefOf.Hacking_Started != null)
                    SoundDefOf.Hacking_Started.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            });

            // --- PROGRESS BAR (Only Parent A) ---
            if (isParentA)
            {
                uploadToil.WithProgressBarToilDelay(TargetIndex.C);
            }

            uploadToil.tickAction = () =>
            {
                // Visuals (Everyone throws hearts)
                if (pawn.IsHashIntervalTick(100))
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }

                // --- SUSTAINER LOOP (Only Parent A) ---
                if (isParentA && SoundDefOf.Hacking_InProgress != null)
                {
                    if (_sustainer == null || _sustainer.Ended)
                    {
                        _sustainer = SoundDefOf.Hacking_InProgress.TrySpawnSustainer(SoundInfo.InMap(pawn, MaintenanceType.PerTick));
                    }
                    _sustainer.Maintain();
                }
            };

            // Cleanup Sustainer
            uploadToil.AddFinishAction(() =>
            {
                if (_sustainer != null && !_sustainer.Ended)
                {
                    _sustainer.End();
                    _sustainer = null;
                }
            });

            yield return uploadToil;

            // 4. FINISH
            yield return new Toil
            {
                initAction = () =>
                {
                    // --- COMPLETE SOUND (Only Parent A) ---
                    if (isParentA && SoundDefOf.Hacking_Completed != null)
                        SoundDefOf.Hacking_Completed.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

                    var stationComp = TargetC.Thing.TryGetComp<CompAndroidReproduction>();
                    stationComp?.Notify_UploadComplete(pawn);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}