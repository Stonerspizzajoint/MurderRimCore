using HarmonyLib;
using Verse;

namespace MRWD.Patches
{
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class Pawn_SpawnSetup_InvalidateObservationCache
    {
        public static void Postfix(Pawn __instance)
        {
            try
            {
                ObservationLearningUtil.InvalidateMapCache(__instance?.Map);
            }
            catch { }
        }
    }
}
