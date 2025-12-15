using RimWorld;
using Verse;

namespace MRHP
{
    public class Verb_SentinelJump : Verb_CastAbilityJump
    {
        public override ThingDef JumpFlyerDef => verbProps.spawnDef ?? base.JumpFlyerDef;

        protected override bool TryCastShot()
        {
            if (this.ability != null && this.ability.CooldownTicksRemaining > 0) return false;

            Pawn caster = CasterPawn;
            Map map = caster.Map;
            IntVec3 destCell = CurrentTarget.Cell;

            // FIX 1: "HasLocation" does not exist. We just check if the cell is valid.
            if (map == null || !destCell.IsValid) return false;

            // 1. Determine Victim
            Pawn victim = null;

            // If we clicked a Pawn directly
            if (CurrentTarget.HasThing && CurrentTarget.Thing is Pawn p)
            {
                victim = p;
            }
            // If we clicked the ground, check if there is a pawn standing there
            else
            {
                victim = destCell.GetFirstPawn(map);
            }

            // 2. Create Flyer
            // destCell here tells the flyer where it is GOING
            PawnFlyer flyer = PawnFlyer.MakeFlyer(JumpFlyerDef, caster, destCell, this.verbProps.flightEffecterDef, this.verbProps.soundLanding);

            if (flyer != null)
            {
                // FIX 2: Spawn the flyer at the CASTER'S position (Start point), not the destination.
                // Previously, spawning at destCell made it finish instantly/invisibly.
                if (!flyer.Spawned)
                {
                    GenSpawn.Spawn(flyer, caster.Position, map, WipeMode.Vanish);
                }

                // 3. Setup Sentinel Logic
                if (flyer is SentinelPounceFlyer pounceFlyer && victim != null)
                {
                    pounceFlyer.SetTarget(victim);
                }

                // 4. Cooldown
                if (this.ability != null)
                {
                    this.ability.StartCooldown(this.ability.def.cooldownTicksRange.RandomInRange);
                }

                return true;
            }

            return false;
        }
    }
}