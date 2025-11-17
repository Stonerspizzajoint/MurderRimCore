using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.Patches
{
    // Minimal hook: buffer actor XP only if the map has observation-capable watchers cached.
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class SkillRecord_Learn_ObservationBuffer
    {
        public static void Postfix(SkillRecord __instance, float xp, bool direct)
        {
            try
            {
                if (xp <= 0f || __instance == null) return;
                var actor = __instance.Pawn;
                var map = actor?.Map;
                if (map == null) return;

                var obsCache = map.GetComponent<MapComponent_ObservationCache>();
                if (obsCache == null || obsCache.Watchers.Count == 0) return;

                map.GetComponent<ObservationXpBuffer>()?.AddXP(actor, __instance.def, xp);
            }
            catch (Exception e)
            {
                Log.Error("[MurderRimCore] Observation learning buffer error: " + e);
            }
        }
    }
}