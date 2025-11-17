using RimWorld;
using Verse;

namespace MurderRimCore.MRWD
{
    // Trait-gated behavior via Disabled property (need may exist but is inert/hidden when trait absent).
    public class Need_DoorSatisfaction : Need
    {
        // S-shaped-ish growth as door score rises.
        private static readonly UnityEngine.AnimationCurve TargetCurve = new UnityEngine.AnimationCurve(
            new UnityEngine.Keyframe(0f, 0.00f), // exact zero when score is zero
            new UnityEngine.Keyframe(0.5f, 0.20f),
            new UnityEngine.Keyframe(1.0f, 0.40f),
            new UnityEngine.Keyframe(2.0f, 0.65f),
            new UnityEngine.Keyframe(3.0f, 0.80f),
            new UnityEngine.Keyframe(4.0f, 0.92f),
            new UnityEngine.Keyframe(6.0f, 1.00f)
        );

        private const float ApproachPerInterval = 0.08f;

        public Need_DoorSatisfaction(Pawn pawn) : base(pawn)
        {
            curLevelInt = 0f;
        }

        // If pawn lacks trait, the need remains hidden + frozen.
        private bool Disabled => pawn?.story?.traits == null || !pawn.story.traits.HasTrait(MRWD_DefOf.MRWD_DoorEnthusiast);

        public override int GUIChangeArrow
        {
            get
            {
                if (Disabled) return 0;
                float t = GetTargetLevel();
                if (UnityEngine.Mathf.Abs(t - CurLevel) <= 0.01f) return 0;
                return t > CurLevel ? 1 : -1;
            }
        }

        public override float CurInstantLevel => CurLevel;
        public override bool ShowOnNeedList => !Disabled;

        public override void NeedInterval()
        {
            if (Disabled)
            {
                // starts at 0
                CurLevel = 0f;
                return;
            }

            float target = GetTargetLevel();
            float delta = UnityEngine.Mathf.Clamp(target - CurLevel, -ApproachPerInterval, ApproachPerInterval);
            CurLevel = UnityEngine.Mathf.Clamp01(CurLevel + delta);
        }

        private float GetTargetLevel()
        {
            var map = pawn.MapHeld;
            if (map == null) return 0f; // away from map -> treat as unsafe baseline

            var comp = map.GetComponent<MapComponent_DoorMetrics>();
            if (comp == null) return 0f;

            // EXACTLY zero when there are no player-owned doors.
            if (comp.playerDoorCount <= 0)
                return 0f;

            float perColonist = comp.GetPerColonistScore();
            return UnityEngine.Mathf.Clamp01(TargetCurve.Evaluate(perColonist));
        }
    }
}