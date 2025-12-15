using System;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore.Patch
{
    [HarmonyPatch(typeof(PawnGenerator), "GenerateSkills")]
    public static class PawnGenerator_GenerateSkills_WorkerDronesAgePatch
    {
        // Cache a delegate to the private GenerateRandomAge method
        private static readonly Action<Pawn, PawnGenerationRequest> GenerateRandomAgeDelegate =
            (Action<Pawn, PawnGenerationRequest>)Delegate.CreateDelegate(
                typeof(Action<Pawn, PawnGenerationRequest>),
                AccessTools.Method(typeof(PawnGenerator), "GenerateRandomAge", new Type[]
                {
                    typeof(Pawn),
                    typeof(PawnGenerationRequest)
                })
            );

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            if (pawn == null)
                return;

            // Only interfere with your own worker drones
            if (!DroneHelper.IsWorkerDrone(pawn))
                return;

            if (pawn.ageTracker == null)
                return;

            // Only undo VRE age logic for synthetic‑body drones
            Gene_SyntheticBody syntheticGene = pawn.genes != null
                ? pawn.genes.GetGene(VREA_DefOf.VREA_SyntheticBody) as Gene_SyntheticBody
                : null;

            if (syntheticGene == null)
                return;

            // Call vanilla age generator to overwrite VRE's 0–25y roll.
            if (GenerateRandomAgeDelegate != null)
            {
                GenerateRandomAgeDelegate(pawn, request);
            }
            else
            {
                Log.Error("[MurderRimCore] Failed to bind PawnGenerator.GenerateRandomAge delegate; worker drone ages may remain capped by VRE.");
            }
        }
    }
}