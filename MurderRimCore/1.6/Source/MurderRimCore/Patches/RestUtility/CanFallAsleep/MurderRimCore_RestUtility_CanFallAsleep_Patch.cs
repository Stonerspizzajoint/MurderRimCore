using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanFallAsleep))]
    public static class MurderRimCore_RestUtility_CanFallAsleep_Patch
    {
        static bool Prefix(Pawn pawn, ref bool __result)
        {
            var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
            if (sleepMode != null)
            {
                float curLevel = sleepMode.CurLevel;
                float maxLevel = 0.75f; // You can use your own threshold logic here
                __result = curLevel < maxLevel && !pawn.health.hediffSet.HasHediffBlocksSleeping();
                return false; // Skip original
            }
            return true; // Run original for vanilla pawns
        }
    }
}
