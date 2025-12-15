using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound; // Added for sound support

namespace MRHP
{
    public class HediffComp_Pinned : HediffComp
    {
        public Pawn sentinel;
        public int attacksEndured = 0;
        private int safetyBufferTicks = 0;

        // Settings
        private float chancePerStruggle = 0.01f;
        private float chancePerSkill = 0.02f;

        // Snapshot of stats before pinning
        public float originalDodgeChance = 0f;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref sentinel, "sentinel");
            Scribe_Values.Look(ref attacksEndured, "attacksEndured", 0);
            Scribe_Values.Look(ref safetyBufferTicks, "safetyBufferTicks", 0);
            Scribe_Values.Look(ref chancePerStruggle, "chancePerStruggle", 0.01f);
            Scribe_Values.Look(ref chancePerSkill, "chancePerSkill", 0.02f);
            Scribe_Values.Look(ref originalDodgeChance, "originalDodgeChance", 0f);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (safetyBufferTicks > 0)
            {
                safetyBufferTicks--;
                return;
            }

            if (sentinel == null || sentinel.Dead || sentinel.Downed || !sentinel.Spawned)
            {
                parent.Severity = 0;
                return;
            }

            Pawn victim = parent.pawn;

            if (sentinel.Position.DistanceTo(victim.Position) > 1.5f)
            {
                parent.Severity = 0;
                return;
            }

            if (sentinel.CurJobDef != MRHP_DefOf.MRHP_SentinelMaul)
            {
                parent.Severity = 0;
                return;
            }
        }

        // --- API ---
        public void Notify_PounceStarted(Pawn p, float struggleChance, float skillChance, float snapshotDodge)
        {
            this.sentinel = p;
            this.safetyBufferTicks = 120;
            this.chancePerStruggle = struggleChance;
            this.chancePerSkill = skillChance;
            this.originalDodgeChance = snapshotDodge;
        }

        public void Notify_MaulingActive(Pawn p)
        {
            this.sentinel = p;
            this.safetyBufferTicks = 5;
        }

        public bool TryBreakout()
        {
            Pawn victim = parent.pawn;
            attacksEndured++;

            float breakChance = SentinelAIUtils.CalculateBreakoutChance(
                victim,
                sentinel,
                chancePerSkill,
                chancePerStruggle,
                attacksEndured,
                originalDodgeChance
            );

            if (Rand.Value < breakChance)
            {
                if (victim.Map != null)
                {
                    MoteMaker.ThrowText(victim.DrawPos, victim.Map, "KICKED OFF!", Color.white);

                    // ADDED: Play Kick Sound
                    SoundDefOf.Pawn_Melee_Punch_HitBuilding_Generic.PlayOneShot(victim);
                }

                parent.Severity = 0;
                return true;
            }
            return false;
        }

        // --- DISPLAY ---
        public override string CompLabelInBracketsExtra
        {
            get
            {
                Pawn victim = parent.pawn;
                if (victim == null || sentinel == null) return base.CompLabelInBracketsExtra;

                float chance = SentinelAIUtils.CalculateBreakoutChance(
                    victim,
                    sentinel,
                    chancePerSkill,
                    chancePerStruggle,
                    attacksEndured,
                    originalDodgeChance
                );

                return chance.ToStringPercent() + " Breakout";
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                string s = base.CompTipStringExtra;
                if (sentinel != null) s += $"\nPinned by: {sentinel.LabelShort} (Size: {sentinel.BodySize})";
                s += $"\nStruggle Intensity: {attacksEndured}";
                return s;
            }
        }
    }
}