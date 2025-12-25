using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VREAndroids;
using System.Collections.Generic;

namespace MRHP
{
    public class JobGiver_AndroidRage : ThinkNode_JobGiver
    {
        private const float CombatRadius = 65f;
        private const float HuntRadius = 40f;
        private const int ThreatReactionTicks = 120;
        private const int ExecutionCooldownTicks = 600;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. Safety Checks
            if (pawn.CurJobDef == MRHP_DefOf.MRHP_SentinelMaul ||
                pawn.CurJobDef == MRHP_DefOf.MRHP_ExecuteAndroid)
                return null;

            MentalState_AndroidRage rage = pawn.MentalState as MentalState_AndroidRage;
            if (rage == null) return null;

            Pawn currentTarget = rage.target;

            // 2. Retaliation Override
            if (pawn.mindState.lastHarmTick > Find.TickManager.TicksGame - ThreatReactionTicks)
            {
                Pawn likelyAttacker = FindClosestHostileAndroid(pawn, 25f, true);
                if (likelyAttacker != null && likelyAttacker != currentTarget)
                {
                    rage.target = likelyAttacker;
                    currentTarget = likelyAttacker;
                    // No mote text. Use visual if needed
                }
            }

            // 3. Target Maintenance
            if (IsTargetInvalid(pawn, currentTarget))
            {
                rage.target = null;
                currentTarget = null;
            }
            else if (currentTarget != null)
            {
                if (currentTarget.Position.DistanceTo(pawn.Position) > CombatRadius)
                {
                    rage.target = null;
                    currentTarget = null;
                }
                else if (currentTarget.Downed)
                {
                    Pawn activeThreat = FindClosestHostileAndroid(pawn, 20f, true);
                    if (activeThreat != null)
                    {
                        rage.target = activeThreat;
                        currentTarget = activeThreat;
                        // No mote text. Use visual if needed
                    }
                }
            }

            // 4. ACQUIRE NEW TARGET
            if (currentTarget == null)
            {
                currentTarget = FindLocalAndroidTarget(pawn, HuntRadius);

                if (currentTarget != null)
                {
                    rage.target = currentTarget;
                }
                else
                {
                    // === NO LOCAL TARGETS FOUND ===
                    rage.RecoverFromState(); // End the rage state if no enemies nearby
                    return null;
                }
            }

            if (currentTarget == null) return null;

            pawn.mindState.enemyTarget = currentTarget;

            // 5. ABILITY LOGIC
            Job abilityJob = TryGetAbilityJob(pawn, currentTarget);
            if (abilityJob != null)
                return abilityJob;

            // 6. DEFAULT ATTACK
            if (!currentTarget.Downed)
            {
                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, currentTarget);
                attackJob.maxNumMeleeAttacks = 1;
                attackJob.expiryInterval = 120;
                attackJob.collideWithPawns = true;
                return attackJob;
            }

            if (ShouldExecute(currentTarget))
            {
                bool onCooldown = (Find.TickManager.TicksGame - rage.lastFailedExecutionTick) < ExecutionCooldownTicks;
                if (!onCooldown && pawn.CanReserve(currentTarget) && !SentinelAIUtils.IsSomeoneExecuting(currentTarget, pawn))
                {
                    return JobMaker.MakeJob(MRHP_DefOf.MRHP_ExecuteAndroid, currentTarget);
                }
            }

            return JobMaker.MakeJob(JobDefOf.AttackMelee, currentTarget);
        }

        // -------- HELPERS --------

        private Pawn FindLocalAndroidTarget(Pawn pawn, float radius)
        {
            return (Pawn)GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(pawn), radius,
                (t) => {
                    Pawn p = t as Pawn;
                    if (!IsValidTarget(pawn, p)) return false;
                    return true;
                }
            );
        }

        private Pawn FindClosestHostileAndroid(Pawn center, float radius, bool mustBeAwake)
        {
            return (Pawn)GenClosest.ClosestThingReachable(
                center.Position, center.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(center), radius,
                (t) => {
                    Pawn p = t as Pawn;
                    if (!IsValidTarget(center, p)) return false;
                    if (mustBeAwake && p.Downed) return false;
                    return true;
                }
            );
        }

        private bool IsValidTarget(Pawn hunter, Pawn victim)
        {
            if (victim == null || victim.Dead || victim.Destroyed || victim.Map != hunter.Map) return false;
            if (!Utils.IsAndroid(victim)) return false;
            if (victim.Faction == hunter.Faction) return false;
            if (SentinelAIUtils.IsTargetOvercrowded(victim, hunter)) return false;
            if (SentinelAIUtils.IsSomeoneExecuting(victim, hunter)) return false;
            return true;
        }

        private bool IsTargetInvalid(Pawn attacker, Pawn victim)
        {
            if (victim == null || victim.Dead || victim.Destroyed || victim.Map != attacker.Map) return true;
            if (!Utils.IsAndroid(victim)) return true;
            if (SentinelAIUtils.IsTargetOvercrowded(victim, attacker)) return true;
            if (SentinelAIUtils.IsSomeoneExecuting(victim, attacker)) return true;
            return false;
        }

        private bool ShouldExecute(Pawn target)
        {
            return target.Downed || target.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.2f;
        }

        /// <summary>
        /// Try to give a job for special abilities, including SentinelFlash and SentinelPounce.
        /// </summary>
        private Job TryGetAbilityJob(Pawn pawn, Pawn target)
        {
            if (pawn.abilities == null) return null;

            // 1. Flash disables androids (priority over Pounce attack!)
            AbilityDef flashDef = DefDatabase<AbilityDef>.GetNamedSilentFail("MRHP_SentinelFlash");
            if (flashDef != null)
            {
                Ability flash = pawn.abilities.GetAbility(flashDef);
                if (flash != null && flash.CanCast && !target.Downed && flash.verb.CanHitTarget(target))
                {
                    return flash.GetJob(target, target);
                }
            }

            // 2. Pounce ability
            AbilityDef pounceDef = DefDatabase<AbilityDef>.GetNamedSilentFail("MRHP_SentinelPounce");
            if (pounceDef != null)
            {
                Ability pounce = pawn.abilities.GetAbility(pounceDef);
                if (pounce != null && pounce.CanCast && !target.Downed && pounce.verb.CanHitTarget(target))
                {
                    if (pawn.Position.DistanceTo(target.Position) > 3f)
                        return pounce.GetJob(target, target);
                }
            }
            return null;
        }
    }
}