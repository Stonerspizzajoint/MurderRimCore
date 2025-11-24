using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore
{
    /// <summary>
    /// Strips VRE Androids' "no blood family for androids" postfix from
    /// PawnRelationWorker.BaseGenerationChanceFactor, so our own logic can run.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UnpatchVREAndroidFamily
    {
        static UnpatchVREAndroidFamily()
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(PawnRelationWorker), "BaseGenerationChanceFactor");
                if (target == null)
                {
                    Log.Warning("[MurderRimCore] Could not find PawnRelationWorker.BaseGenerationChanceFactor to unpatch VRE Androids family logic.");
                    return;
                }

                // In this Harmony version, GetPatchInfo returns HarmonyLib.Patches
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(target);
                if (patches == null)
                {
                    Log.Message("[MurderRimCore] No patches found on BaseGenerationChanceFactor; nothing to unpatch.");
                    return;
                }

                var harmony = new Harmony("MurderRimCore.UnpatchVREAndroidFamily");
                int removed = 0;

                // Patches.Postfixes is IEnumerable<HarmonyLib.Patch>
                foreach (HarmonyLib.Patch postfix in patches.Postfixes)
                {
                    MethodInfo patchMethod = postfix.PatchMethod;
                    if (patchMethod == null)
                        continue;

                    Type declaringType = patchMethod.DeclaringType;
                    if (declaringType == null || declaringType.Namespace == null)
                        continue;

                    // Match anything in the VREAndroids namespace
                    if (declaringType.Namespace.StartsWith("VREAndroids", StringComparison.Ordinal))
                    {
                        harmony.Unpatch(target, patchMethod);
                        removed++;
                        Log.Message($"[MurderRimCore] Unpatched VRE Androids postfix {declaringType.FullName}.{patchMethod.Name} from BaseGenerationChanceFactor.");
                    }
                }

                if (removed == 0)
                {
                    Log.Message("[MurderRimCore] No VRE Androids postfixes matched on BaseGenerationChanceFactor.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MurderRimCore] Failed to unpatch VRE Androids family logic: " + ex);
            }
        }
    }
}