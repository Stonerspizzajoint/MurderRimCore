using System.Collections.Generic;
using System.Linq;
using System.Xml;
using RimWorld;
using Verse;

namespace MurderRimCore.MRWD
{
    public class WorkerDroneSettingsDef : Def
    {
        public WorkerDroneBackstorySettings BackstorySettings = new WorkerDroneBackstorySettings();
        public WorkerDroneSpawnSettings SpawnSettings = new WorkerDroneSpawnSettings();

        public static WorkerDroneSettingsDef Current =>
            DefDatabase<WorkerDroneSettingsDef>.AllDefsListForReading.FirstOrDefault();

        public static WorkerDroneBackstorySettings Backstory => Current?.BackstorySettings;
        public static WorkerDroneSpawnSettings Spawn => Current?.SpawnSettings;
    }

    public class WorkerDroneBackstorySettings : IExposable
    {
        public string colonyCreatedCategory = "ColonyDrone";
        public List<string> defaultCategories = new List<string>();
        public List<string> factionGatedCategories = new List<string>();
        public List<string> pawnkindGatedCategories = new List<string>();
        public StringFloatMap categoryWeightMultipliers = new StringFloatMap();
        public bool exclusiveChosenCategory = false;
        public bool applyForcedTraits = true;

        public float GetCategoryMultiplier(string cat, float fallback = 1f)
        {
            if (string.IsNullOrEmpty(cat) || categoryWeightMultipliers == null) return fallback;
            return categoryWeightMultipliers.Get(cat, fallback);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref colonyCreatedCategory, "colonyCreatedCategory", "ColonyDrone");
            Scribe_Collections.Look(ref defaultCategories, "defaultCategories", LookMode.Value);
            Scribe_Collections.Look(ref factionGatedCategories, "factionGatedCategories", LookMode.Value);
            Scribe_Collections.Look(ref pawnkindGatedCategories, "pawnkindGatedCategories", LookMode.Value);
            Scribe_Deep.Look(ref categoryWeightMultipliers, "categoryWeightMultipliers");
            Scribe_Values.Look(ref exclusiveChosenCategory, "exclusiveChosenCategory", false);
            Scribe_Values.Look(ref applyForcedTraits, "applyForcedTraits", true);
        }
    }

    // "Pawn Generator Settings" (spawn-related)
    public class WorkerDroneSpawnSettings : IExposable
    {
        public float hardHatSpawnChance = 0.3f;

        // Facial hair controls
        public bool allowBeards = true;
        public bool allowOnlyMustaches = true;
        public float beardSpawnChance = 0.2f;

        public bool hardHatUseFavoriteColor = false;
        public float favHardHatColorChance = 0.3f;

        // HAIR SETTINGS:
        public List<string> removedHairTags = new List<string> { "Shaved" };
        public bool hairForAwakened = true;
        public bool noHairForUnawakened = true;

        // NEW: separate bald chance per gender
        public float baldChanceMale = 0.5f;
        public float baldChanceFemale = 0.01f;

        // If true, any pawn with Bald hair will always receive a hard hat.
        public bool alwaysHardHatWhenBald = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref hardHatSpawnChance, "hardHatSpawnChance", 0.3f);

            Scribe_Values.Look(ref allowBeards, "allowBeards", false);
            Scribe_Values.Look(ref allowOnlyMustaches, "allowOnlyMustaches", true);
            Scribe_Values.Look(ref beardSpawnChance, "beardSpawnChance", 1f);

            Scribe_Values.Look(ref hardHatUseFavoriteColor, "hardHatUseFavoriteColor", false);
            Scribe_Values.Look(ref favHardHatColorChance, "favHardHatColorChance", 1f);

            Scribe_Collections.Look(ref removedHairTags, "removedHairTags", LookMode.Value);
            if (removedHairTags == null) removedHairTags = new List<string>();

            Scribe_Values.Look(ref hairForAwakened, "hairForAwakened", true);
            Scribe_Values.Look(ref noHairForUnawakened, "noHairForUnawakened", true);

            Scribe_Values.Look(ref baldChanceMale, "baldChanceMale", 0.5f);
            Scribe_Values.Look(ref baldChanceFemale, "baldChanceFemale", 0.05f);

            Scribe_Values.Look(ref alwaysHardHatWhenBald, "alwaysHardHatWhenBald", true);
        }
    }

    public class StringFloatMap : IExposable
    {
        public Dictionary<string, float> data = new Dictionary<string, float>();

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (data == null) data = new Dictionary<string, float>();
            foreach (XmlNode child in xmlRoot.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                string key = child.Name;
                if (string.IsNullOrEmpty(key)) continue;

                float value = 0f;
                try { value = ParseHelper.FromString<float>(child.InnerText); }
                catch { Log.Error($"[MRC] Failed to parse float for key '{key}' in StringFloatMap."); continue; }

                data[key] = value;
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref data, "data", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && data == null)
                data = new Dictionary<string, float>();
        }

        public float Get(string key, float fallback = 1f)
        {
            if (data == null || string.IsNullOrEmpty(key)) return fallback;
            return data.TryGetValue(key, out var v) ? v : fallback;
        }

        public bool TryGetValue(string key, out float value)
        {
            if (data == null) { value = 0f; return false; }
            return data.TryGetValue(key, out value);
        }
    }
}