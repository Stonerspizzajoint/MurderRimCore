using RimWorld;
using Verse;
using System.Linq;

namespace MRHP
{
    public class SentinelPounceFlyer : PawnFlyer
    {
        private Pawn intendedVictim;

        public void SetTarget(Pawn victim)
        {
            this.intendedVictim = victim;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref intendedVictim, "intendedVictim");
        }

        protected override void RespawnPawn()
        {
            Pawn p = this.FlyingPawn;
            Map map = this.Map;

            base.RespawnPawn();

            if (p == null || !p.Spawned || map == null) return;

            Pawn victim = intendedVictim;
            bool fallbackTriggered = false;
            string failReason = "";

            // Check validity of stored target
            if (victim == null)
            {
                fallbackTriggered = true;
                failReason = "Target is null.";
            }
            else if (victim.Dead)
            {
                fallbackTriggered = true;
                failReason = $"Target {victim.LabelShort} died during flight.";
            }
            else if (!victim.Spawned)
            {
                fallbackTriggered = true;
                failReason = $"Target {victim.LabelShort} despawned.";
            }
            else if (victim.Map != map)
            {
                fallbackTriggered = true;
                failReason = $"Target {victim.LabelShort} is on a different map.";
            }

            // --- FALLBACK LOGIC ---
            if (fallbackTriggered)
            {
                // FIXED: Removed 'x.RaceProps.Humanlike' check.
                // Now targets ANY nearby pawn (Animal, Mech, or Human)
                victim = map.mapPawns.AllPawnsSpawned
                   .Where(x => x != p && !x.Dead && x.Position.DistanceTo(p.Position) < 2.9f)
                   .OrderBy(x => x.Position.DistanceTo(p.Position))
                   .FirstOrDefault();

                if (victim != null)
                {
                    Log.Message($"[MurderRimCore] Pounce Fallback: Found new target {victim.LabelShort}");
                }
            }

            // Execute Combat
            if (victim != null)
            {
                Ability ability = p.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("MRHP_SentinelPounce"));
                CompAbility_SentinelSettings settings = ability?.CompOfType<CompAbility_SentinelSettings>();

                SentinelAIUtils.ResolvePounceCombat(p, victim, settings);
            }
        }
    }
}