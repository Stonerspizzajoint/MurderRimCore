using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using MurderRimCore.MRWD;
using Verse.Sound;

namespace MurderRimCore
{
    /// <summary>
    /// Mod entry + settings UI (refactored). Shows separate male/female bald chance sliders.
    /// </summary>
    public class MRC_Mod : Mod
    {
        public static MRC_ModSettings Settings;
        private Vector2 _scrollPos = Vector2.zero;

        // Buffer for editing removed hair tags (comma separated).
        private static string _removedHairTagsBuffer;

        public MRC_Mod(ModContentPack content) : base(content)
        {
            if (WorkerDroneDependency.Active)
            {
                Settings = GetSettings<MRC_ModSettings>();
                if (Settings.spawn == null)
                    Settings.spawn = new WorkerDroneSpawnSettings();

                if (_removedHairTagsBuffer == null)
                    _removedHairTagsBuffer = string.Join(", ", Settings.spawn.removedHairTags ?? new List<string>());

                // Ensure favorite-color flag is disabled if Ideology is not active
                if (!ModsConfig.IdeologyActive && Settings.spawn.hardHatUseFavoriteColor)
                    Settings.spawn.hardHatUseFavoriteColor = false;
            }
        }

        public override string SettingsCategory() =>
            WorkerDroneDependency.Active ? "MurderRim: Worker Drones" : null;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (!WorkerDroneDependency.Active)
            {
                Widgets.Label(inRect,
                    "Worker Drone settings unavailable. Dependency mod not active:\n" +
                    WorkerDroneDependency.PackageId);
                return;
            }

