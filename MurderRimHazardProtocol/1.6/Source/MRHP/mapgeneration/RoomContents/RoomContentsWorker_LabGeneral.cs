using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using System.Reflection; // Required for accessing private door variables

namespace MRHP
{
    public class RoomContentsWorker_LabGeneral : RoomContents_DeadBody
    {
        // =====================================================
        // 1. SETTINGS
        // =====================================================
        protected override ThingDef KillerThing => null;
        protected override Tool ToolUsed => null;
        protected override DamageDef DamageType => DamageDefOf.Cut;
        protected override FloatRange CorpseAgeDaysRange => new FloatRange(30f, 100f);
        protected override IntRange CorpseRange => new IntRange(1, 4);
        protected override IntRange SurvivalPacksCountRange => IntRange.Zero;
        protected override bool AllHaveSameDeathAge => false;

        protected override IEnumerable<PawnKindDef> GetPossibleKinds()
        {
            yield return MRHP_DefOf.MRHP_JCJenson_Human;
            yield return MRHP_DefOf.MRHP_JCJenson_WorkerDrone;
        }

        // =====================================================
        // 2. MAIN ENTRY POINT
        // =====================================================
        public override void FillRoom(Map map, LayoutRoom room, Faction faction = null, float? threatPoints = null)
        {
            base.FillRoom(map, room, faction, threatPoints);
            SpawnLoot(map, room, threatPoints);
        }

        // =====================================================
        // 3. CORPSE SPAWNING
        // =====================================================
        protected override void SpawnCorpses(Map map, LayoutRoom room)
        {
            int count = this.CorpseRange.RandomInRange;
            ThingDef oilFilthDef = DefDatabase<ThingDef>.GetNamed("MRC_FilthOil", false);

            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = FindBestSpawnCell(room, map);
                if (!cell.IsValid) continue;

                // 1. Setup Pawn
                PawnKindDef kind = this.GetPossibleKinds().RandomElement();
                int deadTicks = Mathf.RoundToInt(this.CorpseAgeDaysRange.RandomInRange * 60000f);

                PawnGenerationRequest request = new PawnGenerationRequest(
                    kind, null, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true,
                    forcedXenotype: null, allowDowned: true, fixedBiologicalAge: 25f,
                    forceNoGear: false, colonistRelationChanceFactor: 0f, relationWithExtraPawnChanceFactor: 0f
                );
                request.BiocodeApparelChance = 0f;
                request.MustBeCapableOfViolence = false;

                Pawn pawn = null;
                try { pawn = PawnGenerator.GeneratePawn(request); }
                catch { continue; }

                if (pawn == null) continue;
                if (pawn.Faction == null && Faction.OfAncients != null) pawn.SetFaction(Faction.OfAncients);

                // 2. Spawn & Kill
                GenSpawn.Spawn(pawn, cell, map);

                // NEW: Check if we spawned in a doorway
                JamDoorOpen(cell, map);

                DamageInfo dinfo = new DamageInfo(this.DamageType, 50f, 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
                pawn.Kill(dinfo, null);

                // 3. Rot & Filth
                if (pawn.Corpse != null)
                {
                    CompRottable comp = pawn.Corpse.GetComp<CompRottable>();
                    if (comp != null) comp.RotProgress = deadTicks;

                    if (this.BloodFilthRange.max > 0)
                    {
                        ThingDef filthToSpawn = pawn.RaceProps.BloodDef ?? ThingDefOf.Filth_Blood;

                        if (oilFilthDef != null && pawn.genes != null && pawn.genes.Xenotype != null &&
                            pawn.genes.Xenotype.defName == "MRWD_WorkerDroneBase")
                        {
                            filthToSpawn = oilFilthDef;
                        }

                        if (filthToSpawn != null)
                        {
                            int filthCount = this.BloodFilthRange.RandomInRange;
                            for (int j = 0; j < filthCount; j++) FilthMaker.TryMakeFilth(cell, map, filthToSpawn, 1);
                        }
                    }
                }
                else if (!pawn.Destroyed) pawn.Destroy();
            }
        }

        // =====================================================
        // 4. LOOT SPAWNING
        // =====================================================
        private void SpawnLoot(Map map, LayoutRoom room, float? threatPoints)
        {
            float budgetMultiplier = 0f;

            if (this.RoomDef.defName == "MRHP_Room_LootVault") budgetMultiplier = 3.0f;
            else if (this.RoomDef.defName == "MRHP_Room_Storage") budgetMultiplier = 0.8f;
            else if (this.RoomDef.defName == "MRHP_Room_ServerArchive" || this.RoomDef.defName == "MRHP_Room_AdminOffice") budgetMultiplier = 0.4f;

            if (budgetMultiplier <= 0f) return;

            float points = threatPoints ?? 400f;
            float totalMarketValue = points * budgetMultiplier;
            if (totalMarketValue < 100f) totalMarketValue = 150f;

            ThingSetMakerParams parms = new ThingSetMakerParams();
            parms.totalMarketValueRange = new FloatRange(totalMarketValue * 0.8f, totalMarketValue * 1.2f);
            parms.qualityGenerator = QualityGenerator.Reward;

            List<Thing> items = ThingSetMakerDefOf.Reward_ItemsStandard.root.Generate(parms);

            foreach (Thing t in items)
            {
                IntVec3 cell = FindBestSpawnCell(room, map);
                if (cell.IsValid)
                {
                    GenSpawn.Spawn(t, cell, map);
                    // NEW: Check if we spawned in a doorway
                    JamDoorOpen(cell, map);
                }
                else t.Destroy();
            }
        }

        // =====================================================
        // 5. HELPER METHODS
        // =====================================================

        // NEW: Forces a door open if an item spawns on it
        private void JamDoorOpen(IntVec3 cell, Map map)
        {
            Building_Door door = cell.GetDoor(map);
            if (door != null)
            {
                // 1. Force it to open physically
                door.StartManualOpenBy(null);
            }
        }

        private IntVec3 FindBestSpawnCell(LayoutRoom room, Map map)
        {
            List<IntVec3> allCells = new List<IntVec3>();
            foreach (var rect in room.rects)
            {
                foreach (var c in rect)
                {
                    if (room.Contains(c)) allCells.Add(c);
                }
            }
            allCells.Shuffle();

            // 1. Empty Floor
            foreach (var c in allCells) if (c.Standable(map)) return c;

            // 2. Walkable (Includes Open Doors & Debris)
            foreach (var c in allCells) if (c.Walkable(map)) return c;

            // 3. Desperate (Top of furniture, excluding walls/closed doors)
            foreach (var c in allCells)
            {
                Building b = c.GetFirstBuilding(map);
                if (b == null || (b.def.defName != "Wall" && !b.def.IsDoor)) return c;
            }

            return IntVec3.Invalid;
        }
    }
}