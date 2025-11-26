using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public enum AndroidReproState
    {
        Idle,
        Setup,
        Gestating,
        WaitingForAssembly
    }

    public class CompAndroidReproduction : ThingComp
    {
        public Pawn ParentA;
        public Pawn ParentB;
        private Pawn _cachedParentA;
        private Pawn _cachedParentB;

        private CompPowerTrader _powerComp;

        public AndroidReproState State = AndroidReproState.Idle;
        public float GestationProgress = 0f;
        private List<Pawn> _uploadFinishedPawns = new List<Pawn>();

        // Flag: If true, we are waiting for an executioner to wipe the system.
        public bool AbortRequested = false;

        public bool Gestating => State == AndroidReproState.Gestating;
        public bool ReadyForAssembly => State == AndroidReproState.WaitingForAssembly && !AbortRequested;

        private List<GeneDef> _cachedGenes = new List<GeneDef>();
        private XenotypeDef _cachedXenotype;
        private Color _cachedSkinColor = Color.white;
        private Color _cachedHairColor = Color.white;

        // REMOVED IdlePower constant. We will use the XML default.
        private const float ActivePower = -1200f;
        private const float GestationDays = 1f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            _powerComp = parent.GetComp<CompPowerTrader>();
            // Force update immediately on spawn/load to ensure correct power
            UpdatePowerConsumption();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var settings = AndroidReproductionSettingsDef.Current;
            if (settings == null || !settings.enabled) yield break;

            // DEBUG
            if (DebugSettings.godMode && State == AndroidReproState.Gestating)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Finish Gestation",
                    defaultDesc = "Instantly completes compilation.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Finish", true),
                    action = () => CompleteGestationPhase()
                };
            }

            // ABORT CONTROLS
            if ((State == AndroidReproState.Gestating || State == AndroidReproState.WaitingForAssembly) && !AbortRequested)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Abort Compilation",
                    defaultDesc = "Permanently purge the neural buffer. This cannot be undone.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
                    action = () =>
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "WARNING: This will permanently delete the offspring data. Resources on the ground will be preserved.",
                            () => OrderAbortJob(),
                            destructive: true
                        ));
                    }
                };
                yield break;
            }

            // SETUP
            if (State == AndroidReproState.Idle)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Android Fusion",
                    defaultDesc = "Select two compatible androids.",
                    icon = settings.Icon ?? ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = () => Find.WindowStack.Add(new Window_AndroidFusionSelection(this))
                };
            }
            else if (State == AndroidReproState.Setup)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Abort Fusion",
                    defaultDesc = "Sever the current neural alignment.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
                    action = () => AbortSetupProcess()
                };
            }
        }

        private void OrderAbortJob()
        {
            AbortRequested = true;
            Pawn worker = null;
            var pawns = parent.Map.mapPawns.FreeColonistsSpawned;

            foreach (Pawn p in pawns)
            {
                if (p.CurJobDef != null && p.CurJobDef.defName == "MRC_AssembleAndroid" && p.CurJob.targetA.Thing == parent)
                {
                    worker = p;
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    break;
                }
            }

            Pawn executioner = null;
            if (worker != null && !worker.Dead && !worker.Downed && worker.workSettings.WorkIsActive(WorkTypeDefOf.Childcare))
            {
                executioner = worker;
            }
            else
            {
                executioner = pawns
                    .Where(p => !p.Downed && !p.Dead && p.workSettings.WorkIsActive(WorkTypeDefOf.Childcare))
                    .OrderBy(p => p.Position.DistanceToSquared(parent.Position))
                    .FirstOrDefault();
            }

            if (executioner == null)
            {
                Messages.Message("Purge requested. Waiting for available colonist...", parent, MessageTypeDefOf.CautionInput);
                return;
            }

            JobDef abortJob = DefDatabase<JobDef>.GetNamed("MRC_AndroidAbortGestation");
            Job job = JobMaker.MakeJob(abortJob, parent);
            executioner.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Messages.Message($"{executioner.LabelShort} is moving to purge the system.", parent, MessageTypeDefOf.NeutralEvent);
        }

        public void ForceAbortGestation()
        {
            ResetSystem();
            Messages.Message("Neural compilation purged. System reset to Idle.", parent, MessageTypeDefOf.NegativeEvent);
        }

        public void ConfirmSelection(Pawn a, Pawn b)
        {
            ParentA = a;
            ParentB = b;
            State = AndroidReproState.Setup;
            _uploadFinishedPawns.Clear();

            JobDef uploadJob = DefDatabase<JobDef>.GetNamed("MRC_AndroidNeuralUpload", false);
            if (uploadJob == null) return;

            IntVec3 center = parent.InteractionCell;
            Rot4 rot = parent.Rotation;
            IntVec3 left = center + new IntVec3(-1, 0, 0).RotatedBy(rot);
            IntVec3 right = center + new IntVec3(1, 0, 0).RotatedBy(rot);
            if (!left.Walkable(parent.Map)) left = center;
            if (!right.Walkable(parent.Map)) right = center;

            Job jobA = JobMaker.MakeJob(uploadJob, left, b, parent);
            Job jobB = JobMaker.MakeJob(uploadJob, right, a, parent);

            a.jobs.TryTakeOrderedJob(jobA, JobTag.Misc);
            b.jobs.TryTakeOrderedJob(jobB, JobTag.Misc);

            Messages.Message($"Fusion sequence initialized.", parent, MessageTypeDefOf.PositiveEvent);
        }

        public void Notify_UploadComplete(Pawn p)
        {
            if (!_uploadFinishedPawns.Contains(p)) _uploadFinishedPawns.Add(p);
            if (_uploadFinishedPawns.Count >= 2) StartGestation();
        }

        private void StartGestation()
        {
            GenerateOffspringBlueprint();
            _cachedParentA = ParentA;
            _cachedParentB = ParentB;

            State = AndroidReproState.Gestating;
            GestationProgress = 0f;
            ParentA = null;
            ParentB = null;
            _uploadFinishedPawns.Clear();

            UpdatePowerConsumption();
            Messages.Message("Upload Complete. Neural compilation started.", parent, MessageTypeDefOf.PositiveEvent);
        }

        private void GenerateOffspringBlueprint()
        {
            _cachedGenes.Clear();
            if (ParentA == null || ParentB == null) return;

            int cpxA = GetSystemComplexity(ParentA);
            int cpxB = GetSystemComplexity(ParentB);
            Pawn dominant = (cpxA <= cpxB) ? ParentA : ParentB;
            Pawn recessive = (dominant == ParentA) ? ParentB : ParentA;

            _cachedXenotype = dominant.genes?.Xenotype ?? XenotypeDefOf.Baseliner;
            _cachedSkinColor = Rand.Value > 0.5f ? ParentA.story.SkinColor : ParentB.story.SkinColor;
            _cachedHairColor = Rand.Value > 0.5f ? ParentA.story.HairColor : ParentB.story.HairColor;

            if (dominant.genes != null)
                foreach (Gene g in dominant.genes.GenesListForReading) _cachedGenes.Add(g.def);

            if (recessive.genes != null)
            {
                int attempts = 2;
                foreach (Gene g in recessive.genes.GenesListForReading.InRandomOrder())
                {
                    if (attempts <= 0) break;
                    if (!_cachedGenes.Contains(g.def) && !IsGeneConflicting(g.def, _cachedGenes))
                    {
                        _cachedGenes.Add(g.def);
                        attempts--;
                    }
                }
            }
        }

        public static int GetSystemComplexity(Pawn p)
        {
            if (p?.genes == null) return 0;
            return p.genes.GenesListForReading.Sum(g => g.def.biostatCpx);
        }

        private bool IsGeneConflicting(GeneDef newGene, List<GeneDef> currentGenes)
        {
            if (newGene?.exclusionTags == null) return false;
            foreach (var existing in currentGenes)
            {
                if (existing.exclusionTags == null) continue;
                if (newGene.exclusionTags.Intersect(existing.exclusionTags).Any()) return true;
            }
            return false;
        }

        public override void CompTick()
        {
            base.CompTick();

            // Enforce power output if gestating
            if (State == AndroidReproState.Gestating && _powerComp != null)
            {
                // If we are gestating, ensure power is HIGH.
                // This catches cases where it might have been reset by something else.
                if (_powerComp.PowerOutput != ActivePower)
                {
                    _powerComp.PowerOutput = ActivePower;
                }

                if (_powerComp.PowerOn)
                {
                    GestationProgress += 1f / (60000f * GestationDays);
                    if (GestationProgress >= 1.0f) CompleteGestationPhase();
                }
            }
        }

        private void CompleteGestationPhase()
        {
            State = AndroidReproState.WaitingForAssembly;
            GestationProgress = 1f;
            UpdatePowerConsumption(); // Will revert to idle power
            Messages.Message("Neural Compilation Complete. Chassis assembly required.", parent, MessageTypeDefOf.PositiveEvent);
        }

        public void Notify_Working()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
                FleckMaker.ThrowMicroSparks(parent.DrawPos, parent.Map);
        }

        public void FinishAssembly(Pawn worker)
        {
            if (AbortRequested) return;

            CellRect rect = parent.OccupiedRect();
            Map map = parent.Map;
            ConsumeItemsInRect(rect, map, ThingDef.Named("Plasteel"), 50);
            ConsumeItemsInRect(rect, map, ThingDef.Named("Uranium"), 10);
            ConsumeItemsInRect(rect, map, ThingDef.Named("ComponentSpacer"), 2);

            Pawn baby = AndroidReproUtils.SpawnAndroidOffspring(
                parent, _cachedXenotype, _cachedGenes, _cachedSkinColor, _cachedHairColor, _cachedParentA, _cachedParentB
            );

            if (baby != null)
                Messages.Message("A new android has been assembled.", baby, MessageTypeDefOf.PositiveEvent);

            ResetSystem();
        }

        private void ConsumeItemsInRect(CellRect rect, Map map, ThingDef def, int count)
        {
            int remaining = count;
            foreach (IntVec3 c in rect)
            {
                if (remaining <= 0) break;
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing t = things[i];
                    if (t.def == def)
                    {
                        int take = Mathf.Min(t.stackCount, remaining);
                        t.SplitOff(take).Destroy();
                        remaining -= take;
                        if (remaining <= 0) break;
                    }
                }
            }
        }

        private void ResetSystem()
        {
            State = AndroidReproState.Idle;
            AbortRequested = false;

            GestationProgress = 0f;
            ParentA = null;
            ParentB = null;
            _cachedParentA = null;
            _cachedParentB = null;
            _cachedGenes.Clear();
            _cachedXenotype = null;
            _uploadFinishedPawns.Clear();
            UpdatePowerConsumption();
        }

        // UPDATED POWER LOGIC
        private void UpdatePowerConsumption()
        {
            if (_powerComp == null) return;

            if (State == AndroidReproState.Gestating)
            {
                _powerComp.PowerOutput = ActivePower;
            }
            else
            {
                // Revert to whatever the XML says the base consumption is
                _powerComp.PowerOutput = -_powerComp.Props.PowerConsumption;
            }
        }

        private void AbortSetupProcess()
        {
            if (ParentA != null) ParentA.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (ParentB != null) ParentB.jobs.EndCurrentJob(JobCondition.InterruptForced);
            ResetSystem();
            Messages.Message("Fusion aborted.", parent, MessageTypeDefOf.NeutralEvent, false);
        }

        public override string CompInspectStringExtra()
        {
            if (State == AndroidReproState.Gestating)
                return $"Neural Compilation: {GestationProgress.ToStringPercent()} ({(_powerComp != null && !_powerComp.PowerOn ? "No Power" : "Compiling")})";

            if (State == AndroidReproState.WaitingForAssembly)
            {
                if (AbortRequested) return "PURGE PENDING. AWAITING EXECUTIONER.";
                return "COMPILATION COMPLETE\nWaiting for Assembly (Requires: 50 Plasteel, 10 Uranium, 2 Adv. Comp)";
            }

            if (State == AndroidReproState.Setup && ParentA != null && ParentB != null)
                return $"Waiting for upload: {ParentA.LabelShort} + {ParentB.LabelShort}";
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref ParentA, "ParentA");
            Scribe_References.Look(ref ParentB, "ParentB");
            Scribe_References.Look(ref _cachedParentA, "CachedParentA");
            Scribe_References.Look(ref _cachedParentB, "CachedParentB");

            Scribe_Values.Look(ref AbortRequested, "AbortRequested", false);

            Scribe_Values.Look(ref State, "State", AndroidReproState.Idle);
            Scribe_Values.Look(ref GestationProgress, "GestationProgress");
            Scribe_Collections.Look(ref _uploadFinishedPawns, "UploadFinishedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref _cachedGenes, "CachedGenes", LookMode.Def);
            Scribe_Defs.Look(ref _cachedXenotype, "CachedXenotype");
            Scribe_Values.Look(ref _cachedSkinColor, "CachedSkinColor");
            Scribe_Values.Look(ref _cachedHairColor, "CachedHairColor");
        }
    }
}