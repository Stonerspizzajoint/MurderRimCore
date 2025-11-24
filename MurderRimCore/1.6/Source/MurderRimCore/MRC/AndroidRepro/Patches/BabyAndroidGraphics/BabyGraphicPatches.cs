using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MurderRimCore.AndroidRepro
{
    internal static class AndroidLifeStageGraphicsFinder
    {
        internal static HediffComp_AndroidLifeStageGraphics Find(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return null;
            var list = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i].TryGetComp<HediffComp_AndroidLifeStageGraphics>();
                if (c != null) return c;
            }
            return null;
        }
    }

    // ========== HEAD GRAPHIC ==========

    [HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
    public static class Patch_AndroidHead_GraphicFor
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn pawn, ref Graphic __result)
        {
            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            var path = cfg.headTexPath != null ? cfg.headTexPath.Trim() : null;
            if (string.IsNullOrEmpty(path)) return;

            var custom = GraphicDatabase.Get<Graphic_Multi>(
                path,
                ShaderDatabase.Cutout,
                Vector2.one,
                Color.white
            );
            if (custom != null) __result = custom;
        }
    }

    // ========== HEAD SCALE ==========

    [HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.MeshSetFor))]
    public static class Patch_AndroidHead_MeshScale
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderNode_Head __instance, Pawn pawn, ref GraphicMeshSet __result)
        {
            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            float scale = Mathf.Clamp(cfg.headScale, 0.25f, 3f);
            if (Mathf.Approximately(scale, 1f)) return;

            __result = MeshPool.GetMeshSetForSize(scale, scale);
        }
    }

    // ========== BODY TRANSPARENCY ==========

    [HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
    public static class Patch_AndroidBody_GraphicFor
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(Pawn pawn, PawnRenderNode_Body __instance, ref Graphic __result)
        {
            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return true;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return true;

            var bodyPath = cfg.transparentBodyTexPath != null ? cfg.transparentBodyTexPath.Trim() : null;
            if (string.IsNullOrEmpty(bodyPath)) return true;

            var shader = __instance.ShaderFor(pawn);
            if (shader == null) return true;

            __result = GraphicDatabase.Get<Graphic_Multi>(
                bodyPath,
                shader,
                Vector2.one,
                Color.white
            );
            return false;
        }
    }

    // ========== HEAD POSITIONAL OFFSET ==========

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BaseHeadOffsetAt))]
    public static class Patch_AndroidHead_BaseHeadOffsetAt
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderer __instance, Rot4 rotation, ref Vector3 __result)
        {
            var pawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
            var pawn = pawnField != null ? (Pawn)pawnField.GetValue(__instance) : null;
            if (pawn == null) return;

            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            __result.x += cfg.offsetX;
            __result.z += cfg.offsetZ;

            if (!Mathf.Approximately(cfg.headLayerOffsetY, 0f))
            {
                __result.y += cfg.headLayerOffsetY;
            }
        }
    }

    // ========== APPAREL / HEADGEAR FLAGS ==========

    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_AndroidHead_GetDrawParms
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderer __instance, ref PawnDrawParms __result)
        {
            var pawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
            var pawn = pawnField != null ? (Pawn)pawnField.GetValue(__instance) : null;
            if (pawn == null) return;

            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            var flags = __result.flags;
            if (cfg.hideBodyApparel)
                flags &= ~PawnRenderFlags.Clothes;
            if (cfg.hideHeadgear)
                flags &= ~PawnRenderFlags.Headgear;

            __result.flags = flags;
        }
    }

    /// <summary>
    /// Suppresses vanilla swaddle render nodes for android infants that use our
    /// custom life-stage graphics (HediffComp_AndroidLifeStageGraphics).
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Swaddle), nameof(PawnRenderNodeWorker_Swaddle.CanDrawNow))]
    public static class Patch_Android_SuppressSwaddle
    {
        [HarmonyPostfix]
        public static void Postfix(PawnRenderNodeWorker_Swaddle __instance, PawnDrawParms parms, ref bool __result)
        {
            // If something else already decided "don't draw", respect that.
            if (!__result)
                return;

            Pawn pawn = parms.pawn;
            if (pawn == null)
                return;

            // Only care about androids with our life-stage graphics comp.
            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null)
                return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null)
                return;

            // At this point, we know:
            // - Pawn is an android (your comp checks Utils.IsAndroid)
            // - Pawn has an active life-stage config
            // For these, we don't want the vanilla swaddle to show.
            __result = false;
        }
    }

    // ========== EYE OFFSET & SCALE ==========

    [HarmonyPatch(typeof(PawnRenderNodeWorker_Eye))]
    public static class Patch_AndroidEyeWorker_OffsetAndScale
    {
        // Position adjustment
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnRenderNodeWorker_Eye.OffsetFor))]
        public static void OffsetFor_Postfix(
            PawnRenderNodeWorker_Eye __instance,
            PawnRenderNode node,
            PawnDrawParms parms,
            ref Vector3 __result,
            ref Vector3 pivot)
        {
            Pawn pawn = parms.pawn;
            if (pawn == null) return;

            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            // 1) Base offset
            if (!Mathf.Approximately(cfg.eyeOffsetX, 0f) ||
                !Mathf.Approximately(cfg.eyeOffsetY, 0f) ||
                !Mathf.Approximately(cfg.eyeOffsetZ, 0f))
            {
                __result.x += cfg.eyeOffsetX;
                __result.y += cfg.eyeOffsetY;
                __result.z += cfg.eyeOffsetZ;
            }

            // 2) Directional offset
            float dx = 0f;
            float dy = 0f;
            float dz = 0f;

            switch (parms.facing.AsInt)
            {
                case 2: // South
                    dx = cfg.eyeSouthOffsetX;
                    dy = cfg.eyeSouthOffsetY;
                    dz = cfg.eyeSouthOffsetZ;
                    break;

                case 1: // East
                    dx = cfg.eyeEastOffsetX;
                    dy = cfg.eyeEastOffsetY;
                    dz = cfg.eyeEastOffsetZ;
                    break;

                case 3: // West
                    dx = cfg.eyeWestOffsetX;
                    dy = cfg.eyeWestOffsetY;
                    dz = cfg.eyeWestOffsetZ;
                    break;

                // North (0) – usually not rendered for eyes
                default:
                    break;
            }

            if (!Mathf.Approximately(dx, 0f) ||
                !Mathf.Approximately(dy, 0f) ||
                !Mathf.Approximately(dz, 0f))
            {
                __result.x += dx;
                __result.y += dy;
                __result.z += dz;
            }
        }

        // Scale adjustment
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnRenderNodeWorker_Eye.ScaleFor))]
        public static void ScaleFor_Postfix(
            PawnRenderNodeWorker_Eye __instance,
            PawnRenderNode node,
            PawnDrawParms parms,
            ref Vector3 __result)
        {
            Pawn pawn = parms.pawn;
            if (pawn == null) return;

            var comp = AndroidLifeStageGraphicsFinder.Find(pawn);
            if (comp == null) return;

            var cfg = comp.GetActiveConfig();
            if (cfg == null) return;

            float scale = Mathf.Clamp(cfg.eyeScale, 0.25f, 3f);
            if (Mathf.Approximately(scale, 1f)) return;

            __result *= scale;
        }

        [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow))]
        public static class Patch_MRC_HediffEyes_GeneOverride
        {
            // This must match the debugLabel in your HediffDef renderNodeProperties
            private const string HediffEyeDebugLabel = "MRC_BabyEyes";

            [HarmonyPriority(Priority.Last)]
            public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
            {
                // If something already decided not to draw this node, don't revive it.
                if (!__result) return;
                if (node == null) return;

                Pawn pawn = parms.pawn;
                if (pawn == null) return;

                // Only care about our hediff-supplied eye node.
                var propsField = AccessTools.Field(node.GetType(), "props");
                var props = propsField != null
                    ? propsField.GetValue(node) as PawnRenderNodeProperties
                    : null;

                if (props == null || props.debugLabel != HediffEyeDebugLabel)
                    return;

                // Pull current stage config, so we respect useHediffEyeWhenNoGene
                var gfxComp = AndroidLifeStageGraphicsFinder.Find(pawn);
                var cfg = gfxComp != null ? gfxComp.GetActiveConfig() : null;

                // If we have no config or this stage doesn't allow hediff eyes as fallback,
                // never draw this node for this stage.
                if (cfg == null || !cfg.useHediffEyeWhenNoGene)
                {
                    __result = false;
                    return;
                }

                // Stage allows fallback eyes, but we must still defer to the gene if present.
                try
                {
                    if (pawn.genes != null &&
                        pawn.genes.HasActiveGene(MRC_AndroidRepro_DefOf.MRWD_DroneBody))
                    {
                        // Gene present → use gene eyes instead, hide hediff eyes.
                        __result = false;
                    }
                    // Else: no gene, stage says "useHediffEyeWhenNoGene == true" → keep __result == true.
                }
                catch
                {
                    // If genes system isn't present for some cursed pawn, just leave __result as-is.
                }
            }
        }
    }
}