using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MurderRimCore
{
    // Gene-only settings for observational learning visuals and basic tuning.
    // Idle-only policy is hardcoded in util (watchers learn only when idle/wait/wander; joy is blocked).
    public class ObservationLearningExtension : DefModExtension
    {
        // Effecter visuals
        public bool enableEffecter = true;
        public EffecterDef effecterDef;
        public string effecterDefName;
        public int effecterCooldownTicks = 180;   // 0 = no cooldown; <0 = default
        public float xpPerEffecterInterval = 0f;  // 0 = disabled (no XP gating)
        public bool attachEffecter = false;
        public int attachDurationTicks = 120;
        public bool extendDurationOnRetrigger = true;

        // Range and multipliers
        public int maxRange = 13;            // cells
        public float baseMultiplier = 0.18f; // rate at distance 0, before other mods
        public float minMultiplier = 0.05f;  // floor after all mods

        // Sight handling (range-first, rate-second)
        public float sightAboveNormalBonus = 0.10f; // +10% flat when Sight > 100%

        // Logic toggles (kept)
        public bool requireLineOfSight = true;
        public bool scaleByPassion = true;

        // Faction scope
        public bool includeOtherFactionPawns = false;

        // “Teacher” bonus: rate bonus if actor is skilled (cap still enforced)
        public int teacherSkillThreshold = 12;
        public float teacherBonus = 1.10f;

        // Optional: per-skill tuning
        public List<SkillMultiplier> perSkillMultipliers;

        // NEW: Per-gene observation blacklist. If a skill is listed here, no XP is granted by observation for it.
        public List<SkillDef> skillBlacklist;

        public float GetPerSkillMultiplier(SkillDef skill)
        {
            if (perSkillMultipliers == null || skill == null) return 1f;
            for (int i = 0; i < perSkillMultipliers.Count; i++)
            {
                var e = perSkillMultipliers[i];
                if (e != null && e.skill == skill) return e.multiplier;
            }
            return 1f;
        }

        public bool IsSkillBlacklisted(SkillDef skill)
        {
            if (skill == null || skillBlacklist == null) return false;
            for (int i = 0; i < skillBlacklist.Count; i++)
            {
                if (skillBlacklist[i] == skill) return true;
            }
            return false;
        }

        public EffecterDef ResolveEffecterDef()
        {
            if (effecterDef != null) return effecterDef;
            if (!string.IsNullOrEmpty(effecterDefName))
            {
                var ed = DefDatabase<EffecterDef>.GetNamedSilentFail(effecterDefName);
                if (ed != null) return ed;
            }
            return null;
        }
    }

    public class SkillMultiplier
    {
        public SkillDef skill;
        public float multiplier = 1f;
    }
}