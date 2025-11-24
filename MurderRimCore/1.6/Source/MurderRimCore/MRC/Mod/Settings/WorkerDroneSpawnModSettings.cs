using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore
{
    public class WorkerDroneSpawnModSettings : ModSettings
    {
        // Beards
        public bool allowBeards = true;
        public bool mustachesOnlyWhenBeardsAllowed = true;
        public float beardSpawnChance = 0.2f; // 0–1

        // Helmets
        public float baseHelmetSpawnChance = 0.5f; // 0–1

        // Helmet color (Ideology)
        public bool matchFavoriteColorWhenIdeology = true;
        public float favoriteColorHelmetChance = 0.3f; // 0–1

        // Bald chances by gender (0–1)
        public float baldChanceMale = 0.5f;
        public float baldChanceFemale = 0.05f;

        public override void ExposeData()
        {
            base.ExposeData();

            // Beards
            Scribe_Values.Look(ref allowBeards, "allowBeards", true);
            Scribe_Values.Look(ref mustachesOnlyWhenBeardsAllowed, "mustachesOnlyWhenBeardsAllowed", true);
            Scribe_Values.Look(ref beardSpawnChance, "beardSpawnChance", 0.2f);

            // Helmets
            Scribe_Values.Look(ref baseHelmetSpawnChance, "baseHelmetSpawnChance", 0.3f);

            // Helmet color
            Scribe_Values.Look(ref matchFavoriteColorWhenIdeology, "matchFavoriteColorWhenIdeology", true);
            Scribe_Values.Look(ref favoriteColorHelmetChance, "favoriteColorHelmetChance", 0.3f);

            // Bald
            Scribe_Values.Look(ref baldChanceMale, "baldChanceMale", 0.5f);
            Scribe_Values.Look(ref baldChanceFemale, "baldChanceFemale", 0.05f);
        }
    }
}