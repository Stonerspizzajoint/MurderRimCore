using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    [HarmonyPatch(typeof(Pawn_AgeTracker), "RecalculateLifeStageIndex")]
    public static class MRC_MimicVRE_AndroidLifeStagePolicy
    {
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(object __instance)
        {
            if (__instance == null) return true;

            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled) return true;

            var pawnField = AccessTools.Field(__instance.GetType(), "pawn");
            if (pawnField == null) return true;

            var pawn = pawnField.GetValue(__instance) as Pawn;
            if (pawn == null) return true;

            // Only care about androids; VREAndroids is hard dep now
            if (!Utils.IsAndroid(pawn))
                return true;

            // If the pawn is on our growth track, we fully control life stages
            AndroidGrowthStage growthStage;
            if (AndroidGrowthUtil.TryGetGrowthStage(pawn, out growthStage) &&
                growthStage != AndroidGrowthStage.None)
            {
                bool isNewborn = growthStage == AndroidGrowthStage.NewbornPill;

                // Override life stage completely based on growth
                AndroidGrowthUtil.ApplyGrowthLifeStage(__instance, pawn, growthStage);

                // We own life stages for growth-tracked androids
                return false;
            }

            // No growth marker; fall back to "androids are adults" like VRE.
            SetLifeStageToAdult(__instance, pawn);
            return false;
        }

        // Mimic VRE behavior: force adult stage for androids (used only when no growth marker).
        private static void SetLifeStageToAdult(object ageTracker, Pawn pawn)
        {
            var stages = pawn.RaceProps != null ? pawn.RaceProps.lifeStageAges : null;
            if (stages == null || stages.Count == 0) return;

            int adultIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                var lsa = stages[i];
                if (lsa == null) continue;

                var def = lsa.def;
                if (def == LifeStageDefOf.HumanlikeAdult ||
                    ((def != null ? def.developmentalStage : DevelopmentalStage.None) & DevelopmentalStage.Adult) != 0 ||
                    (lsa.minAge >= 18f && lsa.minAge < float.MaxValue))
                {
                    adultIndex = i;
                    break;
                }
            }
            if (adultIndex < 0) adultIndex = stages.Count - 1;

            int currentIndex = GetLifeStageIndex(ageTracker);
            bool changed = currentIndex != adultIndex;

            SetLifeStageIndex(ageTracker, adultIndex);

            var growthField = AccessTools.Field(ageTracker.GetType(), "growth");
            if (growthField != null) growthField.SetValue(ageTracker, 1f);

            var lifeStageChangeField = AccessTools.Field(ageTracker.GetType(), "lifeStageChange");
            if (lifeStageChangeField != null && changed) lifeStageChangeField.SetValue(ageTracker, true);

            if (changed)
            {
                QueueGraphicsRefresh(pawn);
                TryCheckChangePawnKindName(pawn);
                NotifyLifeStageStarted(pawn, stages, adultIndex);
                RefreshDynamicComponents(pawn);
            }
        }

        private static int GetLifeStageIndex(object ageTracker)
        {
            var type = ageTracker.GetType();
            var fieldCached = AccessTools.Field(type, "cachedLifeStageIndex");
            if (fieldCached != null)
            {
                object val = fieldCached.GetValue(ageTracker);
                if (val is int) return (int)val;
            }
            var fieldCur = AccessTools.Field(type, "curLifeStageIndex");
            if (fieldCur != null)
            {
                object val = fieldCur.GetValue(ageTracker);
                if (val is int) return (int)val;
            }
            return -1;
        }

        private static void SetLifeStageIndex(object ageTracker, int idx)
        {
            var type = ageTracker.GetType();
            var fieldCached = AccessTools.Field(type, "cachedLifeStageIndex");
            if (fieldCached != null)
            {
                fieldCached.SetValue(ageTracker, idx);
                return;
            }

            var fieldCur = AccessTools.Field(type, "curLifeStageIndex");
            if (fieldCur != null)
            {
                fieldCur.SetValue(ageTracker, idx);
            }
        }

        private static void QueueGraphicsRefresh(Pawn pawn)
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                    if (pawn.IsColonist) PortraitsCache.SetDirty(pawn);
                }
                catch { }
            });
        }

        private static void TryCheckChangePawnKindName(Pawn pawn)
        {
            try { pawn.ageTracker.CheckChangePawnKindName(); } catch { }
        }

        private static void NotifyLifeStageStarted(
            Pawn pawn,
            System.Collections.Generic.List<LifeStageAge> stages,
            int idx)
        {
            try
            {
                LifeStageWorker worker = pawn.ageTracker.CurLifeStage != null
                    ? pawn.ageTracker.CurLifeStage.Worker
                    : null;
                LifeStageAge lsa = (idx >= 0 && idx < stages.Count) ? stages[idx] : null;
                if (worker != null)
                {
                    worker.Notify_LifeStageStarted(pawn, lsa != null ? lsa.def : null);
                }
            }
            catch { }
        }

        private static void RefreshDynamicComponents(Pawn pawn)
        {
            try
            {
                if (pawn.SpawnedOrAnyParentSpawned)
                    PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, false);
            }
            catch { }
        }
    }
}