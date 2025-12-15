using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MRHP
{
    public class LayoutWorker_DebugSentinel : LayoutWorkerComplex
    {
        // FIX 1: Add the required constructor that passes the def to the base class
        public LayoutWorker_DebugSentinel(LayoutDef def) : base(def)
        {
        }

        public override void Spawn(LayoutStructureSketch layoutStructureSketch, Map map, IntVec3 pos, float? threatPoints, List<Thing> allSpawnedThings, bool roofs, bool canReuseSketch, Faction faction)
        {
            Log.Message("[MurderRim] LayoutWorker_DebugSentinel STARTED.");

            // 1. Call the base vanilla spawn (builds walls/floors)
            base.Spawn(layoutStructureSketch, map, pos, threatPoints, allSpawnedThings, roofs, canReuseSketch, faction);

            // 2. Manually check the room definitions
            int roomCount = 0;
            foreach (LayoutRoom room in layoutStructureSketch.structureLayout.Rooms)
            {
                roomCount++;

                // Check if the room has any definitions attached
                if (room.defs == null || room.defs.Count == 0)
                {
                    Log.Error($"[MurderRim] Room #{roomCount} has NO DEFINITIONS. It is a ghost room.");
                    continue;
                }

                // Check the primary definition
                LayoutRoomDef mainDef = room.defs[0];
                Log.Message($"[MurderRim] Room #{roomCount} is defined as: {mainDef.defName}");

                // FIX 2: Check the TYPE field, not a 'Worker' property
                if (mainDef.roomContentsWorkerType == null)
                {
                    Log.Error($"[MurderRim] CRITICAL: Room {mainDef.defName} has a NULL 'roomContentsWorkerType'! Check your XML spelling.");
                }
                else
                {
                    Log.Message($"[MurderRim] Room {mainDef.defName} is using Worker Type: {mainDef.roomContentsWorkerType.FullName}");

                    // Optional: Try to verify if our custom code is actually running
                    if (mainDef.roomContentsWorkerType == typeof(RoomContentsWorker_LabGeneral))
                    {
                        Log.Message("  -> CONFIRMED: This room is linked to your Custom C# Worker.");
                    }
                    else
                    {
                        Log.Warning($"  -> WARNING: This room is NOT using your custom worker. It is using {mainDef.roomContentsWorkerType.Name}");
                    }
                }
            }

            Log.Message($"[MurderRim] LayoutWorker FINISHED. Processed {roomCount} rooms.");
        }
    }
}