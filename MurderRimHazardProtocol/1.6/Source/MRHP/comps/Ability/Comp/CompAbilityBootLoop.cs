using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;

namespace MRHP
{
    public class CompAbilityBootLoop : CompAbilityEffect
    {
        public new CompProperties_AbilityBootLoop Props => (CompProperties_AbilityBootLoop)props;

        private float GetRange() => parent.def.verbProperties.range;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.checkVerbBodyParts)
            {
                BodyPartGroupDef requiredGroup = parent.def.verbProperties.linkedBodyPartsGroup;

                if (requiredGroup != null)
                {
                    Pawn caster = parent.pawn;

                    // FIX 1: Use GetNotMissingParts()
                    // This is the robust vanilla way to find functional body parts.
                    // It automatically handles parents being destroyed (e.g. Head destroyed = FlashNode gone).
                    bool hasFunctionalPart = caster.health.hediffSet.GetNotMissingParts()
                        .Any(part => part.groups.Contains(requiredGroup));

                    if (!hasFunctionalPart)
                    {
                        reason = "MRHP_FlashNodeDestroyed".Translate();
                        return true;
                    }
                }
            }
            return base.GizmoDisabled(out reason);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            BootLoopUtils.DrawConePreview(parent.pawn, target, GetRange(), Props.coneAngle);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster.Map;
            float range = GetRange();
            float aimAngle = (target.Cell - caster.Position).AngleFlat;

            // Visuals
            if (Props.moteDef != null)
            {
                MoteThrown mote = (MoteThrown)ThingMaker.MakeThing(Props.moteDef);
                if (mote != null)
                {
                    mote.Scale = range;
                    float forwardOffset = range / 2f;
                    Vector3 offsetVector = Quaternion.AngleAxis(aimAngle, Vector3.up) * Vector3.forward * forwardOffset;
                    mote.exactPosition = caster.DrawPos + offsetVector;
                    mote.exactRotation = aimAngle;
                    GenSpawn.Spawn(mote, caster.Position, map);
                }
            }
            else
            {
                FleckMaker.Static(caster.Position, map, FleckDefOf.PsycastAreaEffect, range);
            }

            // Logic
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p.Map != map || p == caster) continue;
                if (p.Position.DistanceTo(caster.Position) > range) continue;

                // FIX 2: Replace 'CanSee' with 'GenSight.LineOfSight'
                if (!GenSight.LineOfSight(caster.Position, p.Position, map, true)) continue;

                float angleToVictim = (p.Position - caster.Position).AngleFlat;
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(aimAngle, angleToVictim));

                if (angleDiff <= Props.coneAngle / 2f)
                {
                    if (!p.Dead && !p.Downed)
                    {
                        BootLoopUtils.TryApplyBootLoop(p, caster, Props);
                    }
                }
            }
        }
    }
}