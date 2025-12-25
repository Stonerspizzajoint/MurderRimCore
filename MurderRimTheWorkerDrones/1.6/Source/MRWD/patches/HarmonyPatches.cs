using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MRWD.Patches
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private const string HarmonyId = "com.stonerspizzajoint.MRWD";

        static HarmonyPatches()
        {
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            try
            {
                var harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message($"[MRWD] Applied Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[MRWD] Failed to apply Harmony patches: {ex}");
            }
        }
    }
}

