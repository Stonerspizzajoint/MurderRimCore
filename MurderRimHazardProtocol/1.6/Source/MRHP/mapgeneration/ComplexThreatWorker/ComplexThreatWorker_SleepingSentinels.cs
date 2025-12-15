using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace MRHP
{
    public class ComplexThreatWorker_SleepingSentinels : ComplexThreatWorker
    {
        // SIGNATURE 1: The one the compiler asked for (3 args)
        protected override void ResolveInt(ComplexResolveParams parms, ref float threatPointsUsed, List<Thing> outSpawnedThings)
        {
            ResolveLogic(parms, ref threatPointsUsed, outSpawnedThings);
        }

        // SIGNATURE 2: The one the decompiled code uses (4 args)
        // We add 'virtual' or 'override' depending on if the base class has it. 
        // Since we don't know if it's abstract or virtual in base, we'll use 'public override' if possible, 
        // but to avoid errors, we can just leave it as an overload if it's not abstract.
        // IF the compiler complained about missing 4-arg override, uncomment 'override'.
        public void Resolve(ComplexResolveParams parms, ref float threatPointsUsed, List<Thing> outSpawnedThings, StringBuilder sb)
        {
            ResolveLogic(parms, ref threatPointsUsed, outSpawnedThings);
        }

        // Shared Logic
        private void ResolveLogic(ComplexResolveParams parms, ref float threatPointsUsed, List<Thing> outSpawnedThings)
        {
            float points = parms.points;
            if (points <= 0) points = 300f;

            List<Pawn> sentinels = GenerateSentinels(points);
            if (!sentinels.Any()) return;

            SpawnPawns(sentinels, parms, outSpawnedThings);

            foreach (var p in sentinels)
            {
                threatPointsUsed += p.kindDef.combatPower;
            }
        }

        private List<Pawn> GenerateSentinels(float points)
        {
            List<Pawn> list = new List<Pawn>();
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("MRHP_Sentinel");
            float cost = kind.combatPower;
            int count = Mathf.Max(1, (int)(points / cost));

            for (int i = 0; i < count; i++)
            {
                Pawn p = PawnGenerator.GeneratePawn(kind, null);
                list.Add(p);
            }
            return list;
        }

        private void SpawnPawns(List<Pawn> pawns, ComplexResolveParams parms, List<Thing> outSpawnedThings)
        {
            if (parms.room == null || parms.room.rects.NullOrEmpty()) return;

            CellRect rect = parms.room.rects.RandomElement();
            Map map = parms.map;

            foreach (Pawn p in pawns)
            {
                IntVec3 loc;
                if (CellFinder.TryFindRandomCellInsideWith(rect, c => c.Standable(map), out loc))
                {
                    GenSpawn.Spawn(p, loc, map);
                    if (outSpawnedThings != null) outSpawnedThings.Add(p);

                    p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, null, true);
                    p.mindState.lastDisturbanceTick = -99999;
                    p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, loc), JobCondition.InterruptForced);
                }
            }
        }
    }
}