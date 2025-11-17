using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.TimetablePreventsLayDown))]
    public static class MurderRimCore_RestUtility_TimetablePreventsLayDown_Patch
    {
        static bool Prefix(Pawn pawn, ref bool __result)
        {
            var timetable = pawn.timetable;
            if (timetable != null && timetable.CurrentAssignment != null && !timetable.CurrentAssignment.allowRest)
            {
                var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
                if (sleepMode != null)
                {
                    __result = sleepMode.CurLevel >= 0.2f;
                    return false;
                }
            }
            return true;
        }
    }
}
