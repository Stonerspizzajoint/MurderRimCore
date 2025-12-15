using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using VEF.AnimalBehaviours;

namespace MRHP
{
    public class CompScrapEater : CompEatWeirdFood
    {
        public new CompProperties_ScrapEater Props => (CompProperties_ScrapEater)this.props;

        public bool autoForbidLoot = true;
        private bool dietListBuilt = false;

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

        // ... [Gizmos Code Omitted for Brevity - Keep existing logic] ...
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Same as before...
            if (this.parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Auto-Forbid Loot",
                    defaultDesc = "If ON, items stripped/scrapped will be Forbidden (X).",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/ForbidOff"),
                    isActive = () => autoForbidLoot,
                    toggleAction = () => { autoForbidLoot = !autoForbidLoot; }
                };
            }
        }

        private void PopulateDietList()
        {
            if (Props.customThingToEat == null) Props.customThingToEat = new List<string>();

            // === NEW: MERGE CONVERSION KEYWORDS INTO FILTH KEYWORDS ===
            // This ensures anything in "ScrapConversionRule" is also treated as edible filth.
            if (!Props.filthConversionRules.NullOrEmpty())
            {
                foreach (var rule in Props.filthConversionRules)
                {
                    if (!rule.keywords.NullOrEmpty())
                    {
                        foreach (string k in rule.keywords)
                        {
                            if (!Props.filthKeywords.Contains(k))
                                Props.filthKeywords.Add(k);
                        }
                    }
                }
            }

            // 1. SCAN DEFS
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                // FILTH SCAN
                if (!Props.filthKeywords.NullOrEmpty())
                {
                    if (def.filth != null || def.category == ThingCategory.Filth)
                    {
                        if (MatchesKeyword(def.defName, Props.filthKeywords))
                            AddToDiet(def.defName);
                    }
                }

                // CORPSE SCAN
                if (!Props.corpseKeywords.NullOrEmpty() && def.IsCorpse)
                {
                    ThingDef innerRace = def.ingestible?.sourceDef;
                    if (innerRace != null)
                    {
                        if (MatchesKeyword(innerRace.defName, Props.corpseKeywords) ||
                            MatchesKeyword(innerRace.label, Props.corpseKeywords))
                        {
                            AddToDiet(def.defName);
                        }
                    }
                }

                // CATEGORY SCAN
                if (!Props.eatThingCategories.NullOrEmpty())
                {
                    foreach (ThingCategoryDef cat in Props.eatThingCategories)
                    {
                        if (def.IsWithinCategory(cat))
                        {
                            AddToDiet(def.defName);
                            break;
                        }
                    }
                }
            }

            // 2. SORTING (Corpses First)
            if (Props.prioritizeCorpses)
            {
                Props.customThingToEat.Sort((string a, string b) =>
                {
                    ThingDef defA = DefDatabase<ThingDef>.GetNamedSilentFail(a);
                    ThingDef defB = DefDatabase<ThingDef>.GetNamedSilentFail(b);
                    if (defA == null || defB == null) return 0;

                    bool aIsCorpse = defA.IsCorpse;
                    bool bIsCorpse = defB.IsCorpse;
                    if (aIsCorpse && !bIsCorpse) return -1;
                    if (!aIsCorpse && bIsCorpse) return 1;
                    return 0;
                });
            }
        }

        public void TrySpawnScrap(Thing eatenThing)
        {
            if (eatenThing == null || this.parent.Map == null) return;

            // 1. CORPSE HANDLING
            if (eatenThing is Corpse corpse)
            {
                // A. STRIP APPAREL
                if (corpse.InnerPawn?.apparel != null)
                {
                    List<Apparel> wornApparel = corpse.InnerPawn.apparel.WornApparel;
                    for (int i = wornApparel.Count - 1; i >= 0; i--)
                    {
                        Apparel app = wornApparel[i];
                        if (corpse.InnerPawn.apparel.TryDrop(app, out Apparel droppedApparel, corpse.PositionHeld, autoForbidLoot))
                        {
                            // Managed by TryDrop
                        }
                    }
                }

                // B. STRIP EQUIPMENT
                if (corpse.InnerPawn?.equipment != null)
                {
                    List<ThingWithComps> allEquipment = corpse.InnerPawn.equipment.AllEquipmentListForReading.ToList();
                    foreach (ThingWithComps eq in allEquipment)
                    {
                        corpse.InnerPawn.equipment.TryDropEquipment(eq, out ThingWithComps droppedEq, corpse.PositionHeld, autoForbidLoot);
                    }
                }

                // C. SPAWN SCRAP PRODUCTS
                Pawn eater = this.parent as Pawn;
                IEnumerable<Thing> products = corpse.ButcherProducts(eater, 1f);

                foreach (Thing product in products)
                {
                    int amount = (int)(product.stackCount * Props.corpseScrapEfficiency);
                    if (amount < 1 && Props.corpseScrapEfficiency > 0 && product.stackCount > 0) amount = 1;

                    if (amount > 0)
                    {
                        Thing scrap = ThingMaker.MakeThing(product.def);
                        scrap.stackCount = amount;

                        // FIX: Use GenPlace to stack items
                        if (GenPlace.TryPlaceThing(scrap, this.parent.Position, this.parent.Map, ThingPlaceMode.Near, out Thing resultingThing))
                        {
                            if (resultingThing != null && autoForbidLoot)
                            {
                                resultingThing.SetForbidden(true, false);
                            }
                        }
                    }
                }
            }
            // 2. FILTH HANDLING
            else if (eatenThing is Filth || eatenThing.def.category == ThingCategory.Filth)
            {
                if (!Props.filthConversionRules.NullOrEmpty())
                {
                    foreach (ScrapConversionRule rule in Props.filthConversionRules)
                    {
                        if (MatchesKeyword(eatenThing.def.defName, rule.keywords))
                        {
                            if (Rand.Value <= rule.chance && rule.thingToSpawn != null)
                            {
                                Thing scrap = ThingMaker.MakeThing(rule.thingToSpawn);
                                scrap.stackCount = 1;

                                // FIX: Use GenPlace to stack items
                                if (GenPlace.TryPlaceThing(scrap, this.parent.Position, this.parent.Map, ThingPlaceMode.Near, out Thing resultingThing))
                                {
                                    if (resultingThing != null && autoForbidLoot)
                                    {
                                        resultingThing.SetForbidden(true, false);
                                    }
                                }

                                MoteMaker.ThrowText(this.parent.DrawPos, this.parent.Map, "Scrap!", Color.white);
                            }
                            return; // Only process one rule per filth
                        }
                    }
                }
            }
        }

        // Helpers
        private bool MatchesKeyword(string target, List<string> keywords)
        {
            if (keywords == null) return false;
            for (int i = 0; i < keywords.Count; i++)
            {
                if (target.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private void AddToDiet(string defName)
        {
            if (!Props.blacklist.NullOrEmpty() && Props.blacklist.Contains(defName)) return;
            if (!Props.customThingToEat.Contains(defName)) Props.customThingToEat.Add(defName);
        }
    }
}