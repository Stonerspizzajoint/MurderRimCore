using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MurderRimCore
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit_TryGenerateNewPawnInternal_Postfix
    {
        static HarmonyInit_TryGenerateNewPawnInternal_Postfix()
        {
            var h = new Harmony("murderrimcore.backstoryextension.finalapply");

            // private static Pawn TryGenerateNewPawnInternal(ref PawnGenerationRequest request, out string error, bool ignoreScenarioRequirements, bool ignoreValidator)
            MethodInfo target = typeof(PawnGenerator)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "TryGenerateNewPawnInternal") return false;
                    var p = m.GetParameters();
                    return p.Length == 4 &&
                           p[0].ParameterType.IsByRef &&
                           p[1].IsOut &&
                           p[2].ParameterType == typeof(bool) &&
                           p[3].ParameterType == typeof(bool);
                });

            if (target != null)
            {
                h.Patch(target, postfix: new HarmonyMethod(typeof(HarmonyInit_TryGenerateNewPawnInternal_Postfix), nameof(Postfix)));
            }
            else
            {
                Log.Warning("[MRC] Could not find PawnGenerator.TryGenerateNewPawnInternal; BackstoryExtension features disabled.");
            }
        }

        public static void Postfix(ref Pawn __result)
        {
            var pawn = __result;
            if (pawn == null || pawn.story == null || !pawn.RaceProps.Humanlike) return;

            var extAdult = pawn.story.Adulthood?.GetModExtension<BackstoryExtension>();
            var extChild = pawn.story.Childhood?.GetModExtension<BackstoryExtension>();
            BackstoryExtension ext = MergeExtensions(extAdult, extChild);
            if (ext == null) return;

            // AGE OVERRIDE: Only if explicitly enabled in XML AND a sensible range is provided
            if (ext.overrideAge)
            {
                ApplyAgeOverride(pawn, ext);
            }

            // LIMB REMOVAL
            int toRemove = ext.missingPartCount.RandomInRange;
            if (toRemove <= 0 && ext.RanMissingPart)
                toRemove = 1;

            if (toRemove > 0)
            {
                RemoveRandomSafeLimbs(pawn, toRemove, ext.ensureAtLeastOneLeg);
            }
        }



        private static BackstoryExtension MergeExtensions(BackstoryExtension a, BackstoryExtension b)
        {
            if (a == null) return b;
            if (b == null) return a;

            return new BackstoryExtension
            {
                // Age flags
                overrideAge = a.overrideAge || b.overrideAge,
                chronoIsBio = a.chronoIsBio || b.chronoIsBio,
                forceOverrideFixedAge = a.forceOverrideFixedAge || b.forceOverrideFixedAge,

                // Union of age ranges (only used if overrideAge==true)
                biologicalYears = new FloatRange(
                    Mathf.Min(a.biologicalYears.min, b.biologicalYears.min),
                    Mathf.Max(a.biologicalYears.max, b.biologicalYears.max)),
                extraChronologicalYears = new IntRange(
                    Mathf.Min(a.extraChronologicalYears.min, b.extraChronologicalYears.min),
                    Mathf.Max(a.extraChronologicalYears.max, b.extraChronologicalYears.max)),

                // Limb removal options
                RanMissingPart = a.RanMissingPart || b.RanMissingPart,
                missingPartCount = new IntRange(
                    Mathf.Max(a.missingPartCount.min, b.missingPartCount.min),
                    Mathf.Max(a.missingPartCount.max, b.missingPartCount.max)),
                ensureAtLeastOneLeg = a.ensureAtLeastOneLeg || b.ensureAtLeastOneLeg
            };
        }

        private static void ApplyAgeOverride(Pawn pawn, BackstoryExtension ext)
        {
            try
            {
                // Safety guard: if the biological range is not sensibly defined, do nothing (vanilla ages stay)
                if (ext.biologicalYears.max <= 0f)
                    return;

                float bioYears = Mathf.Clamp(ext.biologicalYears.RandomInRange, 14f, 120f);
                float chronoYears = ext.chronoIsBio
                    ? bioYears
                    : Mathf.Max(bioYears + Mathf.Max(ext.extraChronologicalYears.RandomInRange, 0), bioYears);

                long bioTicks = (long)(bioYears * GenDate.TicksPerYear);
                long chronoTicks = (long)(chronoYears * GenDate.TicksPerYear);

                pawn.ageTracker.AgeBiologicalTicks = bioTicks;
                pawn.ageTracker.AgeChronologicalTicks = chronoTicks;

                if (pawn.ageTracker.AgeChronologicalTicks < pawn.ageTracker.AgeBiologicalTicks)
                    pawn.ageTracker.AgeChronologicalTicks = pawn.ageTracker.AgeBiologicalTicks;

                pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial, true);
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] Age override failed for {pawn.LabelShort}: {e.Message}");
            }
        }

        private static void RemoveRandomSafeLimbs(Pawn pawn, int count, bool ensureAtLeastOneLeg)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var candidates = GetSafeLimbCandidates(pawn);

                    if (candidates.Count == 0)
                        break;

                    if (ensureAtLeastOneLeg)
                    {
                        var remainingLegs = GetCurrentLegs(pawn);
                        if (remainingLegs.Count <= 1)
                        {
                            candidates = candidates.Where(c => !IsLeg(c)).ToList();
                        }
                        if (candidates.Count == 0)
                            break;
                    }

                    var chosen = candidates.RandomElement();

                    if (ensureAtLeastOneLeg && IsLeg(chosen))
                    {
                        var remainingLegs = GetCurrentLegs(pawn);
                        if (remainingLegs.Count <= 1)
                        {
                            var nonLeg = candidates.Where(c => !IsLeg(c)).ToList();
                            if (nonLeg.Count == 0)
                                break;
                            chosen = nonLeg.RandomElement();
                        }
                    }

                    var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, null);
                    missing.Part = chosen;
                    missing.IsFresh = false;
                    pawn.health.AddHediff(missing, chosen);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] Limb removal failed for {pawn.LabelShort}: {e.Message}");
            }
        }

        private static List<BodyPartRecord> GetSafeLimbCandidates(Pawn pawn)
        {
            return pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == BodyPartDepth.Outside)
                .Where(p => p.def != BodyPartDefOf.Head && p.def != BodyPartDefOf.Torso)
                .Where(p => !IsVital(p))
                .Where(p => HasLimbCoreTag(p))
                .ToList();
        }

        private static List<BodyPartRecord> GetCurrentLegs(Pawn pawn)
        {
            return pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == BodyPartDepth.Outside)
                .Where(p => IsLeg(p))
                .ToList();
        }

        private static bool IsVital(BodyPartRecord part)
        {
            var tags = part.def.tags;
            if (tags == null) return false;

            foreach (var t in tags)
            {
                if (t == BodyPartTagDefOf.ConsciousnessSource ||
                    t == BodyPartTagDefOf.BloodPumpingSource ||
                    t == BodyPartTagDefOf.BreathingSource)
                    return true;
            }
            return false;
        }

        private static bool HasLimbCoreTag(BodyPartRecord part)
        {
            var tags = part.def.tags;
            if (tags == null) return false;
            return tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) ||
                   tags.Contains(BodyPartTagDefOf.MovingLimbCore);
        }

        private static bool IsLeg(BodyPartRecord part)
        {
            var tags = part.def.tags;
            if (tags == null) return false;
            bool moving = tags.Contains(BodyPartTagDefOf.MovingLimbCore);
            bool manipulation = tags.Contains(BodyPartTagDefOf.ManipulationLimbCore);
            return moving && !manipulation;
        }
    }
}