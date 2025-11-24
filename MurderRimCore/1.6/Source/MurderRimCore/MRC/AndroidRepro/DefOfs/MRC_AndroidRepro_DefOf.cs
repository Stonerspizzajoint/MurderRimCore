using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [DefOf]
    public static class MRC_AndroidRepro_DefOf
    {

        public static GeneDef MRWD_DroneBody;
        public static HediffDef MRC_FusedGrowthMarker;
        public static JobDef MRC_AbortFusion;
        public static JobDef MRC_AssembleAndroidBody;

        public static LifeStageDef HumanlikeTeenager;

        static MRC_AndroidRepro_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MRC_AndroidRepro_DefOf));
        }
    }
}
