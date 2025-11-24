using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [HarmonyPatch(typeof(VREAndroids.Building_AndroidCreationStation), "GetGizmos")]
    public static class Station_GizmosPatch
    {
        public static void Postfix(VREAndroids.Building_AndroidCreationStation __instance, ref IEnumerable<Gizmo> __result)
        {
            var s = AndroidReproductionSettingsDef.Current;
            var list = (__result != null) ? __result.ToList() : new List<Gizmo>();

            bool settingsEnabled = s != null && s.enabled;
            bool powered = StationPowered(__instance);
            bool busy = IsStationBusy(__instance);

            // Start/Configure fusion
            var startCmd = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Misc/AndroidFusion", false),
                defaultLabel = "Android Fusion",
                defaultDesc = (!settingsEnabled) ? "Android reproduction, not yet fully implimented."
                             : (!powered) ? "Station is unpowered."
                             : (busy ? "In use already." : "Begin an android fusion by selecting two eligible parents.")
            };
            startCmd.action = () =>
            {
                if (!settingsEnabled || !powered || busy) return;
                Pawn selected = Find.Selector.SingleSelectedThing as Pawn;
                Pawn preselected = (selected != null && AndroidFusionUtility.IsEligibleParent(selected, s)) ? selected : null;
                Find.WindowStack.Add(new Dialog_FuseAndroidParents(__instance, selected, preselected));
            };
            if (!settingsEnabled || !powered || busy) startCmd.Disable(null);
            list.Add(startCmd);

            if (AndroidFusionRuntime.TryGetProcess(__instance, out FusionProcess proc) && proc != null)
            {
                // Abort + DEV actions while in Gestation
                if (proc.Stage == FusionStage.Gestation)
                {
                    bool queued = AndroidFusionRuntime.IsAbortQueued(__instance);
                    var abortCmd = new Command_Action
                    {
                        icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false),
                        defaultLabel = queued ? "Abort Gestation (Queued)" : "Abort Gestation",
                        defaultDesc = queued ? "A colonist will abort gestation soon."
                                             : "Queue an abort job to terminate the gestation."
                    };
                    abortCmd.action = () =>
                    {
                        if (queued)
                        {
                            Messages.Message("Abort already queued.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        void Confirm()
                        {
                            AndroidFusionRuntime.QueueAbortOpenJob(__instance);
                            Messages.Message("Abort queued. A colonist will handle it.", MessageTypeDefOf.NeutralEvent, false);
                        }
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "Abort gestation? A colonist will spend a short time to terminate the project.",
                            Confirm, destructive: true, title: "Confirm Abort"));
                    };
                    list.Add(abortCmd);

                    if (DebugSettings.godMode)
                    {
                        var devFinish = new Command_Action
                        {
                            icon = ContentFinder<Texture2D>.Get("UI/Commands/DevCopy", false),
                            defaultLabel = powered ? "DEV: Complete Gestation" : "DEV: Complete Gestation (Unpowered)",
                            defaultDesc = "Instantly finish gestation (moves to Assembly)."
                        };
                        devFinish.action = () =>
                        {
                            if (!powered)
                            {
                                Messages.Message("Station must be powered.", MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            AndroidFusionRuntime.ForceCompleteGestation(__instance);
                        };
                        list.Add(devFinish);
                    }
                }

                // Abort + DEV actions while in Assembly (queued, not instant)
                if (proc.Stage == FusionStage.Assembly)
                {
                    bool queuedAsm = AndroidFusionRuntime.IsAbortQueued(__instance);
                    var abortAsm = new Command_Action
                    {
                        icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false),
                        defaultLabel = queuedAsm ? "Abort Assembly (Queued)" : "Abort Assembly",
                        defaultDesc = queuedAsm
                            ? "A colonist will abort assembly soon."
                            : "Queue an abort job to terminate the assembly. Materials already placed remain in the station."
                    };
                    abortAsm.action = () =>
                    {
                        if (queuedAsm)
                        {
                            Messages.Message("Abort already queued.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        void Confirm()
                        {
                            AndroidFusionRuntime.QueueAbortOpenJob(__instance);
                            Messages.Message("Abort queued. A colonist will handle it.", MessageTypeDefOf.NeutralEvent, false);
                        }
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "Abort assembly? Materials already placed will remain in the station.",
                            Confirm, destructive: true, title: "Confirm Abort"));
                    };
                    list.Add(abortAsm);

                    if (DebugSettings.godMode)
                    {
                        int p = AndroidFusionRuntime.CountInFootprint(__instance, ThingDefOf.Plasteel);
                        int u = AndroidFusionRuntime.CountInFootprintFlexible(__instance, AndroidFusionRuntime.UraniumDefNames);
                        int a = AndroidFusionRuntime.CountInFootprintFlexible(__instance, AndroidFusionRuntime.AdvancedComponentDefNames);
                        bool haveAll = p >= AndroidFusionRuntime.PlasteelReq &&
                                       u >= AndroidFusionRuntime.UraniumReq &&
                                       a >= AndroidFusionRuntime.AdvCompReq;

                        var devAssemble = new Command_Action
                        {
                            icon = ContentFinder<Texture2D>.Get("UI/Commands/DevCopy", false),
                            defaultLabel = haveAll ? "DEV: Instant Assemble" : "DEV: Instant Assemble (Need Mats)",
                            defaultDesc = $"Consume materials inside footprint and spawn newborn.\n" +
                                          $"Inside: Plasteel {p}/{AndroidFusionRuntime.PlasteelReq}, Uranium {u}/{AndroidFusionRuntime.UraniumReq}, Adv.Comp. {a}/{AndroidFusionRuntime.AdvCompReq}"
                        };
                        devAssemble.action = () =>
                        {
                            if (!haveAll)
                            {
                                Messages.Message("Place required materials inside the station footprint first.", MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            if (!AndroidFusionRuntime.TryConsumeAssemblyMaterials(__instance))
                            {
                                Messages.Message("Failed consuming materials.", MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            if (AndroidFusionRuntime.TryGetProcess(__instance, out var pProc))
                                AndroidFusionRuntime.SpawnNewbornFromAssembly(pProc);
                        };
                        list.Add(devAssemble);
                    }
                }
            }

            __result = list;
        }

        private static bool StationPowered(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            var comp = station.compPower;
            return comp != null && comp.PowerOn;
        }

        private static bool IsStationBusy(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            if (AndroidFusionRuntime.TryGetProcess(station, out FusionProcess proc) && proc != null)
            {
                return proc.Stage == FusionStage.Fusion ||
                       proc.Stage == FusionStage.Gestation ||
                       proc.Stage == FusionStage.Assembly;
            }
            return false;
        }
    }
}