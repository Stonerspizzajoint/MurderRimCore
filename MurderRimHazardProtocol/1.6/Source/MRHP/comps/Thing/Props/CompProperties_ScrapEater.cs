using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        // === Need customization (optional) ===
        public string materialNeedLabel;         // e.g. "Material intake"
        public string materialNeedDescription;   // tooltip/description text
        public string materialNeedHediffDefName; // e.g. "MRHP_MaterialDeprived"

        // New single-range property (format: "start~peak" or single number "5")
        public string duplicationChanceRange; // e.g. "3~5" or "5"

        // Chance tuning
        public bool DuplicationByChance = false;
        public float duplicationBaseChance = 0.10f;      // chance at start
        public float duplicationPeakChance = 0.95f;      // chance at peak
        public float duplicationOverflowIncrement = 0.02f;
        public bool duplicationResetOnFail = false;

    // Parses "start~peak" or single "n". Returns true on success.
    public static bool TryParseFeedingRange(string range, out int start, out int peak)
        {
            start = 0;
            peak = 0;
            if (string.IsNullOrWhiteSpace(range)) return false;

            range = range.Trim();
            // Accept "5", " 5 ", "3~5", "3 ~ 5"
            var singleMatch = Regex.Match(range, @"^\s*(\d+)\s*$");
            if (singleMatch.Success)
            {
                if (int.TryParse(singleMatch.Groups[1].Value, out int v))
                {
                    start = Math.Max(0, v);
                    peak = start;
                    return true;
                }
                return false;
            }

            var rangeMatch = Regex.Match(range, @"^\s*(\d+)\s*~\s*(\d+)\s*$");
            if (rangeMatch.Success)
            {
                if (int.TryParse(rangeMatch.Groups[1].Value, out int s) &&
                    int.TryParse(rangeMatch.Groups[2].Value, out int p))
                {
                    start = Math.Max(0, Math.Min(s, p));
                    peak = Math.Max(start, p);
                    return true;
                }
            }

            // fallback: try to parse first number found
            var anyNum = Regex.Match(range, @"(\d+)");
            if (anyNum.Success && int.TryParse(anyNum.Groups[1].Value, out int fallback))
            {
                start = Math.Max(0, fallback);
                peak = start;
                return true;
            }

            return false;
        }
    }
}
