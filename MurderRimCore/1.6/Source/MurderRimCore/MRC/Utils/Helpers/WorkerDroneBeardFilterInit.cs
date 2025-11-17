using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using VREAndroids;

namespace MurderRimCore
{
    /// <summary>
    /// Generation-time beard enforcement for worker drones.
    /// Unawakened drones: always clean-shaven (no facial hair).
    /// Awakened drones: follow settings (allowBeards, allowOnlyMustaches, beardSpawnChance).
    /// - If allowBeards && allowOnlyMustaches: only assign mustaches (and clear everything else).
    /// </summary>
    public static class WorkerDroneBeardFilterInit
    {
        public static bool DebugLog = false;
        private static List<BeardDef> cachedMoustachePool;

        [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
        public static class WorkerDroneBeard_GeneratePawnPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
            {
                if (__result == null) return;
                ApplyMustachePolicy(__result, reason: "GeneratePawn");
            }
        }

        public static void ApplyMustachePolicy(Pawn pawn, string reason = null)
        {
            try
            {
                if (pawn == null || pawn.story == null || !pawn.RaceProps.Humanlike) return;

                var settings = MRC_Settings.Spawn;
                if (settings == null) return;

                bool isDrone =
                    DroneHelper.IsWorkerDrone(pawn) ||
                    (pawn.kindDef?.defName?.IndexOf("WorkerDrone", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isDrone) return;

                var style = pawn.style;
                if (style == null) return;

                BeardDef current = style.beardDef;
                bool hadBeardAssigned = current != null;

                // Awakened status (unawakened are always clean-shaven)
                bool awakened = Utils.IsAwakened(pawn);
                if (!awakened)
                {
                    if (hadBeardAssigned)
                    {
                        style.beardDef = null;
                        if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: unawakened -> cleared '{current.defName}'.");
                        RefreshPawnGraphics(pawn);
                    }
                    return;
                }

                // Settings
                bool allowBeards = settings.allowBeards;
                bool onlyMustache = allowBeards && settings.allowOnlyMustaches;
                float chance = Mathf.Clamp01(settings.beardSpawnChance);

                if (DebugLog)
                    Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: awakened; allowBeards={allowBeards} onlyMustache={onlyMustache} chance={chance} hadBeard={hadBeardAssigned} current='{current?.defName ?? "none"}'");

                bool changed = false;

                if (!allowBeards)
                {
                    // Beards disabled outright
                    if (hadBeardAssigned)
                    {
                        style.beardDef = null;
                        changed = true;
                        if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: beards disabled -> cleared '{current.defName}'.");
                    }
                }
                else if (onlyMustache)
                {
                    // Mustache-only mode:
                    // - Clear any non-moustache facial hair
                    // - With chance, assign a moustache (works whether or not one was pre-assigned)
                    // - Otherwise remain clean-shaven
                    EnsureMoustachePool();

                    // Default: restrict moustaches to Male or Any. Adjust if you want to allow on all genders.
                    List<BeardDef> pool = cachedMoustachePool;
                    if (pawn.gender == Gender.Male)
                    {
                        var filtered = pool.Where(b => b.styleGender == StyleGender.Any || b.styleGender == StyleGender.Male).ToList();
                        if (filtered.Count > 0) pool = filtered;
                    }
                    else
                    {
                        // By default we disallow moustaches for non-males; clear any assigned
                        pool = new List<BeardDef>(); // empty pool => no moustache chosen
                    }

                    // Always strip non-moustache styles in moustache-only mode
                    if (current != null && !IsMoustache(current))
                    {
                        style.beardDef = null;
                        changed = true;
                        if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: moustache-only -> removed non-moustache '{current.defName}'.");
                        current = null;
                    }

                    bool roll = Rand.Value <= chance;

                    if (roll && pool.Count > 0)
                    {
                        // If already has a moustache, keep it; otherwise pick a moustache
                        if (current == null || !IsMoustache(current))
                        {
                            var pick = pool.RandomElement();
                            style.beardDef = pick;
                            changed = true;
                            if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: moustache-only -> assigned moustache '{pick.defName}' (chance={chance}).");
                        }
                        else if (DebugLog)
                        {
                            Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: moustache-only -> already has moustache '{current.defName}', keeping.");
                        }
                    }
                    else
                    {
                        // Chance failed or no pool: ensure clean-shaven
                        if (current != null)
                        {
                            style.beardDef = null;
                            changed = true;
                            if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: moustache-only -> chance failed or no pool, cleared.");
                        }
                    }
                }
                else
                {
                    // General beards allowed; gate on spawn chance
                    if (hadBeardAssigned && Rand.Value > chance)
                    {
                        style.beardDef = null;
                        changed = true;
                        if (DebugLog) Log.Message($"[MRC] ({reason}) {pawn.LabelShort}: chance gate failed -> cleared '{current.defName}'.");
                    }
                }

                if (changed)
                    RefreshPawnGraphics(pawn);
            }
            catch (Exception e)
            {
                Log.Warning($"[MRC] ({reason}) Beard enforcement failed for {pawn?.LabelShort ?? "null"}: {e.Message}");
            }
        }

        private static void EnsureMoustachePool()
        {
            if (cachedMoustachePool != null) return;
            cachedMoustachePool = DefDatabase<BeardDef>.AllDefsListForReading
                .Where(IsMoustache)
                .ToList();
        }

        private static bool IsMoustache(BeardDef beard)
        {
            if (beard == null) return false;

            // Tag-based first
            if (beard.styleTags != null &&
                beard.styleTags.Any(t => t.Equals("MoustacheOnly", StringComparison.Ordinal)))
                return true;

            // Name fallback
            string dn = beard.defName ?? string.Empty;
            if (dn.IndexOf("Moustache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Mustache", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static void RefreshPawnGraphics(Pawn pawn)
        {
            PortraitsCache.SetDirty(pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }
}