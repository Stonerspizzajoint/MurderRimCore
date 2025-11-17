using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MurderRimCore
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit_TraitRestrictions
    {
        private static MethodInfo miGainTrait;
        private static MethodInfo miRemoveTrait;

        // Cached reflective accessors for TraitSet -> pawn
        private static readonly FieldInfo fiTraitSetPawn = AccessTools.Field(typeof(TraitSet), "pawn");
        private static readonly PropertyInfo piTraitSetPawn = AccessTools.Property(typeof(TraitSet), "Pawn");

        static HarmonyInit_TraitRestrictions()
        {
            try
            {
                var h = new Harmony("murderrimcore.trait.restrictions");

                // Patch TraitSet.GainTrait(Trait, bool)
                miGainTrait = AccessTools.Method(typeof(TraitSet), "GainTrait", new[] { typeof(Trait), typeof(bool) });
                if (miGainTrait != null)
                {
                    h.Patch(miGainTrait,
                        prefix: new HarmonyMethod(typeof(HarmonyInit_TraitRestrictions), nameof(GainTrait_Prefix)));
                }
                else
                {
                    Log.Warning("[MRC] TraitSet.GainTrait not found; trait restrictions may not block additions.");
                }

                // Cache RemoveTrait for cleanup pass
                miRemoveTrait = AccessTools.Method(typeof(TraitSet), "RemoveTrait", new[] { typeof(Trait) });

                // Final pass after pawn generation to enforce/remove/add as needed
                var miTryGen = typeof(PawnGenerator).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "TryGenerateNewPawnInternal") return false;
                        var p = m.GetParameters();
                        return p.Length == 4 && p[0].ParameterType.IsByRef && p[1].IsOut && p[2].ParameterType == typeof(bool) && p[3].ParameterType == typeof(bool);
                    });
                if (miTryGen != null)
                {
                    h.Patch(miTryGen,
                        postfix: new HarmonyMethod(typeof(HarmonyInit_TraitRestrictions), nameof(TryGenerateNewPawnInternal_Postfix)));
                }
                else
                {
                    Log.Warning("[MRC] TryGenerateNewPawnInternal not found; final trait cleanup may not run.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MRC] Trait restrictions init failed: {e}");
            }
        }

        // Prefix to block disallowed trait additions anywhere (random, forced, scenario).
        public static bool GainTrait_Prefix(TraitSet __instance, Trait trait, bool suppressConflicts)
        {
            try
            {
                if (__instance == null || trait == null || trait.def == null) return true;

                var pawn = GetPawnFromTraitSet(__instance);
                if (pawn == null) return true;

                if (!TraitAllowedForPawn(trait.def, pawn))
                    return false; // Block adding this trait
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] GainTrait_Prefix error: {e}");
            }
            return true;
        }

        // Final enforcement after pawn is fully generated. Removes illegal traits.
        // Also optionally adds traits marked alwaysAddIfBackstoryPresent.
        public static void TryGenerateNewPawnInternal_Postfix(ref Pawn __result)
        {
            var pawn = __result;
            if (pawn == null || pawn.story == null || pawn.story.traits == null || !pawn.RaceProps.Humanlike) return;

            try
            {
                // Remove disallowed traits that slipped through.
                var toRemove = new List<Trait>();
                foreach (var tr in pawn.story.traits.allTraits)
                {
                    if (!TraitAllowedForPawn(tr.def, pawn))
                        toRemove.Add(tr);
                }

                foreach (var tr in toRemove)
                {
                    if (miRemoveTrait != null)
                    {
                        miRemoveTrait.Invoke(pawn.story.traits, new object[] { tr });
                    }
                    else
                    {
                        pawn.story.traits.allTraits.Remove(tr);
                    }
                }

                // Add traits that must always be present for this backstory (via extension), if missing.
                foreach (var td in DefDatabase<TraitDef>.AllDefsListForReading)
                {
                    var ext = td.GetModExtension<TraitExtension>();
                    if (ext == null) continue;
                    if (!ext.alwaysAddIfBackstoryPresent) continue;

                    if (!ConditionsSatisfied(ext, pawn)) continue;
                    if (pawn.story.traits.HasTrait(td)) continue;

                    int degree = ext.forcedDegree ?? 0;
                    if (!ext.forcedDegree.HasValue && td.degreeDatas != null && td.degreeDatas.Count > 0)
                        degree = td.degreeDatas[0].degree;

                    pawn.story.traits.GainTrait(new Trait(td, degree, forced: true), suppressConflicts: true);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] Final trait enforcement failed for {pawn?.LabelShort ?? "null"}: {e}");
            }
        }

        private static Pawn GetPawnFromTraitSet(TraitSet set)
        {
            if (set == null) return null;

            try
            {
                if (fiTraitSetPawn != null)
                {
                    var p = fiTraitSetPawn.GetValue(set) as Pawn;
                    if (p != null) return p;
                }
                if (piTraitSetPawn != null)
                {
                    var p = piTraitSetPawn.GetValue(set, null) as Pawn;
                    if (p != null) return p;
                }
                // Fallback: Traverse
                return Traverse.Create(set).Field("pawn").GetValue<Pawn>();
            }
            catch
            {
                return null;
            }
        }

        private static bool TraitAllowedForPawn(TraitDef def, Pawn pawn)
        {
            var ext = def.GetModExtension<TraitExtension>();
            if (ext == null) return true;

            // Worker drone restriction
            if (ext.onlyForWorkerDrones && !IsWorkerDrone(pawn))
                return false;

            // Backstory restriction
            if (ext.restrictToBackstories != null && ext.restrictToBackstories.Count > 0)
            {
                var child = pawn.story.Childhood;
                var adult = pawn.story.Adulthood;

                bool hasMatch = (child != null && ext.restrictToBackstories.Contains(child))
                                || (adult != null && ext.restrictToBackstories.Contains(adult));

                if (!hasMatch) return false;
            }

            return true;
        }

        private static bool ConditionsSatisfied(TraitExtension ext, Pawn pawn)
        {
            if (ext.onlyForWorkerDrones && !IsWorkerDrone(pawn))
                return false;

            if (ext.restrictToBackstories != null && ext.restrictToBackstories.Count > 0)
            {
                var child = pawn.story.Childhood;
                var adult = pawn.story.Adulthood;

                if (!((child != null && ext.restrictToBackstories.Contains(child))
                      || (adult != null && ext.restrictToBackstories.Contains(adult))))
                    return false;
            }

            return true;
        }

        private static bool IsWorkerDrone(Pawn pawn)
        {
            try
            {
                if (pawn.genes == null) return false;
                return pawn.genes.HasActiveGene(MRWD.MRWD_DefOf.MRWD_DroneBody);
            }
            catch
            {
                return false;
            }
        }
    }
}