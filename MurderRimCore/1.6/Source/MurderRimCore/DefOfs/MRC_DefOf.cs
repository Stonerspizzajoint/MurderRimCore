using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore
{
    [DefOf]
    public static class MRC_DefOf
    {

        //  MurderRim Core
        // GeneDefs
        public static GeneDef MRC_OilBlood;
        public static GeneDef MRC_SilhouettePerception;

        // ThoughtDefs
        public static ThoughtDef MRC_NeedRoboticRest;

        static MRC_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MRC_DefOf));
        }
    }
}
