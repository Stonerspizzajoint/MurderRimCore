using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    public static class AndroidFusionSpawnHelper
    {
        public static Pawn SpawnNewborn(FusionProcess proc, AndroidReproductionSettingsDef s, Map map, IntVec3 cell)
        {
            if (proc == null || proc.ParentA == null || proc.ParentB == null || s == null || !s.enabled) return null;

            // Explicit gender avoids "Name list for gender=None"
            Gender newbornGender = Rand.Value < 0.5f ? Gender.Male : Gender.Female;

            PawnKindDef kind = !string.IsNullOrEmpty(s.newbornPawnKindDef)
                ? (DefDatabase<PawnKindDef>.GetNamedSilentFail(s.newbornPawnKindDef) ?? proc.ParentA.kindDef)
                : proc.ParentA.kindDef;

            // Babies are a "downed" life stage; allowDowned must be true
            var request = new PawnGenerationRequest(
                kind,
                proc.ParentA.Faction,
                PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: true,
                canGeneratePawnRelations: false, // we add relations ourselves
                fixedBiologicalAge: 0f,
                fixedChronologicalAge: 0f,
                developmentalStages: DevelopmentalStage.Baby,
                fixedGender: newbornGender
            );

            Pawn newborn = PawnGenerator.GeneratePawn(request);
            if (newborn == null) return null;

            // Mark + sentinel BEFORE spawn (other patches may query these)
            FusedNewbornMarkerUtil.MarkWithHediff(newborn);
            try
            {
                if (newborn.genes != null)
                {
                    string sentinelName = AndroidFusionUtility.NameSentinel + " Fusion: " +
                                          (proc.ParentA != null ? proc.ParentA.LabelShortCap : "A") + " + " +
                                          (proc.ParentB != null ? proc.ParentB.LabelShortCap : "B");
                    newborn.genes.xenotypeName = sentinelName;
                }
            }
            catch { }

            // Ensure valid name
            try
            {
                if (newborn.Name == null || string.IsNullOrEmpty(newborn.Name.ToStringFull))
                {
                    newborn.Name = PawnBioAndNameGenerator.GeneratePawnName(newborn, NameStyle.Full, null, false, newborn.genes?.Xenotype);
                }
            }
            catch { }

            // Merge genes
            var genesUnion = AndroidFusionUtility.CollectUnion(proc.ParentA, proc.ParentB, s);
            if (newborn.genes != null)
            {
                // Remove template genes to avoid conflicts
                var existing = newborn.genes.GenesListForReading?.ToArray();
                if (existing != null)
                {
                    for (int i = 0; i < existing.Length; i++)
                    {
                        try { newborn.genes.RemoveGene(existing[i]); } catch { }
                    }
                }

                // Inverse-complexity weights (less complex parent => higher weight)
                float cpxA = SumComplexity(proc.ParentA, s);
                float cpxB = SumComplexity(proc.ParentB, s);
                float wA = (1f / (1f + cpxA)) * s.parentAWeightScale;
                float wB = (1f / (1f + cpxB)) * s.parentBWeightScale;

                var setAOnly = CollectParentOnly(proc.ParentA, s);
                var setBOnly = CollectParentOnly(proc.ParentB, s);

                foreach (var gdef in genesUnion)
                {
                    TryAddGeneWithConflictResolution(newborn, gdef, s, wA, wB, setAOnly, setBOnly);
                }

                // Choose parent for visuals by weights
                Pawn chosenParent = ChooseByWeight(proc.ParentA, proc.ParentB, wA, wB);
                SyncSkinGenesToParent(newborn, chosenParent);
                ApplyChosenParentSkinColor(newborn, chosenParent);
                TryForceGraphicsRefresh(newborn);
            }

            // RELATIONS:
            // - Ensure newborn has Parent -> each parent
            // - Ensure parents have Child -> newborn
            EnsureChildHoldsParentTo(newborn, proc.ParentA);
            if (proc.ParentB != null && proc.ParentB != proc.ParentA)
                EnsureChildHoldsParentTo(newborn, proc.ParentB);

            // Spawn
            GenSpawn.Spawn(newborn, cell, map);

            // Apply once more post-spawn (covers other patches writing during spawn)
            EnsureChildHoldsParentTo(newborn, proc.ParentA);
            if (proc.ParentB != null && proc.ParentB != proc.ParentA)
                EnsureChildHoldsParentTo(newborn, proc.ParentB);

            return newborn;
        }

        /// <summary>
        /// Ensure the CHILD has a direct Parent relation to PARENT.
        /// We do NOT add or query the implied Child relation at all; RimWorld derives that.
        /// Safe to call multiple times.
        /// </summary>
        private static void EnsureChildHoldsParentTo(Pawn child, Pawn parent)
        {
            if (child == null || parent == null || child == parent) return;
            if (child.relations == null) return;

            try
            {
                // Only ensure: child has Parent -> parent
                if (!child.relations.DirectRelationExists(PawnRelationDefOf.Parent, parent))
                {
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                }

                // Do NOT:
                // - Add PawnRelationDefOf.Child anywhere
                // - Query DirectRelationExists with Child
                // Child is treated as an implied relation by the relation worker.
            }
            catch
            {
                // Better to silently skip than spam logs if some other mod does something cursed.
            }
        }

        private static System.Collections.Generic.List<GeneDef> CollectParentOnly(Pawn p, AndroidReproductionSettingsDef s)
        {
            var list = new System.Collections.Generic.List<GeneDef>();
            if (p?.genes == null) return list;
            if (s.inheritEndogenes)
            {
                var endo = p.genes.Endogenes;
                if (endo != null)
                    for (int i = 0; i < endo.Count; i++)
                        if (endo[i]?.def != null) list.Add(endo[i].def);
            }
            if (s.inheritXenogenes)
            {
                var xeno = p.genes.Xenogenes;
                if (xeno != null)
                    for (int i = 0; i < xeno.Count; i++)
                        if (xeno[i]?.def != null) list.Add(xeno[i].def);
            }
            return list;
        }

        private static float SumComplexity(Pawn p, AndroidReproductionSettingsDef s)
        {
            float sum = 0f;
            if (p?.genes == null) return sum;

            if (s.inheritEndogenes)
            {
                var endo = p.genes.Endogenes;
                if (endo != null)
                    for (int i = 0; i < endo.Count; i++)
                        if (endo[i]?.def != null) sum += endo[i].def.biostatCpx;
            }
            if (s.inheritXenogenes)
            {
                var xeno = p.genes.Xenogenes;
                if (xeno != null)
                    for (int i = 0; i < xeno.Count; i++)
                        if (xeno[i]?.def != null) sum += xeno[i].def.biostatCpx;
            }
            return sum;
        }

        private static void TryAddGeneWithConflictResolution(
            Pawn newborn,
            GeneDef gdef,
            AndroidReproductionSettingsDef s,
            float wA,
            float wB,
            System.Collections.Generic.List<GeneDef> setAOnly,
            System.Collections.Generic.List<GeneDef> setBOnly)
        {
            if (newborn?.genes == null || gdef == null) return;
            if (newborn.genes.GetGene(gdef) != null) return;

            try
            {
                newborn.genes.AddGene(gdef, true);
                return;
            }
            catch
            {
                GeneDef conflictDef = null;
                if (gdef.displayCategory != null)
                {
                    var list = newborn.genes.GenesListForReading;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var ex = list[i];
                            if (ex?.def?.displayCategory == gdef.displayCategory)
                            {
                                conflictDef = ex.def;
                                break;
                            }
                        }
                    }
                }
                if (conflictDef == null) return;

                float newGeneWeight = GetOwnershipWeight(gdef, setAOnly, setBOnly, wA, wB);
                float oldGeneWeight = GetOwnershipWeight(conflictDef, setAOnly, setBOnly, wA, wB);

                if (newGeneWeight > oldGeneWeight)
                {
                    var existingGene = newborn.genes.GetGene(conflictDef);
                    if (existingGene != null)
                    {
                        try
                        {
                            newborn.genes.RemoveGene(existingGene);
                            newborn.genes.AddGene(gdef, true);
                        }
                        catch { }
                    }
                }
            }
        }

        private static float GetOwnershipWeight(
            GeneDef gdef,
            System.Collections.Generic.List<GeneDef> setAOnly,
            System.Collections.Generic.List<GeneDef> setBOnly,
            float wA,
            float wB)
        {
            bool fromA = setAOnly != null && setAOnly.Contains(gdef);
            bool fromB = setBOnly != null && setBOnly.Contains(gdef);
            if (fromA && !fromB) return wA;
            if (fromB && !fromA) return wB;
            return 0.5f * (wA + wB);
        }

        private static Pawn ChooseByWeight(Pawn a, Pawn b, float wA, float wB)
        {
            float total = wA + wB;
            if (total <= 0f) total = 0.0001f;
            float roll = Rand.Value * total;
            return (roll <= wA) ? a : b;
        }

        private static bool IsSkinAffectingGene(GeneDef def)
        {
            if (def == null) return false;
            string dn = def.defName ?? string.Empty;
            if (dn.IndexOf("Skin", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (dn.IndexOf("Melanin", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (def.displayCategory != null)
            {
                string cat = def.displayCategory.label ?? string.Empty;
                if (cat.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (cat.IndexOf("melanin", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static void SyncSkinGenesToParent(Pawn newborn, Pawn chosenParent)
        {
            if (newborn?.genes == null) return;

            var parentSkinDefs = new System.Collections.Generic.HashSet<GeneDef>();
            if (chosenParent?.genes != null)
            {
                var pend = chosenParent.genes.Endogenes;
                if (pend != null)
                    for (int i = 0; i < pend.Count; i++)
                        if (pend[i]?.def != null && IsSkinAffectingGene(pend[i].def)) parentSkinDefs.Add(pend[i].def);

                var pxen = chosenParent.genes.Xenogenes;
                if (pxen != null)
                    for (int i = 0; i < pxen.Count; i++)
                        if (pxen[i]?.def != null && IsSkinAffectingGene(pxen[i].def)) parentSkinDefs.Add(pxen[i].def);
            }

            var cur = newborn.genes.GenesListForReading?.ToArray();
            if (cur != null)
            {
                for (int i = 0; i < cur.Length; i++)
                {
                    var g = cur[i];
                    if (g?.def == null) continue;
                    if (IsSkinAffectingGene(g.def) && !parentSkinDefs.Contains(g.def))
                    {
                        try { newborn.genes.RemoveGene(g); } catch { }
                    }
                }
            }

            foreach (var def in parentSkinDefs)
            {
                if (newborn.genes.GetGene(def) == null)
                {
                    try { newborn.genes.AddGene(def, true); } catch { }
                }
            }
        }

        private static void ApplyChosenParentSkinColor(Pawn newborn, Pawn chosenParent)
        {
            if (newborn?.story == null || chosenParent?.story == null) return;

            Color? srcColor = GetParentSkinColor(chosenParent);
            float? parentMelanin = GetParentMelanin(chosenParent);

            bool setBase = false;
            if (srcColor.HasValue) setBase = TrySetSkinColorBase(newborn, srcColor.Value);
            if (parentMelanin.HasValue) TrySetMelaninDirect(newborn, parentMelanin.Value);
            if (!setBase && srcColor.HasValue) TrySetMelaninApprox(newborn, srcColor.Value);
        }

        private static float? GetParentMelanin(Pawn p)
        {
            try
            {
                if (p?.story == null) return null;
                var t = p.story.GetType();
                var prop = t.GetProperty("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop?.CanRead == true)
                {
                    object v = prop.GetValue(p.story, null);
                    if (v is float f) return f;
                }
                var field = t.GetField("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object v = field.GetValue(p.story);
                    if (v is float f) return f;
                }
            }
            catch { }
            return null;
        }

        private static bool TrySetSkinColorBase(Pawn pawn, Color color)
        {
            try
            {
                if (pawn == null || pawn.story == null) return false;
                var t = pawn.story.GetType();

                var prop = t.GetProperty("skinColorBase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop?.CanWrite == true)
                {
                    object boxed = prop.PropertyType == typeof(Color?) ? (Color?)color :
                                   prop.PropertyType == typeof(Color) ? (object)color : null;
                    if (boxed != null)
                    {
                        prop.SetValue(pawn.story, boxed, null);
                        return true;
                    }
                }

                var field = t.GetField("skinColorBase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    if (field.FieldType == typeof(Color)) { field.SetValue(pawn.story, color); return true; }
                    if (field.FieldType == typeof(Color?)) { field.SetValue(pawn.story, (Color?)color); return true; }
                }
            }
            catch { }
            return false;
        }

        private static Color? GetParentSkinColor(Pawn p)
        {
            try
            {
                if (p == null || p.story == null) return null;

                var t = p.story.GetType();
                var prop = t.GetProperty("SkinColor", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    object val = prop.GetValue(p.story, null);
                    if (val is Color c) return c;

                    if (val != null && val.GetType().IsGenericType &&
                        val.GetType().GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var inner = val.GetType().GetGenericArguments()[0];
                        if (inner == typeof(Color))
                        {
                            Color? nc = (Color?)val;
                            if (nc.HasValue) return nc.Value;
                        }
                    }
                }

                var field = t.GetField("SkinColor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object val = field.GetValue(p.story);
                    if (val is Color c) return c;

                    if (val != null && val.GetType().IsGenericType &&
                        val.GetType().GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var inner = val.GetType().GetGenericArguments()[0];
                        if (inner == typeof(Color))
                        {
                            Color? nc = (Color?)val;
                            if (nc.HasValue) return nc.Value;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void TrySetMelaninDirect(Pawn pawn, float mel)
        {
            try
            {
                if (pawn?.story == null) return;
                var t = pawn.story.GetType();
                var prop = t.GetProperty("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop?.CanWrite == true) { prop.SetValue(pawn.story, mel, null); return; }
                var field = t.GetField("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(pawn.story, mel);
            }
            catch { }
        }

        private static bool TrySetMelaninApprox(Pawn pawn, Color color)
        {
            try
            {
                if (pawn?.story == null) return false;
                float mel = Mathf.Clamp01((color.r + color.g + color.b) / 3f);
                var t = pawn.story.GetType();
                var prop = t.GetProperty("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop?.CanWrite == true) { prop.SetValue(pawn.story, mel, null); return true; }
                var field = t.GetField("melanin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) { field.SetValue(pawn.story, mel); return true; }
            }
            catch { }
            return false;
        }

        private static void TryForceGraphicsRefresh(Pawn p)
        {
            try
            {
                p?.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(p);
            }
            catch { }
        }
    }
}