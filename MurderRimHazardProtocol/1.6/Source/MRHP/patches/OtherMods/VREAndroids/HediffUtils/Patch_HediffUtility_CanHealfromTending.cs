using System;
using HarmonyLib;
using Verse;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(HediffUtility), "CanHealFromTending")]
    public static class Patch_HediffUtility_CanHealfromTending
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Hediff_Injury hd, ref bool __result)
        {
            if (hd.pawn.IsRobotic())
            {
                __result = false;
            }
        }
    }
}