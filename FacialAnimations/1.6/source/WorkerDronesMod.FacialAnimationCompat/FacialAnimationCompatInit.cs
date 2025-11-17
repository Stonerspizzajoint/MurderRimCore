using HarmonyLib;
using Verse;

namespace MurderRimCore.FacialAnimationCompat
{
    [StaticConstructorOnStartup]
    public static class FacialAnimationCompatInit
    {
        static FacialAnimationCompatInit()
        {
            try
            {
                var harmony = new Harmony("MurderRimCore.FacialAnimationCompat");
                harmony.PatchAll(); // Automatically patches all types in this assembly with Harmony attributes
                Log.Message("[MurderRimCore] Facial Animation compatibility patches applied.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[MurderRimCore] Error while applying Facial Animation patches: {ex}");
            }
        }
    }
}

