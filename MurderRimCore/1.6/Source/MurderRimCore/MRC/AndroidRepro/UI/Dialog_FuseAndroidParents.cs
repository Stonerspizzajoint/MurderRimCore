using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public class Dialog_FuseAndroidParents : Window
    {
        private readonly VREAndroids.Building_AndroidCreationStation _station;
        private readonly Pawn _creator;

        private Pawn _parentA;
        private Pawn _parentB;

        private Vector2 _scrollA;
        private Vector2 _scrollB;

        public override Vector2 InitialSize => new Vector2(760f, 520f);

        public Dialog_FuseAndroidParents(VREAndroids.Building_AndroidCreationStation station, Pawn creator)
            : this(station, creator, preselected: null)
        {
        }

        public Dialog_FuseAndroidParents(VREAndroids.Building_AndroidCreationStation station, Pawn creator, Pawn preselected)
        {
            _station = station ?? throw new ArgumentNullException(nameof(station));
            _creator = creator;
            forcePause = true;
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            TrySeedPreselected(preselected);
        }

        private void TrySeedPreselected(Pawn pre)
        {
            var s = AndroidReproductionSettingsDef.Current;
            if (pre == null || s == null) return;
            if (AndroidFusionUtility.IsEligibleParent(pre, s))
            {
                if (_parentA == null) _parentA = pre;
                else if (_parentB == null && pre != _parentA) _parentB = pre;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var s = AndroidReproductionSettingsDef.Current;
            if (s == null)
            {
                Widgets.Label(inRect, "SettingsDef missing.");
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Select parents for Android Fusion");
            Text.Font = GameFont.Small;

            float y = inRect.y + 38f;
            string status = null;
            if (!StationPowered(_station))
                status = "Station is unpowered.";
            else if (IsStationBusy(_station))
                status = "Station is busy.";
            else if (!s.enabled)
                status = "Reproduction disabled in settings.";

            if (!status.NullOrEmpty())
            {
                GUI.color = ColoredText.WarningColor;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), status);
                GUI.color = Color.white;
                y += 26f;
            }

            float colW = (inRect.width - 24f) / 2f;
            float top = y + 4f;
            float listH = inRect.height - top - 64f;
            Rect left = new Rect(inRect.x, top, colW, listH);
            Rect right = new Rect(inRect.x + colW + 24f, top, colW, listH);
            Rect bottom = new Rect(inRect.x, inRect.yMax - 48f, inRect.width, 40f);

            DrawParentPicker(left, ref _parentA, 'A', _parentB, ref _scrollA, s);
            DrawParentPicker(right, ref _parentB, 'B', _parentA, ref _scrollB, s);

            string reason = null;
            bool pairValid = AndroidFusionUtility.ValidateParents(_parentA, _parentB, s, out reason);
            bool powered = StationPowered(_station);
            bool busy = IsStationBusy(_station);
            bool ok = s.enabled && powered && !busy && pairValid;

            string label = ok
                ? "Start fusion"
                : (!s.enabled ? "[Disabled] Reproduction off"
                   : (!powered ? "Not Ready: Station unpowered"
                      : (busy ? "Not Ready: Station busy"
                         : "Not Ready: " + (string.IsNullOrEmpty(reason) ? "Invalid selection" : reason))));

            if (Widgets.ButtonText(bottom, label, active: ok) && ok)
            {
                FusionProcess proc = AndroidFusionRuntime.BeginFusion(_station, _parentA, _parentB);
                if (proc != null)
                {
                    AndroidFusionRuntime.AssignFusionJob(_parentA, _station, proc.ParentASlot);
                    AndroidFusionRuntime.AssignFusionJob(_parentB, _station, proc.ParentBSlot);
                    Close();
                }
            }
        }

        private void DrawParentPicker(Rect rect, ref Pawn current, char slotLabel, Pawn exclude, ref Vector2 scroll, AndroidReproductionSettingsDef s)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(6f);

            var headerRect = new Rect(inner.x, inner.y, inner.width, 24f);
            Widgets.Label(headerRect, $"Parent {slotLabel}: {(current != null ? current.LabelShortCap : "None")}");

            if (current != null)
            {
                var clearRect = new Rect(inner.xMax - 84f, inner.y, 78f, 24f);
                if (Widgets.ButtonText(clearRect, "Clear"))
                    current = null;
            }

            var candidates = CandidateParents(s).Where(p => p != exclude).ToList();

            float listTop = inner.y + 28f;
            Rect outRect = new Rect(inner.x, listTop, inner.width, inner.height - (listTop - inner.y));
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, candidates.Count * 30f + 8f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float curY = 4f;

            foreach (var p in candidates)
            {
                var row = new Rect(0f, curY, viewRect.width, 26f);

                string reason = null;
                Pawn first = (slotLabel == 'A') ? p : exclude;
                Pawn second = (slotLabel == 'A') ? exclude : p;
                bool pairValid = (first == null || second == null) || AndroidFusionUtility.ValidateParents(first, second, s, out reason);

                if (Widgets.ButtonText(row, GetPawnLabel(p), active: pairValid))
                {
                    current = p;
                }

                if (!pairValid)
                {
                    TooltipHandler.TipRegion(row, reason ?? "Not eligible with the other selection.");
                }

                curY += 30f;
            }

            Widgets.EndScrollView();

            if (current != null)
            {
                Listing_Standard listing = new Listing_Standard();
                var detailRect = new Rect(inner.x, outRect.yMax + 4f, inner.width, 60f);
                listing.Begin(detailRect);
                listing.Label($"Awakened: {Utils.IsAwakened(current)}");
                listing.Label($"Faction: {(current.Faction != null ? current.Faction.Name : "None")}");
                listing.Label($"Type: {(DroneHelper.IsWorkerDrone(current) ? "Worker Drone" : "Android")}");
                if (current.Drafted) listing.Label("NOTE: Drafted (fusion will cancel if drafted during fusion).");
                if (current.InMentalState) listing.Label("WARNING: Mental state");
                listing.End();
            }
        }

        private IEnumerable<Pawn> CandidateParents(AndroidReproductionSettingsDef s)
        {
            if (s == null) yield break;
            Map map = _station.Map;
            if (map == null) yield break;

            foreach (Pawn p in map.mapPawns.FreeColonists)
            {
                if (p == null || p.Dead) continue;
                if (!AndroidFusionUtility.IsEligibleParent(p, s)) continue;
                if (s.requireAwakenedBoth && !Utils.IsAwakened(p)) continue;
                yield return p;
            }
        }

        private static string GetPawnLabel(Pawn p)
        {
            if (p == null) return "null";
            string kind = p.KindLabel ?? "?";
            return $"{p.LabelShortCap} ({kind})";
        }

        private static bool StationPowered(VREAndroids.Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            var comp = station.compPower;
            return comp != null && comp.PowerOn;
        }

        private static bool IsStationBusy(VREAndroids.Building_AndroidCreationStation station)
        {
            FusionProcess proc;
            if (!AndroidFusionRuntime.TryGetProcess(station, out proc)) return false;
            if (proc == null) return false;

            return proc.Stage == FusionStage.Fusion
                || proc.Stage == FusionStage.Gestation
                || proc.Stage == FusionStage.Assembly;
        }
    }
}