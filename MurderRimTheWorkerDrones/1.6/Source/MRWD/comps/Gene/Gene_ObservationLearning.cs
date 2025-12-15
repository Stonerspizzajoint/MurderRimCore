using Verse;

namespace MRWD
{
    // Attach this to any gene that grants ObservationLearningExtension.
    // Only override Gene methods that exist across RimWorld versions.
    public class Gene_ObservationLearning : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            // Invalidate the map cache so the gene addition is noticed quickly.
            ObservationLearningUtil.InvalidateMapCache(pawn?.Map);
        }

        public override void PostRemove()
        {
            base.PostRemove();
            ObservationLearningUtil.InvalidateMapCache(pawn?.Map);
        }

        public override void PostMake()
        {
            base.PostMake();
            // PostMake runs on creation; invalidate in case pawn already present.
            ObservationLearningUtil.InvalidateMapCache(pawn?.Map);
        }
    }
}
