using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MRWD
{
    public static class ObservationLearningUtil
    {
        private const float DefaultBaseMult = 0.18f;
        private const float DefaultMinMult = 0.05f;
        private const float DefaultSightBonusAboveNormal = 0.10f;

        // Single struct for effecter state
        private struct EffecterState
        {
            public int lastTick;
            public float xpAccum;
        }

        private static readonly Dictionary<int, EffecterState> _effecterStates = new Dictionary<int, EffecterState>(128);
        private static readonly HashSet<string> _missingEffecterDefsLogged = new HashSet<string>();

        public static Func<Pawn, bool> ExtraDronePredicate;

        // Entry point (Harmony patch)
        public static void ProcessObservationTick(SkillRecord actorSkillRecord, float actorXp)
        {
            if (actorXp <= 0f || actorSkillRecord == null) return;
            var actor = actorSkillRecord.Pawn;
            if (actor?.Spawned != true || actor.Map == null) return;

            var skill = actorSkillRecord.def;
            if (skill == null) return;

            int actorLevel = GetSkillLevel(actor, skill);
            if (actorLevel <= 0) return;

            var cache = actor.Map.GetComponent<MapComponent_ObservationCache>();
            if (cache == null) return;
            var watchers = cache.Watchers;
            if (watchers.Count == 0) return;

            // Quick bounding: if no watcher within global max radius squared, skip
            int gmr = cache.GlobalMaxRange;
            int gmrSq = gmr * gmr;

            // Pre-filter watchers near actor
            // (Optional: replace with GenRadial if gmr <= 15 for further culling)
            for (int i = 0; i < watchers.Count; i++)
            {
                var entry = watchers[i];
                var wPawn = entry.pawn;
                if (wPawn == actor) continue;

                // Basic eligibility early rejects
                if (!FastEligibleState(wPawn)) continue;

                // Distance squared check
                int dx = wPawn.Position.x - actor.Position.x;
                int dz = wPawn.Position.z - actor.Position.z;
                int distSq = dx * dx + dz * dz;
                if (distSq > gmrSq) continue;

                float sight = GetSightLevel(wPawn);
                if (sight <= 0f) continue;

                // Determine effective max range for this watcher
                int effRange = EffectiveMaxRange(entry.ext, sight);
                if (effRange <= 0) continue;
                int effSq = effRange * effRange;
                if (distSq > effSq) continue;

                // More detailed checks (LOS, faction, job gating)
                if (!DetailedEligibility(entry.ext, wPawn, actor, effRange)) continue;

                var wSkills = wPawn.skills;
                if (wSkills == null) continue;
                var wSkillRecord = wSkills.GetSkill(skill);
                if (wSkillRecord == null || wSkillRecord.TotallyDisabled) continue;

                if (entry.ext.IsSkillBlacklisted(skill)) continue;

                float mult = ObservationMultiplierFor(entry.ext, wPawn, actor, skill, wSkillRecord, actorLevel, effRange, distSq);
                if (mult <= 0f) continue;

                float observedXp = actorXp * mult;
                if (observedXp <= 0f) continue;

                float granted = GrantObservedXpWithCapFast(wPawn, wSkillRecord, observedXp, actorLevel);
                if (granted <= 0f) continue;

                TryPlayObservationEffecter(wPawn, granted, entry.ext);

                ObservationLearningUI.MarkObserved(wPawn, skill, granted);
            }
        }

        private static bool FastEligibleState(Pawn p)
        {
            if (p == null) return false;
            if (p.Dead || p.Downed) return false;
            if (!p.Awake()) return false;
            if (p.Drafted) return false;
            if (p.InMentalState) return false;
            return true;
        }

        private static bool DetailedEligibility(ObservationLearningExtension ext, Pawn watcher, Pawn actor, int effRange)
        {
            // Faction logic
            if (!ext.includeOtherFactionPawns)
            {
                if (watcher.Faction != actor.Faction) return false;
            }
            else if (watcher.HostileTo(actor))
            {
                return false;
            }

            // Job gating
            var job = watcher.CurJob;
            if (job != null)
            {
                if (job.def?.joyKind != null) return false;
                if (!IsWaitLikeOrWander(job)) return false;
            }

            // LOS
            if (ext.requireLineOfSight && !GenSight.LineOfSight(watcher.Position, actor.Position, watcher.Map, false))
                return false;

            // Redundant distance check omitted (already passed)
            return true;
        }

        // Optimized multiplier; use distSq to avoid recompute sqrt
        private static float ObservationMultiplierFor(
            ObservationLearningExtension ext,
            Pawn watcher,
            Pawn actor,
            SkillDef skill,
            SkillRecord watcherSkill,
            int actorLevel,
            int effRange,
            int distSq)
        {
            float baseMult = ext.baseMultiplier > 0f ? ext.baseMultiplier : DefaultBaseMult;
            float minMult = ext.minMultiplier > 0f ? ext.minMultiplier : DefaultMinMult;

            // Dist falloff using squared distance (approx linear by converting to distance)
            // We still need actual distance for linear falloff: dist = sqrt(distSq)
            // For performance, use Math.Sqrt once instead of repeated horizontal length calls.
            float dist = (float)Math.Sqrt(distSq);
            float falloff = 1f - (dist / Math.Max(1f, effRange));
            if (falloff <= 0f) return 0f;

            float mult = baseMult * falloff;

            mult *= ext.GetPerSkillMultiplier(skill);

            if (ext.scaleByPassion && watcherSkill != null)
            {
                if (watcherSkill.passion == Passion.Minor) mult *= 1.2f;
                else if (watcherSkill.passion == Passion.Major) mult *= 1.5f;
            }

            // Teacher bonus
            if (actorLevel >= (ext.teacherSkillThreshold > 0 ? ext.teacherSkillThreshold : 12))
            {
                float tb = ext.teacherBonus > 0f ? ext.teacherBonus : 1.10f;
                mult *= tb;
            }

            float sightLevel = GetSightLevel(watcher);
            if (sightLevel < 1f)
                mult *= sightLevel;
            else if (sightLevel > 1f)
                mult *= 1f + (ext.sightAboveNormalBonus > 0f ? ext.sightAboveNormalBonus : DefaultSightBonusAboveNormal);

            if (mult < minMult) mult = minMult;
            return mult;
        }

        private static int EffectiveMaxRange(ObservationLearningExtension ext, float sightLevel)
        {
            int baseMax = ext.maxRange > 0 ? ext.maxRange : 13;
            if (sightLevel <= 0f) return 0;
            double scaled = Math.Round(baseMax * sightLevel);
            if (scaled < 1d) scaled = 1d;
            return (int)scaled;
        }

        private static int GetSkillLevel(Pawn p, SkillDef def)
        {
            if (p?.skills == null || def == null) return 0;
            var sr = p.skills.GetSkill(def);
            return sr?.Level ?? 0;
        }

        // Closed-form XP cap (simplified)
        private static float GrantObservedXpWithCapFast(Pawn watcher, SkillRecord watcherSkill, float observedXp, int actorLevel)
        {
            if (watcherSkill.Level >= actorLevel) return 0f;

            // If last allowed level (actorLevel - 1), cap XP so we don't reach actorLevel
            if (watcherSkill.Level >= actorLevel - 1)
            {
                float toNext = watcherSkill.XpRequiredForLevelUp - watcherSkill.xpSinceLastLevel;
                if (toNext <= 0f) return 0f;
                float grant = Math.Min(observedXp, toNext - 0.0001f);
                if (grant <= 0f) return 0f;
                watcher.skills.Learn(watcherSkill.def, grant, false);
                return grant;
            }

            // Otherwise safe to grant full (RimWorld will roll levels internally)
            watcher.skills.Learn(watcherSkill.def, observedXp, false);

            // If we passed actorLevel, retroactively cap (rare with large XP chunks)
            if (watcherSkill.Level >= actorLevel)
            {
                // No rollback API; acceptable minor overshoot if extremely large observedXp
                // For exactness you'd need per-level iterative loop
            }
            return observedXp;
        }

        private static bool IsWaitLikeOrWander(Job job)
        {
            if (job == null || job.def == null) return true;
            var dc = job.def.driverClass;
            if (dc == null) return true;

            return typeof(JobDriver_Wait).IsAssignableFrom(dc)
                || typeof(JobDriver_Goto).IsAssignableFrom(dc)
                || typeof(JobDriver_GoForWalk).IsAssignableFrom(dc)
                || typeof(JobDriver_WaitDowned).IsAssignableFrom(dc)
                || typeof(JobDriver_WaitMaintainPosture).IsAssignableFrom(dc);
        }

        private static float GetSightLevel(Pawn p)
        {
            if (p?.health?.capacities == null) return 1f;
            try { return p.health.capacities.GetLevel(PawnCapacityDefOf.Sight); }
            catch { return 1f; }
        }

        public static void TryPlayObservationEffecter(Pawn watcher, float observedXpGranted, ObservationLearningExtension ext)
        {
            if (ext == null || !ext.enableEffecter) return;

            var effDef = ext.ResolveEffecterDef();
            if (effDef == null)
            {
                string tag = string.IsNullOrEmpty(ext.effecterDefName) ? "(unset)" : ext.effecterDefName;
                if (_missingEffecterDefsLogged.Add(tag))
                {
                    Log.Error("[MRWD] Observation effecter missing: " + tag);
                }
                return;
            }

            int key = watcher.thingIDNumber;
            int now = Find.TickManager.TicksGame;

            int cooldown = ext.effecterCooldownTicks > 0 ? ext.effecterCooldownTicks : 180;
            float xpInterval = ext.xpPerEffecterInterval > 0f ? ext.xpPerEffecterInterval : 0f;

            EffecterState st;
            _effecterStates.TryGetValue(key, out st);

            // XP gating
            if (xpInterval > 0f)
            {
                st.xpAccum += observedXpGranted;
                if (st.xpAccum < xpInterval)
                {
                    _effecterStates[key] = st;
                    return;
                }
                st.xpAccum -= xpInterval;
            }

            // Cooldown gating
            if (cooldown > 0 && st.lastTick > 0 && now - st.lastTick < cooldown)
            {
                _effecterStates[key] = st;
                return;
            }

            st.lastTick = now;
            _effecterStates[key] = st;

            if (ext.attachEffecter)
            {
                ObservationEffecterManager.Instance.EnsureAttached(
                    watcher,
                    effDef,
                    ext.attachDurationTicks <= 0 ? 60 : ext.attachDurationTicks,
                    ext.extendDurationOnRetrigger
                );
            }
            else
            {
                try
                {
                    var eff = effDef.Spawn();
                    TargetInfo ti = watcher;
                    eff.Trigger(ti, ti);
                    eff.Cleanup();
                }
                catch (Exception e)
                {
                    Log.Warning("[MRWD] Failed to trigger observation effecter: " + e);
                }
            }
        }

        public static void InvalidateMapCache(Map map)
        {
            var comp = map?.GetComponent<MapComponent_ObservationCache>();
            comp?.InvalidateCache();
        }
    }
}