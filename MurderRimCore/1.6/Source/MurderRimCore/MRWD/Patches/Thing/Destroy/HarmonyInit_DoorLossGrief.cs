using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.MRWD.Patches
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit_DoorLossGrief
    {
        private static readonly HashSet<int> recentProcessed = new HashSet<int>();
        private static int lastPurgeTick;

        static HarmonyInit_DoorLossGrief()
        {
            var h = new Harmony("murderrimcore.doors.lossgrief");
            // Patch Building_Door.Destroy(DestroyMode) via Thing.Destroy since Door doesn't override in all versions
            var destroy = AccessTools.Method(typeof(Thing), nameof(Thing.Destroy));
            h.Patch(destroy, prefix: new HarmonyMethod(typeof(HarmonyInit_DoorLossGrief), nameof(Pre_Destroy)));
        }

        // Pre-destroy so door is still intact and we can read its data.
        public static void Pre_Destroy(Thing __instance, DestroyMode mode)
        {
            if (!(__instance is Building_Door door)) return;
            if (door.Faction != Faction.OfPlayer) return;

            // Ignore trivial modes that don't represent violent loss (optional)
            // You can customize which modes trigger; here all except Deconstruct.
            if (mode == DestroyMode.Deconstruct) return;

            int id = door.thingIDNumber;
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastPurgeTick > 1000)
            {
                recentProcessed.Clear();
                lastPurgeTick = currentTick;
            }
            if (!recentProcessed.Add(id))
                return; // already processed

            NotifyDoorDestroyed(door);
        }

        private static void NotifyDoorDestroyed(Building_Door door)
        {
            var map = door.Map;
            if (map == null) return;

            // Strength factor (0..1) based on door MaxHitPoints relative to a baseline (300 HP ~ strong)
            float strengthFactor = UnityEngine.Mathf.Clamp01(door.MaxHitPoints / 300f);

            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.story?.traits == null) continue;
                if (!pawn.story.traits.HasTrait(MRWD.MRWD_DefOf.MRWD_DoorEnthusiast)) continue;

                // Create grief memory
                var thoughtDef = MRWD.MRWD_DefOf.MRWD_DoorLost;
                if (thoughtDef == null) continue;

                // Choose stage based on strengthFactor (optional 0 or 1)
                int stage = strengthFactor > 0.55f ? 1 : 0;

                var mem = ThoughtMaker.MakeThought(thoughtDef, stage);
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(mem);
            }
        }
    }
}
