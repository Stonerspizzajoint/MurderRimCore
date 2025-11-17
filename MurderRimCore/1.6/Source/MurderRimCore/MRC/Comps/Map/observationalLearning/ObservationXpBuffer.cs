using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore
{
    // Buffers XP per (actor pawn, skill). Flushes periodically into ObservationDistributor.
    public class ObservationXpBuffer : MapComponent
    {
        private const int FlushIntervalTicks = 30; // tune in settings if desired
        private int lastFlushTick = -1;

        // Key: Pawn -> (SkillDef -> accumulated XP)
        private readonly Dictionary<Pawn, Dictionary<SkillDef, float>> _buffer =
            new Dictionary<Pawn, Dictionary<SkillDef, float>>(128);

        public ObservationXpBuffer(Map map) : base(map) { }

        public void AddXP(Pawn actor, SkillDef skill, float xp)
        {
            if (actor == null || skill == null || xp <= 0f) return;
            if (!actor.Spawned || actor.Map != map) return; // ensure same map

            if (!_buffer.TryGetValue(actor, out var skillMap))
            {
                skillMap = new Dictionary<SkillDef, float>();
                _buffer[actor] = skillMap;
            }

            if (skillMap.TryGetValue(skill, out var cur))
                skillMap[skill] = cur + xp;
            else
                skillMap[skill] = xp;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (lastFlushTick < 0) lastFlushTick = now;
            if (now - lastFlushTick < FlushIntervalTicks) return;

            lastFlushTick = now;
            if (_buffer.Count == 0) return;

            // Flush buffered XP into distributor queue.
            foreach (var kvActor in _buffer)
            {
                Pawn actor = kvActor.Key;
                if (actor == null || !actor.Spawned || actor.Map != map || actor.Dead)
                    continue;

                var skillMap = kvActor.Value;
                foreach (var kvSkill in skillMap)
                {
                    SkillDef skill = kvSkill.Key;
                    float totalXp = kvSkill.Value;
                    if (totalXp > 0f)
                        ObservationDistributor.Enqueue(actor, skill, totalXp);
                }
            }

            _buffer.Clear();
        }
    }

    // Performs the actual distribution of observation XP for one actor batch.
    public static class ObservationWork
    {
        private const int SightCacheInterval = 60;
        private static readonly Dictionary<int, (float sight, int tick)> _sightCache =
            new Dictionary<int, (float sight, int tick)>(256);

        public static void ProcessActor(Pawn actor, SkillDef skill, float accumulatedXp, int watcherBudget)
        {
            if (actor == null || skill == null || accumulatedXp <= 0f) return;
            if (!actor.Spawned || actor.Map == null) return;

            int actorLevel = GetSkillLevel(actor, skill);
            if (actorLevel <= 0) return;

            var map = actor.Map;
            var cache = map.GetComponent<MapComponent_ObservationCache>();
            if (cache == null) return;

            var watchers = cache.Watchers;
            if (watchers.Count == 0) return;

            IntVec3 aPos = actor.Position;
            int processed = 0;
            int globalMaxSq = cache.GlobalMaxRange * cache.GlobalMaxRange;

            for (int i = 0; i < watchers.Count; i++)
            {
                if (processed >= watcherBudget) break;
                var entry = watchers[i];
                var w = entry.pawn;
                if (w == actor) continue;
                if (!FastState(w)) continue;

                int dx = w.Position.x - aPos.x;
                int dz = w.Position.z - aPos.z;
                int distSq = dx * dx + dz * dz;
                if (distSq > globalMaxSq) continue;

                float sight = GetSightCached(w);
                if (sight <= 0f) continue;

                int effRange = EffectiveRange(entry.ext, sight);
                if (effRange <= 0) continue;
                int effSq = effRange * effRange;
                if (distSq > effSq) continue;

                if (!DetailedEligibility(entry.ext, w, actor, effRange)) continue;

                var ws = w.skills;
                if (ws == null) continue;
                var wSkill = ws.GetSkill(skill);
                if (wSkill == null || wSkill.TotallyDisabled) continue;
                if (entry.ext.IsSkillBlacklisted(skill)) continue;
                if (wSkill.Level >= actorLevel) continue;

                float mult = CalcMultiplier(entry.ext, w, actor, wSkill, skill, actorLevel, effRange, distSq, sight);
                if (mult <= 0f) continue;

                float grant = accumulatedXp * mult;
                if (grant <= 0f) continue;

                grant = CapGrantToActorLevel(wSkill, actorLevel, grant);
                if (grant <= 0f) continue;

                ws.Learn(skill, grant, false);

                ObservationLearningUtil.TryPlayObservationEffecter(w, grant, entry.ext);
                ObservationLearningUI.MarkObserved(w, skill, grant);

                processed++;
            }
        }

        private static bool FastState(Pawn p)
        {
            if (p == null || p.Dead || p.Downed) return false;
            if (!p.Awake() || p.Drafted || p.InMentalState) return false;
            return true;
        }

        private static bool DetailedEligibility(ObservationLearningExtension ext, Pawn watcher, Pawn actor, int effRange)
        {
            if (!ext.includeOtherFactionPawns)
            {
                if (watcher.Faction != actor.Faction) return false;
            }
            else if (watcher.HostileTo(actor)) return false;

            var job = watcher.CurJob;
            if (job != null)
            {
                if (job.def?.joyKind != null) return false;
                if (!IsIdleish(job)) return false;
            }

            if (ext.requireLineOfSight && effRange > 5)
            {
                if (!GenSight.LineOfSight(watcher.Position, actor.Position, watcher.Map, false))
                    return false;
            }
            return true;
        }

        private static bool IsIdleish(Job job)
        {
            if (job == null || job.def == null) return true;
            var dc = job.def.driverClass;
            if (dc == null) return true;
            return typeof(Verse.AI.JobDriver_Wait).IsAssignableFrom(dc)
                || typeof(Verse.AI.JobDriver_Goto).IsAssignableFrom(dc);
        }

        private static int GetSkillLevel(Pawn p, SkillDef def)
        {
            if (p?.skills == null) return 0;
            return p.skills.GetSkill(def)?.Level ?? 0;
        }

        private static float GetSightCached(Pawn p)
        {
            int id = p.thingIDNumber;
            int now = Find.TickManager.TicksGame;
            if (_sightCache.TryGetValue(id, out var t))
            {
                if (now - t.tick <= SightCacheInterval) return t.sight;
            }
            float lvl = 1f;
            try
            {
                lvl = p.health?.capacities?.GetLevel(PawnCapacityDefOf.Sight) ?? 1f;
            }
            catch { }
            _sightCache[id] = (lvl, now);
            return lvl;
        }

        private static int EffectiveRange(ObservationLearningExtension ext, float sight)
        {
            int baseMax = ext.maxRange > 0 ? ext.maxRange : 13;
            double scaled = Math.Round(baseMax * sight);
            if (scaled < 1d) scaled = 1d;
            return (int)scaled;
        }

        private static float CalcMultiplier(
            ObservationLearningExtension ext,
            Pawn watcher,
            Pawn actor,
            SkillRecord watcherSkill,
            SkillDef skill,
            int actorLevel,
            int effRange,
            int distSq,
            float sight)
        {
            float baseMult = ext.baseMultiplier > 0f ? ext.baseMultiplier : 0.18f;
            float minMult = ext.minMultiplier > 0f ? ext.minMultiplier : 0.05f;

            float dist = (float)Math.Sqrt(distSq);
            float falloff = 1f - (dist / Math.Max(1f, effRange));
            if (falloff <= 0f) return 0f;

            float mult = baseMult * falloff;
            mult *= ext.GetPerSkillMultiplier(skill);

            if (ext.scaleByPassion)
            {
                if (watcherSkill.passion == Passion.Minor) mult *= 1.2f;
                else if (watcherSkill.passion == Passion.Major) mult *= 1.5f;
            }

            if (actorLevel >= (ext.teacherSkillThreshold > 0 ? ext.teacherSkillThreshold : 12))
                mult *= (ext.teacherBonus > 0f ? ext.teacherBonus : 1.10f);

            if (sight < 1f) mult *= sight;
            else if (sight > 1f) mult *= 1f + (ext.sightAboveNormalBonus > 0f ? ext.sightAboveNormalBonus : 0.10f);

            if (mult < minMult) mult = minMult;
            return mult;
        }

        private static float CapGrantToActorLevel(SkillRecord watcherSkill, int actorLevel, float grant)
        {
            if (watcherSkill.Level >= actorLevel) return 0f;
            if (watcherSkill.Level < actorLevel - 1) return grant;

            float toNext = watcherSkill.XpRequiredForLevelUp - watcherSkill.xpSinceLastLevel;
            if (toNext <= 0f) return 0f;
            return Math.Min(grant, toNext - 0.0001f);
        }
    }

    public static class ObservationDistributor
    {
        private struct Pending
        {
            public Pawn actor;
            public SkillDef skill;
            public float xp;
        }

        private static readonly Queue<Pending> _queue = new Queue<Pending>(256);

        // Tunables
        private const int MaxActorsPerTick = 12;
        private const int MaxWatchersPerActor = 25;

        public static void Enqueue(Pawn actor, SkillDef skill, float xp)
        {
            if (actor == null || skill == null || xp <= 0f) return;
            _queue.Enqueue(new Pending { actor = actor, skill = skill, xp = xp });
        }

        public static void Tick()
        {
            if (_queue.Count == 0) return;

            int actorsProcessed = 0;
            while (_queue.Count > 0 && actorsProcessed < MaxActorsPerTick)
            {
                var p = _queue.Dequeue();
                ObservationWork.ProcessActor(p.actor, p.skill, p.xp, MaxWatchersPerActor);
                actorsProcessed++;
            }
        }
    }
}
