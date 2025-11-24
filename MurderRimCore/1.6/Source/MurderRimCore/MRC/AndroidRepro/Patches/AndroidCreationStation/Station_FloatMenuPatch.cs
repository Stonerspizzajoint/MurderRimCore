using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    // Lightweight float menu that matches the new "footprint-only" material logic.
    // - Uses AndroidFusionRuntime.CountInFootprint/CountInFootprintFlexible for status display.
    // - Does not scan the whole map every frame (prevents FPS drops while menu is open).
    // - Starts the Assemble job; the JobDriver handles self-collect and footprint drops.
    [HarmonyPatch(typeof(VREAndroids.Building_AndroidCreationStation))]
    [HarmonyPatch(nameof(VREAndroids.Building_AndroidCreationStation.GetFloatMenuOptions))]
    public static class Station_FloatMenuPatch
    {
        public static void Postfix(VREAndroids.Building_AndroidCreationStation __instance,
                                   Pawn selPawn,
                                   ref IEnumerable<FloatMenuOption> __result)
        {
            var s = AndroidReproductionSettingsDef.Current;
            List<FloatMenuOption> list = (__result != null) ? __result.ToList() : new List<FloatMenuOption>();

            if (__instance == null || selPawn == null || selPawn.Map != __instance.Map)
            {
                __result = list;
                return;
            }

            // Current process and busy flag
            FusionProcess proc;
            bool hasProc = AndroidFusionRuntime.TryGetProcess(__instance, out proc) && proc != null;
            bool stationBusy = hasProc && (proc.Stage == FusionStage.Fusion ||
                                           proc.Stage == FusionStage.Gestation ||
                                           proc.Stage == FusionStage.Assembly);

            // If disabled in settings, do not show any fusion-related options or dialog
            if (s == null || !s.enabled)
            {
                __result = list;
                return;
            }

            // Actions (kept lightweight)
            if (!stationBusy)
            {
                // Quick start with romantic partners (no heavy scans)
                foreach (var partner in GetRomanticPartners(selPawn, s, __instance.Map))
                {
                    Pawn cap = partner;
                    list.Add(new FloatMenuOption("Begin fusion with " + cap.LabelShortCap, () =>
                    {
                        string reason;
                        if (!AndroidFusionUtility.ValidateParents(selPawn, cap, s, out reason))
                        {
                            Messages.Message("Cannot start fusion: " + reason, MessageTypeDefOf.RejectInput);
                            return;
                        }

                        FusionProcess newProc = AndroidFusionRuntime.BeginFusion(__instance, selPawn, cap);
                        if (newProc == null) return;

                        AndroidFusionRuntime.AssignFusionJob(selPawn, __instance, newProc.ParentASlot);
                        AndroidFusionRuntime.AssignFusionJob(cap, __instance, newProc.ParentBSlot);
                    }));
                }

                // Manual parent selection
                list.Add(new FloatMenuOption("Select parents (fusion dialog)…", () =>
                {
                    Find.WindowStack.Add(new Dialog_FuseAndroidParents(__instance, selPawn));
                }));
            }

            else
            {
                if (proc.Stage == FusionStage.Gestation)
                {
                    bool queued = AndroidFusionRuntime.IsAbortQueued(__instance);
                    if (!queued)
                        list.Add(new FloatMenuOption("Abort gestation (queue abort)", () =>
                        {
                            AndroidFusionRuntime.QueueAbortOpenJob(__instance);
                            Messages.Message("Abort queued – a colonist will abort gestation.", MessageTypeDefOf.NeutralEvent);
                        }));
                    else
                        list.Add(new FloatMenuOption("Abort gestation (queued)", null));

                    if (DebugSettings.godMode && StationPowered(__instance))
                        list.Add(new FloatMenuOption("[DEV] Instant complete gestation", () =>
                        {
                            AndroidFusionRuntime.ForceCompleteGestation(__instance);
                        }));
                }
                else if (proc.Stage == FusionStage.Assembly)
                {
                    bool canCraft = selPawn.workSettings != null &&
                                    selPawn.workSettings.WorkIsActive(WorkTypeDefOf.Crafting);
                    bool canReach = selPawn.CanReach(__instance.InteractionCell, PathEndMode.OnCell, Danger.Some);
                    bool canReserve = selPawn.CanReserve(__instance);

                    if (canCraft && canReach && canReserve)
                    {
                        // Start assembly; JobDriver gathers materials and drops them inside the 3x3 footprint
                        list.Add(new FloatMenuOption("Assemble android body", () =>
                        {
                            Job job = JobMaker.MakeJob(MRC_AndroidRepro_DefOf.MRC_AssembleAndroidBody, __instance);
                            selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }));
                    }
                    else
                    {
                        list.Add(new FloatMenuOption("Assemble android body (unavailable)", null));
                    }

                    // DEV: Instant assemble if the footprint already contains everything
                    if (DebugSettings.godMode)
                    {
                        bool haveAllPlaced =
                            AndroidFusionRuntime.CountInFootprint(__instance, ThingDefOf.Plasteel) >= AndroidFusionRuntime.PlasteelReq &&
                            AndroidFusionRuntime.CountInFootprintFlexible(__instance, AndroidFusionRuntime.UraniumDefNames) >= AndroidFusionRuntime.UraniumReq &&
                            AndroidFusionRuntime.CountInFootprintFlexible(__instance, AndroidFusionRuntime.AdvancedComponentDefNames) >= AndroidFusionRuntime.AdvCompReq;

                        list.Add(new FloatMenuOption(
                            haveAllPlaced ? "[DEV] Instant assemble body" : "[DEV] Instant assemble body (need materials inside station)",
                            () =>
                            {
                                if (!haveAllPlaced)
                                {
                                    Messages.Message("Place required materials inside the station footprint first.", MessageTypeDefOf.RejectInput);
                                    return;
                                }
                                if (!AndroidFusionRuntime.TryConsumeAssemblyMaterials(__instance))
                                {
                                    Messages.Message("Failed consuming materials.", MessageTypeDefOf.RejectInput);
                                    return;
                                }
                                if (AndroidFusionRuntime.TryGetProcess(__instance, out var pProc))
                                    AndroidFusionRuntime.SpawnNewbornFromAssembly(pProc);
                            }));
                    }
                }
            }

            __result = list;
        }

        private static IEnumerable<Pawn> GetRomanticPartners(Pawn a, AndroidReproductionSettingsDef s, Map map)
        {
            if (a?.relations?.DirectRelations == null) yield break;
            foreach (var rel in a.relations.DirectRelations)
            {
                if (rel?.otherPawn == null) continue;
                if (rel.def != PawnRelationDefOf.Lover &&
                    rel.def != PawnRelationDefOf.Spouse &&
                    rel.def != PawnRelationDefOf.Fiance) continue;

                Pawn b = rel.otherPawn;
                if (b.Map != map) continue;
                if (s.requireAwakenedBoth && !(VREAndroids.Utils.IsAwakened(a) && VREAndroids.Utils.IsAwakened(b))) continue;
                if (!AndroidFusionUtility.IsEligibleParent(a, s) || !AndroidFusionUtility.IsEligibleParent(b, s)) continue;
                if (!s.allowCrossFaction && a.Faction != b.Faction) continue;
                yield return b;
            }
        }

        private static bool StationPowered(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            var comp = station.compPower;
            return comp != null && comp.PowerOn;
        }
    }
}