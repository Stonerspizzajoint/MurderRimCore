using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MRWD
{
    public class MapComponent_ObservationCache : MapComponent
    {
        private const int RebuildIntervalTicks = 60;
        private int lastRebuildTick = -1;

        private readonly List<ObservationWatcherEntry> watchers = new List<ObservationWatcherEntry>(64);
        private int globalMaxRange = 0;

        public MapComponent_ObservationCache(Map map) : base(map) { }

        public struct ObservationWatcherEntry
        {
            public Pawn pawn;
            public ObservationLearningExtension ext;
        }

        public IReadOnlyList<ObservationWatcherEntry> Watchers => watchers;
        public int GlobalMaxRange => globalMaxRange;

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastRebuildTick >= RebuildIntervalTicks)
            {
                RebuildCache();
                lastRebuildTick = now;
            }
        }

        public void InvalidateCache()
        {
            lastRebuildTick = -1;
        }

        private void RebuildCache()
        {
            watchers.Clear();
            globalMaxRange = 0;

            var pawns = map.mapPawns.AllPawnsSpawned;
            if (pawns == null) return;

            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null || !p.Spawned || p.Dead) continue;

                // Fast path: find first active gene with the extension
                var ext = GetObservationExt(p);
                if (ext == null) continue;

                watchers.Add(new ObservationWatcherEntry { pawn = p, ext = ext });

                int baseRange = ext.maxRange > 0 ? ext.maxRange : 13;
                int possibleMax = (int)(baseRange * 2f); // assume up to 200% sight
                if (possibleMax > globalMaxRange) globalMaxRange = possibleMax;
            }
        }

        private static ObservationLearningExtension GetObservationExt(Pawn p)
        {
            if (p.genes == null) return null;
            var genes = p.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                var g = genes[i];
                if (g?.Active != true) continue;
                var ext = g.def?.GetModExtension<ObservationLearningExtension>();
                if (ext != null) return ext;
            }
            return null;
        }
    }
}
