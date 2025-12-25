using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(WorkGiver_RepairAndroid), "JobOnThing")]
    public static class Patch_WorkGiver_RepairAndroid_JobOnThing
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (__result != null)
            {
                Pawn patient = t as Pawn;
                if (patient != null && patient.IsRobotic())
                {
                    // FORCE the job to loop back for repairs
                    __result.endAfterTendedOnce = false;
                }
            }
        }
    }
}