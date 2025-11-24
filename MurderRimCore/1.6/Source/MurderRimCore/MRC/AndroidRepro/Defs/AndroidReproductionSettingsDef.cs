using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    public class AndroidReproductionSettingsDef : Def
    {
        public bool enabled = true;

        // Parent eligibility / relationship
        public bool requireLovePartners = true;
        public bool allowCrossFaction = false;
        public bool allowMixedTypes = true;
        public bool onlyHumanlikeAndroids = true;
        public bool requireAwakenedBoth = true;
        public bool fusionRequiresBothAwakened = true;

        // Work & gestation
        public float fusionWorkAmount = 3000f;
        public float fusionTickFactorPerStat = 1f;
        public float gestationDays = 5f;
        public float stationGestationPowerUse = 600f;

        // Gene inheritance toggles
        public bool inheritEndogenes = true;
        public bool inheritXenogenes = true;

        // Caps
        public IntRange maxEndogenes = new IntRange(6, 10);
        public IntRange maxXenogenes = new IntRange(3, 6);
        public bool disableGeneCaps = true;

        // Weight scaling (inverse complexity weighting)
        public float parentAWeightScale = 1f;
        public float parentBWeightScale = 1f;

        // Optional lists
        public List<string> essentialGeneDefs = new List<string>();
        public List<string> nonInheritableGeneDefs = new List<string>();
        public List<string> nonInheritableGeneTags = new List<string>();
        public List<string> excludedGeneTags = new List<string>();

        // Xenotype icon override
        public string fusedXenotypeIconDef = "";

        // Newborn overrides
        public string newbornPawnKindDef = "";
        public bool inheritFactionFromMother = true;

        // Determinism
        public bool enforceDeterministicFusion = true;

        // ===== Synthetic relation settings category =====
        // Grouped here to keep XML tidy: <relationSettings>...</relationSettings>
        public AndroidSyntheticRelationSettings relationSettings = new AndroidSyntheticRelationSettings();

        public static AndroidReproductionSettingsDef Current =>
            DefDatabase<AndroidReproductionSettingsDef>.AllDefsListForReading.FirstOrDefault();

        public float GetSyntheticParentChanceForFaction(Faction faction)
            => relationSettings?.GetParentChanceForFaction(faction) ?? 0f;

        public float GetSyntheticBloodRelationChanceForFaction(Faction faction)
            => relationSettings?.GetBloodRelationChanceForFaction(faction) ?? 0f;

        // Optional: if you ever use this def in save data (mod settings etc.)
        // you can add ExposeData – otherwise Defs are normally just XML-only.
        public virtual void ExposeData()
        {
            // Let base handle Def fields; relationSettings is deep-scribed.
            Scribe_Deep.Look(ref relationSettings, "relationSettings");
            if (relationSettings == null)
                relationSettings = new AndroidSyntheticRelationSettings();
        }
    }

    /// <summary>
    /// Neatly grouped synthetic-family tuning. Shows up in XML as:
    /// &lt;relationSettings&gt; ... &lt;/relationSettings&gt;
    /// </summary>
    public class AndroidSyntheticRelationSettings : IExposable
    {
        // Default/global chance a synthetic pawn is allowed parents at all.
        // 0.0 = factory-only by default; individual factions can override.
        public float globalParentChance = 0.0f;

        // Default/global chance that synthetic&lt;-&gt;synthetic blood relations are allowed.
        // 0.0 = no synthetic family by default; 1.0 = always allowed when other logic permits.
        public float globalBloodRelationChance = 0.0f;

        // Per-faction overrides: FactionDef.defName -> value (0–1, interpreted as chance).
        // If a key exists here, it REPLACES the global, it is NOT multiplied.
        public StringFloatMap factionParentChance = new StringFloatMap();
        public StringFloatMap factionBloodRelationChance = new StringFloatMap();

        public void ExposeData()
        {
            Scribe_Values.Look(ref globalParentChance, "globalParentChance", 0.0f);
            // Backward compat: if you previously used globalBloodRelationFactor, keep the same field name in XML
            Scribe_Values.Look(ref globalBloodRelationChance, "globalBloodRelationChance", 0.0f);

            Scribe_Deep.Look(ref factionParentChance, "factionParentChance");
            Scribe_Deep.Look(ref factionBloodRelationChance, "factionBloodRelationChance");

            if (factionParentChance == null) factionParentChance = new StringFloatMap();
            if (factionBloodRelationChance == null) factionBloodRelationChance = new StringFloatMap();
        }

        /// <summary>
        /// Returns the "parent allowed" chance [0–1] for a synthetic pawn.
        /// If a per-faction value exists, it OVERRIDES the global.
        /// If none exists, globalParentChance is used (can be 0).
        /// </summary>
        public float GetParentChanceForFaction(Faction faction)
        {
            float value = globalParentChance;

            if (faction != null && faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                if (factionParentChance != null &&
                    factionParentChance.TryGetValue(faction.def.defName, out var facVal))
                {
                    value = facVal;
                }
            }

            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// Returns the "blood relation allowed" chance [0–1] for synthetic&lt;-&gt;synthetic pairs.
        /// If a per-faction value exists, it OVERRIDES the global.
        /// If none exists, globalBloodRelationChance is used (can be 0).
        /// </summary>
        public float GetBloodRelationChanceForFaction(Faction faction)
        {
            float value = globalBloodRelationChance;

            if (faction != null && faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                if (factionBloodRelationChance != null &&
                    factionBloodRelationChance.TryGetValue(faction.def.defName, out var facVal))
                {
                    value = facVal;
                }
            }

            return Mathf.Clamp01(value);
        }
    }
}