using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [DefOf]
    public static class MRC_AndroidRepro_DefOf
    {

        public static HediffDef MRC_FusedNewbornMarkerHediff;
        public static JobDef MRC_AbortFusion;
        public static JobDef MRC_AssembleAndroidBody;
        public static HeadTypeDef MRC_BabyAndroid_Head;
        public static RenderSkipFlagDef MRC_SkipBabyDrone;

        static MRC_AndroidRepro_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MRC_AndroidRepro_DefOf));
        }
    }
}
