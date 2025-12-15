using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(HealthCardUtility), "DrawOverviewTab")]
    public static class Patch_HealthCardUtility_DrawOverviewTab
    {
        // REFLECTION: Get reference to the private method 'DrawLeftRow'
        private static readonly MethodInfo DrawLeftRowMethod = AccessTools.Method(typeof(HealthCardUtility), "DrawLeftRow");

        [HarmonyPriority(Priority.High)]
        public static bool Prefix(ref float __result, Rect rect, Pawn pawn, float curY)
        {
            if (pawn.IsRobotic())
            {
                __result = DrawOverviewTabRobot(rect, pawn, curY);
                return false;
            }
            return true;
        }

        private static float DrawOverviewTabRobot(Rect leftRect, Pawn pawn, float curY)
        {
            curY += 4f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);

            // 1. SUMMARY TEXT: "[Gender] [PawnKindLabel], age [Biological Age]"
            string genderLabel = pawn.gender.GetLabel();
            string kindLabel = pawn.KindLabel;
            string ageLabel = pawn.ageTracker.AgeBiologicalYears.ToString();

            string summary = $"{genderLabel} {kindLabel}, age {ageLabel}";

            Widgets.Label(new Rect(0f, curY, leftRect.width, 34f), summary.CapitalizeFirst());

            GUI.color = Color.white;
            curY += 34f;

            // 2. CAPACITIES
            Text.Font = GameFont.Small;
            if (!pawn.Dead)
            {
                IEnumerable<PawnCapacityDef> capacities = DefDatabase<PawnCapacityDef>.AllDefs
                    .Where(x => x.showOnMechanoids) // Use mechanoid filter for robots
                    .OrderBy(x => x.listOrder);

                foreach (PawnCapacityDef cap in capacities)
                {
                    if (PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, cap))
                    {
                        PawnCapacityDef localCap = cap;
                        Pair<string, Color> efficiencyLabel = HealthCardUtility.GetEfficiencyLabel(pawn, cap);

                        Func<string> textGetter = delegate ()
                        {
                            if (pawn.Dead) return "";
                            return HealthCardUtility.GetPawnCapacityTip(pawn, localCap);
                        };

                        string label = !cap.labelMechanoids.NullOrEmpty() ? cap.labelMechanoids : cap.label;

                        // REFLECTION INVOKE
                        object[] parameters = new object[]
                        {
                            leftRect,
                            curY,
                            label.CapitalizeFirst(),
                            efficiencyLabel.First,
                            efficiencyLabel.Second,
                            new TipSignal(textGetter, pawn.thingIDNumber ^ (int)cap.index)
                        };

                        DrawLeftRowMethod.Invoke(null, parameters);

                        // Update curY because it was passed by ref in the original method
                        curY = (float)parameters[1];
                    }
                }
            }
            return curY;
        }
    }
}