using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Safety net: after any GenSpawn.Spawn, mark fused newborns, strip sentinel, ensure needs, and normalize relations.
    [HarmonyPatch]
    public static class FusedNewborn_PostSpawnPatch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var t = typeof(GenSpawn);
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var five = methods.FirstOrDefault(m =>
            {
                if (m.Name != "Spawn") return false;
                var p = m.GetParameters();
                return p.Length == 5 &&
                       p[0].ParameterType == typeof(Thing) &&
                       p[1].ParameterType == typeof(IntVec3) &&
                       p[2].ParameterType == typeof(Map) &&
                       p[3].ParameterType == typeof(WipeMode) &&
                       p[4].ParameterType == typeof(bool);
            });
            if (five != null) return five;

            var four = methods.FirstOrDefault(m =>
            {
                if (m.Name != "Spawn") return false;
                var p = m.GetParameters();
                return p.Length == 4 &&
                       p[0].ParameterType == typeof(Thing) &&
                       p[1].ParameterType == typeof(IntVec3) &&
                       p[2].ParameterType == typeof(Map) &&
                       p[3].ParameterType == typeof(WipeMode);
            });
            return four;
        }

        [HarmonyPostfix]
        public static void Postfix(Thing __result)
        {
            var pawn = __result as Pawn;
            if (pawn == null) return;

            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled) return;

            // Ensure marker if applicable
            if (!FusedNewbornMarkerUtil.IsMarkedByHediff(pawn))
            {
                bool looksFused = false;
                if (pawn.genes != null)
                {
                    string name = pawn.genes.xenotypeName ?? string.Empty;
                    looksFused = name.IndexOf(AndroidFusionUtility.NameSentinel, StringComparison.Ordinal) >= 0;
                }
                if (looksFused) FusedNewbornMarkerUtil.MarkWithHediff(pawn);
            }

            // Strip sentinel
            if (pawn.genes != null)
            {
                string nm = pawn.genes.xenotypeName ?? string.Empty;
                if (nm.IndexOf(AndroidFusionUtility.NameSentinel, StringComparison.Ordinal) >= 0)
                    pawn.genes.xenotypeName = nm.Replace(AndroidFusionUtility.NameSentinel, string.Empty).Trim();
            }
        }
    }
}