using System;
using Verse;
using RimWorld;

namespace MurderRimCore
{
    public class ThoughtWorker_NeedRoboticRest : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // Try to get your custom need
            var needSleepMode = p.needs?.TryGetNeed<Need_SleepMode>();
            if (needSleepMode == null)
            {
                return ThoughtState.Inactive;
            }

            switch (needSleepMode.CurCategory)
            {
                case RestCategory.Rested:
                    return ThoughtState.Inactive;
                case RestCategory.Tired:
                    return ThoughtState.ActiveAtStage(0);
                case RestCategory.VeryTired:
                    return ThoughtState.ActiveAtStage(1);
                case RestCategory.Exhausted:
                    return ThoughtState.ActiveAtStage(2);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

