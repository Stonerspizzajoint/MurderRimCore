using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public static class AndroidReproUtils
    {

        /// <summary>
        /// Generates and spawns a new android pawn, applying genetics, family relationships, and specific spawn logic.
        /// </summary>
        public static Pawn SpawnAndroidOffspring(
            Thing parentStation,
            XenotypeDef xenotype,
            List<GeneDef> genes,
            Color skinColor,
            Color hairColor,
            Pawn parentA,
            Pawn parentB
        )
        {
            if (parentStation == null || parentStation.Map == null) return null;

            // 1. Create Request (Standard)
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: xenotype != null ? PawnKindDefOf.Colonist : PawnKindDefOf.Colonist,
                faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: true,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                developmentalStages: DevelopmentalStage.Newborn // Crucial
            );

            // 2. Generate
            Pawn baby = PawnGenerator.GeneratePawn(request);

            // 3. Apply Genetics & Cosmetics (As before...)
            if (baby.genes != null)
            {
                baby.genes.SetXenotype(xenotype);
                baby.genes.Endogenes.Clear();
                baby.genes.Xenogenes.Clear();
                if (genes != null)
                {
                    foreach (GeneDef g in genes) baby.genes.AddGene(g, true);
                }
            }
            if (baby.story != null)
            {
                baby.story.skinColorOverride = skinColor;
                baby.story.HairColor = hairColor;
            }
            if (baby.relations != null)
            {
                if (parentA != null) baby.relations.AddDirectRelation(PawnRelationDefOf.Parent, parentA);
                if (parentB != null && parentB != parentA) baby.relations.AddDirectRelation(PawnRelationDefOf.Parent, parentB);
            }

            // --- THE SANITIZATION PROTOCOLS ---

            // 4. WIPE IDEOLOGY
            // Drones start with a blank slate (No Ideo).
            if (baby.Ideo != null)
            {
                baby.ideo.SetIdeo(null); // Remove ideo membership
            }

            // 5. FORCE BACKSTORY (Stage 0 = Newborn)
            ApplyStageBackstory(baby, 0);

            // 6. RESET SKILLS
            // Even with the backstory, sometimes generation adds skill points. Wipe them.
            if (baby.skills != null)
            {
                foreach (SkillRecord skill in baby.skills.skills)
                {
                    skill.Level = 0;
                    skill.passion = Passion.None; // Optional: Maybe keep passions from genes?
                    skill.xpSinceLastLevel = 0;
                }
            }

            // 7. APPLY CHASSIS & SPAWN
            Hediff chassis = baby.health.AddHediff(AndroidRep_DefOf.MRC_AndroidChildhoodMarker);
            chassis.Severity = 0.01f;

            IntVec3 spawnSpot = parentStation.InteractionCell + IntVec3.East;
            if (!spawnSpot.Walkable(parentStation.Map)) spawnSpot = parentStation.InteractionCell;

            GenSpawn.Spawn(baby, spawnSpot, parentStation.Map);
            
            // 8. VISUALS
            baby.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(baby);

            return baby;
        }

        /// <summary>
        /// Calculates the total metabolic complexity of a pawn's genes.
        /// </summary>
        public static int GetSystemComplexity(Pawn p)
        {
            if (p?.genes == null) return 0;
            int sum = 0;
            foreach (var g in p.genes.GenesListForReading)
            {
                sum += g.def.biostatCpx;
            }
            return sum;
        }

        /// <summary>
        /// Checks validity for fusion window.
        /// </summary>
        public static bool IsValidCandidate(Pawn p, out string reason)
        {
            reason = null;
            var settings = AndroidReproductionSettingsDef.Current;
            if (settings == null) return true;

            if (!Utils.IsAndroid(p))
            {
                reason = "Not Android";
                return false;
            }

            if (settings.requireAwakened && !Utils.IsAwakened(p))
            {
                reason = "Not Awakened";
                return false;
            }

            if (p.ageTracker.AgeBiologicalYearsFloat < settings.minAge)
            {
                reason = $"Too Young (<{settings.minAge})";
                return false;
            }

            bool hasPartner = LovePartnerRelationUtility.HasAnyLovePartner(p);
            if (settings.requireLover && !hasPartner)
            {
                reason = "No Partner";
                return false;
            }

            if (settings.requireLoverOnMap && hasPartner)
            {
                Pawn partner = LovePartnerRelationUtility.ExistingLovePartner(p);
                if (partner == null || partner.Map != p.Map || partner.Dead)
                {
                    reason = "Signal Lost (Absent)";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks compatibility between two specific pawns.
        /// </summary>
        public static bool IsCompatiblePair(Pawn a, Pawn b, out string failReason)
        {
            failReason = null;
            var settings = AndroidReproductionSettingsDef.Current;

            if (a == b) { failReason = "Self-Targeting"; return false; }

            if (settings != null && settings.requireLover)
            {
                if (!LovePartnerRelationUtility.LovePartnerRelationExists(a, b))
                {
                    failReason = "Signal Mismatch (Not Partners)";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a specific GeneDef conflicts with any genes in the provided list via Exclusion Tags.
        /// </summary>
        public static bool IsGeneConflicting(GeneDef newGene, List<GeneDef> currentGenes)
        {
            if (newGene == null || currentGenes == null) return false;

            // If the new gene has no exclusion tags, it likely conflicts with nothing (unless hardcoded elsewhere)
            if (newGene.exclusionTags.NullOrEmpty()) return false;

            foreach (var existing in currentGenes)
            {
                if (existing.exclusionTags.NullOrEmpty()) continue;

                // Check for intersection of tags
                foreach (string tag in newGene.exclusionTags)
                {
                    if (existing.exclusionTags.Contains(tag)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the pawn is a "Born Android" (Baby Drone) that should be exempt from VRE forced aging.
        /// </summary>
        public static bool IsAndroidBorn(Pawn p)
        {
            if (p == null || p.health == null) return false;

            // Ensure DefOf is initialized to avoid startup crashes
            if (AndroidRep_DefOf.MRC_AndroidChildhoodMarker == null) return false;

            return p.health.hediffSet.HasHediff(AndroidRep_DefOf.MRC_AndroidChildhoodMarker);
        }

        /// <summary>
        /// Updates the pawn's childhood backstory to match their current simulation stage.
        /// </summary>
        /// <param name="pawn">The android pawn.</param>
        /// <param name="stageIndex">0 = Newborn, 1 = Child, 2+ = Colony Born (Adult)</param>
        public static void ApplyStageBackstory(Pawn pawn, int stageIndex)
        {
            if (pawn == null || pawn.story == null) return;

            BackstoryDef targetStory = null;

            // Select the correct Def based on the stage index
            switch (stageIndex)
            {
                case 0: // Newborn (The Pill)
                    targetStory = AndroidRep_DefOf.MRC_AndroidNewborn;
                    break;

                case 1: // Child (The Prototype)
                    targetStory = AndroidRep_DefOf.MRC_AndroidChild;
                    break;

                case 2: // Adult (The Final Form)
                case 3:
                    targetStory = AndroidRep_DefOf.MRC_AndroidColonyBorn;
                    break;
            }

            // Only apply if it's different to avoid unnecessary updates
            if (targetStory != null && pawn.story.Childhood != targetStory)
            {
                pawn.story.Childhood = targetStory;

                // If they are a newborn or child, ensure they have NO adulthood story
                if (stageIndex < 2)
                {
                    pawn.story.Adulthood = null;
                }
            }
        }
    }
}