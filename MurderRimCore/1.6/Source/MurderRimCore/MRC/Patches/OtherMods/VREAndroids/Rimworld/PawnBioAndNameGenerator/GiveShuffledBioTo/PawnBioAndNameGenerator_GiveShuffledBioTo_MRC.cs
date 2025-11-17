using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore.Patches
{
    // Creation Station / shuffled bio path:
    // Force the resolved category (ColonyDrone/ColonyAndroid for unawakened; Worker/Awakened for awakened),
    // delegate to Utils.TryAssignBackstory (our other patch handles weighted pick + forced traits),
    // set pawn name, and skip vanilla.
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveShuffledBioTo")]
    public static class PawnBioAndNameGenerator_GiveShuffledBioTo_MRC
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(
            Pawn pawn,
            FactionDef factionType,
            string requiredLastName,
            List<BackstoryCategoryFilter> backstoryCategories,
            bool forceNoBackstory = false,
            bool forceNoNick = false,
            XenotypeDef xenotype = null)
        {
            if (pawn == null) return true;

            string cat = BackstoryCategoryResolver.ResolveCategory(pawn);
            if (cat == null)
            {
                // Not android/drone; let vanilla continue
                return true;
            }

            Utils.TryAssignBackstory(pawn, cat);
            pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(pawn, NameStyle.Full, requiredLastName, forceNoNick, xenotype);
            return false;
        }
    }
}