using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using VREAndroids;

namespace MRHP
{
    public static class BootLoopUtils
    {
        // --- 1. PREVIEW HELPER ---
        public static void DrawConePreview(Pawn caster, LocalTargetInfo target, float range, float coneAngle)
        {
            if (caster == null || caster.Map == null) return;
            List<IntVec3> cells = GetConeCells(caster.Position, target, caster.Map, range, coneAngle);
            GenDraw.DrawFieldEdges(cells);
        }

        // --- 2. LOGIC ---

        // Overload A: Uses the updated Props
        public static void TryApplyBootLoop(Pawn target, Pawn caster, CompProperties_AbilityBootLoop props)
        {
            // NEW LOGIC: Check for Critical Vulnerability Gene
            HediffDef hediffToApply = props.hediffDef;

            if (target.genes != null && target.genes.HasActiveGene(MRHP_DefOf.MRHP_BootLoopCritical))
            {
                // If props defines a critical hediff, use it. Otherwise, fallback to standard.
                if (props.hediffDefCritical != null)
                {
                    hediffToApply = props.hediffDefCritical;
                }
            }

            TryApplyBootLoop(target, caster, props.stunDuration, hediffToApply, props.useSightBasedChance, props.hitMoteDef);
        }

        // Overload B: The core logic (Unchanged structure, just handles the passed hediff)
        public static void TryApplyBootLoop(Pawn target, Pawn caster, float stunDuration, HediffDef hediffDef, bool useSightChance, ThingDef hitMote)
        {
            if (target == null) return;

            // A. Target Validation
            if (!Utils.IsAndroid(target)) return;

            // B. Immunity Check (Hediff)
            // Prevent re-applying if they already have it (especially important for permanent loops)
            if (hediffDef != null && target.health.hediffSet.HasHediff(hediffDef)) return;

            // C. Gene Immunity
            if (target.genes != null && target.genes.HasActiveGene(MRHP_DefOf.MRHP_BootLoopImmunity))
            {
                MoteMaker.ThrowText(target.DrawPos, target.Map, "IMMUNE", Color.grey);
                return;
            }

            // D. Gear Check
            if (HasEyeProtection(target))
            {
                MoteMaker.ThrowText(target.DrawPos, target.Map, "BLOCKED", Color.green);
                return;
            }

            // E. Angle/Facing Check
            if (caster != null)
            {
                float angleToCaster = (caster.Position - target.Position).AngleFlat;
                float targetFacingAngle = target.Rotation.AsAngle;
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(targetFacingAngle, angleToCaster));

                if (angleDiff > 100f) // Looking away
                {
                    if (Rand.Value < 0.90f) { MoteMaker.ThrowText(target.DrawPos, target.Map, "RESISTED", Color.white); return; }
                }
                else if (angleDiff > 45f) // Looking sideways
                {
                    if (Rand.Value < 0.40f) { MoteMaker.ThrowText(target.DrawPos, target.Map, "RESISTED", Color.white); return; }
                }
            }

            // F. Sight Check
            if (useSightChance)
            {
                float sight = target.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
                if (sight <= 0.01f) { MoteMaker.ThrowText(target.DrawPos, target.Map, "BLIND", Color.white); return; }
                if (sight < 1.0f && Rand.Value > sight) { MoteMaker.ThrowText(target.DrawPos, target.Map, "RESISTED", Color.white); return; }
            }

            // G. APPLY EFFECT
            target.stances.stunner.StunFor((int)stunDuration, caster, true, true);

            if (hediffDef != null)
            {
                target.health.AddHediff(hediffDef);

                // If it's the permanent critical loop, maybe add a text popup?
                if (hediffDef == MRHP_DefOf.MRHP_BootLoopPerminent) // Assuming you define this
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "CRITICAL ERROR", Color.red);
                }
            }

            // H. SPAWN MOTE (Visual Feedback)
            if (hitMote != null)
            {
                MoteThrown mote = (MoteThrown)ThingMaker.MakeThing(hitMote);
                if (mote != null)
                {
                    mote.Scale = 1.0f;
                    mote.exactPosition = target.DrawPos + new Vector3(0, 0, 0.5f);
                    mote.exactRotation = 0f;
                    mote.SetVelocity(0f, 0.3f);
                    mote.rotationRate = -120f;
                    GenSpawn.Spawn(mote, target.Position, target.Map);
                }
            }
            else
            {
                MoteMaker.ThrowText(target.DrawPos, target.Map, "BOOT LOOP", Color.cyan);
            }
        }

        // ... HELPERS REMAIN UNCHANGED ...
        public static bool HasEyeProtection(Pawn p)
        {
            if (p.apparel == null) return false;
            foreach (Apparel ap in p.apparel.WornApparel)
            {
                if (ap.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes)) return true;
            }
            return false;
        }

        public static List<IntVec3> GetConeCells(IntVec3 origin, LocalTargetInfo target, Map map, float range, float coneAngle)
        {
            List<IntVec3> cells = new List<IntVec3>();
            if (map == null) return cells;

            float aimAngle = (target.Cell - origin).AngleFlat;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, range, true))
            {
                if (cell.InBounds(map) && cell != origin)
                {
                    float angleToCell = (cell - origin).AngleFlat;
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(aimAngle, angleToCell));
                    if (angleDiff <= coneAngle / 2f) cells.Add(cell);
                }
            }
            return cells;
        }
    }
}