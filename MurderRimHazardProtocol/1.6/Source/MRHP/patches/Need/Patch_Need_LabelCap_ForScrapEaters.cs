using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MRHP.patches
{
    // Replace the label shown above the need bar (Need.LabelCap getter) for the Food need only
    [HarmonyPatch(typeof(Need), "LabelCap", MethodType.Getter)]
    public static class Patch_Need_LabelCap_ForScrapEaters
    {
        public static void Postfix(Need __instance, ref string __result)
        {
            try
            {
                // Only modify the label for the Food need
                if (__instance.def != NeedDefOf.Food) return;

                Pawn pawn = (Pawn)AccessTools.Field(typeof(Need), "pawn").GetValue(__instance) as Pawn;
                if (pawn == null) return;

                var comp = pawn.TryGetComp<MRHP.CompScrapEater>();
                if (comp == null || comp.Props == null) return;

                string custom = comp.Props.materialNeedLabel;
                if (!string.IsNullOrWhiteSpace(custom))
                    __result = custom.CapitalizeFirst();
            }
            catch (Exception ex)
            {
                Log.Error($"[MRHP] Patch_Need_LabelCap_ForScrapEaters failed: {ex}");
            }
        }
    }

}