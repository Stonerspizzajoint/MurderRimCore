using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public enum AndroidGrowthStage
    {
        None = 0,
        NewbornPill = 1,
        TeenFrame = 2,
        AdultFrame = 3
    }

    /// <summary>
    /// Central logic for android "growth" – driven by MRC_FusedGrowthMarker severity
    /// and overriding Pawn_AgeTracker life stage calculations.
    /// </summary>
    public static class AndroidGrowthUtil
    {
        private static readonly HediffDef GrowthMarkerDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("MRC_FusedGrowthMarker");

        /// <summary>
        /// Returns true if this pawn is an android with the growth marker and outputs its current growth stage.
        /// Model:
        /// - No marker / invalid: false
        /// - 0.0 .. &lt; 0.25 : NewbornPill
        /// - &gt;= 0.25       : TeenFrame (adult body)
        /// </summary>
        public static bool TryGetGrowthStage(Pawn pawn, out AndroidGrowthStage stage)
        {
            stage = AndroidGrowthStage.None;
            if (pawn == null || pawn.RaceProps == null) return false;
            if (!Utils.IsAndroid(pawn)) return false;
            if (GrowthMarkerDef == null) return false;

            Hediff marker = pawn.health != null
                ? pawn.health.hediffSet.GetFirstHediffOfDef(GrowthMarkerDef)
                : null;
            if (marker == null) return false;

            float sev = marker.Severity;

            if (sev >= 0.25f - 0.0001f)
            {
                stage = AndroidGrowthStage.TeenFrame;
            }
            else
            {
                stage = AndroidGrowthStage.NewbornPill;
            }

            return true;
        }

        /// <summary>
        /// Applies a life stage consistent with the growth stage, overriding vanilla/VRE logic.
        /// Returns false to skip original RecalculateLifeStageIndex.
        /// </summary>
        public static bool ApplyGrowthLifeStage(object ageTrackerObj, Pawn pawn, AndroidGrowthStage growthStage)
        {
            List<LifeStageAge> stages = pawn.RaceProps != null ? pawn.RaceProps.lifeStageAges : null;
            if (stages == null || stages.Count == 0)
                return true; // let vanilla handle if race is weird

            Type ageTrackerType = ageTrackerObj.GetType();

            int targetIndex = ComputeLifeStageIndexForGrowth(stages, growthStage, pawn);
            int currentIndex = GetLifeStageIndex(ageTrackerObj);

            if (currentIndex == targetIndex)
            {
                // We still handled it; block vanilla/VRE from overwriting.
                return false;
            }

            SetLifeStageIndex(ageTrackerObj, targetIndex);

            float ageYears = (pawn.ageTracker != null) ? pawn.ageTracker.AgeBiologicalYearsFloat : 0f;
            float growthFrac = ComputeGrowthFraction(stages, ageYears, targetIndex);
            var growthField = AccessTools.Field(ageTrackerType, "growth");
            if (growthField != null) growthField.SetValue(ageTrackerObj, growthFrac);

            var lifeStageChangeField = AccessTools.Field(ageTrackerType, "lifeStageChange");
            if (lifeStageChangeField != null) lifeStageChangeField.SetValue(ageTrackerObj, true);

            QueueGraphicsRefresh(pawn);
            TryCheckChangePawnKindName(pawn);
            NotifyLifeStageStarted(pawn, stages, targetIndex);
            RefreshDynamicComponents(pawn);

            return false; // fully handled
        }

        /// <summary>
        /// Map AndroidGrowthStage -> race lifeStage index.
        /// - NewbornPill =&gt; first stage (baby/newborn)
        /// - TeenFrame   =&gt; adult index
        /// - AdultFrame  =&gt; adult index
        /// </summary>
        private static int ComputeLifeStageIndexForGrowth(List<LifeStageAge> stages, AndroidGrowthStage growthStage, Pawn pawn)
        {
            if (stages == null || stages.Count == 0) return 0;

            int adultIndex = GetAdultLifeStageIndex(stages, pawn);

            switch (growthStage)
            {
                case AndroidGrowthStage.NewbornPill:
                    return 0;

                case AndroidGrowthStage.TeenFrame:
                case AndroidGrowthStage.AdultFrame:
                default:
                    return adultIndex;
            }
        }

        private static int GetAdultLifeStageIndex(List<LifeStageAge> stages, Pawn pawn)
        {
            // 1) Prefer explicit Adult flag on LifeStageDef
            for (int i = 0; i < stages.Count; i++)
            {
                LifeStageDef def = stages[i].def;
                if (def != null && (def.developmentalStage & DevelopmentalStage.Adult) != 0)
                    return i;
            }

            // 2) Fallback: based on Pawn_AgeTracker.AdultMinAge
            float adultMinAge = (pawn.ageTracker != null) ? pawn.ageTracker.AdultMinAge : stages.Last().minAge;
            int idx = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].minAge <= adultMinAge)
                    idx = i;
            }

            if (idx < 0) idx = stages.Count - 1;
            return idx;
        }

        private static int GetLifeStageIndex(object ageTracker)
        {
            Type type = ageTracker.GetType();
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
            Type type = ageTracker.GetType();
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

        private static float ComputeGrowthFraction(List<LifeStageAge> list, float ageYears, int currentIndex)
        {
            if (list == null || currentIndex < 0 || currentIndex >= list.Count)
                return 1f;

            float currentMin = list[currentIndex] != null ? list[currentIndex].minAge : 0f;
            float nextMin = (currentIndex + 1 < list.Count && list[currentIndex + 1] != null)
                ? list[currentIndex + 1].minAge
                : currentMin + 1f;

            float span = Mathf.Max(0.0001f, nextMin - currentMin);
            return Mathf.Clamp01((ageYears - currentMin) / span);
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
                catch
                {
                }
            });
        }

        private static void TryCheckChangePawnKindName(Pawn pawn)
        {
            try
            {
                pawn.ageTracker.CheckChangePawnKindName();
            }
            catch
            {
            }
        }

        private static void NotifyLifeStageStarted(Pawn pawn, List<LifeStageAge> stages, int idx)
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
            catch
            {
            }
        }

        private static void RefreshDynamicComponents(Pawn pawn)
        {
            try
            {
                if (pawn.SpawnedOrAnyParentSpawned)
                    PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, false);
            }
            catch
            {
            }
        }
    }
}