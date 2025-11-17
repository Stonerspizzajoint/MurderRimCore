using HarmonyLib;
using Verse;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(Thing), "DeSpawn")]
    public static class Thing_DeSpawn_InvalidateObservationCache
    {
        public static void Postfix(Thing __instance)
        {
            var pawn = __instance as Pawn;
            if (pawn == null) return;
            try
            {
                ObservationLearningUtil.InvalidateMapCache(pawn.Map);
            }
            catch { }
        }
    }
}
