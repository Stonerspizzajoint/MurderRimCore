using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Hides specific inspect tabs (e.g., Feeding) for android babies.
    // This is done by filtering Thing.GetInspectTabs output for the pawn in question.
    [HarmonyPatch(typeof(Thing), "GetInspectTabs")]
    public static class HideTabsForAndroidBabies
    {
        // Add any additional tab types to hide here by their full type name.
        // Safe across versions: if a tab doesn't exist, it simply won't match.
        private static readonly string[] TabTypeFullNamesToHide = new[]
        {
            // Biotech feeding/settings tab for infants
            "RimWorld.ITab_Pawn_Feeding",

            // Examples you can enable if desired:
            // "RimWorld.ITab_Pawn_Gear",
            // "RimWorld.ITab_Pawn_Royalty",
            // "RimWorld.ITab_Pawn_Ideo",
        };

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, ref IEnumerable<InspectTabBase> __result)
        {
            try
            {
                if (__instance == null || __result == null) return;

                var pawn = __instance as Pawn;
                if (pawn == null) return;

                if (!IsAndroidBaby(pawn)) return;

                // Filter the tabs
                var list = __result.ToList();
                list.RemoveAll(tab => ShouldHide(tab));
                __result = list;
            }
            catch { /* non-fatal UI filter */ }
        }

        private static bool ShouldHide(InspectTabBase tab)
        {
            if (tab == null) return false;
            string full = tab.GetType().FullName ?? string.Empty;

            // Match exact full names listed above
            for (int i = 0; i < TabTypeFullNamesToHide.Length; i++)
            {
                if (string.Equals(full, TabTypeFullNamesToHide[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsAndroidBaby(Pawn pawn)
        {
            if (pawn == null) return false;

            // If our fused-newborn marker is present, treat as protected android baby
            try
            {
                if (FusedNewbornMarkerUtil.IsMarkedByHediff(pawn))
                    return true;
            }
            catch { }

            // Must be in baby stage
            if (pawn.DevelopmentalStage != DevelopmentalStage.Baby)
                return false;

            // Try VREAndroids.Utils.IsAndroid via reflection (no hard dependency)
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
    }
}
