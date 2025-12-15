using RimWorld;
using Verse;
using UnityEngine;

namespace MRWD
{
    // Dynamically maps Need_DoorSatisfaction level to any number of thought stages.
    // Stage distribution:
    //   level = 0          -> stage 0
    //   level in (0 .. 1]  -> floor( level * (stageCount - 1) )
    // This guarantees:
    //   - adding/removing stages in XML requires NO code changes
    //   - last stage reached only when level is very high (close to 1)
    public class ThoughtWorker_DoorEnthusiastThought : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p?.story?.traits == null) return ThoughtState.Inactive;
            if (!p.story.traits.HasTrait(MRWD_DefOf.MRWD_DoorEnthusiast)) return ThoughtState.Inactive;

            if (!(p.needs?.TryGetNeed(MRWD_DefOf.MRWD_DoorSatisfaction, out Need needBase) ?? false))
                return ThoughtState.Inactive;

            var need = needBase as Need_DoorSatisfaction;
            if (need == null || !need.ShowOnNeedList) return ThoughtState.Inactive;

            int stagesCount = def?.stages?.Count ?? 0;
            if (stagesCount <= 0) return ThoughtState.Inactive; // malformed def

            float level = need.CurLevel;

            // Exact zero (no doors) -> stage 0.
            if (level <= 0f)
                return ThoughtState.ActiveAtStage(0);

            // Even distribution across remaining stages.
            // Multiply by (stagesCount - 1) so level=1 -> last stage.
            int stage = Mathf.FloorToInt(level * (stagesCount - 1));

            // Clamp safety (just in case of floating point edge).
            if (stage < 0) stage = 0;
            if (stage > stagesCount - 1) stage = stagesCount - 1;

            return ThoughtState.ActiveAtStage(stage);
        }
    }
}