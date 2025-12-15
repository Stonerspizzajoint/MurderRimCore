using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MRHP
{
    public class JobGiver_AndroidRage : ThinkNode_JobGiver
    {
        // RANGES
        private const float AggroRadius = 65f;
        private const float ChaseRadius = 100f;
        private const float SafetyScanRadius = 20f;

        // COOLDOWNS
        private const int ThreatReactionTicks = 120; // 2 Seconds
        private const int ExecutionCooldownTicks = 600;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // ---------------------------------------------------------
            // 1. SAFETY CHECKS
            // ---------------------------------------------------------
            if (pawn.CurJobDef == MRHP_DefOf.MRHP_SentinelMaul ||
                pawn.CurJobDef == MRHP_DefOf.MRHP_ExecuteAndroid)
            {
                return null;
            }

            MentalState_AndroidRage rage = pawn.MentalState as MentalState_AndroidRage;
            if (rage == null) return null;

            Pawn currentTarget = rage.target;

            // ---------------------------------------------------------
            // 2. THREAT OVERRIDE ("Retaliation")
            // ---------------------------------------------------------
            if (pawn.mindState.lastHarmTick > Find.TickManager.TicksGame - ThreatReactionTicks)
            {
                Pawn likelyAttacker = FindClosestHostileAndroid(pawn, 40f, true);
                if (likelyAttacker != null && likelyAttacker != currentTarget)
                {
                    rage.target = likelyAttacker;
                    currentTarget = likelyAttacker;
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "ENGAGING THREAT", Color.red);
                }
            }

            // ---------------------------------------------------------
            // 3. TARGET MAINTENANCE (Drop invalid targets)
            // ---------------------------------------------------------
            if (IsTargetInvalid(pawn, currentTarget))
            {
                rage.target = null;
                currentTarget = null;
            }
            else if (currentTarget != null)
            {
                // Chase Limit
                if (currentTarget.Position.DistanceTo(pawn.Position) > ChaseRadius)
                {
                    rage.target = null;
                    currentTarget = null;
                }
                // Don't focus on the dead/dying if threats exist
                else if (currentTarget.Downed)
                {
                    Pawn activeThreat = FindClosestHostileAndroid(pawn, SafetyScanRadius, true);
                    if (activeThreat != null)
                    {
                        rage.target = activeThreat;
                        currentTarget = activeThreat;
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "THREAT PRIORITY", Color.yellow);
                    }
                }
            }

            // ---------------------------------------------------------
            // 4. ACQUIRE NEW TARGET (If null or dropped)
            // ---------------------------------------------------------
            if (currentTarget == null)
            {
                // A. Prioritize AWAKE targets first
                currentTarget = FindNewAndroidTarget(pawn, true);

                // B. If no awake targets, find DOWNED targets (Cleanup)
                if (currentTarget == null)
                {
                    currentTarget = FindNewAndroidTarget(pawn, false);
                }

                rage.target = currentTarget;
            }

            if (currentTarget == null) return null;
            pawn.mindState.enemyTarget = currentTarget;

            // ---------------------------------------------------------
            // 5. COMBAT DECISIONS
            // ---------------------------------------------------------

            // Priority 1: ABILITIES
            Job abilityJob = TryGetAbilityJob(pawn, currentTarget);
            if (abilityJob != null) return abilityJob;

            // Priority 2: ATTACK (Standard Combat)
            if (!currentTarget.Downed)
            {
                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, currentTarget);
                attackJob.maxNumMeleeAttacks = 1;
                attackJob.expiryInterval = 120;
                attackJob.collideWithPawns = true;
                return attackJob;
            }

            // Priority 3: EXECUTE (Cleanup Only)
            if (ShouldExecute(currentTarget))
            {
                bool onCooldown = (Find.TickManager.TicksGame - rage.lastFailedExecutionTick) < ExecutionCooldownTicks;

                // Double check reservation here just in case
                if (!onCooldown && pawn.CanReserve(currentTarget) && !SentinelAIUtils.IsSomeoneExecuting(currentTarget, pawn))
                {
                    return JobMaker.MakeJob(MRHP_DefOf.MRHP_ExecuteAndroid, currentTarget);
                }
            }

            // Fallback Melee
            return JobMaker.MakeJob(JobDefOf.AttackMelee, currentTarget);
        }

        // --- HELPERS ---

        private Job TryGetAbilityJob(Pawn pawn, Pawn target)
        {
            if (pawn.abilities == null) return null;

            AbilityDef pounceDef = DefDatabase<AbilityDef>.GetNamed("MRHP_SentinelPounce");
            Ability pounce = pawn.abilities.GetAbility(pounceDef);

            if (pounce != null && pounce.CanCast && !target.Downed && pounce.verb.CanHitTarget(target))
            {
                if (pawn.Position.DistanceTo(target.Position) > 3f)
                {
                    return pounce.GetJob(target, target);
                }
            }

            AbilityDef flashDef = DefDatabase<AbilityDef>.GetNamed("MRHP_SentinelFlash");
            Ability flash = pawn.abilities.GetAbility(flashDef);

            if (flash != null && flash.CanCast && !target.Downed && flash.verb.CanHitTarget(target))
            {
                return flash.GetJob(target, target);
            }

            return null;
        }

        private bool IsTargetInvalid(Pawn attacker, Pawn victim)
        {
            if (victim == null || victim.Dead || victim.Destroyed || victim.Map != attacker.Map) return true;
            if (!Utils.IsAndroid(victim)) return true;
            if (SentinelAIUtils.IsTargetOvercrowded(victim, attacker)) return true;

            // CRITICAL ADDITION: If someone else is already executing this target, 
            // consider them "Invalid" so we drop them and find someone else immediately.
            if (SentinelAIUtils.IsSomeoneExecuting(victim, attacker)) return true;

            return false;
        }

        private Pawn FindNewAndroidTarget(Pawn pawn, bool prioritizeAwake)
        {
            return (Pawn)GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(pawn), AggroRadius,
                (t) => {
                    Pawn p = t as Pawn;
                    if (!IsValidTarget(pawn, p)) return false;
                    if (prioritizeAwake && p.Downed) return false;
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
            if (victim == null || victim.Dead) return false;
            if (!Utils.IsAndroid(victim)) return false;
            if (victim.Faction == hunter.Faction) return false;
            if (SentinelAIUtils.IsTargetOvercrowded(victim, hunter)) return false;

            // CRITICAL ADDITION: Don't pick targets already being executed
            if (SentinelAIUtils.IsSomeoneExecuting(victim, hunter)) return false;

            if (!GenSight.LineOfSight(hunter.Position, victim.Position, hunter.Map, true)) return false;
            return true;
        }

        private bool ShouldExecute(Pawn target)
        {
            return target.Downed || target.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.2f;
        }
    }
}