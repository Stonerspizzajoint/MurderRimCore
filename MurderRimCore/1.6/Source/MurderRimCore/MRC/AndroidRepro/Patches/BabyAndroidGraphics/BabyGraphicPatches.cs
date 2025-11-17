using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using VEF;

namespace MurderRimCore.AndroidRepro
{
    internal static class SimpleBabyHeadFinder
    {
        internal static HediffComp_SimpleBabyHead Find(Pawn pawn)
        {
            if (pawn?.health == null) return null;
            var list = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i].TryGetComp<HediffComp_SimpleBabyHead>();
                if (c != null) return c;
            }
            return null;
        }
    }

    // 1) Replace head texture at the head node
    [HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
    public static class Patch_HeadNode_GraphicFor
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn pawn, ref Graphic __result)
        {
            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            var path = comp.Props.headTexPath?.Trim();
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

    // Scales the head mesh set while the hediff is present.
    [HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.MeshSetFor))]
    public static class Patch_HeadMeshScale
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderNode_Head __instance, Pawn pawn, ref GraphicMeshSet __result)
        {
            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            float scale = Mathf.Clamp(comp.Props.headScale, 0.25f, 3f);
            if (Mathf.Approximately(scale, 1f)) return; // No change

            // Replace with scaled mesh set (uniform scaling).
            __result = MeshPool.GetMeshSetForSize(scale, scale);
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker_Eye))]
    public static class Patch_EyeWorker_OffsetAndScale
    {
        // Position adjustment
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnRenderNodeWorker_Eye.OffsetFor))]
        public static void OffsetFor_Postfix(PawnRenderNodeWorker_Eye __instance,
                                             PawnRenderNode node,
                                             PawnDrawParms parms,
                                             ref Vector3 __result,
                                             ref Vector3 pivot)
        {
            var pawn = parms.pawn;
            if (pawn == null) return;

            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            var p = comp.Props;

            // 1) Apply base offset
            if (!Mathf.Approximately(p.eyeOffsetX, 0f) ||
                !Mathf.Approximately(p.eyeOffsetY, 0f) ||
                !Mathf.Approximately(p.eyeOffsetZ, 0f))
            {
                __result.x += p.eyeOffsetX;
                __result.y += p.eyeOffsetY;
                __result.z += p.eyeOffsetZ;
            }

            // 2) Add directional offset on top of base
            float dx = 0f, dy = 0f, dz = 0f;
            switch (parms.facing.AsInt)
            {
                case 2: // South
                    dx = p.eyeSouthOffsetX;
                    dy = p.eyeSouthOffsetY;
                    dz = p.eyeSouthOffsetZ;
                    break;

                case 1: // East
                    dx = p.eyeEastOffsetX;
                    dy = p.eyeEastOffsetY;
                    dz = p.eyeEastOffsetZ;
                    break;

                case 3: // West
                    dx = p.eyeWestOffsetX;
                    dy = p.eyeWestOffsetY;
                    dz = p.eyeWestOffsetZ;
                    break;

                // North (0) – your omni node usually doesn’t render north eyes; ignore for now.
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
        public static void ScaleFor_Postfix(PawnRenderNodeWorker_Eye __instance,
                                            PawnRenderNode node,
                                            PawnDrawParms parms,
                                            ref Vector3 __result)
        {
            var pawn = parms.pawn;
            if (pawn == null) return;

            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            float scale = Mathf.Clamp(comp.Props.eyeScale, 0.25f, 3f);
            if (Mathf.Approximately(scale, 1f)) return;

            __result *= scale;
        }
    }

    // 2) Make body invisible by returning your transparent body graphic at the body node
    [HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
    public static class Patch_BodyNode_GraphicFor
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(Pawn pawn, PawnRenderNode_Body __instance, ref Graphic __result)
        {
            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return true; // run vanilla

            var bodyPath = comp.Props.transparentBodyTexPath?.Trim();
            if (string.IsNullOrEmpty(bodyPath)) return true; // no override -> run vanilla

            // Use the same shader the node would use
            var shader = __instance.ShaderFor(pawn);
            if (shader == null) return true;

            // Return transparent body (must exist at the given base path with direction suffixes)
            __result = GraphicDatabase.Get<Graphic_Multi>(
                bodyPath,
                shader,
                Vector2.one,
                Color.white
            );
            return false; // skip vanilla body graphic selection
        }
    }

    // 3) Adjust head position (offset X/Z)
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BaseHeadOffsetAt))]
    public static class Patch_BaseHeadOffsetAt_Add
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderer __instance, Rot4 rotation, ref Vector3 __result)
        {
            var pawn = (Pawn)AccessTools.Field(typeof(PawnRenderer), "pawn").GetValue(__instance);
            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            var props = comp.Props;

            __result.x += props.offsetX;
            __result.z += props.offsetZ;

            // Layer tweak: nudge the head up/down in Y
            if (!Mathf.Approximately(props.headLayerOffsetY, 0f))
            {
                __result.y += props.headLayerOffsetY;
            }
        }
    }

    // 4) Optionally strip apparel/headgear flags so shirts/pants/hats don't render
    // Signature: private PawnDrawParms GetDrawParms(Vector3, float, Rot4, RotDrawMode, PawnRenderFlags)
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_GetDrawParms_FilterApparel
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(PawnRenderer __instance, ref PawnDrawParms __result)
        {
            var pawn = (Pawn)AccessTools.Field(typeof(PawnRenderer), "pawn").GetValue(__instance);
            var comp = SimpleBabyHeadFinder.Find(pawn);
            if (comp == null) return;

            var flags = __result.flags;
            if (comp.Props.hideBodyApparel)
                flags &= ~PawnRenderFlags.Clothes;
            if (comp.Props.hideHeadgear)
                flags &= ~PawnRenderFlags.Headgear;

            __result.flags = flags;
        }
    }
}