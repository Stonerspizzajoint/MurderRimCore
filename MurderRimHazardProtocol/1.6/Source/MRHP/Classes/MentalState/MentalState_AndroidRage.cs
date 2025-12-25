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

            if (p.def == this.pawn.def) return false;
            if (p == target) return true;
            if (Utils.IsAndroid(p)) return true;
            if (target != null && p.Faction != null && p.Faction == target.Faction) return true;
            if (p.mindState != null && p.mindState.enemyTarget == this.pawn) return true;

            return false;
        }

        public override bool ForceHostileTo(Faction f)
        {
            if (pawn.Faction != null && pawn.Faction == f) return false;
            if (target != null && target.Faction == f) return true;
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