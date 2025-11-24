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
        private readonly Building_AndroidCreationStation _station;
        private readonly Pawn _creator;

        private Pawn _parentA;
        private Pawn _parentB;

        private Vector2 _scroll;

        // Larger window to avoid clipping and allow bigger UI elements
        public override Vector2 InitialSize => new Vector2(780f, 620f);

        // Age threshold
        private const float MinBiologicalAgeYears = 18f;

        // Hediff that marks newborn fusions
        private static readonly HediffDef FusedNewbornMarkerDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("MRC_FusedNewbornMarkerHediff");

        public Dialog_FuseAndroidParents(Building_AndroidCreationStation station, Pawn creator)
            : this(station, creator, null)
        {
        }

        public Dialog_FuseAndroidParents(Building_AndroidCreationStation station, Pawn creator, Pawn preselected)
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
            if (!AndroidFusionUtility.IsEligibleParent(pre, s)) return;

            if (_parentA == null)
            {
                _parentA = pre;
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

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "Android Fusion – Parent Selection");
            Text.Font = GameFont.Small;

            float y = inRect.y + 38f;

            // Status line
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
                y += 24f;
            }

            float bottomStripHeight = 92f;
            Rect topRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - bottomStripHeight);
            Rect bottomRect = new Rect(inRect.x, inRect.yMax - bottomStripHeight + 4f, inRect.width, bottomStripHeight - 8f);

            float listWidth = topRect.width * 0.55f;
            Rect listRect = new Rect(topRect.x, topRect.y, listWidth - 8f, topRect.height);
            Rect pairRect = new Rect(topRect.x + listWidth + 8f, topRect.y, topRect.width - listWidth - 8f, topRect.height);

            DrawCandidateList(listRect, s);
            DrawPairSummary(pairRect, s);
            DrawBottomControls(bottomRect, s);
        }

        // ======================= LEFT: CANDIDATES =======================

        private void DrawCandidateList(Rect rect, AndroidReproductionSettingsDef s)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            const float headerHeight = 26f;
            Rect headerRect = new Rect(inner.x, inner.y, inner.width, headerHeight);
            DrawSelectionHeader(headerRect, s);

            Widgets.DrawLineHorizontal(inner.x, inner.y + headerHeight - 2f, inner.width);

            var candidates = BuildCandidateEntries(s).ToList();

            float listTop = inner.y + headerHeight + 4f;
            float listHeight = inner.height - headerHeight - 6f;
            Rect outRect = new Rect(inner.x, listTop, inner.width, listHeight);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, candidates.Count * 52f + 4f);

            Widgets.BeginScrollView(outRect, ref _scroll, viewRect);
            float curY = 4f;

            foreach (var entry in candidates)
            {
                Rect row = new Rect(0f, curY, viewRect.width, 48f); // taller row
                DrawCandidateRow(row, entry);
                curY += 52f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSelectionHeader(Rect rect, AndroidReproductionSettingsDef s)
        {
            Rect inner = rect.ContractedBy(2f);
            Text.Anchor = TextAnchor.MiddleLeft;

            string extra = s.requireLovePartners
                ? " (only lovers on this map can fuse)"
                : "";

            Widgets.Label(inner, "Select parents from this list" + extra + ":");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private class CandidateEntry
        {
            public Pawn Pawn;

            // All romantic partners (lover/spouse/fiancé), regardless of map/faction
            public List<Pawn> LoversAll = new List<Pawn>();

            // Lovers that are on the same map, same faction, and in colony faction
            public List<Pawn> LoversValid = new List<Pawn>();

            public bool HasValidLover;
            public bool CanSelect;
            public bool IsPink;
            public bool TooYoung;
            public bool HasNewbornMarker;
            public string DisabledReason;

            // 0 = valid lover, 1 = lover but invalid, 2 = no lover, 3 = age restricted
            public int SortGroup;
        }

        private IEnumerable<CandidateEntry> BuildCandidateEntries(AndroidReproductionSettingsDef s)
        {
            var map = _station.Map;
            if (map == null) yield break;

            // All android colonist pawns, excluding currently selected parents
            var all = map.mapPawns.FreeColonists
                .Where(p =>
                    p != null &&
                    !p.Dead &&
                    AndroidFusionUtility.IsEligibleParent(p, s) &&
                    p != _parentA &&
                    p != _parentB)
                .ToList();

            List<Pawn> GetAllLovers(Pawn pawn)
            {
                var result = new List<Pawn>();
                if (pawn.relations == null) return result;

                var rels = pawn.relations.DirectRelations;
                if (rels == null) return result;

                foreach (var dr in rels)
                {
                    if (dr.otherPawn == null) continue;
                    if (dr.def == PawnRelationDefOf.Lover ||
                        dr.def == PawnRelationDefOf.Spouse ||
                        dr.def == PawnRelationDefOf.Fiance)
                    {
                        result.Add(dr.otherPawn);
                    }
                }

                return result;
            }

            List<Pawn> FilterValidLovers(IEnumerable<Pawn> lovers, Pawn self, Map curMap)
            {
                var list = new List<Pawn>();
                foreach (var lover in lovers)
                {
                    if (lover == null) continue;
                    if (lover.Map != curMap) continue;
                    if (lover.Faction != self.Faction) continue;
                    if (lover.Faction != Faction.OfPlayer) continue;
                    list.Add(lover);
                }
                return list;
            }

            var tempEntries = new List<CandidateEntry>();

            foreach (var p in all)
            {
                var loversAll = GetAllLovers(p);
                var loversValid = FilterValidLovers(loversAll, p, map);

                bool hasValidLover = loversValid.Count > 0;
                bool hasAnyLover = loversAll.Count > 0;

                bool canSelect = true;
                bool tooYoung = p.ageTracker != null && p.ageTracker.AgeBiologicalYearsFloat < MinBiologicalAgeYears;
                bool hasMarker = FusedNewbornMarkerDef != null &&
                                     p.health?.hediffSet?.HasHediff(FusedNewbornMarkerDef) == true;

                string disabledReason = null;

                // Lover requirement vs partners
                if (!hasAnyLover && s.requireLovePartners)
                {
                    canSelect = false;
                    disabledReason = "No lover; love partners required by settings.";
                }

                // If there are lovers but none valid, we treat them as "lover but invalid"
                if (hasAnyLover && !hasValidLover && s.requireLovePartners)
                {
                    // Already handled above (no valid lover) but we keep this notion for sort group.
                    if (disabledReason.NullOrEmpty())
                        disabledReason = "Lover is not on this map or in the colony faction.";
                    canSelect = false;
                }

                // Age & newborn marker always override
                if (hasMarker)
                {
                    canSelect = false;
                    disabledReason = "Too young: fused newborn.";
                }
                else if (tooYoung)
                {
                    canSelect = false;
                    disabledReason = "Too young: under 18 biological years.";
                }

                bool isPink = hasValidLover; // highlight when they have at least one valid lover

                var entry = new CandidateEntry
                {
                    Pawn = p,
                    LoversAll = loversAll,
                    LoversValid = loversValid,
                    HasValidLover = hasValidLover,
                    CanSelect = canSelect,
                    IsPink = isPink,
                    TooYoung = tooYoung,
                    HasNewbornMarker = hasMarker,
                    DisabledReason = disabledReason
                };

                // Sort order:
                // 3: age restricted (bottom, regardless of lovers)
                // 0: hasValidLover (top)
                // 1: has lovers, but none valid
                // 2: no lovers
                if (entry.TooYoung || entry.HasNewbornMarker)
                {
                    entry.SortGroup = 3;
                }
                else if (entry.HasValidLover)
                {
                    entry.SortGroup = 0;
                }
                else if (entry.LoversAll.Count > 0)
                {
                    entry.SortGroup = 1;
                }
                else
                {
                    entry.SortGroup = 2;
                }

                tempEntries.Add(entry);
            }

            // Lovers at top, clustered; then invalid lovers; then no lovers; then age-restricted
            var sorted = tempEntries
                .OrderBy(e => e.SortGroup)
                .ThenBy(e =>
                {
                    // For grouping lovers next to each other, sort by first lover's name if any, else own name
                    Pawn key = null;
                    if (e.LoversValid.Count > 0)
                        key = e.LoversValid[0];
                    else if (e.LoversAll.Count > 0)
                        key = e.LoversAll[0];

                    return key != null ? key.LabelShortCap : e.Pawn.LabelShortCap;
                })
                .ThenBy(e => e.Pawn.LabelShortCap);

            foreach (var e in sorted)
                yield return e;
        }

        private void DrawCandidateRow(Rect row, CandidateEntry entry)
        {
            var pawn = entry.Pawn;

            Widgets.DrawBoxSolid(row, new Color(0f, 0f, 0f, 0.14f));

            // Pink edge highlight for any pawn with a valid lover
            if (entry.IsPink)
            {
                var pink = new Color(1f, 0.5f, 0.7f, 0.7f);
                Rect bar = new Rect(row.x, row.y, 4f, row.height);
                Widgets.DrawBoxSolid(bar, pink);
            }

            bool active = entry.CanSelect;

            if (Widgets.ButtonInvisible(row) && active)
            {
                if (_parentA == null) _parentA = pawn;
                else if (_parentB == null) _parentB = pawn;
                else _parentB = pawn;
            }

            float x = row.x + 8f;

            // Larger icon in list (36x36)
            Rect iconRect = new Rect(x, row.y + 6f, 36f, 36f);
            Widgets.ThingIcon(iconRect, pawn);
            x += 40f;

            string label = pawn.LabelShortCap;
            Rect labelRect = new Rect(x, row.y + 6f, row.width - (x - row.x) - 6f, 20f);
            Widgets.Label(labelRect, label.Truncate(labelRect.width));

            // Info line: either age-block reason or lovers summary
            string info;
            if (entry.HasNewbornMarker)
            {
                info = "Too young: fused newborn";
            }
            else if (entry.TooYoung)
            {
                info = "Too young (<18)";
            }
            else
            {
                // Prefer valid lovers if any; else show all lovers; else "No lover"
                List<Pawn> toShow = entry.LoversValid.Count > 0
                    ? entry.LoversValid
                    : entry.LoversAll;

                if (toShow.Count > 0)
                {
                    string names = string.Join(", ", toShow.Select(l => l.LabelShortCap));
                    info = "Lovers: " + names;
                }
                else
                {
                    info = "No lover";
                }
            }

            Rect infoRect = new Rect(x, row.y + 28f, row.width - (x - row.x) - 6f, 20f);

            if (entry.HasNewbornMarker || entry.TooYoung)
                GUI.color = new Color(1f, 0.5f, 0.5f); // red-ish for hard block
            else
                GUI.color = entry.HasValidLover ? new Color(1f, 0.6f, 0.8f) : Color.gray;

            Widgets.Label(infoRect, info.Truncate(infoRect.width));
            GUI.color = Color.white;

            // Grey overlay for non-selectable entries
            if (!active)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.4f);
                Widgets.DrawBoxSolid(row, GUI.color);
                GUI.color = Color.white;

                string tip = entry.DisabledReason;
                if (tip.NullOrEmpty())
                    tip = info;

                if (!tip.NullOrEmpty())
                    TooltipHandler.TipRegion(row, tip);
            }
            else
            {
                TooltipHandler.TipRegion(row, pawn.Name?.ToStringFull ?? pawn.LabelShortCap);
            }
        }

        // ======================= RIGHT: CURRENT PAIR =======================

        private void DrawPairSummary(Rect rect, AndroidReproductionSettingsDef s)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            Rect header = new Rect(inner.x, inner.y, inner.width, 20f);
            Widgets.Label(header, "Current Pair");

            float rowTop = header.yMax + 6f;
            const float cellHeight = 80f;
            Rect rowRect = new Rect(inner.x, rowTop, inner.width, cellHeight);
            Widgets.DrawBoxSolid(rowRect, new Color(0f, 0f, 0f, 0.18f));

            float half = rowRect.width / 2f;
            DrawParentCell(new Rect(rowRect.x, rowRect.y, half, cellHeight), ref _parentA, "A");
            DrawParentCell(new Rect(rowRect.x + half, rowRect.y, half, cellHeight), ref _parentB, "B");

            float typeRowTop = rowRect.yMax + 4f;
            Rect typeRow = new Rect(inner.x, typeRowTop, inner.width, 20f);
            float halfType = typeRow.width / 2f;

            Text.Anchor = TextAnchor.UpperCenter;
            string typeA = GetTypeLabel(_parentA);
            string typeB = GetTypeLabel(_parentB);
            Widgets.Label(new Rect(typeRow.x, typeRow.y, halfType, 20f), typeA);
            Widgets.Label(new Rect(typeRow.x + halfType, typeRow.y, halfType, 20f), typeB);
            Text.Anchor = TextAnchor.UpperLeft;

            float statusTop = typeRow.yMax + 4f;
            Rect statusRect = new Rect(inner.x, statusTop, inner.width, 24f);

            string reason;
            bool validPair = AndroidFusionUtility.ValidateParents(_parentA, _parentB, s, out reason);

            if (_parentA == null || _parentB == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(statusRect, "Select two parents from the list on the left.");
                GUI.color = Color.white;
            }
            else if (validPair)
            {
                GUI.color = new Color(0.7f, 1f, 0.7f);
                Widgets.Label(statusRect, "Pair is valid for fusion.");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 0.7f, 0.7f);
                string shortReason = reason ?? "Invalid combination.";
                if (shortReason.Length > 80)
                    shortReason = shortReason.Substring(0, 77) + "...";
                Widgets.Label(statusRect, "Not valid: " + shortReason);
                GUI.color = Color.white;
            }

            float hintTop = statusRect.yMax + 8f;
            float hintHeight = inner.yMax - hintTop - 4f;
            if (hintHeight > 0f)
            {
                Rect hintRect = new Rect(inner.x, hintTop, inner.width, hintHeight);
                Text.Font = GameFont.Tiny;

                if (s.requireLovePartners)
                {
                    Widgets.Label(hintRect,
                        "Hints:\n" +
                        "• Only lovers on the same map and in the colony faction can fuse.\n" +
                        "• Pawns with a valid lover are highlighted pink in the list.\n" +
                        "• Androids under 18 or fused newborns cannot be selected.\n" +
                        "• Click a portrait to clear that parent.\n" +
                        "• First list click sets Parent A, second sets Parent B; further clicks replace Parent B.");
                }
                else
                {
                    Widgets.Label(hintRect,
                        "Hints:\n" +
                        "• Pawns with lovers are highlighted pink in the list.\n" +
                        "• Androids under 18 or fused newborns cannot be selected.\n" +
                        "• Lover requirement is disabled; any eligible pair may fuse.\n" +
                        "• Click a portrait to clear that parent.\n" +
                        "• First list click sets Parent A, second sets Parent B; further clicks replace Parent B.");
                }

                Text.Font = GameFont.Small;
            }
        }

        private void DrawParentCell(Rect rect, ref Pawn pawn, string placeholder)
        {
            Rect inner = rect.ContractedBy(4f);
            Widgets.DrawBoxSolid(inner, new Color(0f, 0f, 0f, 0.08f));
            Widgets.DrawHighlightIfMouseover(inner);

            if (Widgets.ButtonInvisible(inner) && pawn != null)
            {
                pawn = null;
            }

            if (pawn != null)
            {
                float iconSize = 56f;
                Rect iconRect = new Rect(
                    inner.x + 6f,
                    inner.y + (inner.height - iconSize) / 2f,
                    iconSize,
                    iconSize);
                Widgets.ThingIcon(iconRect, pawn);

                float textX = iconRect.xMax + 6f;
                float textWidth = inner.xMax - textX - 2f;

                Text.Anchor = TextAnchor.UpperLeft;
                Rect nameRect = new Rect(textX, inner.y + 12f, textWidth, 20f);
                Widgets.Label(nameRect, pawn.LabelShortCap.Truncate(textWidth));
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inner, placeholder);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private string GetTypeLabel(Pawn pawn)
        {
            if (pawn == null) return "";
            string typeStr = DroneHelper.IsWorkerDrone(pawn) ? "Worker" : "Drone";
            string awakenStr = Utils.IsAwakened(pawn) ? "Awakened" : "Dormant";
            return $"{typeStr} • {awakenStr}";
        }

        // ======================= BOTTOM: CONTROLS =======================

        private void DrawBottomControls(Rect rect, AndroidReproductionSettingsDef s)
        {
            Rect inner = rect.ContractedBy(6f);

            Rect stepsRect = new Rect(inner.x, inner.y, inner.width * 0.62f, inner.height);
            Rect buttonRect = new Rect(inner.x + inner.width * 0.64f, inner.y + 14f, inner.width * 0.34f, 38f);

            Text.Font = GameFont.Tiny;
            Widgets.Label(stepsRect,
                "1. Click a pawn in the list to assign Parent A.\n" +
                "2. Click another pawn to assign Parent B.\n" +
                "3. Click a portrait to clear that parent.\n" +
                "4. Start fusion when the pair is valid and the station is ready.");
            Text.Font = GameFont.Small;

            string reason;
            bool pairValid = AndroidFusionUtility.ValidateParents(_parentA, _parentB, s, out reason);
            bool powered = StationPowered(_station);
            bool busy = IsStationBusy(_station);
            bool ok = s.enabled && powered && !busy && pairValid;

            string label = ok
                ? "Start fusion"
                : (!s.enabled ? "[Disabled] Reproduction disabled in settings"
                   : (!powered ? "Not Ready: Station unpowered"
                      : (busy ? "Not Ready: Station busy"
                         : "Not Ready: " + (string.IsNullOrEmpty(reason) ? "Select both parents" : reason))));

            if (Widgets.ButtonText(buttonRect, label, active: ok) && ok)
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

        // ======================= HELPERS =======================

        private static bool StationPowered(Building_AndroidCreationStation station)
        {
            if (station == null) return false;
            var comp = station.compPower;
            return comp != null && comp.PowerOn;
        }

        private static bool IsStationBusy(Building_AndroidCreationStation station)
        {
            if (!AndroidFusionRuntime.TryGetProcess(station, out var proc) || proc == null)
                return false;

            return proc.Stage == FusionStage.Fusion
                || proc.Stage == FusionStage.Gestation
                || proc.Stage == FusionStage.Assembly;
        }
    }
}