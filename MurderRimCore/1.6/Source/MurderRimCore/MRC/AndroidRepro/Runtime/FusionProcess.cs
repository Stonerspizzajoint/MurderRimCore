using Verse;
using RimWorld;
using System;

namespace MurderRimCore.AndroidRepro
{
    public enum FusionStage
    {
        Idle,
        Fusion,
        Gestation,
        Assembly,   // awaiting body construction with materials
        Complete,
        Aborted
    }

    public class FusionProcess
    {
        public VREAndroids.Building_AndroidCreationStation Station;
        public Pawn ParentA;
        public Pawn ParentB;

        public FusionStage Stage = FusionStage.Idle;

        public float FusionProgress;      // 0..FusionRequired
        public float GestationTicks;      // ticks progressed
        public float FusionRequired;
        public float GestationRequired;   // ticks

        public CustomXenotype FusedProject;

        // Exact standing slots
        public IntVec3 ParentASlot = IntVec3.Invalid;
        public IntVec3 ParentBSlot = IntVec3.Invalid;

        public void Start(VREAndroids.Building_AndroidCreationStation station, Pawn a, Pawn b, AndroidReproductionSettingsDef s, IntVec3 slotA, IntVec3 slotB)
        {
            Station = station;
            ParentA = a;
            ParentB = b;
            ParentASlot = slotA;
            ParentBSlot = slotB;

            Stage = FusionStage.Fusion;
            FusionProgress = 0f;
            FusionRequired = s.fusionWorkAmount;
            GestationTicks = 0f;
            GestationRequired = s.gestationDays * GenDate.TicksPerDay;
        }

        public float FusionPercent
        {
            get
            {
                if (FusionRequired <= 0f) return 0f;
                return Math.Min(1f, FusionProgress / FusionRequired);
            }
        }

        public float GestationPercent
        {
            get
            {
                if (GestationRequired <= 0f) return 0f;
                return Math.Min(1f, GestationTicks / GestationRequired);
            }
        }

        public bool ParentsInSlots
        {
            get
            {
                if (ParentA == null || ParentB == null || Station == null || Station.Map == null) return false;
                if (ParentA.Map != Station.Map || ParentB.Map != Station.Map) return false;
                return ParentA.Position == ParentASlot && ParentB.Position == ParentBSlot;
            }
        }

        public void Abort(string reason = null)
        {
            Stage = FusionStage.Aborted;
        }
    }
}