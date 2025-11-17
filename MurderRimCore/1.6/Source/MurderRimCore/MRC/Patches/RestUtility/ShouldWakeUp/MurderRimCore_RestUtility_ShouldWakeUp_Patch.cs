using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.ShouldWakeUp))]
    public static class MurderRimCore_RestUtility_ShouldWakeUp_Patch
    {
        static bool Prefix(Pawn pawn, ref bool __result)
        {
            var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
            if (sleepMode != null)
            {
                float wakeThreshold = 1f; // You can use your own logic or copy from vanilla
                __result = sleepMode.CurLevel >= wakeThreshold || pawn.health.hediffSet.HasHediffBlocksSleeping();
                return false;
            }
            return true;
        }
    }
}
