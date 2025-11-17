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

        // Protected android baby stage duration (biological years)
        public float fusedAndroidBabyStageYearsMax = 1.0f;

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

        public static AndroidReproductionSettingsDef Current =>
            DefDatabase<AndroidReproductionSettingsDef>.AllDefsListForReading.FirstOrDefault();
    }
}