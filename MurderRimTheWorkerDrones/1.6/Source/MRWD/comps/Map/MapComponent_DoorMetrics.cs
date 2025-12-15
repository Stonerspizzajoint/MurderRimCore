using Verse;
using RimWorld;

namespace MRWD
{
    // Tracks door metrics (player doors only) to feed the need's target.
    public class MapComponent_DoorMetrics : MapComponent
    {
        private const int UpdateIntervalTicks = 600;
        private int lastUpdateTick = -9999;

        public int playerDoorCount;
        public float perColonistScore;

        public MapComponent_DoorMetrics(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now - lastUpdateTick >= UpdateIntervalTicks)
            {
                Recalculate();
                lastUpdateTick = now;
            }
        }

        private void Recalculate()
        {
            playerDoorCount = 0;
            float weighted = 0f;

            var doors = map.listerThings.ThingsInGroup(ThingRequestGroup.Door);
            if (doors != null)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    if (doors[i] is Building_Door door && door.Faction == Faction.OfPlayer)
                    {
                        playerDoorCount++;
                        // Weight by max HP (scaled) to reward sturdier/advanced doors
                        float hpWeight = UnityEngine.Mathf.Max(1f, door.MaxHitPoints / 100f);
                        weighted += hpWeight;
                    }
                }
            }

            int colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0;
            if (colonists <= 0) colonists = 1;

            perColonistScore = weighted / colonists;
        }

        public float GetPerColonistScore() => perColonistScore;
    }
}