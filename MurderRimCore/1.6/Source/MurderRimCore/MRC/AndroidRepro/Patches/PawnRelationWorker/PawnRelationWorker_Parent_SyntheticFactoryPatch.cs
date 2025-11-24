using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids; // for pawn.IsAndroid()
using MurderRimCore.AndroidRepro;

namespace MurderRimCore.Patch
{
    /// <summary>
    /// Makes most synthetics (androids / worker drones) factory-born:
    /// gates parent creation at the CreateRelation stage using global + per-faction settings
    /// from AndroidReproductionSettingsDef.relationSettings.
    /// </summary>
    [HarmonyPatch(typeof(PawnRelationWorker_Parent), nameof(PawnRelationWorker_Parent.CreateRelation))]
    public static class PawnRelationWorker_Parent_SyntheticFactoryPatch
    {
        // Signature must match: void CreateRelation(Pawn generated, Pawn other, ref PawnGenerationRequest request)
        [HarmonyPrefix]
        public static bool Prefix(Pawn generated, Pawn other, ref PawnGenerationRequest request)
        {
            // If repro system doesn't exist or is disabled, do nothing.
            var reproDef = AndroidReproductionSettingsDef.Current;
            if (reproDef == null || !reproDef.enabled)
                return true;

            if (generated == null || other == null)
                return true;

            // Only care about synthetics as the generated child.
            if (!IsSynthetic(generated))
                return true;

            // Chance this synthetic is allowed to have parents at all.
            float chance = reproDef.GetSyntheticParentChanceForFaction(generated.Faction);
            chance = Mathf.Clamp01(chance);

            // If chance is zero or the roll fails, skip creating any parent relation.
            if (chance <= 0f || Rand.Value >= chance)
            {
                // No parents: treat as factory-made. Do NOT call original CreateRelation.
                return false;
            }

            // Rare synthetic child with parents: allow base CreateRelation to run.
            return true;
        }

        private static bool IsSynthetic(Pawn pawn)
        {
            if (pawn == null)
                return false;

            // VRE Androids helper
            if (pawn.IsAndroid())
                return true;

            // Your worker drone helper
            if (DroneHelper.IsWorkerDrone(pawn))
                return true;

            return false;
        }
    }
}