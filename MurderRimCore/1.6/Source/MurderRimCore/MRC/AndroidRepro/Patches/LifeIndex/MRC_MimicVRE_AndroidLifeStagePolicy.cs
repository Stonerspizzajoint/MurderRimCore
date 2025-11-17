using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Replacement for VRE's android life stage policy:
    // - Androids: force adult stage just like VRE did, EXCEPT protected fused newborns.
    // - Protected fused newborns (have marker, under age limit): keep age-appropriate stage (baby).
    // - Non-androids: do nothing (let vanilla/other mods run).
    [HarmonyPatch(typeof(Pawn_AgeTracker), "RecalculateLifeStageIndex")]
    public static class MRC_MimicVRE_AndroidLifeStagePolicy
    {
        [HarmonyPrefix]
        [HarmonyPriority(2147483647)]
        public static bool Prefix(object __instance)
        {
            if (__instance == null) return true;

            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled) return true;

            var pawnField = AccessTools.Field(__instance.GetType(), "pawn");
            if (pawnField == null) return true;
            var pawn = pawnField.GetValue(__instance) as Pawn;
            if (pawn == null) return true;

            if (!IsAndroidCompat(pawn)) return true;

            bool isProtected = FusedNewbornMarkerUtil.IsMarkedByHediff(pawn) &&
                               pawn.ageTracker.AgeBiologicalYearsFloat < s.fusedAndroidBabyStageYearsMax;

            if (isProtected)
            {
                // Keep age-appropriate (baby) and ensure needs
                bool allowOriginal = ApplyLifeStageByAge(__instance, pawn, pawn.ageTracker.AgeBiologicalYearsFloat);
                NeedEnsureUtil.EnsureAndroidBabyNeeds(pawn);
                return allowOriginal; // usually false (we handled it)
            }

            // Mimic VRE: force adult for androids
            SetLifeStageToAdult(__instance, pawn);

            // We fully handled androids; skip original method and other prefixes
            return false;
        }

        // Try to detect android without hard-compile dependency on VREAndroids
        private static bool IsAndroidCompat(Pawn pawn)
        {
            // Try VREAndroids.Utils.IsAndroid via reflection
            try
            {
                var t = AccessTools.TypeByName("VREAndroids.Utils");
                if (t != null)
                {
                    var m = AccessTools.Method(t, "IsAndroid", new Type[] { typeof(Pawn) });
                    if (m != null)
                    {
                        object res = m.Invoke(null, new object[] { pawn });
                        if (res is bool && (bool)res) return true;
                    }
                }
            }
            catch { }

            // Fallback heuristic: race defName contains "android"
            string dn = pawn.def != null ? (pawn.def.defName ?? "") : "";
            if (dn.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        // Compute life stage from age and apply; returns false to skip original when handled,
        // or true to let original run if we cannot determine stages.
        private static bool ApplyLifeStageByAge(object ageTracker, Pawn pawn, float ageYears)
        {
            var stages = pawn.RaceProps.lifeStageAges;
            if (stages == null || stages.Count == 0) return true; // let vanilla handle

            int currentIndex = GetLifeStageIndex(ageTracker);
            int targetIndex = ComputeLifeStageIndexForAge(stages, ageYears);
            if (targetIndex < 0) targetIndex = 0;

            if (currentIndex == targetIndex)
            {
                // No change; we still handled and want to block VRE/others re-forcing adult
                return false;
            }

            SetLifeStageIndex(ageTracker, targetIndex);

            var lifeStageChangeField = AccessTools.Field(ageTracker.GetType(), "lifeStageChange");
            if (lifeStageChangeField != null) lifeStageChangeField.SetValue(ageTracker, true);

            var growthField = AccessTools.Field(ageTracker.GetType(), "growth");
            if (growthField != null)
            {
                float growth = ComputeGrowthFraction(stages, ageYears, targetIndex);
                growthField.SetValue(ageTracker, growth);
            }

            QueueGraphicsRefresh(pawn);
            TryCheckChangePawnKindName(pawn);
            NotifyLifeStageStarted(pawn, stages, targetIndex);
            RefreshDynamicComponents(pawn);

            return false; // handled
        }

        // Mimic VRE behavior: force adult stage for androids (outside protected window)
        private static void SetLifeStageToAdult(object ageTracker, Pawn pawn)
        {
            var stages = pawn.RaceProps.lifeStageAges;
            if (stages == null || stages.Count == 0) return;

            int adultIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                var lsa = stages[i];
                if (lsa == null) continue;
                if (lsa.def == LifeStageDefOf.HumanlikeAdult ||
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
            var fieldCached = AccessTools.Field(ageTracker.GetType(), "cachedLifeStageIndex");
            if (fieldCached != null)
            {
                object val = fieldCached.GetValue(ageTracker);
                if (val is int) return (int)val;
            }
            var fieldCur = AccessTools.Field(ageTracker.GetType(), "curLifeStageIndex");
            if (fieldCur != null)
            {
                object val = fieldCur.GetValue(ageTracker);
                if (val is int) return (int)val;
            }
            return -1;
        }

        private static void SetLifeStageIndex(object ageTracker, int idx)
        {
            var fieldCached = AccessTools.Field(ageTracker.GetType(), "cachedLifeStageIndex");
            if (fieldCached != null) { fieldCached.SetValue(ageTracker, idx); return; }

            var fieldCur = AccessTools.Field(ageTracker.GetType(), "curLifeStageIndex");
            if (fieldCur != null) { fieldCur.SetValue(ageTracker, idx); }
        }

        private static int ComputeLifeStageIndexForAge(System.Collections.Generic.List<LifeStageAge> list, float ageYears)
        {
            if (list == null || list.Count == 0) return -1;
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var lsa = list[i];
                if (lsa == null) continue;
                if (ageYears >= lsa.minAge) idx = i;
                else break;
            }
            if (idx < 0) idx = 0;
            return idx;
        }

        private static float ComputeGrowthFraction(System.Collections.Generic.List<LifeStageAge> list, float ageYears, int currentIndex)
        {
            if (list == null || currentIndex < 0 || currentIndex >= list.Count) return 1f;
            float currentMin = list[currentIndex] != null ? list[currentIndex].minAge : 0f;
            float nextMin = (currentIndex + 1 < list.Count && list[currentIndex + 1] != null)
                ? list[currentIndex + 1].minAge
                : currentMin + 1f;
            float span = Math.Max(0.0001f, nextMin - currentMin);
            return Mathf.Clamp01((ageYears - currentMin) / span);
        }

        private static void QueueGraphicsRefresh(Pawn pawn)
        {
            LongEventHandler.ExecuteWhenFinished(delegate
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

        private static void NotifyLifeStageStarted(Pawn pawn, System.Collections.Generic.List<LifeStageAge> stages, int idx)
        {
            try
            {
                LifeStageWorker worker = (pawn.ageTracker.CurLifeStage != null) ? pawn.ageTracker.CurLifeStage.Worker : null;
                LifeStageAge lsa = (idx >= 0 && idx < stages.Count) ? stages[idx] : null;
                if (worker != null) worker.Notify_LifeStageStarted(pawn, lsa != null ? lsa.def : null);
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