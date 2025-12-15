using System.Collections.Generic;
using RimWorld;
using Verse;
using MRWD;

namespace MurderRimCore
{
    public static class WorkerDroneBackstoryResolver
    {
        public static bool DebugLog = false;

        public static HashSet<string> GetAllowedCategoriesForWorkerDrone(Pawn pawn)
        {
            if (pawn == null || !DroneHelper.IsWorkerDrone(pawn))
                return null;

            WorkerDroneBackstorySettings settings = WorkerDroneSettingsDef.Backstory;
            if (settings == null)
                return null;

            HashSet<string> result = new HashSet<string>();

            // 1) Global worker drone categories
            if (settings.globalCategories != null)
            {
                for (int i = 0; i < settings.globalCategories.Count; i++)
                {
                    string cat = settings.globalCategories[i];
                    if (!string.IsNullOrEmpty(cat))
                        result.Add(cat);
                }
            }

            // 2) Per-pawnkind mapping
            if (pawn.kindDef != null && !string.IsNullOrEmpty(pawn.kindDef.defName))
            {
                WorkerDronePawnkindBackstorySettings pkSettings =
                    settings.GetPawnkindSettings(pawn.kindDef.defName);

                if (pkSettings != null && pkSettings.ExclusiveBackstoryCatagories != null)
                {
                    for (int i = 0; i < pkSettings.ExclusiveBackstoryCatagories.Count; i++)
                    {
                        string cat = pkSettings.ExclusiveBackstoryCatagories[i];
                        if (!string.IsNullOrEmpty(cat))
                            result.Add(cat);
                    }
                }
            }

            // 3) Faction-specific categories
            FactionDef facDef = (pawn.Faction != null) ? pawn.Faction.def : null;
            if (facDef != null && !string.IsNullOrEmpty(facDef.defName))
            {
                WorkerDroneFactionBackstorySettings facSettings =
                    settings.GetFactionSettings(facDef.defName);

                if (facSettings != null && facSettings.ExclusiveBackstoryCatagories != null)
                {
                    for (int i = 0; i < facSettings.ExclusiveBackstoryCatagories.Count; i++)
                    {
                        string cat = facSettings.ExclusiveBackstoryCatagories[i];
                        if (!string.IsNullOrEmpty(cat))
                            result.Add(cat);
                    }
                }
            }

            if (DebugLog)
            {
                string cats = (result.Count > 0) ? string.Join(", ", result) : "<none>";
                Log.Message(
                    "[MRC] Allowed worker-drone categories for " +
                    pawn +
                    " (faction=" + (facDef != null ? facDef.defName : "null") +
                    ", kind=" + (pawn.kindDef != null ? pawn.kindDef.defName : "null") +
                    "): " + cats
                );
            }

            return result;
        }
    }
}