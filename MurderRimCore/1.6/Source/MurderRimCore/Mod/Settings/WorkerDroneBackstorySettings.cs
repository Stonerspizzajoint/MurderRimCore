using System.Collections.Generic;
using System.Xml;
using Verse;

namespace MurderRimCore
{
    public class WorkerDroneBackstorySettings : IExposable
    {
        public List<string> globalCategories = new List<string>();
        public List<WorkerDroneFactionBackstorySettings> factionCategoryMap =
            new List<WorkerDroneFactionBackstorySettings>();
        public List<WorkerDronePawnkindBackstorySettings> pawnkindCategoryMap =
            new List<WorkerDronePawnkindBackstorySettings>();

        public StringFloatMap categoryWeightMultipliers = new StringFloatMap();
        public StringFloatMap backstoryCommonalityMultipliers = new StringFloatMap();

        // How strongly pawnkind-owned categories are favored for their pawnkinds.
        public float pawnkindCategoryWeightBoost = 3.0f;

        public bool exclusiveChosenCategory = false;
        public bool applyForcedTraits = true;

        public float GetCategoryMultiplier(string cat, float fallback = 1f)
        {
            return categoryWeightMultipliers != null
                ? categoryWeightMultipliers.Get(cat, fallback)
                : fallback;
        }

        public float GetBackstoryMultiplier(string defName, float fallback = 1f)
        {
            return backstoryCommonalityMultipliers != null
                ? backstoryCommonalityMultipliers.Get(defName, fallback)
                : fallback;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref globalCategories, "globalCategories", LookMode.Value);
            Scribe_Collections.Look(ref factionCategoryMap, "factionCategoryMap", LookMode.Deep);
            Scribe_Collections.Look(ref pawnkindCategoryMap, "pawnkindCategoryMap", LookMode.Deep);

            Scribe_Deep.Look(ref categoryWeightMultipliers, "categoryWeightMultipliers");
            Scribe_Deep.Look(ref backstoryCommonalityMultipliers, "backstoryCommonalityMultipliers");

            Scribe_Values.Look(ref pawnkindCategoryWeightBoost, "pawnkindCategoryWeightBoost", 3.0f);
            Scribe_Values.Look(ref exclusiveChosenCategory, "exclusiveChosenCategory", false);
            Scribe_Values.Look(ref applyForcedTraits, "applyForcedTraits", true);

            if (globalCategories == null)
                globalCategories = new List<string>();
            if (factionCategoryMap == null)
                factionCategoryMap = new List<WorkerDroneFactionBackstorySettings>();
            if (pawnkindCategoryMap == null)
                pawnkindCategoryMap = new List<WorkerDronePawnkindBackstorySettings>();
            if (categoryWeightMultipliers == null)
                categoryWeightMultipliers = new StringFloatMap();
            if (backstoryCommonalityMultipliers == null)
                backstoryCommonalityMultipliers = new StringFloatMap();
        }

        public WorkerDroneFactionBackstorySettings GetFactionSettings(string factionDefName)
        {
            if (string.IsNullOrEmpty(factionDefName) || factionCategoryMap == null)
                return null;

            for (int i = 0; i < factionCategoryMap.Count; i++)
            {
                WorkerDroneFactionBackstorySettings entry = factionCategoryMap[i];
                if (entry != null && entry.FactionDef == factionDefName)
                    return entry;
            }

            return null;
        }

        public WorkerDronePawnkindBackstorySettings GetPawnkindSettings(string pawnKindDefName)
        {
            if (string.IsNullOrEmpty(pawnKindDefName) || pawnkindCategoryMap == null)
                return null;

            for (int i = 0; i < pawnkindCategoryMap.Count; i++)
            {
                WorkerDronePawnkindBackstorySettings entry = pawnkindCategoryMap[i];
                if (entry != null && entry.PawnKindDef == pawnKindDefName)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Return all pawnkind defNames that own this category via pawnkindCategoryMap.
        /// </summary>
        public List<string> GetOwnerPawnkindsForCategory(string category)
        {
            List<string> owners = null;

            if (string.IsNullOrEmpty(category) || pawnkindCategoryMap == null)
                return null;

            for (int i = 0; i < pawnkindCategoryMap.Count; i++)
            {
                WorkerDronePawnkindBackstorySettings entry = pawnkindCategoryMap[i];
                if (entry == null || string.IsNullOrEmpty(entry.PawnKindDef) ||
                    entry.ExclusiveBackstoryCatagories == null)
                    continue;

                for (int j = 0; j < entry.ExclusiveBackstoryCatagories.Count; j++)
                {
                    string cat = entry.ExclusiveBackstoryCatagories[j];
                    if (cat == category)
                    {
                        if (owners == null)
                            owners = new List<string>();
                        owners.Add(entry.PawnKindDef);
                        break;
                    }
                }
            }

            return owners;
        }

        /// <summary>
        /// Returns true if this pawnkind is one of the owners of the given category.
        /// </summary>
        public bool PawnkindOwnsCategory(string pawnKindDefName, string category)
        {
            if (string.IsNullOrEmpty(pawnKindDefName) || string.IsNullOrEmpty(category))
                return false;

            List<string> owners = GetOwnerPawnkindsForCategory(category);
            if (owners == null)
                return false;

            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i] == pawnKindDefName)
                    return true;
            }

            return false;
        }
    }

    public class WorkerDroneFactionBackstorySettings : IExposable
    {
        public string FactionDef;
        public List<string> ExclusiveBackstoryCatagories = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionDef, "FactionDef");
            Scribe_Collections.Look(ref ExclusiveBackstoryCatagories, "ExclusiveBackstoryCatagories", LookMode.Value);

            if (ExclusiveBackstoryCatagories == null)
                ExclusiveBackstoryCatagories = new List<string>();
        }
    }

    public class WorkerDronePawnkindBackstorySettings : IExposable
    {
        public string PawnKindDef;
        public List<string> ExclusiveBackstoryCatagories = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref PawnKindDef, "PawnKindDef");
            Scribe_Collections.Look(ref ExclusiveBackstoryCatagories, "ExclusiveBackstoryCatagories", LookMode.Value);

            if (ExclusiveBackstoryCatagories == null)
                ExclusiveBackstoryCatagories = new List<string>();
        }
    }
}