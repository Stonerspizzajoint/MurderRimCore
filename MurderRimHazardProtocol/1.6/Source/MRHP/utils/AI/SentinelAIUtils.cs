using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;
using Verse.Sound;

namespace MRHP
{

    public static class SentinelAIUtils
    {
        // ============================================================
        // REGION: CHECKS (Targeting & Limits)
        // ============================================================
        #region Checks

        public static bool IsTargetOvercrowded(Pawn victim, Pawn self)
        {
            if (victim == null || victim.Map == null) return false;

            int count = 0;
            IReadOnlyList<Pawn> allPawns = victim.Map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p == self) continue;
                if (p.CurJob == null) continue;

                if (p.CurJob.targetA.Thing == victim)
                {
                    JobDef def = p.CurJob.def;

                    if (def == MRHP_DefOf.MRHP_SentinelMaul ||
                        def == MRHP_DefOf.MRHP_SentinelPounceJob ||
                        def == MRHP_DefOf.MRHP_ExecuteAndroid)
                    {
                        count++;
                    }
                }
            }
            return count >= 2;
        }

        public static bool IsSomeoneExecuting(Pawn victim, Pawn self)
        {
            if (victim == null || victim.Map == null) return false;

            IReadOnlyList<Pawn> allPawns = victim.Map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p == self) continue;

                if (p.CurJob != null &&
                    p.CurJob.targetA.Thing == victim &&
                    p.CurJob.def == MRHP_DefOf.MRHP_ExecuteAndroid)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckStaggerOnDamage(Pawn pawn, float currentDmg, float damageTaken, int threshold)
        {
            if ((currentDmg + damageTaken) >= threshold)
            {
                if (pawn.Map != null)
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "STAGGERED!", Color.white);

                pawn.stances.stunner.StunFor(120, pawn, false, true);
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return true;
            }
            return false;
        }

        #endregion

        // ============================================================
        // REGION: MATH (Calculations & Probability)
        // ============================================================
        #region Math

        public static float CalculateBreakoutChance(Pawn victim, Pawn sentinel, float chancePerSkill, float chancePerStruggle, int attacksEndured, float originalDodgeChance)
        {
            if (victim.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.1f) return 0f;

            int meleeSkill = victim.skills?.GetSkill(SkillDefOf.Melee).Level ?? 0;

            float agilityFactor = originalDodgeChance * 0.5f;
            float baseChance = (meleeSkill * chancePerSkill) + (attacksEndured * chancePerStruggle) + agilityFactor;

            if (sentinel != null)
            {
                float victimSize = victim.BodySize;
                float sentinelSize = sentinel.BodySize <= 0.1f ? 1f : sentinel.BodySize;

                if (sentinelSize > victimSize * 2.5f) return 0.01f;

                float sizeRatio = victimSize / sentinelSize;
                baseChance *= sizeRatio;
            }

            return Mathf.Clamp(baseChance, 0f, 1f);
        }

        #endregion

        // ============================================================
        // REGION: COMBAT LOGIC (Actions & Outcomes)
        // ============================================================
        #region Combat Logic

        public static void ResolvePounceCombat(Pawn attacker, Pawn victim, CompAbility_SentinelSettings settings)
        {
            if (attacker == null || victim == null) return;

            // VISUAL: Always play an impact effect upon landing
            if (attacker.Map != null)
            {
                // A heavy dust cloud to show weight
                FleckMaker.ThrowDustPuffThick(attacker.Position.ToVector3Shifted(), attacker.Map, 2.0f, Color.grey);

                // Optional: A subtle camera shake if you want real weight?
                // Find.CameraDriver.shaker.DoShake(1.0f); 
            }

            // 1. Check Reservations (Prevent stacking bugs)
            if (!attacker.CanReserve(victim, 1, -1, null, false))
            {
                // Removed text
                attacker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, victim), JobCondition.InterruptForced);
                return;
            }

            float struggleBonus = settings?.Props.breakoutChancePerStruggle ?? 0.01f;
            float skillBreakout = settings?.Props.breakoutBaseChancePerSkill ?? 0.02f;
            float rawDodgeChance = victim.GetStatValue(StatDefOf.MeleeDodgeChance);

            float attackerSize = attacker.BodySize <= 0.01f ? 1f : attacker.BodySize;
            float victimSize = victim.BodySize;
            float sizeRatio = victimSize / attackerSize;

            float adjustedDodgeChance = rawDodgeChance * sizeRatio;
            adjustedDodgeChance = Mathf.Clamp(adjustedDodgeChance, 0f, 0.9f);

            bool evaded = Rand.Value < adjustedDodgeChance;

            if (evaded)
            {
                MoteMaker.ThrowText(victim.DrawPos, victim.Map, "DODGED!", Color.green);

                // Sound: Whoosh/Dodge
                MRHP_DefOf.Pawn_MeleeDodge?.PlayOneShot(victim);

                if (victimSize > attackerSize * 1.5f)
                {
                    Messages.Message($"{victim.LabelShort} was too heavy to pin!", victim, MessageTypeDefOf.NeutralEvent, true);
                }

                victim.stances.stunner.StunFor(60, attacker, false, true);
                attacker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, victim), JobCondition.InterruptForced);
            }
            else
            {
                // Success: Pin
                Hediff pin = HediffMaker.MakeHediff(MRHP_DefOf.MRHP_Pinned, victim);
                victim.health.AddHediff(pin);

                HediffComp_Pinned comp = pin.TryGetComp<HediffComp_Pinned>();
                if (comp != null)
                {
                    comp.Notify_PounceStarted(attacker, struggleBonus, skillBreakout, rawDodgeChance);
                }

                MoteMaker.ThrowText(victim.DrawPos, victim.Map, "PINNED!", Color.red);

                // Sound: Heavy impact or Thud
                // SoundDefOf.BodyFall_Generic_Heavy.PlayOneShot(victim);

                Job maulJob = JobMaker.MakeJob(MRHP_DefOf.MRHP_SentinelMaul, victim);
                attacker.jobs.StartJob(maulJob, JobCondition.InterruptForced);
            }
        }

        public static bool PerformMaulAttack(Pawn attacker, Pawn victim)
        {
            if (victim == null || attacker == null) return false;

            // OPTIMIZED: Use MRHP_DefOf.MRHP_Pinned instead of Named()
            Hediff pin = victim.health.hediffSet.GetFirstHediffOfDef(MRHP_DefOf.MRHP_Pinned);
            HediffComp_Pinned comp = pin?.TryGetComp<HediffComp_Pinned>();

            if (comp != null)
            {
                comp.Notify_MaulingActive(attacker);
            }
            else
            {
                attacker.jobs.EndCurrentJob(JobCondition.Incompletable);
                return false;
            }

            if (attacker.IsHashIntervalTick(45))
            {
                if (victim.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.2f || victim.Dead)
                {
                    attacker.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return false;
                }

                if (comp.TryBreakout())
                {
                    attacker.stances.stunner.StunFor(90, victim, false, true);
                    attacker.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    return false;
                }

                attacker.meleeVerbs.TryMeleeAttack(victim, null, false);
                FleckMaker.ThrowMicroSparks(victim.DrawPos, attacker.Map);
            }
            return true;
        }

        public static void PerformMechanicalExecution(Pawn executioner, Pawn victim)
        {
            if (victim == null || executioner == null) return;

            if (executioner.Map != null)
            {
                SoundDefOf.Building_Deconstructed.PlayOneShot(executioner);
            }

            BodyPartRecord targetPart = victim.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Head).FirstOrFallback();

            if (targetPart == null)
            {
                targetPart = victim.RaceProps.body.corePart;
            }

            DamageInfo dinfo = new DamageInfo(DamageDefOf.ExecutionCut, 9999, 999, -1, executioner, targetPart);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetIgnoreInstantKillProtection(true);

            if (!victim.Dead) victim.Kill(dinfo);

            if (victim.Map != null)
            {
                if (victim.RaceProps.IsMechanoid)
                    FilthMaker.TryMakeFilth(victim.Position, victim.Map, ThingDefOf.Filth_MachineBits, 5);
                else
                    FilthMaker.TryMakeFilth(victim.Position, victim.Map, ThingDefOf.Filth_Blood, 5);
            }
        }

        public static void TryTriggerRage(Pawn sentinel, Pawn ignoreVictim, Map map)
        {
            if (sentinel == null || map == null) return;

            Pawn aggressor = (Pawn)GenClosest.ClosestThingReachable(
                sentinel.Position, map, ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch, TraverseParms.For(sentinel), 30f,
                (t) => t is Pawn p && !p.Dead && !p.Downed && p != ignoreVictim && p.HostileTo(sentinel)
            );

            if (aggressor != null)
            {
                sentinel.mindState.mentalStateHandler.Reset();
                bool started = sentinel.mindState.mentalStateHandler.TryStartMentalState(
                    stateDef: MRHP_DefOf.MRHP_AndroidRage, // OPTIMIZED
                    reason: null,
                    forceWake: true,
                    causedByMood: false,
                    otherPawn: null,
                    transitionSilently: false
                );

                if (started && sentinel.MentalState is MentalState_AndroidRage customState)
                {
                    customState.target = aggressor;
                }
            }
        }

        #endregion
    }
}