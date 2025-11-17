using HarmonyLib;
using Verse;

namespace MurderRimCore.FacialAnimationCompat
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class FacialAnimationCompat_TickManager_DoSingleTick_FacialAnimationBatch_Patch
    {
        static void Postfix()
        {
            MurderRimCore.FacialAnimationCompat.FacialAnimationBatcher.ProcessQueue();
        }
    }
}

