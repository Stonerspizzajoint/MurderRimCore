using HarmonyLib;
using Verse;
using RimWorld;
using VEF.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(ConditionalGraphicSet), nameof(ConditionalGraphicSet.GetState))]
    public static class MurderRimCore_ConditionalGraphicSet_GetState_Patch
    {
        static void Postfix(ConditionalGraphicSet __instance, Pawn pawn, PawnRenderNode node, ref bool __result)
        {
            if (__result)
                return;

            if (__instance.tagRequirements != null && __instance.tagRequirements.Contains("Sleeping"))
            {
                bool isLayingDown = pawn.CurJobDef == JobDefOf.LayDown;
                bool isActuallyAsleep = false;
                if (pawn.jobs?.curDriver is JobDriver_LayDown lay)
                    isActuallyAsleep = lay.asleep;

                Log.Message($"[MurderRimCore] Pawn {pawn} isLayingDown: {isLayingDown}, isActuallyAsleep: {isActuallyAsleep}, CurJobDef: {pawn.CurJobDef}, curDriver: {pawn.jobs?.curDriver}");

                // Trigger for either laying down or asleep, adjust as needed
                if (isLayingDown && isActuallyAsleep)
                {
                    __result = true;
                }
            }
        }
    }
}
