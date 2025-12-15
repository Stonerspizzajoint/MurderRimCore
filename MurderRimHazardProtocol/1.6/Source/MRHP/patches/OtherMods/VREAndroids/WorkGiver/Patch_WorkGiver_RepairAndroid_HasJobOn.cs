using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids; // You must reference VRE Androids assembly for this!

namespace MRHP.Patches
{
    // 1. PATCH: HasJobOn
    // This intercepts the check for "Can I repair this person?"
    [HarmonyPatch(typeof(WorkGiver_RepairAndroid), "HasJobOn")]
    public static class Patch_WorkGiver_RepairAndroid_HasJobOn
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            Pawn patient = t as Pawn;

            // A. IS THIS OUR ROBOT?
            if (patient != null && patient.IsRobotic())
            {
                // B. DO OUR OWN CHECKS (Skipping VRE Gene checks)

                // 1. Doctor must be capable of Crafting
                if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Crafting))
                {
                    __result = false;
                    return false; // Stop execution, return false
                }

                // 2. Must be in bed (Standard logic)
                if (!patient.InBed())
                {
                    __result = false;
                    return false;
                }

                // 3. Standard Reservation & Faction checks
                if (pawn.Faction == patient.Faction && patient.HostileTo(pawn))
                {
                    __result = false;
                    return false;
                }

                if (t.IsForbidden(pawn))
                {
                    __result = false;
                    return false;
                }

                if (!pawn.CanReserveAndReach(t, PathEndMode.InteractionCell, Danger.Deadly, 1, -1, null, false))
                {
                    __result = false;
                    return false;
                }

                // 4. Is there actual damage to fix?
                // We borrow the logic from the driver to check for injuries
                __result = JobDriver_RepairAndroid.CanRepairAndroid(patient);

                return false; // SKIP ORIGINAL VRE METHOD (So it doesn't check for genes and fail)
            }

            // C. NOT OUR ROBOT?
            return true; // Run original VRE logic for standard Androids
        }
    }
}