using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System;
using Verse.AI.Group;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(JobGiver_GetRest), nameof(JobGiver_GetRest.GetPriority))]
    public static class MurderRimCore_JobGiver_GetRest_GetPriority_Patch
    {
        private static readonly AccessTools.FieldRef<JobGiver_GetRest, RestCategory> minCategoryRef =
            AccessTools.FieldRefAccess<JobGiver_GetRest, RestCategory>("minCategory");
        private static readonly AccessTools.FieldRef<JobGiver_GetRest, float> maxLevelPercentageRef =
            AccessTools.FieldRefAccess<JobGiver_GetRest, float>("maxLevelPercentage");

        static bool Prefix(JobGiver_GetRest __instance, Pawn pawn, ref float __result)
        {
            var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
            if (sleepMode != null)
            {
                RestCategory minCategory = minCategoryRef(__instance);
                float maxLevelPercentage = maxLevelPercentageRef(__instance);

                if (sleepMode.CurCategory < minCategory)
                {
                    __result = 0f;
                    return false;
                }
                if (sleepMode.CurLevelPercentage > maxLevelPercentage)
                {
                    __result = 0f;
                    return false;
                }
                if (Find.TickManager.TicksGame < pawn.mindState.canSleepTick)
                {
                    __result = 0f;
                    return false;
                }
                Lord lord = pawn.GetLord();
                if (lord != null && !lord.CurLordToil.AllowSatisfyLongNeeds)
                {
                    __result = 0f;
                    return false;
                }
                if (!RestUtility.CanFallAsleep(pawn))
                {
                    __result = 0f;
                    return false;
                }
                TimeAssignmentDef timeAssignmentDef;
                if (pawn.RaceProps.Humanlike)
                {
                    timeAssignmentDef = ((pawn.timetable == null) ? TimeAssignmentDefOf.Anything : pawn.timetable.CurrentAssignment);
                }
                else
                {
                    int num = GenLocalDate.HourOfDay(pawn);
                    if (num < 7 || num > 21)
                    {
                        timeAssignmentDef = TimeAssignmentDefOf.Sleep;
                    }
                    else
                    {
                        timeAssignmentDef = TimeAssignmentDefOf.Anything;
                    }
                }
                float curLevel = sleepMode.CurLevel;
                if (timeAssignmentDef == TimeAssignmentDefOf.Anything)
                {
                    __result = (curLevel < 0.3f) ? 8f : 0f;
                    return false;
                }
                else if (timeAssignmentDef == TimeAssignmentDefOf.Work)
                {
                    __result = 0f;
                    return false;
                }
                else if (timeAssignmentDef == TimeAssignmentDefOf.Meditate)
                {
                    __result = (curLevel < 0.16f) ? 8f : 0f;
                    return false;
                }
                else if (timeAssignmentDef == TimeAssignmentDefOf.Joy)
                {
                    __result = (curLevel < 0.3f) ? 8f : 0f;
                    return false;
                }
                else if (timeAssignmentDef == TimeAssignmentDefOf.Sleep)
                {
                    __result = 8f;
                    return false;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            return true; // Run vanilla for pawns without your need
        }
    }
}

