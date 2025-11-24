using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MurderRimCore.AndroidRepro
{
    public static class AndroidFusionRuntime
    {
        private static readonly Dictionary<VREAndroids.Building_AndroidCreationStation, FusionProcess> processes =
            new Dictionary<VREAndroids.Building_AndroidCreationStation, FusionProcess>();

        private static readonly HashSet<VREAndroids.Building_AndroidCreationStation> abortQueued =
            new HashSet<VREAndroids.Building_AndroidCreationStation>();

        // Requirements
        public const int PlasteelReq = 62;
        public const int UraniumReq = 15;
        public const int AdvCompReq = 3;

        // Flexible def name arrays (add modded names here if needed)
        public static readonly string[] AdvancedComponentDefNames = { "ComponentSpacer", "AdvancedComponent" };
        public static readonly string[] UraniumDefNames = { "Uranium", "UraniumOre" };

        // Optional debug
        public static bool VerboseAssemblyLog = false;
        private static void LogAsm(string msg)
        {
            if (VerboseAssemblyLog) Log.Message("[FusionAssembly] " + msg);
        }

        public static bool TryGetProcess(VREAndroids.Building_AndroidCreationStation station, out FusionProcess proc)
            => processes.TryGetValue(station, out proc);

        public static FusionProcess BeginFusion(VREAndroids.Building_AndroidCreationStation station, Pawn a, Pawn b)
        {
            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled) return null;
            if (!StationPowered(station))
            {
                Messages.Message("Android Creation Station needs power to begin fusion.", MessageTypeDefOf.RejectInput);
                return null;
            }

            if (processes.TryGetValue(station, out FusionProcess existing))
            {
                if (existing.Stage == FusionStage.Fusion ||
                    existing.Stage == FusionStage.Gestation ||
                    existing.Stage == FusionStage.Assembly)
                {
                    Messages.Message("Station already busy.", MessageTypeDefOf.RejectInput);
                    return null;
                }
            }

            if (!FusionSlotUtility.TryFindFusionSlots(station, out IntVec3 left, out IntVec3 right))
            {
                Messages.Message("Cannot find valid fusion slots. Clear area around station.", MessageTypeDefOf.RejectInput);
                return null;
            }

            var proc = new FusionProcess();
            proc.Start(station, a, b, s, left, right);
            processes[station] = proc;
            ClearAbortQueued(station);
            return proc;
        }

        public static void TickStation(VREAndroids.Building_AndroidCreationStation station)
        {
            if (!processes.TryGetValue(station, out FusionProcess proc)) return;
            if (proc.Stage == FusionStage.Idle || proc.Stage == FusionStage.Aborted || proc.Stage == FusionStage.Complete) return;

            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled)
            {
                proc.Abort("Settings disabled.");
                ClearAbortQueued(station);
                return;
            }

            if (proc.ParentA == null || proc.ParentB == null ||
                proc.ParentA.Dead || proc.ParentB.Dead ||
                proc.ParentA.Map != station.Map || proc.ParentB.Map != station.Map)
            {
                proc.Abort("Parent missing/dead.");
                ClearAbortQueued(station);
                return;
            }

            if (proc.Stage == FusionStage.Fusion && (proc.ParentA.Drafted || proc.ParentB.Drafted))
            {
                proc.Abort("Drafted parent.");
                Messages.Message("Fusion cancelled: a parent was drafted.", MessageTypeDefOf.NegativeEvent);
                ClearAbortQueued(station);
                return;
            }

            if (proc.Stage == FusionStage.Fusion && !StationPowered(station)) return;

            if (proc.Stage == FusionStage.Fusion && !proc.ParentsInSlots && Find.TickManager.TicksGame % 90 == 0)
                ReensureJobs(proc);

            if (proc.Stage == FusionStage.Gestation)
            {
                var comp = station.compPower;
                if (comp != null && comp.PowerOn)
                    comp.PowerOutput = -s.stationGestationPowerUse;
                else if (comp != null && !comp.PowerOn)
                    return;

                proc.GestationTicks += 1f;
                if (proc.GestationTicks >= proc.GestationRequired)
                    BeginAssembly(proc);
            }
        }

        private static void ReensureJobs(FusionProcess proc)
        {
            if (proc.Stage != FusionStage.Fusion) return;

            void Check(Pawn p, IntVec3 slot)
            {
                if (p == null || !p.Spawned) return;
                if (p.Position != slot || p.CurJob == null || p.CurJob.def != JobDefOf_Fusion.MRC_FuseAtCreationStation)
                    AssignFusionJob(p, proc.Station, slot);
            }

            Check(proc.ParentA, proc.ParentASlot);
            Check(proc.ParentB, proc.ParentBSlot);
        }

        public static void NotifyFusionWork(VREAndroids.Building_AndroidCreationStation station, Pawn worker, float deltaWork)
        {
            if (!processes.TryGetValue(station, out FusionProcess proc)) return;
            if (proc.Stage != FusionStage.Fusion) return;

            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled) return;
            if (!StationPowered(station)) return;
            if (!proc.ParentsInSlots) return;
            if (s.fusionRequiresBothAwakened &&
                !(VREAndroids.Utils.IsAwakened(proc.ParentA) && VREAndroids.Utils.IsAwakened(proc.ParentB)))
                return;

            // Hearts during active fusion, same style as lovin.
            TrySpawnFusionHearts(proc);

            proc.FusionProgress += deltaWork;
            if (proc.FusionProgress >= proc.FusionRequired)
            {
                GrantLovinThoughts(proc);
                proc.FusedProject = AndroidFusionUtility.BuildFusedProject(proc.ParentA, proc.ParentB, s);
                proc.Stage = FusionStage.Gestation;
            }
        }

        private const int FusionHeartIntervalTicks = 100;
        private const float FusionHeartScale = 0.42f; // same as JobDriver_Lovin

        private static void TrySpawnFusionHearts(FusionProcess proc)
        {
            var map = proc?.Station?.Map;
            if (map == null) return;

            Pawn a = proc.ParentA;
            Pawn b = proc.ParentB;
            if (a == null || b == null) return;
            if (!a.Spawned || !b.Spawned) return;

            // Use the same interval logic as Lovin: once every 100 ticks per pawn.
            int ticks = Find.TickManager.TicksGame;
            if (ticks % FusionHeartIntervalTicks != 0)
                return;

            ThrowHeartMetaIcon(a);
            ThrowHeartMetaIcon(b);
        }

        private static void ThrowHeartMetaIcon(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;

            // Slight random horizontal offset so hearts don't perfectly stack.
            IntVec3 cell = pawn.Position;
            Map map = pawn.Map;

            // This uses RimWorld's built-in meta icon logic (same as lovin).
            FleckMaker.ThrowMetaIcon(cell, map, FleckDefOf.Heart, FusionHeartScale);
        }

        private static void GrantLovinThoughts(FusionProcess proc)
        {
            if (proc == null) return;
            Pawn a = proc.ParentA;
            Pawn b = proc.ParentB;
            if (a?.needs?.mood?.thoughts?.memories != null)
                try { a.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, b); } catch { }
            if (b?.needs?.mood?.thoughts?.memories != null)
                try { b.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, a); } catch { }
        }

        private static void BeginAssembly(FusionProcess proc)
        {
            proc.Stage = FusionStage.Assembly;
            Messages.Message("Gestation complete: assemble body (62 Plasteel, 15 Uranium, 3 Advanced Components).",
                MessageTypeDefOf.NeutralEvent);
            LogAsm("Assembly started.");
        }

        public static void SpawnNewbornFromAssembly(FusionProcess proc)
        {
            var map = proc.Station.Map;
            if (map == null) { proc.Abort("Map missing."); return; }
            IntVec3 c = proc.Station.Position;
            Pawn newborn = AndroidFusionSpawnHelper.SpawnNewborn(proc, AndroidReproductionSettingsDef.Current, map, c);
            if (newborn != null)
            {
                Messages.Message("Fused android newborn created: " + newborn.LabelShortCap, newborn, MessageTypeDefOf.PositiveEvent);
                proc.Stage = FusionStage.Complete;
                LogAsm("Assembly complete -> newborn spawned.");
            }
            else
            {
                proc.Abort("Spawn failed.");
                LogAsm("Assembly failed: spawn failure.");
            }
        }

        // ---------------- Footprint helpers (ONLY inside building cells) ----------------

        public static IEnumerable<IntVec3> FootprintCells(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station?.Map == null) yield break;
            CellRect rect = GenAdj.OccupiedRect(station.Position, station.Rotation, station.def.size);
            foreach (var c in rect.Cells)
                if (c.InBounds(station.Map))
                    yield return c;
        }

        public static int CountInFootprint(VREAndroids.Building_AndroidCreationStation station, ThingDef def)
        {
            if (station?.Map == null) return 0;
            int total = 0;
            foreach (var c in FootprintCells(station))
            {
                var list = c.GetThingList(station.Map);
                for (int i = 0; i < list.Count; i++)
                    if (list[i].def == def) total += list[i].stackCount;
            }
            return total;
        }

        public static int CountInFootprintFlexible(VREAndroids.Building_AndroidCreationStation station, string[] defNames)
        {
            if (station?.Map == null) return 0;
            int total = 0;
            foreach (var c in FootprintCells(station))
            {
                var list = c.GetThingList(station.Map);
                for (int i = 0; i < list.Count; i++)
                    if (defNames.Contains(list[i].def.defName))
                        total += list[i].stackCount;
            }
            return total;
        }

        // Backward compat: callers expecting CountAtStation now count in footprint
        public static int CountAtStation(VREAndroids.Building_AndroidCreationStation station, ThingDef def)
            => CountInFootprint(station, def);

        public static bool TryConsumeAssemblyMaterials(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station?.Map == null) return false;

            int plasteelNeed = PlasteelReq;
            int uraniumNeed = UraniumReq;
            int advNeed = AdvCompReq;

            foreach (var c in FootprintCells(station))
            {
                var list = c.GetThingList(station.Map);

                // Plasteel
                ConsumeFromList(list, ThingDefOf.Plasteel, ref plasteelNeed);

                // Uranium (flexible names)
                if (uraniumNeed > 0)
                {
                    var us = list.Where(t => UraniumDefNames.Contains(t.def.defName)).ToList();
                    for (int i = 0; i < us.Count && uraniumNeed > 0; i++)
                        ConsumeFromThing(us[i], ref uraniumNeed);
                }

                // Advanced components (flexible names)
                if (advNeed > 0)
                {
                    var acs = list.Where(t => AdvancedComponentDefNames.Contains(t.def.defName)).ToList();
                    for (int i = 0; i < acs.Count && advNeed > 0; i++)
                        ConsumeFromThing(acs[i], ref advNeed);
                }

                if (plasteelNeed <= 0 && uraniumNeed <= 0 && advNeed <= 0)
                    break;
            }

            bool ok = plasteelNeed <= 0 && uraniumNeed <= 0 && advNeed <= 0;
            LogAsm($"Consume result: plasteelNeed={plasteelNeed}, uraniumNeed={uraniumNeed}, advNeed={advNeed}, success={ok}");
            return ok;
        }

        private static void ConsumeFromList(List<Thing> list, ThingDef def, ref int need)
        {
            if (need <= 0) return;
            for (int i = list.Count - 1; i >= 0 && need > 0; i--)
            {
                var t = list[i];
                if (t.def != def) continue;
                ConsumeFromThing(t, ref need);
            }
        }

        private static void ConsumeFromThing(Thing t, ref int need)
        {
            if (need <= 0 || t == null || t.stackCount <= 0) return;
            int take = need < t.stackCount ? need : t.stackCount;
            if (take == t.stackCount)
            {
                need -= t.stackCount;
                t.Destroy(DestroyMode.Vanish);
            }
            else
            {
                t.SplitOff(take).Destroy(DestroyMode.Vanish);
                need -= take;
            }
        }

        // --------------- Reachable counts (for WorkGiver/validation) ---------------

        public static int CountReachableUnforbidden(Pawn pawn, ThingDef def)
        {
            if (pawn?.Map == null) return 0;
            int total = 0;
            foreach (var thing in pawn.Map.listerThings.ThingsOfDef(def))
            {
                if (thing.stackCount <= 0) continue;
                if (thing.IsForbidden(pawn)) continue;
                if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Some)) continue;
                total += thing.stackCount;
            }
            return total;
        }

        public static IEnumerable<Thing> AllReachableOfAny(Pawn pawn, string[] defNames)
        {
            if (pawn?.Map == null) yield break;
            foreach (var t in pawn.Map.listerThings.AllThings)
            {
                if (t.stackCount <= 0) continue;
                if (t.IsForbidden(pawn)) continue;
                if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Some)) continue;
                if (defNames.Contains(t.def.defName))
                    yield return t;
            }
        }

        /// <summary>
        /// Returns true if, based on what is already inside the station footprint
        /// and what is reachable on the map, this pawn could complete assembly.
        /// Mirrors JobDriver_AssembleAndroidBody.InitRemainingRequirements + ValidStack.
        /// </summary>
        public static bool HasAllReachableAssemblyMaterials(Pawn pawn, VREAndroids.Building_AndroidCreationStation station)
        {
            if (pawn == null || station == null || pawn.Map == null) return false;
            var map = pawn.Map;

            // Start from what's already inside the station (same as InitRemainingRequirements)
            int remPlasteel = PlasteelReq - CountInFootprint(station, ThingDefOf.Plasteel);
            int remUranium = UraniumReq - CountInFootprintFlexible(station, UraniumDefNames);
            int remAdvComp = AdvCompReq - CountInFootprintFlexible(station, AdvancedComponentDefNames);

            if (remPlasteel < 0) remPlasteel = 0;
            if (remUranium < 0) remUranium = 0;
            if (remAdvComp < 0) remAdvComp = 0;

            // Already fully supplied
            if (remPlasteel == 0 && remUranium == 0 && remAdvComp == 0)
            {
                LogAsm($"HasAllReachable: already fully supplied in footprint.");
                return true;
            }

            bool ValidStack(Thing stack)
            {
                if (stack == null || stack.stackCount <= 0) return false;
                if (stack.IsForbidden(pawn)) return false;
                if (!pawn.CanReach(stack, PathEndMode.Touch, Danger.Some)) return false;
                return true;
            }

            // Check Plasteel
            if (remPlasteel > 0)
            {
                int avail = 0;
                foreach (var stack in map.listerThings.ThingsOfDef(ThingDefOf.Plasteel))
                {
                    if (!ValidStack(stack)) continue;
                    avail += stack.stackCount;
                    if (avail >= remPlasteel) break;
                }
                if (avail < remPlasteel)
                {
                    LogAsm($"HasAllReachable: insufficient Plasteel (need {remPlasteel}, found {avail}).");
                    return false;
                }
            }

            // Check Uranium family
            if (remUranium > 0)
            {
                int avail = 0;
                foreach (var stack in map.listerThings.AllThings)
                {
                    if (!UraniumDefNames.Contains(stack.def.defName)) continue;
                    if (!ValidStack(stack)) continue;
                    avail += stack.stackCount;
                    if (avail >= remUranium) break;
                }
                if (avail < remUranium)
                {
                    LogAsm($"HasAllReachable: insufficient Uranium (need {remUranium}, found {avail}).");
                    return false;
                }
            }

            // Check advanced components
            if (remAdvComp > 0)
            {
                int avail = 0;
                foreach (var stack in map.listerThings.AllThings)
                {
                    if (!AdvancedComponentDefNames.Contains(stack.def.defName)) continue;
                    if (!ValidStack(stack)) continue;
                    avail += stack.stackCount;
                    if (avail >= remAdvComp) break;
                }
                if (avail < remAdvComp)
                {
                    LogAsm($"HasAllReachable: insufficient AdvComp (need {remAdvComp}, found {avail}).");
                    return false;
                }
            }

            LogAsm($"HasAllReachable: all requirements satisfiable for pawn {pawn.LabelShort}.");
            return true;
        }

        // --------------- Station busy/abort helpers ---------------

        public static IEnumerable<VREAndroids.Building_AndroidCreationStation> StationsAwaitingAssembly(Map map)
        {
            foreach (var kv in processes)
            {
                var st = kv.Key;
                var p = kv.Value;
                if (st != null && st.Map == map && p != null && p.Stage == FusionStage.Assembly)
                    yield return st;
            }
        }

        // Allow queueing abort in Gestation OR Assembly
        public static void QueueAbortOpenJob(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return;
            if (!TryGetProcess(station, out var proc) || proc == null) return;
            if (proc.Stage != FusionStage.Gestation && proc.Stage != FusionStage.Assembly) return;
            abortQueued.Add(station);

            // NEW: immediately cancel any active assembly job so the abort job can reserve the station.
            TryCancelActiveAssemblyJob(station);
        }

        public static bool IsAbortQueued(VREAndroids.Building_AndroidCreationStation station)
            => station != null && abortQueued.Contains(station);

        public static void ClearAbortQueued(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return;
            abortQueued.Remove(station);
        }

        // Query queued stations for either stage
        public static IEnumerable<VREAndroids.Building_AndroidCreationStation> StationsWithAbortQueued(Map map)
        {
            foreach (var s in abortQueued)
            {
                if (s == null || s.Map != map) continue;
                if (!TryGetProcess(s, out var p) || p == null) continue;
                if (p.Stage == FusionStage.Gestation || p.Stage == FusionStage.Assembly)
                    yield return s;
            }
        }

        // --------------- Misc helpers ---------------

        public static void Abort(VREAndroids.Building_AndroidCreationStation station, string reason = null)
        {
            if (TryGetProcess(station, out var proc))
                proc.Abort(reason);
            ClearAbortQueued(station);
        }

        public static void AssignFusionJob(Pawn pawn, VREAndroids.Building_AndroidCreationStation station, IntVec3 slot)
        {
            if (pawn == null || station == null) return;
            if (!StationPowered(station))
            {
                Messages.Message("Android Creation Station is unpowered.", MessageTypeDefOf.RejectInput);
                return;
            }
            if (pawn.InMentalState)
            {
                Messages.Message(pawn.LabelShort + " is in a mental state.", MessageTypeDefOf.RejectInput);
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf_Fusion.MRC_FuseAtCreationStation, station, slot);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        public static void ForceCompleteGestation(VREAndroids.Building_AndroidCreationStation station)
        {
            if (!StationPowered(station))
            {
                Messages.Message("Android Creation Station needs power to perform fusion.", MessageTypeDefOf.RejectInput);
                return;
            }
            if (!TryGetProcess(station, out var proc))
            {
                Messages.Message("No active fusion/gestation found.", MessageTypeDefOf.RejectInput);
                return;
            }
            var s = AndroidReproductionSettingsDef.Current;
            if (s == null || !s.enabled)
            {
                Messages.Message("Reproduction settings disabled.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (proc.Stage == FusionStage.Fusion)
            {
                GrantLovinThoughts(proc);
                proc.FusedProject = AndroidFusionUtility.BuildFusedProject(proc.ParentA, proc.ParentB, s);
                proc.Stage = FusionStage.Gestation;
            }
            if (proc.Stage == FusionStage.Gestation)
            {
                proc.GestationTicks = proc.GestationRequired;
                BeginAssembly(proc);
                Messages.Message("Gestation completed (DEV). Body assembly required.", MessageTypeDefOf.NeutralEvent);
            }
        }

        private static bool StationPowered(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            var comp = station.compPower;
            return comp != null && comp.PowerOn;
        }

        // Cancel any current assembly job targeting this station to release its reservation.
        private static void TryCancelActiveAssemblyJob(VREAndroids.Building_AndroidCreationStation station)
        {
            var map = station?.Map;
            if (map == null) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var j = p?.jobs?.curJob;
                if (j == null) continue;
                if (j.def == MRC_AndroidRepro_DefOf.MRC_AssembleAndroidBody && j.targetA.Thing == station)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }
    }
}