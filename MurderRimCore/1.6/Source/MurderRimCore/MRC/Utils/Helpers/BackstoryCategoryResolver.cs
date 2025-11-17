using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore
{
    public static class BackstoryCategoryResolver
    {
        public static bool DebugLog = false;

        public static string ResolveCategory(Pawn pawn)
        {
            if (pawn == null) return null;

            bool isDrone = DroneHelper.IsWorkerDrone(pawn);
            bool isAndroid = IsAndroidSafe(pawn);
            bool awakened = IsAwakenedSafe(pawn);

            // Worker drones:
            if (isDrone)
            {
                var cat = awakened ? "WorkerDrone" : "ColonyDrone";
                if (DebugLog) Log.Message($"[MRC] Category -> {cat} (Drone={isDrone}, Awakened={awakened}) for {pawn}");
                return cat;
            }

            // Non-drone androids:
            if (isAndroid)
            {
                var cat = awakened ? "AwakenedAndroid" : "ColonyAndroid";
                if (DebugLog) Log.Message($"[MRC] Category -> {cat} (Android, Awakened={awakened}) for {pawn}");
                return cat;
            }

            // Not android/drone: let vanilla flow
            if (DebugLog) Log.Message($"[MRC] Category -> <none> (not android/drone) for {pawn}");
            return null;
        }

        private static bool IsAwakenedSafe(Pawn pawn)
        {
            try
            {
                // Prefer VREAndroids.Utils.IsAwakened if available
                return Utils.IsAwakened(pawn);
            }
            catch
            {
                // Fallback to extension (if present in your environment)
                try { return pawn.IsAwakened(); } catch { return false; }
            }
        }

        private static bool IsAndroidSafe(Pawn pawn)
        {
            try { return pawn.IsAndroid(); } catch { return false; }
        }

        // True only if the pawn's faction explicitly lists the category in its backstory filters.
        public static bool FactionAllowsCategory(Pawn pawn, string category)
        {
            if (pawn == null || category == null) return false;
            var facDef = pawn.Faction?.def;
            if (facDef == null || facDef.backstoryFilters == null) return false;

            var filters = facDef.backstoryFilters;
            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i];
                if (f?.categories == null) continue;
                if (f.categories.Contains(category)) return true;
            }
            return false;
        }
    }
}
