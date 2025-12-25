using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using VEF.AnimalBehaviours;
using VREAndroids; // Direct dependency

namespace MRHP
{
    public class CompScrapEater : CompEatWeirdFood
    {
        public new CompProperties_ScrapEater Props => (CompProperties_ScrapEater)this.props;

        public bool autoForbidLoot = true;
        private bool dietListBuilt = false;

        // Add fields for duplication timing if desired
        // e.g. public int lastDuplicationTick = 0;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoForbidLoot, "autoForbidLoot", true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (Props.autoPopulateList && !dietListBuilt)
            {
                PopulateDietList();
                dietListBuilt = true;
            }
            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.parent.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Auto-Forbid Loot",
                defaultDesc = "If ON, items stripped/scrapped will be forbidden.",
                // Use built-in RimWorld textures and switch icon depending on state
                icon = autoForbidLoot ? TexCommand.ForbidOn : TexCommand.ForbidOff,
                isActive = () => autoForbidLoot,
                toggleAction = () => { autoForbidLoot = !autoForbidLoot; }
            };
        }

        public override string CompInspectStringExtra()
        {
            try
            {
                if (Props == null) return base.CompInspectStringExtra();
                if (!Props.advanceLifeStage || Props.advanceAfterXFeedings <= 0) return base.CompInspectStringExtra();

                int current = this.currentFeedings;
                int needed = Props.advanceAfterXFeedings;

                // Deterministic mode: keep the simple original display
                if (!Props.DuplicationByChance)
                {
                    int remaining = Math.Max(0, needed - current);
                    return Props.fissionAfterXFeedings
                        ? $"Feedings: {current}/{needed} (duplicates in {remaining})"
                        : $"Feedings: {current}/{needed} (advances in {remaining})";
                }

                // Chance mode: parse range and compute chance
                int start, peak;
                bool parsed = CompProperties_ScrapEater.TryParseFeedingRange(Props.duplicationChanceRange, out start, out peak);
                if (!parsed)
                {
                    // fallback to deterministic threshold as both start and peak
                    start = Math.Max(0, needed);
                    peak = start;
                }

                start = Math.Max(0, start);
                peak = Math.Max(start, peak);

                float baseChance = Mathf.Clamp01(Props.duplicationBaseChance);
                float peakChance = Mathf.Clamp01(Props.duplicationPeakChance);
                float overflowInc = Math.Max(0f, Props.duplicationOverflowIncrement);

                float chance;
                if (current < start)
                {
                    chance = baseChance * ((float)current / (float)Math.Max(1, start));
                }
                else if (current <= peak)
                {
                    float t = (start == peak) ? 1f : (float)(current - start) / (float)(peak - start);
                    chance = Mathf.Lerp(baseChance, peakChance, t);
                }
                else
                {
                    int overflow = current - peak;
                    chance = peakChance + overflow * overflowInc;
                }
                chance = Mathf.Clamp01(chance);

                // Build a short, clear string: "Feedings: X | Chance: 47% | Likely: 3–5"
                string rangeText = parsed ? $"{start}–{peak}" : $"{needed}";
                string chanceText = chance.ToStringPercent();

                return $"Feedings: {current} | Chance: {chanceText} | Likely: {rangeText}";
            }
            catch (Exception ex)
            {
                Log.Error($"[ScrapEater] CompInspectStringExtra error: {ex}");
                return base.CompInspectStringExtra();
            }
        }

        // =========================================================
        //               DUPLICATION / "ROBO-FISSION" LOGIC
        // =========================================================

        public void TryDuplicateSelfIfReady(Pawn pawn)
        {
            if (pawn == null)
                pawn = this.parent as Pawn;

            if (pawn == null || !pawn.Spawned || Props == null)
            {
                Log.Warning("[ScrapEater] TryDuplicateSelfIfReady: No valid pawn, or not spawned, or Props null.");
                return;
            }

            Log.Message($"[ScrapEater] Duplication check on {pawn.LabelShort} feedings={this.currentFeedings}/{Props.advanceAfterXFeedings}");

            // No feeding system
            if (Props.advanceAfterXFeedings <= 0)
            {
                Log.Message("[ScrapEater] Duplication/advancement disabled (advanceAfterXFeedings <= 0)");
                return;
            }

            // If chance mode is disabled, keep original deterministic behavior
            if (!Props.DuplicationByChance)
            {
                if (this.currentFeedings < Props.advanceAfterXFeedings)
                {
                    Log.Message($"[ScrapEater] {pawn.LabelShort} has not met the feeding requirement: {this.currentFeedings}/{Props.advanceAfterXFeedings}");
                    return;
                }

                // deterministic: proceed to duplicate/advance
                PerformDuplicationOrAdvance(pawn);
                this.currentFeedings = 0;
                Log.Message($"[ScrapEater] {pawn.LabelShort} currentFeedings reset to 0");
                return;
            }

            // -------------------------
            // Chance-based duplication
            // -------------------------
            // Parse single-range property (e.g., "3~5" or "5")
            int start, peak;
            if (!CompProperties_ScrapEater.TryParseFeedingRange(Props.duplicationChanceRange, out start, out peak))
            {
                // fallback to using advanceAfterXFeedings as both start and peak
                start = Math.Max(0, Props.advanceAfterXFeedings);
                peak = start;
                Log.Message($"[ScrapEater] duplicationChanceRange invalid or empty; falling back to advanceAfterXFeedings={start}");
            }

            start = Math.Max(0, start);
            peak = Math.Max(start, peak);

            float baseChance = Mathf.Clamp01(Props.duplicationBaseChance);
            float peakChance = Mathf.Clamp01(Props.duplicationPeakChance);
            float overflowInc = Math.Max(0f, Props.duplicationOverflowIncrement);

            float chance;

            if (this.currentFeedings < start)
            {
                // scale up from 0 to baseChance proportionally before start
                chance = baseChance * ((float)this.currentFeedings / (float)Math.Max(1, start));
            }
            else if (this.currentFeedings <= peak)
            {
                // linear ramp from baseChance at start to peakChance at peak
                float t = (start == peak) ? 1f : (float)(this.currentFeedings - start) / (float)(peak - start);
                chance = Mathf.Lerp(baseChance, peakChance, t);
            }
            else
            {
                // past peak: start at peakChance and add overflow increments per extra feeding
                int overflow = this.currentFeedings - peak;
                chance = peakChance + overflow * overflowInc;
            }

            chance = Mathf.Clamp01(chance);

            Log.Message($"[ScrapEater] {pawn.LabelShort} duplication chance computed: {chance:P1} (feedings={this.currentFeedings}, start={start}, peak={peak})");

            // Roll
            if (Rand.Value < chance)
            {
                Log.Message($"[ScrapEater] Duplication roll succeeded for {pawn.LabelShort} (chance {chance:P1})");
                PerformDuplicationOrAdvance(pawn);
                this.currentFeedings = 0;
                Log.Message($"[ScrapEater] {pawn.LabelShort} currentFeedings reset to 0");
            }
            else
            {
                Log.Message($"[ScrapEater] Duplication roll failed for {pawn.LabelShort} (chance {chance:P1})");
                if (Props.duplicationResetOnFail)
                {
                    this.currentFeedings = 0;
                    Log.Message($"[ScrapEater] {pawn.LabelShort} currentFeedings reset to 0 due to duplicationResetOnFail.");
                }
            }
        }

        // Helper: perform the existing fission/advance logic (extracted from your original method)
        private void PerformDuplicationOrAdvance(Pawn pawn)
        {
            if (pawn == null || Props == null)
            {
                Log.Warning("[ScrapEater] PerformDuplicationOrAdvance called with null pawn or Props.");
                return;
            }

            bool debugMode = false; // set true while testing, false for release

            try
            {
                // ---------- FISSION MODE ----------
                if (Props.fissionAfterXFeedings)
                {
                    // Determine offspring kind: parent's own kind unless overridden
                    PawnKindDef baseKind = pawn.kindDef;
                    PawnKindDef targetKind = baseKind;

                    if (!string.IsNullOrWhiteSpace(Props.defToFissionTo))
                    {
                        // Use GetNamedSilentFail to avoid exceptions; fallback to parent's kind
                        var maybe = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.defToFissionTo);
                        if (maybe != null) targetKind = maybe;
                        else Log.Warning($"[ScrapEater] defToFissionTo '{Props.defToFissionTo}' not found; falling back to parent's kind.");
                    }

                    if (targetKind == null)
                    {
                        Log.Warning("[ScrapEater] No valid PawnKindDef to spawn for fission; aborting.");
                        return;
                    }

                    if (debugMode) Log.Message($"[ScrapEater] Spawning {Props.numberOfOffspring}x {targetKind.defName} for {pawn.LabelShort}");

                    for (int i = 0; i < Math.Max(0, Props.numberOfOffspring); i++)
                    {
                        try
                        {
                            // Build a minimal, safe PawnGenerationRequest. Avoid passing null strings to Named().
                            PawnGenerationRequest req = new PawnGenerationRequest(
                                targetKind,
                                pawn.Faction,
                                PawnGenerationContext.NonPlayer,
                                pawn.Tile,
                                allowDead: false,
                                allowDowned: true,
                                canGeneratePawnRelations: false,
                                colonistRelationChanceFactor: 0f,
                                validatorPreGear: null,
                                validatorPostGear: null,
                                fixedBiologicalAge: 0f,
                                fixedChronologicalAge: 0f,
                                fixedGender: null
                            );

                            Pawn child = PawnGenerator.GeneratePawn(req);
                            if (child == null)
                            {
                                Log.Warning($"[ScrapEater] PawnGenerator.GeneratePawn returned null for kind {targetKind.defName}.");
                                continue;
                            }

                            // Add parent relation and reset age safely
                            child.relations?.AddDirectRelation(PawnRelationDefOf.Parent, pawn);
                            if (child.ageTracker != null)
                            {
                                child.ageTracker.AgeBiologicalTicks = 0;
                                child.ageTracker.AgeChronologicalTicks = 0;
                            }

                            GenSpawn.Spawn(child, pawn.Position, pawn.Map, WipeMode.Vanish);
                        }
                        catch (Exception ex)
                        {
                            // Use vanilla logging; keep the try/catch so we don't crash the job driver
                            Log.Error($"[ScrapEater] Exception while generating/spawning child: {ex}");
                        }
                    }

                    Messages.Message($"{pawn.LabelCap} replicates!", pawn, MessageTypeDefOf.PositiveEvent, true);
                    return;
                }

                // ---------- ADVANCEMENT MODE ----------
                if (!string.IsNullOrWhiteSpace(Props.defToAdvanceTo))
                {
                    // Resolve PawnKindDef safely
                    PawnKindDef advKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.defToAdvanceTo);
                    if (advKind == null)
                    {
                        Log.Warning($"[ScrapEater] defToAdvanceTo '{Props.defToAdvanceTo}' not found; cannot advance {pawn.LabelShort}.");
                        return;
                    }

                    try
                    {
                        PawnGenerationRequest req = new PawnGenerationRequest(
                            advKind,
                            pawn.Faction,
                            PawnGenerationContext.NonPlayer,
                            pawn.Tile,
                            allowDead: false,
                            allowDowned: true,
                            canGeneratePawnRelations: false,
                            colonistRelationChanceFactor: 0f
                        );

                        Pawn adv = PawnGenerator.GeneratePawn(req);
                        if (adv == null)
                        {
                            Log.Warning($"[ScrapEater] PawnGenerator.GeneratePawn returned null for advancement kind {advKind.defName}.");
                            return;
                        }

                        // Transfer name if appropriate
                        if (pawn.Name != null && !pawn.Name.ToString().UncapitalizeFirst().Contains(pawn.def.label))
                            adv.Name = pawn.Name;

                        adv.relations?.AddDirectRelation(PawnRelationDefOf.Parent, pawn);
                        GenSpawn.Spawn(adv, pawn.Position, pawn.Map, WipeMode.Vanish);

                        Messages.Message($"{pawn.LabelCap} advances!", pawn, MessageTypeDefOf.PositiveEvent, true);
                        pawn.Destroy(DestroyMode.Vanish);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[ScrapEater] Exception while advancing pawn {pawn.LabelShort}: {ex}");
                    }

                    return;
                }

                // If neither mode applied
                if (debugMode) Log.Message($"[ScrapEater] No fission/advance definitions for {pawn.LabelShort}.");
            }
            catch (Exception ex)
            {
                Log.Error($"[ScrapEater] PerformDuplicationOrAdvance top-level exception: {ex}");
            }
        }


        // =========================================================
        //                  LOOT SPAWNING LOGIC
        // =========================================================
        public void TrySpawnScrap(Thing eatenThing)
        {
            if (eatenThing == null || this.parent.Map == null) return;

            // 1. CORPSE HANDLING
            if (eatenThing is Corpse corpse)
            {
                if (autoForbidLoot && !corpse.Destroyed) corpse.SetForbidden(true, false);

                Pawn victim = corpse.InnerPawn;
                if (victim == null) return;

                // A. STRIP APPAREL & EQUIPMENT
                if (corpse.AnythingToStrip())
                {
                    if (victim.apparel != null)
                    {
                        List<Apparel> worn = victim.apparel.WornApparel.ToList();
                        foreach (Apparel app in worn)
                            victim.apparel.TryDrop(app, out Apparel _, corpse.PositionHeld, autoForbidLoot);
                    }
                    if (victim.equipment != null)
                    {
                        List<ThingWithComps> eq = victim.equipment.AllEquipmentListForReading.ToList();
                        foreach (ThingWithComps t in eq)
                            victim.equipment.TryDropEquipment(t, out ThingWithComps _, corpse.PositionHeld, autoForbidLoot);
                    }
                    FleckMaker.ThrowDustPuff(corpse.Position, corpse.Map, 1f);
                }

                // B. SPAWN SCRAP
                try
                {
                    // DIRECT CHECK FOR ANDROIDS (No Meat, No Crash)
                    if (IsAndroidOrMech(victim))
                    {
                        float size = victim.BodySize;

                        // Androids drop Steel/Plasteel/Components instead of meat
                        GenerateAndSpawn(ThingDefOf.Steel, size * 20f);
                        GenerateAndSpawn(ThingDefOf.Plasteel, size * 5f);
                        GenerateAndSpawn(ThingDefOf.ComponentIndustrial, size * 0.8f);
                    }
                    else
                    {
                        // ORGANIC Handling
                        IEnumerable<Thing> products = corpse.ButcherProducts(null, 1.0f);
                        if (products != null)
                        {
                            foreach (Thing product in products)
                            {
                                GenerateAndSpawn(product.def, product.stackCount);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If something weird happens, we log it but don't stop the destruction
                    Log.Warning($"[MRHP] ScrapEater error on {victim.LabelShort}: {ex.Message}");
                }
            }
            // 2. FILTH HANDLING
            else if (eatenThing is Filth || eatenThing.def.category == ThingCategory.Filth)
            {
                if (!Props.filthConversionRules.NullOrEmpty())
                {
                    foreach (var rule in Props.filthConversionRules)
                    {
                        if (MatchesKeyword(eatenThing.def.defName, rule.keywords))
                        {
                            if (Rand.Value <= rule.chance && rule.thingToSpawn != null)
                            {
                                GenerateAndSpawn(rule.thingToSpawn, 1);
                                MoteMaker.ThrowText(this.parent.DrawPos, this.parent.Map, "Scrap!", Color.white);
                            }
                            return;
                        }
                    }
                }
            }
        }

        // Put this inside CompScrapEater
        public void TryReduceHediffByOneStage(Pawn pawn, HediffDef def = null)
        {
            try
            {
                if (pawn == null) return;

                // Resolve hediff def from props if not provided
                HediffDef targetDef = def;
                if (targetDef == null && this.Props != null && !string.IsNullOrWhiteSpace(this.Props.materialNeedHediffDefName))
                {
                    targetDef = DefDatabase<HediffDef>.GetNamedSilentFail(this.Props.materialNeedHediffDefName);
                    if (targetDef == null)
                        Log.Warning($"[ScrapEater] materialNeedHediffDefName '{this.Props.materialNeedHediffDefName}' not found; falling back to Malnutrition.");
                }
                if (targetDef == null) targetDef = HediffDefOf.Malnutrition;

                // Find existing hediff instance
                Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(targetDef);
                if (hediff == null) return;

                // If the def has stages, move to previous stage's minSeverity
                var stages = targetDef.stages;
                if (stages == null || stages.Count == 0)
                {
                    // fallback: reduce severity by a fixed chunk
                    float fallbackDelta = 0.25f;
                    hediff.Severity = Math.Max(0f, hediff.Severity - fallbackDelta);
                    Log.Message($"[ScrapEater] Reduced {pawn.LabelShort}'s {targetDef.defName} severity by {fallbackDelta:0.##} (fallback). New severity: {hediff.Severity:0.###}");
                    return;
                }

                // Determine current stage index (highest index where severity >= minSeverity)
                int currentIndex = 0;
                for (int i = 0; i < stages.Count; i++)
                {
                    float min = stages[i].minSeverity;
                    if (hediff.Severity >= min) currentIndex = i;
                }

                // Compute previous stage index and target severity
                int prevIndex = Math.Max(0, currentIndex - 1);
                float targetSeverity = stages[prevIndex].minSeverity;

                // If targetSeverity is >= current severity (edge case), fallback to subtracting a chunk
                if (targetSeverity >= hediff.Severity)
                {
                    float fallbackDelta = 0.25f;
                    hediff.Severity = Math.Max(0f, hediff.Severity - fallbackDelta);
                    Log.Message($"[ScrapEater] Reduced {pawn.LabelShort}'s {targetDef.defName} severity by fallback {fallbackDelta:0.##}. New severity: {hediff.Severity:0.###}");
                }
                else
                {
                    hediff.Severity = targetSeverity;
                    Log.Message($"[ScrapEater] Reduced {pawn.LabelShort}'s {targetDef.defName} to previous stage severity {targetSeverity:0.###}.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ScrapEater] TryReduceHediffByOneStage failed: {ex}");
            }
        }

        private bool IsAndroidOrMech(Pawn p)
        {
            // 1. Vanilla Mech Check
            if (p.RaceProps.IsMechanoid) return true;

            // 2. VREAndroids Check (Hard Dependency)
            if (VREAndroids.Utils.IsAndroid(p)) return true;

            return false;
        }

        private void GenerateAndSpawn(ThingDef def, float baseAmount)
        {
            if (def == null || baseAmount <= 0) return;

            // Efficiency Calc
            float calculated = baseAmount * Props.corpseScrapEfficiency;
            int finalAmount = (int)calculated;
            if (Rand.Value < (calculated - finalAmount)) finalAmount++;

            if (finalAmount == 0 && Props.corpseScrapEfficiency > 0 && baseAmount >= 1)
                finalAmount = 1;

            if (finalAmount > 0)
            {
                Thing scrap = ThingMaker.MakeThing(def);
                scrap.stackCount = finalAmount;
                if (GenPlace.TryPlaceThing(scrap, this.parent.Position, this.parent.Map, ThingPlaceMode.Near, out Thing placed))
                {
                    if (placed != null && autoForbidLoot) placed.SetForbidden(true, false);
                }
            }
        }

        // --- DIET HELPERS ---
        private void PopulateDietList()
        {
            if (Props.customThingToEat == null) Props.customThingToEat = new List<string>();

            if (!Props.filthConversionRules.NullOrEmpty())
                foreach (var r in Props.filthConversionRules)
                    if (!r.keywords.NullOrEmpty())
                        foreach (string k in r.keywords)
                            if (!Props.filthKeywords.Contains(k)) Props.filthKeywords.Add(k);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!Props.filthKeywords.NullOrEmpty() && (def.filth != null || def.category == ThingCategory.Filth))
                {
                    if (MatchesKeyword(def.defName, Props.filthKeywords)) AddToDiet(def.defName);
                }
                if (!Props.corpseKeywords.NullOrEmpty() && def.IsCorpse)
                {
                    ThingDef inner = def.ingestible?.sourceDef;
                    if (inner != null && (MatchesKeyword(inner.defName, Props.corpseKeywords) || MatchesKeyword(inner.label, Props.corpseKeywords)))
                        AddToDiet(def.defName);
                }
                if (!Props.eatThingCategories.NullOrEmpty())
                    foreach (ThingCategoryDef cat in Props.eatThingCategories) if (def.IsWithinCategory(cat)) { AddToDiet(def.defName); break; }
            }

            if (Props.prioritizeCorpses)
            {
                Props.customThingToEat.Sort((a, b) =>
                {
                    ThingDef dA = DefDatabase<ThingDef>.GetNamedSilentFail(a);
                    ThingDef dB = DefDatabase<ThingDef>.GetNamedSilentFail(b);
                    if (dA == null || dB == null) return 0;
                    if (dA.IsCorpse && !dB.IsCorpse) return -1;
                    if (!dA.IsCorpse && dB.IsCorpse) return 1;
                    return 0;
                });
            }
        }
        private bool MatchesKeyword(string t, List<string> k)
        {
            if (k == null) return false;
            for (int i = 0; i < k.Count; i++) if (t.IndexOf(k[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        private void AddToDiet(string d)
        {
            if (!Props.blacklist.NullOrEmpty() && Props.blacklist.Contains(d)) return;
            if (!Props.customThingToEat.Contains(d)) Props.customThingToEat.Add(d);
        }
    }
}