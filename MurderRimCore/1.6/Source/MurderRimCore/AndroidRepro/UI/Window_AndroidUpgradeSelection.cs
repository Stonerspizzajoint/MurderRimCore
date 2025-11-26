using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MurderRimCore.AndroidRepro
{
    /// <summary>
    /// Window that allows players to select skills, passions, and traits when upgrading an android.
    /// Similar to vanilla growth moment selection but tailored for androids.
    /// </summary>
    public class Window_AndroidUpgradeSelection : Window
    {
        private readonly Pawn _pawn;
        private readonly HediffComp_AndroidGrowth _growthComp;
        private readonly Pawn _surgeon;

        // Selection state
        private readonly Dictionary<SkillDef, int> _skillPointsAllocated = new Dictionary<SkillDef, int>();
        private readonly HashSet<SkillDef> _selectedPassions = new HashSet<SkillDef>();
        private TraitDef _selectedTrait;
        private int _selectedTraitDegree;

        private int _remainingSkillPoints;
        private int _remainingPassions;
        private bool _canSelectTrait;

        // UI state
        private Vector2 _scrollPosition;

        // Visual constants
        private const float RowHeight = 32f;
        private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color SoftGrey = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color SelectedColor = new Color(0.3f, 0.6f, 0.3f, 0.5f);

        public Window_AndroidUpgradeSelection(Pawn pawn, HediffComp_AndroidGrowth growthComp, Pawn surgeon)
        {
            _pawn = pawn;
            _growthComp = growthComp;
            _surgeon = surgeon;

            // Initialize allocation limits from comp props
            _remainingSkillPoints = growthComp.Props.skillPointsPerUpgrade;
            _remainingPassions = growthComp.Props.passionsPerUpgrade;
            _canSelectTrait = growthComp.IsAtFinalStage; // Only on adult upgrade

            doCloseX = false; // Must confirm or cancel
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            // Initialize skill allocations
            foreach (var skill in DefDatabase<SkillDef>.AllDefs)
            {
                _skillPointsAllocated[skill] = 0;
            }
        }

        public override Vector2 InitialSize => new Vector2(700f, 650f);

        public override void DoWindowContents(Rect inRect)
        {
            // Header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect headerRect = new Rect(0, 0, inRect.width, 40f);
            Widgets.DrawBoxSolid(headerRect, HeaderColor);
            GUI.color = SoftGrey;
            
            string stageName = _growthComp.CurrentStageIndex switch
            {
                0 => "Child",
                1 => "Teen",
                2 => "Adult",
                _ => "Unknown"
            };
            Widgets.Label(headerRect, $"FRAME UPGRADE: {_pawn.LabelShortCap} → {stageName}");
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float y = 50f;

            // Info panel
            Rect infoRect = new Rect(10f, y, inRect.width - 20f, 60f);
            Widgets.DrawBoxSolid(infoRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Rect infoInner = infoRect.ContractedBy(8f);
            
            GUI.color = Color.cyan;
            Widgets.Label(infoInner, $"Skill Points Remaining: {_remainingSkillPoints}");
            Widgets.Label(new Rect(infoInner.x, infoInner.y + 20f, infoInner.width, 20f), 
                $"Passions Remaining: {_remainingPassions}");
            if (_canSelectTrait)
            {
                Widgets.Label(new Rect(infoInner.x, infoInner.y + 40f, infoInner.width, 20f), 
                    $"Trait Selection: {(_selectedTrait != null ? "Selected" : "Available")}");
            }
            GUI.color = Color.white;

            y += 70f;

            // Skills section
            Rect skillsHeaderRect = new Rect(10f, y, inRect.width - 20f, 25f);
            Widgets.DrawBoxSolid(skillsHeaderRect, HeaderColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(skillsHeaderRect.x + 10f, skillsHeaderRect.y, skillsHeaderRect.width, skillsHeaderRect.height), 
                "SKILL ALLOCATION");
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;

            // Scrollable skill list
            float skillListHeight = DefDatabase<SkillDef>.DefCount * (RowHeight + 2f);
            Rect skillListOuterRect = new Rect(10f, y, inRect.width - 20f, 200f);
            Rect skillListInnerRect = new Rect(0f, 0f, skillListOuterRect.width - 16f, skillListHeight);

            Widgets.BeginScrollView(skillListOuterRect, ref _scrollPosition, skillListInnerRect);
            
            float skillY = 0f;
            foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefs.OrderBy(s => s.label))
            {
                DrawSkillRow(new Rect(0f, skillY, skillListInnerRect.width, RowHeight), skill);
                skillY += RowHeight + 2f;
            }
            
            Widgets.EndScrollView();
            y += 210f;

            // Passions section
            Rect passionHeaderRect = new Rect(10f, y, inRect.width - 20f, 25f);
            Widgets.DrawBoxSolid(passionHeaderRect, HeaderColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(passionHeaderRect.x + 10f, passionHeaderRect.y, passionHeaderRect.width, passionHeaderRect.height), 
                "PASSION SELECTION");
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;

            DrawPassionSelection(new Rect(10f, y, inRect.width - 20f, 80f));
            y += 90f;

            // Trait section (only on final upgrade)
            if (_canSelectTrait)
            {
                Rect traitHeaderRect = new Rect(10f, y, inRect.width - 20f, 25f);
                Widgets.DrawBoxSolid(traitHeaderRect, HeaderColor);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(traitHeaderRect.x + 10f, traitHeaderRect.y, traitHeaderRect.width, traitHeaderRect.height), 
                    "TRAIT SELECTION (Adult Only)");
                Text.Anchor = TextAnchor.UpperLeft;
                y += 30f;

                DrawTraitSelection(new Rect(10f, y, inRect.width - 20f, 60f));
                y += 70f;
            }

            // Buttons
            Rect buttonRect = new Rect(0f, inRect.height - 40f, inRect.width, 40f);
            
            if (Widgets.ButtonText(new Rect(buttonRect.x + 10f, buttonRect.y, 150f, 35f), "Cancel"))
            {
                // Cancel without applying changes
                Close();
            }

            bool canConfirm = _remainingSkillPoints == 0;
            GUI.color = canConfirm ? Color.green : Color.gray;
            if (Widgets.ButtonText(new Rect(buttonRect.xMax - 160f, buttonRect.y, 150f, 35f), "Confirm Upgrade"))
            {
                if (canConfirm)
                {
                    ApplyUpgrade();
                    Close();
                }
                else
                {
                    Messages.Message("Must allocate all skill points before confirming.", MessageTypeDefOf.RejectInput);
                }
            }
            GUI.color = Color.white;
        }

        private void DrawSkillRow(Rect rect, SkillDef skill)
        {
            int currentLevel = _pawn.skills?.GetSkill(skill)?.Level ?? 0;
            int allocated = _skillPointsAllocated[skill];

            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            if (allocated > 0)
            {
                Widgets.DrawBoxSolid(rect, SelectedColor);
            }

            // Skill name
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(rect.x + 10f, rect.y, 150f, rect.height);
            Widgets.Label(labelRect, skill.LabelCap);

            // Current level
            Rect levelRect = new Rect(labelRect.xMax + 10f, rect.y, 80f, rect.height);
            GUI.color = Color.gray;
            Widgets.Label(levelRect, $"Lv: {currentLevel}");
            GUI.color = Color.white;

            // Allocated points
            Rect allocatedRect = new Rect(levelRect.xMax + 10f, rect.y, 80f, rect.height);
            GUI.color = allocated > 0 ? Color.green : Color.gray;
            Widgets.Label(allocatedRect, $"+{allocated}");
            GUI.color = Color.white;

            // Minus button
            Rect minusRect = new Rect(rect.xMax - 100f, rect.y + 4f, 24f, 24f);
            if (allocated > 0)
            {
                if (Widgets.ButtonText(minusRect, "-"))
                {
                    _skillPointsAllocated[skill]--;
                    _remainingSkillPoints++;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            // Plus button
            Rect plusRect = new Rect(rect.xMax - 70f, rect.y + 4f, 24f, 24f);
            if (_remainingSkillPoints > 0)
            {
                if (Widgets.ButtonText(plusRect, "+"))
                {
                    _skillPointsAllocated[skill]++;
                    _remainingSkillPoints--;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawPassionSelection(Rect rect)
        {
            float x = rect.x;
            float buttonWidth = 100f;
            float spacing = 10f;

            foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefs.OrderBy(s => s.label))
            {
                if (x + buttonWidth > rect.xMax)
                {
                    x = rect.x;
                    rect.y += 30f;
                }

                Rect buttonRect = new Rect(x, rect.y, buttonWidth, 25f);
                bool isSelected = _selectedPassions.Contains(skill);

                if (isSelected)
                {
                    Widgets.DrawBoxSolid(buttonRect, SelectedColor);
                }

                if (Widgets.ButtonText(buttonRect, skill.skillLabel.CapitalizeFirst()))
                {
                    if (isSelected)
                    {
                        _selectedPassions.Remove(skill);
                        _remainingPassions++;
                    }
                    else if (_remainingPassions > 0)
                    {
                        _selectedPassions.Add(skill);
                        _remainingPassions--;
                    }
                    else
                    {
                        Messages.Message("No passion slots remaining.", MessageTypeDefOf.RejectInput);
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                x += buttonWidth + spacing;
            }
        }

        private void DrawTraitSelection(Rect rect)
        {
            // Get valid traits for androids
            var validTraits = DefDatabase<TraitDef>.AllDefs
                .Where(t => !t.disabledWorkTags.HasFlag(WorkTags.None) || t.disabledWorkTags == WorkTags.None)
                .Where(t => t.commonality > 0)
                .OrderByDescending(t => t.commonality)
                .Take(20) // Limit options
                .ToList();

            float x = rect.x;
            float buttonWidth = 120f;
            float spacing = 5f;
            float rowY = rect.y;

            foreach (TraitDef trait in validTraits)
            {
                if (x + buttonWidth > rect.xMax)
                {
                    x = rect.x;
                    rowY += 28f;
                }

                Rect buttonRect = new Rect(x, rowY, buttonWidth, 24f);
                
                // Get the default degree (usually 0)
                int degree = trait.degreeDatas?.FirstOrDefault()?.degree ?? 0;
                string label = trait.DataAtDegree(degree)?.label ?? trait.defName;
                
                bool isSelected = _selectedTrait == trait && _selectedTraitDegree == degree;

                if (isSelected)
                {
                    Widgets.DrawBoxSolid(buttonRect, SelectedColor);
                }

                if (Widgets.ButtonText(buttonRect, label.CapitalizeFirst()))
                {
                    if (isSelected)
                    {
                        _selectedTrait = null;
                        _selectedTraitDegree = 0;
                    }
                    else
                    {
                        _selectedTrait = trait;
                        _selectedTraitDegree = degree;
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                x += buttonWidth + spacing;
            }
        }

        private void ApplyUpgrade()
        {
            // Apply skill points
            foreach (var kvp in _skillPointsAllocated)
            {
                if (kvp.Value > 0 && _pawn.skills != null)
                {
                    SkillRecord skill = _pawn.skills.GetSkill(kvp.Key);
                    if (skill != null)
                    {
                        skill.Level += kvp.Value;
                    }
                }
            }

            // Apply passions
            foreach (SkillDef passionSkill in _selectedPassions)
            {
                if (_pawn.skills != null)
                {
                    SkillRecord skill = _pawn.skills.GetSkill(passionSkill);
                    if (skill != null)
                    {
                        // Upgrade passion level
                        if (skill.passion == Passion.None)
                            skill.passion = Passion.Minor;
                        else if (skill.passion == Passion.Minor)
                            skill.passion = Passion.Major;
                    }
                }
            }

            // Apply trait (only on adult upgrade)
            if (_canSelectTrait && _selectedTrait != null && _pawn.story?.traits != null)
            {
                if (!_pawn.story.traits.HasTrait(_selectedTrait, _selectedTraitDegree))
                {
                    _pawn.story.traits.GainTrait(new Trait(_selectedTrait, _selectedTraitDegree));
                }
            }

            // Perform the actual upgrade through the comp
            _growthComp.PerformUpgrade();

            // Play success sound
            SoundDefOf.Quest_Succeded.PlayOneShotOnCamera();

            string stageName = _growthComp.CurrentStageIndex switch
            {
                1 => "Child",
                2 => "Teen",
                3 => "Adult",
                _ => "Unknown"
            };

            Messages.Message($"{_pawn.LabelShortCap} has been upgraded to {stageName} frame!", 
                _pawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
