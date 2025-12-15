using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEF.AnimalBehaviours;
using Verse;

namespace MRHP
{
    // === HELPER CLASS FOR XML ===
    public class ScrapConversionRule
    {
        public List<string> keywords = new List<string>(); // e.g. "Slag", "Rubble"
        public ThingDef thingToSpawn;                      // e.g. Steel
        public float chance = 0.1f;                        // 0.1 = 10% chance
    }

    public class CompProperties_ScrapEater : CompProperties_EatWeirdFood
    {
        public CompProperties_ScrapEater()
        {
            this.compClass = typeof(CompScrapEater);
        }

        // === EXISTING SETTINGS ===
        public bool autoPopulateList = true;
        public bool prioritizeCorpses = false;
        public List<string> filthKeywords = new List<string>();
        public List<string> corpseKeywords = new List<string>();
        public List<ThingCategoryDef> eatThingCategories = new List<ThingCategoryDef>();
        public List<string> blacklist = new List<string>();

        // === NEW LOOT SETTINGS ===

        // CORPSES: How efficient is the digestion? 
        // 1.0 = Full butcher yield (OP). 0.1 = 10% of yield.
        public float corpseScrapEfficiency = 0.2f;

        // FILTH: Specific rules for turning dirt into gold.
        public List<ScrapConversionRule> filthConversionRules = new List<ScrapConversionRule>();
    }
}
