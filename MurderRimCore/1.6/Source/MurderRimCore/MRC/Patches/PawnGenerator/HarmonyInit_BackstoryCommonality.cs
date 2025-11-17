using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace MurderRimCore
{
    // Multiplies vanilla backstory selection weights by BackstoryExtension.backstoryCommonality.
    [StaticConstructorOnStartup]
    public static class HarmonyInit_BackstoryCommonality
    {
        static HarmonyInit_BackstoryCommonality()
        {
            var h = new Harmony("murderrimcore.backstory.commonality");

            // RimWorld.PawnBioAndNameGenerator.BackstorySelectionWeight(BackstoryDef)
            var miBackstoryWeight = AccessTools.Method(typeof(PawnBioAndNameGenerator), "BackstorySelectionWeight", new[] { typeof(BackstoryDef) });
            if (miBackstoryWeight != null)
            {
                h.Patch(miBackstoryWeight,
                    postfix: new HarmonyMethod(typeof(HarmonyInit_BackstoryCommonality), nameof(BackstorySelectionWeight_Postfix)));
            }
            else
            {
                Log.Warning("[MRC] Could not find PawnBioAndNameGenerator.BackstorySelectionWeight");
            }

            // RimWorld.PawnBioAndNameGenerator.BioSelectionWeight(PawnBio)
            var miBioWeight = AccessTools.Method(typeof(PawnBioAndNameGenerator), "BioSelectionWeight", new[] { typeof(PawnBio) });
            if (miBioWeight != null)
            {
                h.Patch(miBioWeight,
                    postfix: new HarmonyMethod(typeof(HarmonyInit_BackstoryCommonality), nameof(BioSelectionWeight_Postfix)));
            }
            else
            {
                Log.Warning("[MRC] Could not find PawnBioAndNameGenerator.BioSelectionWeight");
            }
        }

        // Shuffled backstory path
        public static void BackstorySelectionWeight_Postfix(BackstoryDef bs, ref float __result)
        {
            var ext = bs?.GetModExtension<BackstoryExtension>();
            if (ext == null) return;

            float m = ext.commonality;
            if (m < 0f) m = 0f; // clamp
            __result *= m;
        }

        // Solid bio path (uses both childhood and adulthood of the bio)
        public static void BioSelectionWeight_Postfix(PawnBio bio, ref float __result)
        {
            if (bio == null) return;

            float m = 1f;

            var child = bio.childhood;
            var adult = bio.adulthood;

            var childExt = child?.GetModExtension<BackstoryExtension>();
            var adultExt = adult?.GetModExtension<BackstoryExtension>();

            if (childExt != null)
            {
                float c = childExt.commonality;
                if (c < 0f) c = 0f;
                m *= c;
            }
            if (adultExt != null)
            {
                float a = adultExt.commonality;
                if (a < 0f) a = 0f;
                m *= a;
            }

            __result *= m;
        }
    }
}
