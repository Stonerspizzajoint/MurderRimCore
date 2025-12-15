using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore
{
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class WorkerDrone_SpawnVisualsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result)
        {
            // Simple check: Do we have a pawn?
            if (__result == null) return;

            // Simple check: Is it a worker drone?
            if (!DroneHelper.IsWorkerDrone(__result)) return;

            // Delegate all logic to the Utility class
            WorkerDroneVisuals.ApplyWorkerDroneVisuals(__result);
        }
    }
}