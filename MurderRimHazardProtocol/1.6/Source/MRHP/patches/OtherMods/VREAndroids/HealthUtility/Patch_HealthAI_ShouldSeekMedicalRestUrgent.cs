using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(HealthAIUtility), "ShouldSeekMedicalRestUrgent")]
    public static class Patch_HealthAI_ShouldSeekMedicalRestUrgent
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            // 1. If vanilla logic already says "Yes", don't interfere.
            if (__result) return;

            // 2. If it's our Robot
            if (pawn != null && pawn.IsRobotic())
            {
                // 3. REUSE OUR LOGIC
                // We call the VRE method. 
                // Because we ALREADY patched CanRepairAndroid in 'Patch_JobDriver_RepairAndroid',
                // this call will successfully run OUR check (HasAnyInjury) and return true.
                if (JobDriver_RepairAndroid.CanRepairAndroid(pawn))
                {
                    __result = true;
                    return;
                }
            }
        }
    }
}