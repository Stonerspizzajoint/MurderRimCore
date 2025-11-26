using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [DefOf]
    public static class AndroidRep_DefOf
    {
        // The designated Child Android
        public static HediffDef MRC_AndroidChildhoodMarker;

        // Backstories
        public static BackstoryDef MRC_AndroidNewborn;
        public static BackstoryDef MRC_AndroidChild;
        public static BackstoryDef MRC_AndroidColonyBorn;

        static AndroidRep_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AndroidRep_DefOf));
        }
    }
}
