using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
    public static class MurderRimCore_JobGiver_GetRest_TryGiveJob_Patch
    {
        static bool Prefix(Pawn pawn, ref Job __result)
        {
            // Use your custom need if present
            var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
            if (sleepMode != null)
            {
                // Mimic vanilla logic, but use sleepMode.CurLevel and thresholds
                if (sleepMode.CurLevel < 0.75f) // or your own threshold
                {
                    // Find a bed using RestUtility (already works for any pawn)
                    Building_Bed bed = RestUtility.FindBedFor(pawn);
                    if (bed != null)
                    {
                        __result = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                        return false; // Skip vanilla
                    }
                    // If no bed, rest on ground
                    __result = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
                    return false;
                }
                // No rest needed
                __result = null;
                return false;
            }
            return true; // Run vanilla for pawns without your need
        }
    }
}

