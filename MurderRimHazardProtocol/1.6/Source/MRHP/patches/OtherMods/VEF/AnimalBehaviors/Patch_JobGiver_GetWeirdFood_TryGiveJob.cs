using HarmonyLib;
using Verse;
using Verse.AI;
using VEF.AnimalBehaviours;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(JobGiver_GetWeirdFood), "TryGiveJob")]
    public static class Patch_JobGiver_GetWeirdFood_TryGiveJob
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || pawn == null)
                return;

            if (__result.def != InternalDefOf.VEF_IngestWeird)
                return;

            if (pawn.TryGetComp<CompScrapEater>() == null)
                return;

            __result.def = MRHP_DefOf.MRHP_IngestScrap;
        }
    }
}
