using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MRHP
{
    [HarmonyPatch(typeof(StunHandler), "Notify_DamageApplied")]
    public static class Patch_StunHandler_Notify_DamageApplied
    {
        public static void Postfix(StunHandler __instance, DamageInfo dinfo)
        {
            Pawn pawn = __instance.parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.Downed) return;

            // 1. Check Trigger
            if (dinfo.Def == DamageDefOf.EMP && pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
            {
                // 2. Calculate Resistance Multiplier
                // Stat is usually 0.0 to 1.0. 
                // We want the INVERSE (0.0 Res = 1.0 Effect, 0.9 Res = 0.1 Effect)
                float resistance = pawn.GetStatValue(MRHP_DefOf.EMPResistance);
                float effectMultiplier = Mathf.Clamp01(1f - resistance);

                // If fully immune, do nothing
                if (effectMultiplier <= 0f) return;

                // --- A. APPLY SCALED STUN ---
                // Base: 45 ticks per point.
                // With 50% resistance, this becomes 22.5 ticks per point.
                int stunTicks = (int)(dinfo.Amount * 45f * effectMultiplier);
                if (stunTicks > 0)
                {
                    __instance.StunFor(stunTicks, dinfo.Instigator, true, true);
                }

                // --- B. APPLY SCALED SHOCK HEDIFF ---
                // You requested this be "a lot shorter".
                // Reduced base from 100f to 40f, then applied multiplier.
                int shockDuration = (int)(dinfo.Amount * 40f * effectMultiplier);

                if (shockDuration > 60) // Only apply if it lasts at least 1 second
                {
                    Hediff hediff = HediffMaker.MakeHediff(VREA_DefOf.VREA_ElectromagneticShock, pawn, null);
                    HediffComp_Disappears disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
                    if (disappearsComp != null)
                    {
                        disappearsComp.ticksToDisappear = shockDuration;
                    }
                    pawn.health.AddHediff(hediff, null, null, null);
                }

                // --- C. APPLY SCALED BURN DAMAGE ---
                // Scales total damage down by resistance. 
                // High resistance = very little damage = fewer parts hit.
                float totalDamageToDeal = dinfo.Amount * effectMultiplier;

                IEnumerable<BodyPartRecord> notMissingParts = pawn.health.hediffSet.GetNotMissingParts();

                if (notMissingParts.Any() && totalDamageToDeal >= 1f)
                {
                    float remainingDamage = totalDamageToDeal;

                    while (remainingDamage > 0f)
                    {
                        // Chunks of 3 to 5
                        float chunkDamage = Mathf.Min(remainingDamage, (float)Rand.RangeInclusive(3, 5));
                        remainingDamage -= chunkDamage;

                        BodyPartRecord targetPart = notMissingParts.RandomElement();

                        DamageInfo burnInfo = new DamageInfo(
                            VREA_DefOf.VREA_EMPBurn,
                            chunkDamage,
                            0f,
                            -1f,
                            dinfo.Instigator,
                            targetPart
                        );

                        pawn.TakeDamage(burnInfo);
                    }
                }
            }
        }
    }
}