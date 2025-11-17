using HarmonyLib;
using Verse;

namespace MurderRimCore
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatcher
    {
        static HarmonyPatcher()
        {
            var harmony = new Harmony("MurderRimCore");
            harmony.PatchAll();
            Log.Message("[MurderRimCore] Harmony patches applied.");
        }
    }
}

