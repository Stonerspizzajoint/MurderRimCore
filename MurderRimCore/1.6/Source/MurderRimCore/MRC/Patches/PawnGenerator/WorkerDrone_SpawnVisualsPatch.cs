using HarmonyLib;
using RimWorld;
using Verse;
using MurderRimCore.MRWD;

namespace MurderRimCore
{
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class WorkerDrone_SpawnVisualsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, ref PawnGenerationRequest request, ref string error, bool ignoreScenarioRequirements, bool ignoreValidator)
        {
            var pawn = __result;
            if (pawn == null) return;

            if (!DroneHelper.IsWorkerDrone(pawn))
                return;

            var s = MurderRimCoreMod.SpawnSettings;

            // Fallback to Def if the mod settings somehow failed to load (unlikely, but safety is sweet).
            if (s == null) s = WorkerDroneSettingsDef.Spawn;

            if (s == null) return;

            // First, make sure hair is never a "shaved" style
            SanitizeHair(pawn);

            bool isNowBald = ApplyBaldOrHair(pawn, s);
            ApplyBeard(pawn, s);
            ApplyHelmet(pawn, s, forceHelmet: isNowBald);
        }

        private static void SanitizeHair(Pawn pawn)
        {
            if (pawn.story == null || pawn.story.hairDef == null || !pawn.RaceProps.Humanlike)
                return;

            // If current hair is tagged as shaved, pick a non‑shaved replacement.
            if (!IsShavedHair(pawn.story.hairDef))
                return;

            var allHair = DefDatabase<HairDef>.AllDefsListForReading;
            if (allHair == null || allHair.Count == 0)
                return;

            // Filter out shaved‑tagged hair
            var validHair = allHair.FindAll(h => !IsShavedHair(h));

            if (validHair.Count > 0)
            {
                pawn.story.hairDef = validHair.RandomElement();
            }
            else
            {
                // Fallback: hard bald if literally everything else is shaved.
                var bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
                if (bald != null)
                    pawn.story.hairDef = bald;
            }
        }

        private static bool ApplyBaldOrHair(Pawn pawn, WorkerDroneSpawnModSettings s)
        {
            if (pawn.story == null || !pawn.RaceProps.Humanlike) return false;

            bool makeBald = false;
            if (pawn.gender == Gender.Male && Rand.Value < s.baldChanceMale)
                makeBald = true;
            else if (pawn.gender == Gender.Female && Rand.Value < s.baldChanceFemale)
                makeBald = true;

            if (!makeBald) return false;

            var bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
            if (bald != null)
            {
                pawn.story.hairDef = bald;
                return true;
            }

            return false;
        }

        private static void ApplyBeard(Pawn pawn, WorkerDroneSpawnModSettings s)
        {
            if (pawn.style == null || !pawn.RaceProps.Humanlike)
                return;

            // Females: never beards
            if (pawn.gender == Gender.Female)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            if (!s.allowBeards)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            if (Rand.Value >= s.beardSpawnChance)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            var allBeards = DefDatabase<BeardDef>.AllDefsListForReading;
            if (allBeards == null || allBeards.Count == 0)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }

            BeardDef chosen = null;

            if (s.mustachesOnlyWhenBeardsAllowed)
            {
                var moustachesOnly = allBeards.FindAll(b =>
                    b.styleTags != null &&
                    b.styleTags.Contains("MoustacheOnly") &&
                    !IsShavedBeard(b));

                if (moustachesOnly.Count == 0)
                {
                    pawn.style.beardDef = BeardDefOf.NoBeard;
                    return;
                }

                chosen = moustachesOnly.RandomElement();
            }
            else
            {
                var validBeards = allBeards.FindAll(b => !IsShavedBeard(b));

                if (validBeards.Count == 0)
                {
                    pawn.style.beardDef = BeardDefOf.NoBeard;
                    return;
                }

                chosen = validBeards.RandomElement();
            }

            pawn.style.beardDef = chosen ?? BeardDefOf.NoBeard;
        }

        private static void ApplyHelmet(Pawn pawn, WorkerDroneSpawnModSettings s, bool forceHelmet)
        {
            if (pawn.apparel == null) return;

            // If not forced, respect base chance.
            if (!forceHelmet && Rand.Value >= s.baseHelmetSpawnChance)
                return;

            var helmetDef = MRWD_DefOf.MRWD_Headgear_Hardhat;
            if (helmetDef == null)
                return;

            var helmet = ThingMaker.MakeThing(helmetDef) as Apparel;
            if (helmet == null)
                return;

            if (ModsConfig.IdeologyActive && s.matchFavoriteColorWhenIdeology && pawn.story != null)
            {
                if (pawn.story.favoriteColor != null && Rand.Value < s.favoriteColorHelmetChance)
                {
                    var compColorable = helmet.TryGetComp<CompColorable>();
                    if (compColorable != null)
                    {
                        compColorable.SetColor(pawn.story.favoriteColor.color);
                    }
                }
            }

            pawn.apparel.Wear(helmet, dropReplacedApparel: true);
        }

        // Helpers to detect "shaved" styles

        private static bool IsShavedHair(HairDef h)
        {
            if (h == null)
                return true;

            if (h.styleTags != null && h.styleTags.Contains("Shaved"))
                return true;

            return false;
        }

        private static bool IsShavedBeard(BeardDef b)
        {
            if (b == null)
                return true;

            if (b.styleTags != null && b.styleTags.Contains("Shaved"))
                return true;

            return false;
        }
    }
}