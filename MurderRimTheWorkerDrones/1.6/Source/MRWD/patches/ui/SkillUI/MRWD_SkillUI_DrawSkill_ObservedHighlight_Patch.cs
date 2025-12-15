using HarmonyLib;
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MRWD.Patches
{
    // Highlight the skill row that was recently improved via observation.
    // Matches: public static void DrawSkill(SkillRecord skill, Rect holdingRect, SkillUI.SkillDrawMode mode, string tooltipPrefix = "")
    [HarmonyPatch(typeof(SkillUI), nameof(SkillUI.DrawSkill), new Type[] { typeof(SkillRecord), typeof(Rect), typeof(SkillUI.SkillDrawMode), typeof(string) })]
    public static class MRWD_SkillUI_DrawSkill_ObservedHighlight_Patch
    {
        static void Postfix(SkillRecord skill, Rect holdingRect, SkillUI.SkillDrawMode mode, string tooltipPrefix)
        {
            try
            {
                if (skill == null) return;
                Pawn pawn = skill.Pawn;
                if (pawn == null) return;

                float intensity;
                if (!ObservationLearningUI.ShouldHighlight(pawn, skill.def, out intensity)) return;

                // Draw a thin cyan bar on the left edge of the skill row
                var bar = new Rect(holdingRect.x, holdingRect.y, 3f, holdingRect.height);
                var c = new Color(0.2f, 0.9f, 1f, Mathf.Clamp01(intensity));
                Widgets.DrawBoxSolid(bar, c);
            }
            catch (Exception e)
            {
                Log.Warning("[MRWD] Skill highlight UI error: " + e);
            }
        }
    }
}
