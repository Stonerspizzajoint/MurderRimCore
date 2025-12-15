using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids; // direct reference for Utils.IsAndroid

namespace MurderRimCore
{
    // Shared helpers + strict config validation (C# 7.3 compatible)
    public static class SilhouetteUtil
    {
        private static GeneDef _silhouetteGene;
        private static SilhouetteGeneExtension _ext;

        private static HashSet<ThoughtDef> _faceThoughts;
        private static HashSet<ThingDef> _targets;
        private static bool _initialized;

        private static readonly FieldInfo MemorySocialOtherPawnFI =
            AccessTools.Field(typeof(Thought_MemorySocial), "otherPawn");
        private static readonly FieldInfo SituationalSocialOtherPawnFI =
            AccessTools.Field(typeof(Thought_SituationalSocial), "otherPawn");

        // Use your DefOf reference instead of string lookup
        private static GeneDef SilhouetteGene
        {
            get
            {
                if (_silhouetteGene == null)
                {
                    _silhouetteGene = MRC_DefOf.MRC_SilhouettePerception;
                }
                return _silhouetteGene;
            }
        }

        private static SilhouetteGeneExtension Ext
        {
            get
            {
                if (_ext == null)
                {
                    var gene = SilhouetteGene;
                    if (gene != null)
                    {
                        _ext = gene.GetModExtension<SilhouetteGeneExtension>();
                    }
                }
                return _ext;
            }
        }

        private static void Fail(string message)
        {
            Log.Error($"[MD Silhouette Perception] {message}");
            throw new InvalidOperationException($"MD Silhouette Perception: {message}");
        }

        // Call this from your central bootstrap AFTER defs are loaded.
        public static void ValidateConfig()
        {
            InitIfNeeded();
        }

        private static void InitIfNeeded()
        {
            if (_initialized) return;

            _faceThoughts = new HashSet<ThoughtDef>();
            _targets = new HashSet<ThingDef>();

            if (SilhouetteGene == null)
                Fail("MRC_DefOf.MRC_SilhouettePerception returned null. Ensure your DefOf is set up and defs are loaded before ValidateConfig.");

            if (Ext == null)
                Fail($"Gene '{SilhouetteGene.defName}' is missing {nameof(SilhouetteGeneExtension)}.");

            if (Ext.faceDependentThoughtDefNames == null)
                Fail($"Gene '{SilhouetteGene.defName}' must specify faceDependentThoughtDefNames (can be empty, but not null).");

            if (Ext.silhouetteTargetThingDefNames == null)
                Fail($"Gene '{SilhouetteGene.defName}' must specify silhouetteTargetThingDefNames (can be empty, but not null).");

            foreach (var name in Ext.faceDependentThoughtDefNames)
            {
                var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                if (def == null)
                    Fail($"Unknown ThoughtDef '{name}' referenced by gene '{SilhouetteGene.defName}'.");
                _faceThoughts.Add(def);
            }

            foreach (var name in Ext.silhouetteTargetThingDefNames)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null)
                    Fail($"Unknown ThingDef '{name}' referenced by gene '{SilhouetteGene.defName}'.");
                _targets.Add(def);
            }

            _initialized = true;
        }

        public static bool HasSilhouettePerception(this Pawn p)
        {
            return p != null && p.genes != null && p.genes.HasActiveGene(SilhouetteGene);
        }

        public static bool IsSilhouetteTarget(this Pawn p)
        {
            InitIfNeeded();
            if (p == null || p.def == null) return false;

            // VRE Androids are NEVER seen as silhouettes
            if (Utils.IsAndroid(p))
                return false;

            // Otherwise, rely on configured target races
            return _targets.Contains(p.def);
        }

        public static bool IsFaceDependent(this ThoughtDef def)
        {
            InitIfNeeded();
            if (def == null) return false;
            return _faceThoughts.Contains(def);
        }

        public static Pawn GetOtherPawn(Thought_MemorySocial thought)
        {
            if (thought == null || MemorySocialOtherPawnFI == null) return null;
            return MemorySocialOtherPawnFI.GetValue(thought) as Pawn;
        }

        public static Pawn GetOtherPawn(Thought_SituationalSocial thought)
        {
            if (thought == null || SituationalSocialOtherPawnFI == null) return null;
            return SituationalSocialOtherPawnFI.GetValue(thought) as Pawn;
        }
    }
}