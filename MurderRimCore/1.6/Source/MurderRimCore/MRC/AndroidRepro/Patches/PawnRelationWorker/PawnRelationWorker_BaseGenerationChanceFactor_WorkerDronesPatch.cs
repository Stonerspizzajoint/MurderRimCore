using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;
using MurderRimCore.AndroidRepro;
using UnityEngine;

namespace MurderRimCore.Patch
{
    /// <summary>
    /// Synthetic-family control, respecting AndroidReproductionSettingsDef.enabled.
    /// - Flesh&lt;-&gt;flesh: untouched.
    /// - Cross-type (synthetic vs flesh): no blood relations.
    /// - Synthetic&lt;-&gt;synthetic: allowed by a per-faction CHANCE gate.
    ///   If the chance check fails, factor = 0; if it passes, we let vanilla/VRE compute the factor,
    ///   then optionally boost "same-type" synthetic pairs.
    /// </summary>
    [HarmonyPatch(typeof(PawnRelationWorker), nameof(PawnRelationWorker.BaseGenerationChanceFactor))]
    public static class PawnRelationWorker_BaseGenerationChanceFactor_WorkerDronesPatch
    {
        // PREFIX: gate whether synthetic relations are even allowed for this pair.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(
            PawnRelationWorker __instance,
            Pawn generated,
            Pawn other,
            PawnGenerationRequest request,
            ref float __result)
        {
            var reproDef = AndroidReproductionSettingsDef.Current;
            if (reproDef == null || !reproDef.enabled)
                return true; // system off: vanilla + VRE

            if (__instance == null || __instance.def == null)
                return true;

            if (!__instance.def.familyByBloodRelation)
                return true;

            if (generated == null || other == null)
                return true;

            bool genSynthetic = IsSynthetic(generated);
            bool otherSynthetic = IsSynthetic(other);

            // Flesh <-> flesh: not our concern
            if (!genSynthetic && !otherSynthetic)
                return true;

            // Cross-type (synthetic vs flesh): forbid blood relations entirely
            if (genSynthetic != otherSynthetic)
            {
                __result = 0f;
                return false;
            }

            // Synthetic <-> synthetic: apply a CHANCE gate based on faction
            Faction f = generated.Faction ?? other.Faction;
            float chance = reproDef.GetSyntheticBloodRelationChanceForFaction(f);

            chance = Mathf.Clamp01(chance);

            if (chance <= 0f)
            {
                __result = 0f;
                return false; // no synthetic blood relations for this faction
            }

            if (Rand.Value > chance)
            {
                __result = 0f;
                return false; // this particular pair failed the relation chance
            }

            // Passed the synthetic relation gate: let vanilla/VRE compute the base factor.
            // Postfix will handle same-type bias.
            return true;
        }

        // POSTFIX: bias same-type synthetic pairs (worker<->worker, android<->android) to be more likely.
        [HarmonyPostfix]
        public static void Postfix(
            PawnRelationWorker __instance,
            Pawn generated,
            Pawn other,
            PawnGenerationRequest request,
            ref float __result)
        {
            var reproDef = AndroidReproductionSettingsDef.Current;
            if (reproDef == null || !reproDef.enabled)
                return;

            if (__instance == null || __instance.def == null)
                return;

            if (!__instance.def.familyByBloodRelation)
                return;

            if (generated == null || other == null)
                return;

            // If the factor is already 0, there's nothing to bias.
            if (__result <= 0f)
                return;

            bool genWorker = DroneHelper.IsWorkerDrone(generated);
            bool otherWorker = DroneHelper.IsWorkerDrone(other);

            bool genAndroid = generated.IsAndroid() && !genWorker;
            bool otherAndroid = other.IsAndroid() && !otherWorker;

            // Same-type bias:
            // - Worker Drone <-> Worker Drone
            // - Android <-> Android
            if ((genWorker && otherWorker) || (genAndroid && otherAndroid))
            {
                // You can tune this or move it into relationSettings later.
                const float sameTypeMultiplier = 2.5f;

                __result *= sameTypeMultiplier;
            }
        }

        private static bool IsSynthetic(Pawn pawn)
        {
            if (pawn == null)
                return false;

            if (pawn.IsAndroid())
                return true;

            if (DroneHelper.IsWorkerDrone(pawn))
                return true;

            return false;
        }
    }
}