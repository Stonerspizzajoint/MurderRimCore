using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    public enum AndroidGraphicsStageKind
    {
        Newborn,
        Teen,
        Adult
    }

    public class AndroidLifeStageGraphicsStage
    {
        public AndroidGraphicsStageKind stage;

        public string headTexPath;
        public string transparentBodyTexPath;

        public float? offsetX;
        public float? offsetZ;
        public float? headScale;
        public float? headLayerOffsetY;

        public float? eyeScale;

        public float? eyeOffsetX;
        public float? eyeOffsetY;
        public float? eyeOffsetZ;

        public float? eyeSouthOffsetX;
        public float? eyeSouthOffsetY;
        public float? eyeSouthOffsetZ;

        public float? eyeEastOffsetX;
        public float? eyeEastOffsetY;
        public float? eyeEastOffsetZ;

        public float? eyeWestOffsetX;
        public float? eyeWestOffsetY;
        public float? eyeWestOffsetZ;

        public bool? hideBodyApparel;
        public bool? hideHeadgear;

        // For this stage, use hediff eye when no MRWD_DroneBody gene is present
        public bool? useHediffEyeWhenNoGene;
    }

    public class HediffCompProperties_AndroidLifeStageGraphics : HediffCompProperties
    {
        // Base/default values
        public string headTexPath;
        public string transparentBodyTexPath;

        public float offsetX = 0f;
        public float offsetZ = 0f;
        public float headScale = 1f;
        public float headLayerOffsetY = 0f;

        public float eyeScale = 1f;

        public float eyeOffsetX = 0f;
        public float eyeOffsetY = 0f;
        public float eyeOffsetZ = 0f;

        public float eyeSouthOffsetX = 0f;
        public float eyeSouthOffsetY = 0f;
        public float eyeSouthOffsetZ = 0f;

        public float eyeEastOffsetX = 0f;
        public float eyeEastOffsetY = 0f;
        public float eyeEastOffsetZ = 0f;

        public float eyeWestOffsetX = 0f;
        public float eyeWestOffsetY = 0f;
        public float eyeWestOffsetZ = 0f;

        public bool hideBodyApparel = false;
        public bool hideHeadgear = false;

        // Base default: if a stage doesn't specify, this is the fallback behavior
        public bool useHediffEyeWhenNoGene = false;

        // Per-stage overrides
        public List<AndroidLifeStageGraphicsStage> stages = new List<AndroidLifeStageGraphicsStage>();

        public HediffCompProperties_AndroidLifeStageGraphics()
        {
            compClass = typeof(HediffComp_AndroidLifeStageGraphics);
        }

        public ResolvedGraphicsConfig ResolveForGrowthStage(AndroidGrowthStage growthStage)
        {
            AndroidGraphicsStageKind kind;

            // Only Newborn + Teen are special:
            // - NewbornPill => Newborn graphics
            // - TeenFrame   => Adult graphics (body fully grown)
            // Everything else => Adult graphics
            switch (growthStage)
            {
                case AndroidGrowthStage.NewbornPill:
                    kind = AndroidGraphicsStageKind.Newborn;
                    break;

                case AndroidGrowthStage.TeenFrame:
                    kind = AndroidGraphicsStageKind.Adult;
                    break;

                default:
                    kind = AndroidGraphicsStageKind.Adult;
                    break;
            }

            AndroidLifeStageGraphicsStage stage = null;
            if (stages != null)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    var s = stages[i];
                    if (s != null && s.stage == kind)
                    {
                        stage = s;
                        break;
                    }
                }
            }

            var cfg = new ResolvedGraphicsConfig();

            cfg.headTexPath = stage != null && !string.IsNullOrEmpty(stage.headTexPath)
                ? stage.headTexPath
                : headTexPath;

            cfg.transparentBodyTexPath = stage != null && !string.IsNullOrEmpty(stage.transparentBodyTexPath)
                ? stage.transparentBodyTexPath
                : transparentBodyTexPath;

            cfg.offsetX = stage != null && stage.offsetX.HasValue ? stage.offsetX.Value : offsetX;
            cfg.offsetZ = stage != null && stage.offsetZ.HasValue ? stage.offsetZ.Value : offsetZ;
            cfg.headScale = stage != null && stage.headScale.HasValue ? stage.headScale.Value : headScale;
            cfg.headLayerOffsetY = stage != null && stage.headLayerOffsetY.HasValue ? stage.headLayerOffsetY.Value : headLayerOffsetY;

            cfg.eyeScale = stage != null && stage.eyeScale.HasValue ? stage.eyeScale.Value : eyeScale;

            cfg.eyeOffsetX = stage != null && stage.eyeOffsetX.HasValue ? stage.eyeOffsetX.Value : eyeOffsetX;
            cfg.eyeOffsetY = stage != null && stage.eyeOffsetY.HasValue ? stage.eyeOffsetY.Value : eyeOffsetY;
            cfg.eyeOffsetZ = stage != null && stage.eyeOffsetZ.HasValue ? stage.eyeOffsetZ.Value : eyeOffsetZ;

            cfg.eyeSouthOffsetX = stage != null && stage.eyeSouthOffsetX.HasValue ? stage.eyeSouthOffsetX.Value : eyeSouthOffsetX;
            cfg.eyeSouthOffsetY = stage != null && stage.eyeSouthOffsetY.HasValue ? stage.eyeSouthOffsetY.Value : eyeSouthOffsetY;
            cfg.eyeSouthOffsetZ = stage != null && stage.eyeSouthOffsetZ.HasValue ? stage.eyeSouthOffsetZ.Value : eyeSouthOffsetZ;

            cfg.eyeEastOffsetX = stage != null && stage.eyeEastOffsetX.HasValue ? stage.eyeEastOffsetX.Value : eyeEastOffsetX;
            cfg.eyeEastOffsetY = stage != null && stage.eyeEastOffsetY.HasValue ? stage.eyeEastOffsetY.Value : eyeEastOffsetY;
            cfg.eyeEastOffsetZ = stage != null && stage.eyeEastOffsetZ.HasValue ? stage.eyeEastOffsetZ.Value : eyeEastOffsetZ;

            cfg.eyeWestOffsetX = stage != null && stage.eyeWestOffsetX.HasValue ? stage.eyeWestOffsetX.Value : eyeWestOffsetX;
            cfg.eyeWestOffsetY = stage != null && stage.eyeWestOffsetY.HasValue ? stage.eyeWestOffsetY.Value : eyeWestOffsetY;
            cfg.eyeWestOffsetZ = stage != null && stage.eyeWestOffsetZ.HasValue ? stage.eyeWestOffsetZ.Value : eyeWestOffsetZ;

            cfg.hideBodyApparel = stage != null && stage.hideBodyApparel.HasValue ? stage.hideBodyApparel.Value : hideBodyApparel;
            cfg.hideHeadgear = stage != null && stage.hideHeadgear.HasValue ? stage.hideHeadgear.Value : hideHeadgear;

            cfg.useHediffEyeWhenNoGene = stage != null && stage.useHediffEyeWhenNoGene.HasValue
                ? stage.useHediffEyeWhenNoGene.Value
                : useHediffEyeWhenNoGene;

            return cfg;
        }
    }

    public class ResolvedGraphicsConfig
    {
        public string headTexPath;
        public string transparentBodyTexPath;

        public float offsetX;
        public float offsetZ;
        public float headScale;
        public float headLayerOffsetY;

        public float eyeScale;

        public float eyeOffsetX;
        public float eyeOffsetY;
        public float eyeOffsetZ;

        public float eyeSouthOffsetX;
        public float eyeSouthOffsetY;
        public float eyeSouthOffsetZ;

        public float eyeEastOffsetX;
        public float eyeEastOffsetY;
        public float eyeEastOffsetZ;

        public float eyeWestOffsetX;
        public float eyeWestOffsetY;
        public float eyeWestOffsetZ;

        public bool hideBodyApparel;
        public bool hideHeadgear;

        public bool useHediffEyeWhenNoGene;
    }

    public class HediffComp_AndroidLifeStageGraphics : HediffComp
    {
        public HediffCompProperties_AndroidLifeStageGraphics Props
        {
            get { return (HediffCompProperties_AndroidLifeStageGraphics)props; }
        }

        private ResolvedGraphicsConfig cachedConfig;
        private AndroidGrowthStage cachedGrowthStage = AndroidGrowthStage.None;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            ForceRefresh();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null) return;
            if (!Utils.IsAndroid(pawn)) return;

            AndroidGrowthStage stage;
            if (!AndroidGrowthUtil.TryGetGrowthStage(pawn, out stage) ||
                stage == AndroidGrowthStage.None)
                return;

            if (stage != cachedGrowthStage || cachedConfig == null)
            {
                cachedGrowthStage = stage;
                cachedConfig = Props.ResolveForGrowthStage(stage);
                ForceRefresh();
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            // No age forcing anymore. Just refresh visuals once if needed.
            ForceRefresh();
        }

        public ResolvedGraphicsConfig GetActiveConfig()
        {
            Pawn pawn = Pawn;
            if (pawn == null) return null;
            if (!Utils.IsAndroid(pawn)) return null;

            AndroidGrowthStage stage;
            if (!AndroidGrowthUtil.TryGetGrowthStage(pawn, out stage) ||
                stage == AndroidGrowthStage.None)
                return null;

            if (stage != cachedGrowthStage || cachedConfig == null)
            {
                cachedGrowthStage = stage;
                cachedConfig = Props.ResolveForGrowthStage(stage);
            }

            return cachedConfig;
        }

        private void ForceRefresh()
        {
            try
            {
                Pawn pawn = Pawn;
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
                if (pawn != null)
                {
                    PortraitsCache.SetDirty(pawn);
                }
            }
            catch { }
        }
    }
}