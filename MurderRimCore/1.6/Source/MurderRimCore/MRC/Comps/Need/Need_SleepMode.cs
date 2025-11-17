using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MurderRimCore
{
    public class Need_SleepMode : Need_Rest
    {
        // Re-implement private fields from Need_Rest
        private int lastRestTick = -999;
        private float lastRestEffectiveness = 1f;
        private int ticksAtZero;

        public Need_SleepMode(Pawn pawn) : base(pawn)
        {
            this.threshPercents = new List<float> { 0.28f, 0.14f };
        }

        public override void SetInitialLevel()
        {
            this.CurLevel = Rand.Range(0.9f, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.ticksAtZero, "ticksAtZero", 0, false);
        }

        public override void NeedInterval()
        {
            if (!this.IsFrozen)
            {
                if (this.Resting)
                {
                    float num = this.lastRestEffectiveness;
                    num *= this.pawn.GetStatValue(StatDefOf.RestRateMultiplier, true, -1);
                    if (num > 0f)
                    {
                        this.CurLevel += 0.005714286f * num;
                    }
                }
                else
                {
                    float num2 = this.RestFallPerTick * 150f * this.pawn.GetStatValue(StatDefOf.RestFallRateFactor, true, -1);
                    this.CurLevel -= num2;
                }
            }

            // Only accumulate ticksAtZero if below 0.1
            if (this.CurLevel < 0.1f)
            {
                this.ticksAtZero += 150;
            }
            else
            {
                this.ticksAtZero = 0;
            }

            // Probabilistic collapse below 0.1, chance increases as CurLevel approaches 0
            if (this.CurLevel < 0.1f && CanInvoluntarilySleep(this.pawn))
            {
                // Linear chance: 0% at 0.1, 100% at 0
                float collapseChance = 1f - (this.CurLevel / 0.1f); // 0.0 at 0.1, 1.0 at 0
                if (Rand.Value < collapseChance)
                {
                    Building_Bed bed = this.pawn.CurrentBed();
                    LocalTargetInfo targetA;
                    if (bed != null)
                        targetA = bed;
                    else if (this.pawn.SpawnedParentOrMe != null)
                        targetA = this.pawn.SpawnedParentOrMe;
                    else
                        targetA = this.pawn.Position;
                    Job job = JobMaker.MakeJob(JobDefOf.LayDown, targetA);
                    job.startInvoluntarySleep = true;
                    this.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.SatisfyingNeeds, false, false, null, false, true, true);
                    if (this.pawn.InMentalState && this.pawn.MentalStateDef.recoverFromCollapsingExhausted)
                    {
                        this.pawn.mindState.mentalStateHandler.CurState.RecoverFromState();
                    }
                    LifeStageDef curLifeStage = this.pawn.ageTracker.CurLifeStage;
                    if (curLifeStage == null || curLifeStage.involuntarySleepIsNegativeEvent)
                    {
                        if (PawnUtility.ShouldSendNotificationAbout(this.pawn))
                        {
                            Messages.Message("MessageInvoluntarySleep".Translate(this.pawn.LabelShort, this.pawn), this.pawn, MessageTypeDefOf.NegativeEvent, true);
                        }
                        TaleRecorder.RecordTale(TaleDefOf.Exhausted, this.pawn);
                    }
                }
            }

            // Sync with vanilla rest if present
            var restNeed = this.pawn.needs?.TryGetNeed<Need_Rest>();
            if (restNeed != null && restNeed != this)
            {
                restNeed.CurLevel = this.CurLevel;
            }
        }

        public void TickResting(float restEffectiveness)
        {
            if (restEffectiveness <= 0f)
                return;
            this.lastRestTick = Find.TickManager.TicksGame;
            this.lastRestEffectiveness = restEffectiveness;
        }

        public bool Resting => Find.TickManager.TicksGame < this.lastRestTick + this.pawn.UpdateRateTicks;

        private static bool CanInvoluntarilySleep(Pawn pawn)
        {
            var jobs = pawn.jobs;
            bool flag = jobs?.curDriver?.asleep ?? false;
            if (flag) return false;
            if (!RestUtility.CanFallAsleep(pawn)) return false;
            if (!pawn.Spawned)
            {
                if (!pawn.ageTracker.CurLifeStage.canSleepWhileHeld) return false;
                if (!(pawn.SpawnedParentOrMe is Pawn)) return false;
                if (pawn.IsWorldPawn()) return false;
                if (pawn.IsCaravanMember()) return false;
            }
            return !pawn.ageTracker.CurLifeStage.canVoluntarilySleep || pawn.CurJobDef != JobDefOf.LayDown;
        }
    }
}

