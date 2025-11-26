using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public class Window_AndroidFusionSelection : Window
    {
        private readonly CompAndroidReproduction _comp;
        private Pawn _selectedA;
        private Pawn _selectedB;
        private Vector2 _scrollPosition;
        private readonly AndroidReproductionSettingsDef _settings;

        // STATE
        private float _timeOffset;
        private bool _sortCpxAscending = true;

        // VISUALS
        private const float RowHeight = 52f;
        private static readonly Color RomanticPink = new Color(1f, 0.6f, 0.8f);
        private static readonly Color CardBgColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color SoftGrey = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color DarkBorder = new Color(0.3f, 0.3f, 0.3f, 1f);

        public Window_AndroidFusionSelection(CompAndroidReproduction comp)
        {
            _comp = comp;
            _settings = AndroidReproductionSettingsDef.Current;
            doCloseX = true;
            forcePause = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(1000f, 750f);

        public override void DoWindowContents(Rect inRect)
        {
            _timeOffset += Time.deltaTime;

            // CRT Effect
            GUI.color = new Color(1f, 1f, 1f, 0.03f);
            for (float i = 0; i < inRect.height; i += 4f) Widgets.DrawLineHorizontal(0, i, inRect.width);
            GUI.color = Color.white;

            // Header
            Rect headerRect = new Rect(0, 0, inRect.width, 40);
            Widgets.DrawBoxSolid(headerRect, HeaderColor);
            GUI.color = DarkBorder;
            Widgets.DrawBox(headerRect);

            GUI.color = SoftGrey;
            string title = _settings?.uiTitle ?? "NEURAL FUSION INTERFACE";
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Layout
            Rect mainRect = new Rect(0, 50, inRect.width, inRect.height - 100);
            Rect listRect = mainRect.LeftPart(0.62f).ContractedBy(4);
            Rect rightRect = mainRect.RightPart(0.36f).ContractedBy(4);

            Widgets.DrawMenuSection(listRect);
            Widgets.DrawMenuSection(rightRect);

            DrawPawnList(listRect.ContractedBy(10));
            DrawSelectedPanel(rightRect.ContractedBy(10));

            // Footer
            Rect bottomRect = new Rect(0, inRect.height - 40, inRect.width, 40);
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 120, 35), "Abort")) Close();

            if (_selectedA != null && _selectedB != null)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0.6f, 1f, 0.6f);
                if (Widgets.ButtonText(new Rect(bottomRect.xMax - 180, bottomRect.y, 180, 35), "INITIATE UPLOAD"))
                {
                    _comp.ConfirmSelection(_selectedA, _selectedB);
                    Close();
                }
                GUI.color = oldColor;
            }
        }

        private void DrawPawnList(Rect rect)
        {
            var rawList = _comp.parent.Map.mapPawns.FreeColonistsSpawned.Where(p => Utils.IsAndroid(p)).ToList();

            if (rawList.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(rect, "NO VIABLE PATTERNS DETECTED\n\n[Requires: Awakened, Adult, Alive]");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Sort Button
            Rect sortButtonRect = new Rect(rect.x, rect.y, 150, 24);
            string sortLabel = _sortCpxAscending ? "Sort: CPX ▲" : "Sort: CPX ▼";
            if (Widgets.ButtonText(sortButtonRect, sortLabel))
            {
                _sortCpxAscending = !_sortCpxAscending;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(sortButtonRect, "Toggle Complexity Sorting (Low/High)");

            Rect listBodyRect = new Rect(rect.x, rect.y + 30, rect.width, rect.height - 30);

            var query = rawList
                .OrderByDescending(p => LovePartnerRelationUtility.HasAnyLovePartner(p))
                .ThenBy(p => p.ageTracker.AgeBiologicalYearsFloat < (_settings?.minAge ?? 0));

            if (_sortCpxAscending) query = query.ThenBy(p => AndroidReproUtils.GetSystemComplexity(p));
            else query = query.ThenByDescending(p => AndroidReproUtils.GetSystemComplexity(p));

            var candidates = query.ToList();

            // Scroll View
            Rect viewRect = new Rect(0, 0, listBodyRect.width - 16, candidates.Count * (RowHeight + 4));
            Widgets.BeginScrollView(listBodyRect, ref _scrollPosition, viewRect);

            float y = 0f;
            foreach (Pawn p in candidates)
            {
                if (p == _selectedA || p == _selectedB) continue;

                bool valid = AndroidReproUtils.IsValidCandidate(p, out string failReason);

                if (valid)
                {
                    if (_selectedA != null && _selectedB == null) { if (!AndroidReproUtils.IsCompatiblePair(_selectedA, p, out string r)) { valid = false; failReason = r; } }
                    else if (_selectedB != null && _selectedA == null) { if (!AndroidReproUtils.IsCompatiblePair(_selectedB, p, out string r)) { valid = false; failReason = r; } }
                    else if (_selectedA != null && _selectedB != null) { valid = false; failReason = "Buffer Full"; }
                }

                Rect rowRect = new Rect(0, y, viewRect.width, RowHeight);
                Widgets.DrawBoxSolid(rowRect, CardBgColor);

                if (valid)
                {
                    Widgets.DrawHighlightIfMouseover(rowRect);
                    if (Widgets.ButtonInvisible(rowRect)) SelectPawn(p);
                }
                else Widgets.DrawBoxSolid(rowRect, new Color(0, 0, 0, 0.6f));

                GUI.color = DarkBorder;
                Widgets.DrawBox(rowRect);
                GUI.color = Color.white;

                // 1. Icon
                Rect iconRect = new Rect(rowRect.x + 6, rowRect.y + 6, 40, 40);
                Widgets.ThingIcon(iconRect, p);

                // Data prep
                Pawn partner = LovePartnerRelationUtility.ExistingLovePartner(p);
                bool isTargetPartner = (_selectedA != null && partner == _selectedA) || (_selectedB != null && partner == _selectedB);
                int complexity = AndroidReproUtils.GetSystemComplexity(p);

                // 2. Name (Calculated width to append icon)
                Rect nameRectBase = new Rect(iconRect.xMax + 12, rowRect.y + 6, 200, 24);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Vector2 nameSize = Text.CalcSize(p.LabelShortCap);

                // Ensure name doesn't overflow too far right
                Rect nameRect = new Rect(nameRectBase.x, nameRectBase.y, Mathf.Min(nameSize.x, 180f), nameRectBase.height);

                if (isTargetPartner) GUI.color = RomanticPink;
                else if (valid) GUI.color = SoftGrey;
                else GUI.color = Color.gray;

                Widgets.Label(nameRect, p.LabelShortCap);
                GUI.color = Color.white; // Reset color for icon

                // --- XENOTYPE ICON (Next to name) ---
                if (p.genes != null)
                {
                    Rect xenoIconRect = new Rect(nameRect.xMax + 8f, rowRect.y + 4f, 24f, 24f);

                    // Draw Icon
                    GUI.DrawTexture(xenoIconRect, p.genes.XenotypeIcon);

                    // Hover Tooltip
                    if (Mouse.IsOver(xenoIconRect))
                    {
                        Widgets.DrawHighlight(xenoIconRect);
                        string tip = $"<b>{p.genes.XenotypeLabelCap}</b>\n\n{p.genes.XenotypeDescShort}";
                        TooltipHandler.TipRegion(xenoIconRect, tip);
                    }
                }

                // 3. Status (Left Side, below name)
                Rect statusRect = new Rect(iconRect.xMax + 12, rowRect.y + 28, 200, 20);
                Text.Font = GameFont.Tiny;
                if (!valid) { GUI.color = new Color(1f, 0.4f, 0.4f); Widgets.Label(statusRect, $"[{failReason}]"); }
                else if (partner != null) { GUI.color = RomanticPink; Widgets.Label(statusRect, $"Link: {partner.LabelShort}"); }
                else { GUI.color = Color.gray; Widgets.Label(statusRect, "Status: Unpaired"); }

                // 4. Stats (Far Right)
                Rect statsRect = new Rect(rowRect.xMax - 120, rowRect.y + 6, 110, 40);
                Text.Anchor = TextAnchor.MiddleRight;
                float cpxNorm = Mathf.Clamp01(complexity / 40f);
                GUI.color = Color.Lerp(Color.cyan, Color.red, cpxNorm);
                Widgets.Label(statsRect, $"CPX: {complexity}");

                // Reset
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                y += RowHeight + 4f;
            }
            Widgets.EndScrollView();
        }

        private void DrawSelectedPanel(Rect rect)
        {
            // Header
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 30);
            Widgets.DrawBoxSolid(titleRect, HeaderColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = SoftGrey;
            Widgets.Label(titleRect, "ACTIVE CONNECTION");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float slotHeight = 80f;
            float gap = 60f;
            float startY = rect.y + 40f;

            // 1. Top Slot (Parent A)
            Rect slotA = new Rect(rect.x, startY, rect.width, slotHeight);
            DrawSlot(slotA, _selectedA, "PRIMARY SOURCE", () => _selectedA = null);

            // --- CONSOLE HEADER ---
            float headerY = slotA.yMax + 4f;
            Rect headerRect = new Rect(rect.x, headerY, rect.width, 18f);

            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.DrawLine(new Vector2(headerRect.x, headerRect.center.y), new Vector2(headerRect.x + 20, headerRect.center.y), GUI.color, 1f);
            Widgets.DrawLine(new Vector2(headerRect.xMax - 20, headerRect.center.y), new Vector2(headerRect.xMax, headerRect.center.y), GUI.color, 1f);

            GUI.color = SoftGrey;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, "[ SYSTEM COMPLEXITY SIGNATURE ]");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            // ---------------------

            // 2. The Dual Wave Monitor
            Rect gapRect = new Rect(rect.x, headerY + 18f, rect.width, gap - 22f);
            DrawDualWaveMonitor(gapRect);

            // 3. Bottom Slot (Parent B)
            Rect slotB = new Rect(rect.x, slotA.yMax + gap, rect.width, slotHeight);
            DrawSlot(slotB, _selectedB, "SECONDARY SOURCE", () => _selectedB = null);

            // 4. Hint Box
            float hintY = slotB.yMax + 20f;
            Rect hintRect = new Rect(rect.x, hintY, rect.width, rect.height - (hintY - rect.y));
            Widgets.DrawBoxSolid(hintRect, new Color(0.08f, 0.08f, 0.08f));
            GUI.color = DarkBorder;
            Widgets.DrawBox(hintRect);
            GUI.color = Color.white;

            Rect hintInner = hintRect.ContractedBy(10);
            GUI.color = Color.yellow;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(hintInner.x, hintInner.y, hintInner.width, 20), ":: ARCHIVE PROTOCOL ::");

            GUI.color = SoftGrey;
            string hintText = "Data inheritance is governed by System Complexity.\n\n> LOW Complexity = DOMINANT\n> HIGH Complexity = RECESSIVE\n\nSort candidates to optimize subroutines.";
            Widgets.Label(new Rect(hintInner.x, hintInner.y + 25, hintInner.width, hintInner.height - 25), hintText);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void DrawDualWaveMonitor(Rect rect)
        {
            float boxWidth = (rect.width - 10f) / 2f;

            Rect leftBox = new Rect(rect.x, rect.y, boxWidth, rect.height);
            Rect rightBox = new Rect(rect.xMax - boxWidth, rect.y, boxWidth, rect.height);

            Widgets.DrawBoxSolid(leftBox, new Color(0.05f, 0.05f, 0.05f));
            Widgets.DrawBoxSolid(rightBox, new Color(0.05f, 0.05f, 0.05f));

            GUI.color = DarkBorder;
            Widgets.DrawBox(leftBox);
            Widgets.DrawBox(rightBox);
            GUI.color = Color.white;

            if (_selectedA != null)
            {
                float cpxA = AndroidReproUtils.GetSystemComplexity(_selectedA);
                DrawClippedWave(leftBox, true, cpxA, _selectedA.LabelShort);
            }
            else DrawClippedWave(leftBox, false, 0, "SOURCE A");

            if (_selectedB != null)
            {
                float cpxB = AndroidReproUtils.GetSystemComplexity(_selectedB);
                DrawClippedWave(rightBox, true, cpxB, _selectedB.LabelShort);
            }
            else DrawClippedWave(rightBox, false, 0, "SOURCE B");
        }

        private void DrawClippedWave(Rect box, bool active, float complexity, string label)
        {
            GUI.color = SoftGrey;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(box.x + 4, box.y + 2, box.width, 20), label);
            Text.Font = GameFont.Small;

            if (!active)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 0f, 0f, 0.4f + Mathf.Sin(_timeOffset * 5f) * 0.2f);
                Widgets.Label(box, "NO SIGNAL");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            GUI.BeginGroup(box);
            {
                Rect innerRect = new Rect(0, 0, box.width, box.height);
                DrawWaveInsideGroup(innerRect, complexity);
            }
            GUI.EndGroup();

            GUI.color = Color.white;
        }

        private void DrawWaveInsideGroup(Rect rect, float complexity)
        {
            GUI.color = new Color(0.2f, 0.8f, 0.2f);

            float chaosFactor = Mathf.Clamp01(complexity / 60f);
            float freq = Mathf.Lerp(0.1f, 0.5f, chaosFactor);
            float speed = 10f;
            float amplitude = rect.height * 0.3f;
            float midY = rect.height / 2f;

            float startX = 5f;
            float endX = rect.width - 5f;

            Vector2 lastPoint = CalculateWavePoint(startX, _timeOffset, speed, freq, chaosFactor, midY, amplitude);

            for (float x = startX; x <= endX; x += 3f)
            {
                Vector2 newPoint = CalculateWavePoint(x, _timeOffset, speed, freq, chaosFactor, midY, amplitude);
                if (Vector2.Distance(lastPoint, newPoint) > 0.1f)
                {
                    Widgets.DrawLine(lastPoint, newPoint, GUI.color, 1f);
                }
                lastPoint = newPoint;
            }

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(0, rect.height - 20, rect.width - 4, 20), $"SIG: {complexity}");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private Vector2 CalculateWavePoint(float x, float time, float speed, float freq, float chaos, float midY, float amp)
        {
            float sampleX = x - (time * speed);
            float wave = Mathf.Sin(sampleX * freq);

            if (chaos > 0.3f)
            {
                wave += Mathf.Sin(sampleX * freq * 2.5f) * 0.5f;
            }
            return new Vector2(x, midY + (wave * amp));
        }

        private void DrawSlot(Rect rect, Pawn pawn, string label, System.Action onClear)
        {
            Widgets.DrawBoxSolid(rect, CardBgColor);
            GUI.color = DarkBorder;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            if (pawn != null)
            {
                if (Mouse.IsOver(rect)) { Widgets.DrawHighlight(rect); TooltipHandler.TipRegion(rect, "Click to EJECT source"); }
                if (Widgets.ButtonInvisible(rect)) { SoundDefOf.Click.PlayOneShotOnCamera(); onClear(); }

                Rect icon = new Rect(rect.x + 6, rect.y + 6, 68, 68);
                Widgets.ThingIcon(icon, pawn);

                Rect text = new Rect(icon.xMax + 10, rect.y + 10, rect.width - 90, 28);
                Text.Font = GameFont.Medium;
                GUI.color = SoftGrey;
                Widgets.Label(text, pawn.LabelShortCap);
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                Rect subText = new Rect(icon.xMax + 10, rect.y + 42, rect.width - 90, 25);

                string xeno = pawn.genes?.XenotypeLabelCap ?? "Baseliner";
                int cpx = AndroidReproUtils.GetSystemComplexity(pawn);

                GUI.color = Color.gray;
                Widgets.Label(subText, $"{xeno}");

                float cpxNorm = Mathf.Clamp01(cpx / 40f);
                GUI.color = Color.Lerp(Color.cyan, Color.red, cpxNorm);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(subText, $"CPX: {cpx}");
                Text.Anchor = TextAnchor.UpperLeft;

                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.1f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, $"< {label} >");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void SelectPawn(Pawn p)
        {
            if (_selectedA == null) _selectedA = p;
            else if (_selectedB == null) _selectedB = p;
            else Messages.Message("Neural buffer full. Eject a source first.", MessageTypeDefOf.RejectInput, false);
        }
    }
}