            var viewRect = new Rect(0, 0, inRect.width - 16f, inRect.height + 560f);
            Widgets.BeginScrollView(inRect, ref _scrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            DrawHeader(listing);

            DrawPawnGeneratorSection(listing);
            listing.GapLine();

            DrawBeardSection(listing);
            listing.GapLine();

            DrawHairSection(listing);
            listing.GapLine();

            DrawHardHatSection(listing);
            listing.GapLine();

            DrawFooterButtons(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawHeader(Listing_Standard listing)
        {
            listing.Label("MurderRim: Worker Drone Settings");
            listing.Gap(6f);
            listing.Label("These settings control how worker drones are generated (hair, facial hair, hard hats, etc.). Changes affect newly generated pawns.");
            listing.Gap(8f);
        }

        private void DrawPawnGeneratorSection(Listing_Standard listing)
        {
            listing.Label("Pawn Generator");
            listing.Gap(6f);

            float hat = Mathf.Clamp01(Settings.spawn.hardHatSpawnChance);
            listing.Label($"Hard hat spawn chance: {hat.ToStringPercent()}");
            hat = listing.Slider(hat, 0f, 1f);
            Settings.spawn.hardHatSpawnChance = hat;

            listing.Gap(6f);
        }

        private void DrawBeardSection(Listing_Standard listing)
        {
            listing.Label("Facial Hair");
            listing.Gap(6f);

            bool allowBeards = Settings.spawn.allowBeards;
            listing.CheckboxLabeled("Allow beards", ref allowBeards,
                "If disabled, worker drones will be clean-shaven (no beards or mustaches).");
            Settings.spawn.allowBeards = allowBeards;

            if (allowBeards)
            {
                listing.Gap(6f);

                float beardChance = Mathf.Clamp01(Settings.spawn.beardSpawnChance);
                listing.Label($"Facial hair spawn chance: {beardChance.ToStringPercent()}");
                beardChance = listing.Slider(beardChance, 0f, 1f);
                Settings.spawn.beardSpawnChance = beardChance;

                listing.Gap(6f);
                bool onlyMustaches = Settings.spawn.allowOnlyMustaches;
                listing.CheckboxLabeled("Allow only mustaches", ref onlyMustaches,
                    "If enabled, male awakened drones that would have had a beard may get a mustache instead.");
                Settings.spawn.allowOnlyMustaches = onlyMustaches;
            }
            else
            {
                Settings.spawn.allowOnlyMustaches = false;
            }
        }

        private void DrawHairSection(Listing_Standard listing)
        {
            listing.Label("Hair");
            listing.Gap(4f);
            listing.Label("Control hair behavior for awakened and unawakened worker drones, and filter out unwanted hair styles by tags.");
            listing.Gap(6f);

            bool hairAwakened = Settings.spawn.hairForAwakened;
            listing.CheckboxLabeled("Allow hair for awakened drones", ref hairAwakened,
                "If disabled, awakened worker drones are always bald.");
            Settings.spawn.hairForAwakened = hairAwakened;

            bool noHairUnawakened = Settings.spawn.noHairForUnawakened;
            listing.CheckboxLabeled("No hair for unawakened drones", ref noHairUnawakened,
                "If enabled, unawakened worker drones are always bald.");
            Settings.spawn.noHairForUnawakened = noHairUnawakened;

            listing.Gap(6f);

            float baldChanceMale = Mathf.Clamp01(Settings.spawn.baldChanceMale);
            listing.Label($"Bald chance (male, when hair otherwise allowed): {baldChanceMale.ToStringPercent()}");
            baldChanceMale = listing.Slider(baldChanceMale, 0f, 1f);
            Settings.spawn.baldChanceMale = baldChanceMale;

            listing.Gap(6f);

            float baldChanceFemale = Mathf.Clamp01(Settings.spawn.baldChanceFemale);
            listing.Label($"Bald chance (female, when hair otherwise allowed): {baldChanceFemale.ToStringPercent()}");
            baldChanceFemale = listing.Slider(baldChanceFemale, 0f, 1f);
            Settings.spawn.baldChanceFemale = baldChanceFemale;

            listing.Gap(8f);

            bool alwaysHatWhenBald = Settings.spawn.alwaysHardHatWhenBald;
            listing.CheckboxLabeled("Always give hard hat when Bald", ref alwaysHatWhenBald,
                "If enabled, any worker drone with the 'Bald' hair style will always receive a hard hat.");
            Settings.spawn.alwaysHardHatWhenBald = alwaysHatWhenBald;

            listing.Gap(8f);
            listing.Label("Removed hair tags (comma-separated):");
            var rect = listing.GetRect(Text.LineHeight * 2f);
            _removedHairTagsBuffer = Widgets.TextArea(rect, _removedHairTagsBuffer);

            var parsed = _removedHairTagsBuffer
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
            Settings.spawn.removedHairTags = parsed;
        }

        private void DrawHardHatSection(Listing_Standard listing)
        {
            listing.Label("Hard Hat Coloring");
            listing.Gap(6f);

            bool ideologyActive = ModsConfig.IdeologyActive;
            if (!ideologyActive && Settings.spawn.hardHatUseFavoriteColor)
                Settings.spawn.hardHatUseFavoriteColor = false;

            if (ideologyActive)
            {
                bool useFav = Settings.spawn.hardHatUseFavoriteColor;
                listing.CheckboxLabeled("Use favorite color for hard hat (Ideology)", ref useFav,
                    "If enabled and roll succeeds, awakened drones' hard hats use their favorite color.");
                Settings.spawn.hardHatUseFavoriteColor = useFav;

                if (useFav)
                {
                    float favChance = Mathf.Clamp01(Settings.spawn.favHardHatColorChance);
                    listing.Label($"Favorite hard hat color chance: {favChance.ToStringPercent()}");
                    favChance = listing.Slider(favChance, 0f, 1f);
                    Settings.spawn.favHardHatColorChance = favChance;
                }
            }
            else
            {
                listing.Label("Favorite hard hat color (Ideology DLC required).");
            }
        }

        private void DrawFooterButtons(Listing_Standard listing)
        {
            listing.Gap(8f);
            var row = listing.GetRect(32f);
            var left = new Rect(row.x, row.y, row.width / 2f - 4f, row.height);
            var right = new Rect(row.x + row.width / 2f + 4f, row.y, row.width / 2f - 4f, row.height);

            if (Widgets.ButtonText(left, "Reset to XML defaults"))
            {
                Settings.LoadDefaultsFromDef();
                _removedHairTagsBuffer = string.Join(", ", Settings.spawn.removedHairTags ?? new List<string>());
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(right, "Save Now"))
            {
                WriteSettings();
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }
    }

    // Facade: always read live settings when available, else fall back to Def
    public static class MRC_Settings
    {
        public static WorkerDroneSpawnSettings Spawn =>
            WorkerDroneDependency.Active
                ? (MRC_Mod.Settings?.spawn ?? WorkerDroneSettingsDef.Current?.SpawnSettings)
                : WorkerDroneSettingsDef.Current?.SpawnSettings;
    }

    public class MRC_ModSettings : ModSettings
    {
        public WorkerDroneSpawnSettings spawn = new WorkerDroneSpawnSettings();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref spawn, "spawn");
        }

        public void LoadDefaultsFromDef()
        {
            var def = WorkerDroneSettingsDef.Current;
            if (def == null) return;

            spawn.hardHatSpawnChance = def.SpawnSettings.hardHatSpawnChance;
            spawn.allowBeards = def.SpawnSettings.allowBeards;
            spawn.allowOnlyMustaches = def.SpawnSettings.allowOnlyMustaches;
            spawn.beardSpawnChance = def.SpawnSettings.beardSpawnChance;
            spawn.hardHatUseFavoriteColor = def.SpawnSettings.hardHatUseFavoriteColor;
            spawn.favHardHatColorChance = def.SpawnSettings.favHardHatColorChance;
            spawn.removedHairTags = def.SpawnSettings.removedHairTags?.ToList() ?? new List<string>();
            spawn.hairForAwakened = def.SpawnSettings.hairForAwakened;
            spawn.noHairForUnawakened = def.SpawnSettings.noHairForUnawakened;
            spawn.baldChanceMale = def.SpawnSettings.baldChanceMale;
            spawn.baldChanceFemale = def.SpawnSettings.baldChanceFemale;
            spawn.alwaysHardHatWhenBald = def.SpawnSettings.alwaysHardHatWhenBald;
        }
    }
}