using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore.Patches
{
    [StaticConstructorOnStartup]
    public static class MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch
    {
        private static readonly Harmony H = new Harmony("murderrimcore.vreandroids.exclusiontags.highlight");
        private static Type BaseType;

        // State captured before DrawGene runs so we can compute the tile rect and disabled state
        private struct DrawState
        {
            public bool consider;      // only for unselected section (we only block clicks there)
            public bool disabled;      // this tile is incompatible with selected genes
            public float startX;       // curX before the original method advances it
            public float curY;
            public float packWidth;
            public Rect containingRect;
        }

        static MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch()
        {
            BaseType = AccessTools.TypeByName("VREAndroids.Window_CreateAndroidBase");
            if (BaseType == null) return;

            // Patch base and overrides on all subclasses
            PatchForType(BaseType, declaredOnly: false);
            foreach (var t in GetAllDerivedTypes(BaseType))
                PatchForType(t, declaredOnly: true);
        }

        private static void PatchForType(Type t, bool declaredOnly)
        {
            // DrawGene(GeneDef geneDef, bool selectedSection, ref float curX, float curY, float packWidth, Rect containingRect, bool isMatch)
            var drawGene = declaredOnly
                ? AccessTools.DeclaredMethod(t, "DrawGene", new[] { typeof(GeneDef), typeof(bool), typeof(float).MakeByRefType(), typeof(float), typeof(float), typeof(Rect), typeof(bool) })
                : AccessTools.Method(t, "DrawGene", new[] { typeof(GeneDef), typeof(bool), typeof(float).MakeByRefType(), typeof(float), typeof(float), typeof(Rect), typeof(bool) });
            if (drawGene != null)
            {
                H.Patch(drawGene,
                    prefix: new HarmonyMethod(typeof(MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch), nameof(DrawGene_Prefix)),
                    postfix: new HarmonyMethod(typeof(MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch), nameof(DrawGene_Postfix)));
            }

            // GeneTip(GeneDef geneDef, bool selectedSection) -> string
            var geneTip = declaredOnly
                ? AccessTools.DeclaredMethod(t, "GeneTip", new[] { typeof(GeneDef), typeof(bool) })
                : AccessTools.Method(t, "GeneTip", new[] { typeof(GeneDef), typeof(bool) });
            if (geneTip != null)
            {
                H.Patch(geneTip,
                    postfix: new HarmonyMethod(typeof(MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch), nameof(GeneTip_Postfix)));
            }

            // CanAccept()
            var canAccept = declaredOnly
                ? AccessTools.DeclaredMethod(t, "CanAccept", Type.EmptyTypes)
                : AccessTools.Method(t, "CanAccept", Type.EmptyTypes);
            if (canAccept != null)
            {
                H.Patch(canAccept,
                    postfix: new HarmonyMethod(typeof(MurderRimCore_VREAndroids_ExclusionTagsHighlight_Patch), nameof(CanAccept_Postfix)));
            }
        }

        private static IEnumerable<Type> GetAllDerivedTypes(Type baseType)
        {
            IEnumerable<Assembly> asms;
            try
            {
                asms = GenTypes.AllTypes?.Select(t => t.Assembly).Distinct() ?? AppDomain.CurrentDomain.GetAssemblies();
            }
            catch
            {
                asms = AppDomain.CurrentDomain.GetAssemblies();
            }

            foreach (var asm in asms)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (baseType.IsAssignableFrom(t) && t != baseType)
                        yield return t;
                }
            }
        }

        // Capture startX and precompute if this gene is disabled (conflicts with selected genes).
        private static void DrawGene_Prefix(object __instance, GeneDef geneDef, bool selectedSection, ref float curX, float curY, float packWidth, Rect containingRect, bool isMatch, ref DrawState __state)
        {
            __state = default;
            if (geneDef == null) return;

            __state.startX = curX;
            __state.curY = curY;
            __state.packWidth = packWidth;
            __state.containingRect = containingRect;

            // Only suppress selection in unselected section
            __state.consider = !selectedSection;

            if (!__state.consider) return;

            var selected = GetSelectedGenes(__instance);
            if (selected == null || selected.Count == 0) return;

            __state.disabled = SharesAnyExclusionTagWithAny(geneDef, selected);
        }

        // Draw conditional red overlay on hover; suppress clicks if disabled by selected genes (unselected section only).
        private static void DrawGene_Postfix(object __instance, GeneDef geneDef, bool selectedSection, ref float curX, float curY, float packWidth, Rect containingRect, bool isMatch, ref bool __result, DrawState __state)
        {
            // Determine if we should draw the overlay for this tile based on what's being hovered.
            var hovered = GetHoveredGene(__instance);
            bool overlay = false;

            if (hovered != null && geneDef != null)
            {
                var selected = GetSelectedGenes(__instance) ?? new List<GeneDef>(0);
                bool hoveredIsSelected = selected.Contains(hovered);

                // Hover triggers:
                // - If hovering a selected gene: overlay all genes that share exclusion tags with the hovered selected gene.
                // - If hovering an unselected gene that is incompatible with the current selection: overlay that hovered gene AND all genes sharing tags with it.
                if (hoveredIsSelected)
                {
                    overlay = (geneDef != hovered) && ShareAnyTag(geneDef, hovered);
                }
                else
                {
                    bool hoveredIsDisabled = SharesAnyExclusionTagWithAny(hovered, selected);
                    if (hoveredIsDisabled)
                    {
                        if (geneDef == hovered)
                            overlay = true;
                        else
                            overlay = ShareAnyTag(geneDef, hovered);
                    }
                }
            }

            if (overlay)
            {
                var rect = new Rect(__state.startX, __state.curY, __state.packWidth, GeneCreationDialogBase.GeneSize.y + 8f);
                if (__state.containingRect.Overlaps(rect))
                {
                    var overlayCol = new Color(0.6f, 0f, 0f, 0.18f);
                    Widgets.DrawBoxSolid(rect, overlayCol);
                }
            }

            // Prevent toggling selection if this unselected tile is incompatible with the already-selected genes
            if (__state.consider && __state.disabled && __result)
            {
                __result = false;
                // Optional: SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        // Add a concise incompatibility line only when a gene is incompatible with current selection (unselected list),
        // and strip the "Click to add" hint when incompatible.
        private static void GeneTip_Postfix(object __instance, GeneDef geneDef, bool selectedSection, ref string __result)
        {
            if (geneDef == null || selectedSection) return;

            var selected = GetSelectedGenes(__instance);
            if (selected == null || selected.Count == 0) return;

            // Which selected genes conflict with this one?
            var conflicts = ConflictingGenes(geneDef, selected);
            if (conflicts.Count == 0) return;

            // Remove the "Click to add" hint if present (to avoid misleading the user)
            __result = RemoveClickToAddLine(__result);

            // Append concise note listing only conflicting gene names
            var geneList = string.Join(", ", conflicts.Select(g => g.LabelCap));
            var add = ("Incompatible with " + geneList + ".").Colorize(ColorLibrary.RedReadable);

            __result = string.IsNullOrEmpty(__result) ? add : (__result + "\n" + add);
        }

        // Final safeguard for presets or defaults that include conflicting pairs
        private static void CanAccept_Postfix(object __instance, ref bool __result)
        {
            if (!__result) return;

            var selected = GetSelectedGenes(__instance);
            if (selected == null || selected.Count < 2) return;

            var seen = new Dictionary<string, GeneDef>(StringComparer.Ordinal);
            foreach (var g in selected)
            {
                if (g?.exclusionTags == null) continue;
                foreach (var tag in g.exclusionTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    if (seen.TryGetValue(tag, out var prev) && prev != null && prev != g)
                    {
                        Messages.Message(
                            $"Components with the same exclusion tag cannot be combined: {prev.LabelCap} and {g.LabelCap} both have '{tag}'.",
                            MessageTypeDefOf.RejectInput, false);
                        __result = false;
                        return;
                    }
                    seen[tag] = g;
                }
            }
        }

        // Helpers

        private static List<GeneDef> GetSelectedGenes(object instance)
        {
            if (instance == null || BaseType == null) return null;
            var f = AccessTools.Field(BaseType, "selectedGenes");
            return f?.GetValue(instance) as List<GeneDef>;
        }

        private static GeneDef GetHoveredGene(object instance)
        {
            if (instance == null || BaseType == null) return null;
            var f = AccessTools.Field(BaseType, "hoveredGene");
            return f?.GetValue(instance) as GeneDef;
        }

        private static List<GeneDef> ConflictingGenes(GeneDef gene, List<GeneDef> others)
        {
            if (gene?.exclusionTags == null || gene.exclusionTags.Count == 0) return new List<GeneDef>(0);
            var set = new HashSet<string>(gene.exclusionTags.Where(t => !string.IsNullOrEmpty(t)), StringComparer.Ordinal);
            var res = new List<GeneDef>();
            foreach (var o in others)
            {
                if (o?.exclusionTags == null) continue;
                if (o == gene) continue;
                foreach (var t in o.exclusionTags)
                {
                    if (t != null && set.Contains(t))
                    {
                        res.Add(o);
                        break;
                    }
                }
            }
            return res;
        }

        private static bool SharesAnyExclusionTagWithAny(GeneDef gene, List<GeneDef> others)
        {
            if (gene?.exclusionTags == null || gene.exclusionTags.Count == 0) return false;
            foreach (var o in others)
            {
                if (o == null || ReferenceEquals(o, gene)) continue;
                if (ShareAnyTag(gene, o)) return true;
            }
            return false;
        }

        private static bool ShareAnyTag(GeneDef a, GeneDef b)
        {
            if (a?.exclusionTags == null || b?.exclusionTags == null) return false;
            for (int i = 0; i < a.exclusionTags.Count; i++)
            {
                var t = a.exclusionTags[i];
                if (string.IsNullOrEmpty(t)) continue;
                for (int j = 0; j < b.exclusionTags.Count; j++)
                {
                    if (string.Equals(t, b.exclusionTags[j], StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static string RemoveClickToAddLine(string tip)
        {
            if (string.IsNullOrEmpty(tip)) return tip;

            string clickAdd = "ClickToAdd".Translate().ToString(); // localized baseline
            var lines = tip.Split(new[] { '\n' }, StringSplitOptions.None).ToList();
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.IndexOf(clickAdd, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lines.RemoveAt(i);
                    // There might be a preceding blank line; trim trailing empties
                    while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                        lines.RemoveAt(lines.Count - 1);
                    break;
                }
            }
            return string.Join("\n", lines);
        }
    }
}