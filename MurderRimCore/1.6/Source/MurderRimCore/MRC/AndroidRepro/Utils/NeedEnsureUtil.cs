using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    public static class NeedEnsureUtil
    {
        // Default needs to ensure for android babies
        private static readonly string[] DefaultAndroidBabyNeedDefs = new[]
        {
            "MRC_SleepMode",
            "VREA_ReactorPower"
        };

        public static void EnsureAndroidBabyNeeds(Pawn pawn)
        {
            if (pawn == null || pawn.needs == null) return;
            EnsureNeeds(pawn, DefaultAndroidBabyNeedDefs);
        }

        public static void EnsureNeeds(Pawn pawn, params string[] needDefNames)
        {
            if (pawn == null || pawn.needs == null || needDefNames == null || needDefNames.Length == 0) return;

            // Prefer the internal AddNeed(NeedDef) method if present
            MethodInfo addNeedMi = AccessTools.Method(typeof(Pawn_NeedsTracker), "AddNeed", new Type[] { typeof(NeedDef) });

            for (int i = 0; i < needDefNames.Length; i++)
            {
                string defName = needDefNames[i];
                if (string.IsNullOrEmpty(defName)) continue;

                NeedDef nd = DefDatabase<NeedDef>.GetNamedSilentFail(defName);
                if (nd == null) continue; // Not loaded; skip

                // Already has it
                if (pawn.needs.TryGetNeed(nd) != null) continue;

                try
                {
                    if (addNeedMi != null)
                    {
                        // Use RimWorld's own helper to add the need properly
                        addNeedMi.Invoke(pawn.needs, new object[] { nd });
                    }
                    else
                    {
                        // Fallback: construct and inject manually
                        // Create instance of the need class with pawn ctor
                        Need need = (Need)Activator.CreateInstance(nd.needClass, new object[] { pawn });
                        // Assign def (public field on Need)
                        FieldInfo defField = AccessTools.Field(typeof(Need), "def");
                        if (defField != null) defField.SetValue(need, nd);

                        // Append to internal needs list
                        FieldInfo needsField = AccessTools.Field(typeof(Pawn_NeedsTracker), "needs");
                        List<Need> list = needsField != null ? (List<Need>)needsField.GetValue(pawn.needs) : null;
                        if (list != null) list.Add(need);

                        // Initialize level
                        need.SetInitialLevel();
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("[MRC-Repro] Failed to ensure need '" + defName + "' on " + (pawn.Name != null ? pawn.Name.ToStringShort : "pawn") + ": " + e.Message);
                }
            }
        }
    }
}
