using HarmonyLib;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Drive fusion/gestation by hooking the base Thing.DoTick, which exists in your build.
    // We only act when the instance is the VRE Android Creation Station.
    [HarmonyPatch(typeof(Thing), "DoTick")]
    public static class Thing_DoTickPatch
    {
        public static void Postfix(Thing __instance)
        {
            VREAndroids.Building_AndroidCreationStation station = __instance as VREAndroids.Building_AndroidCreationStation;
            if (station == null) return;

            AndroidFusionRuntime.TickStation(station);
        }
    }
}
