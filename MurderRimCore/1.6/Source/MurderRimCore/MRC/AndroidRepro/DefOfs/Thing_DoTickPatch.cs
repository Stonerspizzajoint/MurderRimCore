using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [DefOf]
    public static class JobDefOf_Fusion
    {
        public static JobDef MRC_FuseAtCreationStation;

        static JobDefOf_Fusion()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf_Fusion));
        }
    }
}
