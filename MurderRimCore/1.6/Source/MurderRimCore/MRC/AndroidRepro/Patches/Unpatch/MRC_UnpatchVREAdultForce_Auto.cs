using HarmonyLib;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Always unpatch the VREAndroids adult-forcing prefix (owner 'VREAndroidsMod') at startup,
    // since we fully replace its behavior below.
    [StaticConstructorOnStartup]
    public static class MRC_UnpatchVREAdultForce_Auto
    {
        static MRC_UnpatchVREAdultForce_Auto()
        {
            try
            {
                var target = AccessTools.Method(typeof(Pawn_AgeTracker), "RecalculateLifeStageIndex");
                if (target == null) return;

                var info = Harmony.GetPatchInfo(target);
                if (info == null || info.Prefixes == null) return;

                var harmony = new Harmony("MurderRimCore.AndroidRepro.UnpatchVREAdultForce");
                foreach (var pre in info.Prefixes)
                {
                    if (pre.owner == "VREAndroidsMod")
                    {
                        harmony.Unpatch(target, HarmonyPatchType.Prefix, pre.owner);
                        Log.Message("[MRC-Repro] Unpatched VREAndroids adult-forcing prefix (owner='VREAndroidsMod').");
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Warning("[MRC-Repro] Failed to unpatch VREAndroids adult-forcing prefix: " + e.Message);
            }
        }
    }
}
