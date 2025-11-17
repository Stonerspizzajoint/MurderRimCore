using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore
{
    // Tracks "recently observed" skills for pawns, so we can highlight them in the Skills UI.
    public static class ObservationLearningUI
    {
        // How long after observation to highlight in the UI (in ticks)
        private const int HighlightDurationTicks = 240; // ~4 seconds at 60 TPS

        private struct Entry
        {
            public int lastTick;
            public float accumXp; // for optional intensity scaling (lightweight)
        }

        // Keyed by pawn.thingIDNumber -> SkillDef -> Entry
        private static readonly Dictionary<int, Dictionary<SkillDef, Entry>> _byPawn =
            new Dictionary<int, Dictionary<SkillDef, Entry>>(128);

        // Called whenever a pawn is granted observation XP
        public static void MarkObserved(Pawn pawn, SkillDef skill, float grantedXp)
        {
            if (pawn == null || skill == null || grantedXp <= 0f) return;

            int key = pawn.thingIDNumber;
            var now = Find.TickManager.TicksGame;

            Dictionary<SkillDef, Entry> map;
            if (!_byPawn.TryGetValue(key, out map))
            {
                map = new Dictionary<SkillDef, Entry>(8);
                _byPawn[key] = map;
            }

            Entry e;
            if (!map.TryGetValue(skill, out e))
            {
                e = new Entry { lastTick = now, accumXp = grantedXp };
            }
            else
            {
                e.lastTick = now;
                e.accumXp += grantedXp;
                if (e.accumXp > 100f) e.accumXp = 100f; // clamp
            }

            map[skill] = e;
        }

        // Returns true if we should highlight, with a 0..1 intensity for cosmetic effects
        public static bool ShouldHighlight(Pawn pawn, SkillDef skill, out float intensity)
        {
            intensity = 0f;
            if (pawn == null || skill == null) return false;

            int key = pawn.thingIDNumber;
            Dictionary<SkillDef, Entry> map;
            if (!_byPawn.TryGetValue(key, out map)) return false;

            Entry e;
            if (!map.TryGetValue(skill, out e)) return false;

            int age = Find.TickManager.TicksGame - e.lastTick;
            if (age >= HighlightDurationTicks) return false;

            // Fade out as time passes; optionally scale a bit with XP magnitude
            float timeFactor = 1f - (age / (float)HighlightDurationTicks);
            float xpFactor = Mathf.Clamp01(e.accumXp / 35f); // 35 xp ~ full boost
            intensity = Mathf.Clamp01(0.6f * timeFactor + 0.4f * xpFactor);

            return true;
        }

        // Optional: call on pawn despawn/death to keep the map tidy (not strictly necessary)
        public static void ClearFor(Pawn pawn)
        {
            if (pawn == null) return;
            _byPawn.Remove(pawn.thingIDNumber);
        }
    }
}
