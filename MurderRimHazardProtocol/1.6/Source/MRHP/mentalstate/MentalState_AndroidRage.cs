using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using VREAndroids;

namespace MRHP
{
    public class MentalState_AndroidRage : MentalState
    {
        public Pawn target;
        public int lastFailedExecutionTick = -99999;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref lastFailedExecutionTick, "lastFailedExecutionTick", -99999);
        }


        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override bool ForceHostileTo(Thing t)
        {
            Pawn p = t as Pawn;
            if (p == null) return false;

            // 1. Never attack fellow Sentinels (or same Def)
            if (p.def == this.pawn.def) return false;

            // 2. Always attack the locked target
            if (p == target) return true;

            // 3. Always attack ANY Android we see
            if (Utils.IsAndroid(p)) return true;

            // 4. Attack anyone in the same faction as our target
            if (target != null && p.Faction != null && p.Faction == target.Faction) return true;

            // 5. Self Defense
            if (p.mindState != null && p.mindState.enemyTarget == this.pawn) return true;

            return false;
        }

        public override bool ForceHostileTo(Faction f)
        {
            // 1. Safety: Never declare war on own faction
            if (pawn.Faction != null && pawn.Faction == f) return false;

            // 2. If our specific target belongs to this faction, we hate the whole faction
            if (target != null && target.Faction == f) return true;

            // 3. If they have ANY androids on the map, we hate them
            if (pawn.Map != null)
            {
                List<Pawn> factionPawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(f);
                for (int i = 0; i < factionPawns.Count; i++)
                {
                    if (Utils.IsAndroid(factionPawns[i])) return true;
                }
            }

            return false;
        }
    }
}