using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace MRHP.Patches
{
    // Intercept Need_Food.NeedInterval for pawns that have CompScrapEater and apply configured hediff
    [HarmonyPatch(typeof(Need_Food), "NeedInterval")]
    public static class Patch_NeedFood_NeedInterval_ForScrapEaters
    {
        public static bool Prefix(Need_Food __instance)
        {
            try
            {
                // Get pawn (protected field on Need)
                Pawn pawn = (Pawn)AccessTools.Field(typeof(Need), "pawn").GetValue(__instance);
                if (pawn == null) return true; // fallback to vanilla

                // Only intercept for pawns that have CompScrapEater
                var comp = pawn.TryGetComp<MRHP.CompScrapEater>();
                if (comp == null || comp.Props == null) return true; // not our pawn, run vanilla

                // Access protected IsFrozen property
                bool isFrozen = (bool)AccessTools.Property(typeof(Need_Food), "IsFrozen").GetValue(__instance, null);

                // Decay logic (mirrors vanilla Need_Food.NeedInterval)
                if (!isFrozen)
                {
                    float fall = __instance.FoodFallPerTick * 150f;
                    __instance.CurLevel -= fall;
                }

                // Update lastNonStarvingTick if not starving
                FieldInfo lastNonStarvingField = AccessTools.Field(typeof(Need_Food), "lastNonStarvingTick");
                var curCategory = (HungerCategory)AccessTools.Property(typeof(Need_Food), "CurCategory").GetValue(__instance, null);
                if (curCategory != HungerCategory.Starving)
                {
                    lastNonStarvingField.SetValue(__instance, Find.TickManager.TicksGame);
                }

                // If not frozen or pawn is deathresting, adjust configured deprivation hediff
                if (!isFrozen || pawn.Deathresting)
                {
                    // Compute severity change using same formula as vanilla MalnutritionSeverityPerInterval
                    int seed = pawn.thingIDNumber ^ 2551674;
                    float lerp = Mathf.Lerp(0.8f, 1.2f, Rand.ValueSeeded(seed));
                    float severityPerInterval = 0.0011325f * lerp;

                    // Resolve configured hediff name from comp props, fallback to Malnutrition
                    HediffDef targetDef = null;
                    string configuredHediff = comp.Props.materialNeedHediffDefName;
                    if (!string.IsNullOrWhiteSpace(configuredHediff))
                    {
                        targetDef = DefDatabase<HediffDef>.GetNamedSilentFail(configuredHediff);
                        if (targetDef == null)
                        {
                            Log.Warning($"[MRHP] materialNeedHediffDefName '{configuredHediff}' not found; falling back to Malnutrition.");
                        }
                    }

                    if (targetDef == null)
                        targetDef = HediffDefOf.Malnutrition;

                    if (curCategory == HungerCategory.Starving)
                    {
                        HealthUtility.AdjustSeverity(pawn, targetDef, severityPerInterval);
                    }
                    else
                    {
                        HealthUtility.AdjustSeverity(pawn, targetDef, -severityPerInterval);
                    }
                }

                // Skip original NeedInterval for this pawn
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[MRHP] Patch_NeedFood_NeedInterval_ForScrapEaters failed: {ex}");
                // On error, fall back to vanilla behavior
                return true;
            }
        }
    }

    // Replace the tooltip text shown when inspecting the need for pawns with CompScrapEater
    [HarmonyPatch(typeof(Need_Food), "GetTipString")]
    public static class Patch_NeedFood_GetTipString_ForScrapEaters
    {
        public static void Postfix(Need_Food __instance, ref string __result)
        {
            try
            {
                Pawn pawn = (Pawn)AccessTools.Field(typeof(Need), "pawn").GetValue(__instance);
                if (pawn == null) return;

                var comp = pawn.TryGetComp<MRHP.CompScrapEater>();
                if (comp == null || comp.Props == null) return;

                // Use configured label/description if present, otherwise sensible defaults
                string label = !string.IsNullOrWhiteSpace(comp.Props.materialNeedLabel)
                    ? comp.Props.materialNeedLabel.CapitalizeFirst()
                    : "Material intake".CapitalizeFirst();

                string description = !string.IsNullOrWhiteSpace(comp.Props.materialNeedDescription)
                    ? comp.Props.materialNeedDescription
                    : "Mechanical pawns have a designed urge to intake scrap material. Low intake reduces efficiency but will not cause death.";

                float pct = __instance.CurLevelPercentage;
                string percent = pct.ToStringPercent();
                string curLevel = __instance.CurLevel.ToString("0.##");
                string maxLevel = __instance.MaxLevel.ToString("0.##");

                __result = string.Concat(new string[]
                {
                    (label + ": " + percent).Colorize(ColoredText.TipSectionTitleColor),
                    " (",
                    curLevel,
                    " / ",
                    maxLevel,
                    ")\n",
                    description
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[MRHP] Patch_NeedFood_GetTipString_ForScrapEaters failed: {ex}");
            }
        }
    }
}