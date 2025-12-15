using UnityEngine;
using Verse;

namespace MurderRimCore
{
    public class MurderRimCoreMod : Mod
    {
        public static WorkerDroneSpawnModSettings SpawnSettings;

        public MurderRimCoreMod(ModContentPack content) : base(content)
        {
            SpawnSettings = GetSettings<WorkerDroneSpawnModSettings>();
        }

        public override string SettingsCategory()
        {
            return "Murder Rim: Worker Drones";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Worker Drone Spawn Visuals (only applies to DroneHelper.IsWorkerDrone pawns):");

            listing.GapLine();

            // Beards
            listing.Label("Beards");
            listing.CheckboxLabeled("Allow beards on worker drones", ref SpawnSettings.allowBeards);

            if (SpawnSettings.allowBeards)
            {
                listing.CheckboxLabeled("Mustaches only when beards allowed", ref SpawnSettings.mustachesOnlyWhenBeardsAllowed);
                listing.Label($"Base chance to spawn with facial hair: {(SpawnSettings.beardSpawnChance * 100f):0}%");
                SpawnSettings.beardSpawnChance = listing.Slider(SpawnSettings.beardSpawnChance, 0f, 1f);
            }

            listing.GapLine();

            // Helmets
            listing.Label("Helmets");
            listing.Label($"Base chance to spawn with a helmet: {(SpawnSettings.baseHelmetSpawnChance * 100f):0}%");
            SpawnSettings.baseHelmetSpawnChance = listing.Slider(SpawnSettings.baseHelmetSpawnChance, 0f, 1f);

            listing.Gap();

            // Helmet color / Ideology
            listing.CheckboxLabeled("If Ideology is installed, allow helmets to match favorite color", ref SpawnSettings.matchFavoriteColorWhenIdeology);
            if (SpawnSettings.matchFavoriteColorWhenIdeology)
            {
                listing.Label($"Chance helmet uses favorite color: {(SpawnSettings.favoriteColorHelmetChance * 100f):0}%");
                SpawnSettings.favoriteColorHelmetChance = listing.Slider(SpawnSettings.favoriteColorHelmetChance, 0f, 1f);
            }

            listing.GapLine();

            // Bald
            listing.Label("Bald chances (worker drones only)");

            listing.Label($"Male bald chance: {(SpawnSettings.baldChanceMale * 100f):0}%");
            SpawnSettings.baldChanceMale = listing.Slider(SpawnSettings.baldChanceMale, 0f, 1f);

            listing.Label($"Female bald chance: {(SpawnSettings.baldChanceFemale * 100f):0}%");
            SpawnSettings.baldChanceFemale = listing.Slider(SpawnSettings.baldChanceFemale, 0f, 1f);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}