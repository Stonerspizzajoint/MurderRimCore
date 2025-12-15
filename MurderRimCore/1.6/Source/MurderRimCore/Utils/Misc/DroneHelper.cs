using RimWorld;
using Verse;
using VREAndroids;

namespace MurderRimCore
{
    public static class DroneHelper
    {
        public static bool IsWorkerDrone(Pawn pawn)
        {
            if (pawn == null) return false;
            try
            {
                if (pawn.genes == null) return false;
                var gene = pawn.genes.GetGene(MRWD.MRWD_DefOf.MRWD_DroneBody);
                return gene != null && gene.Active && Utils.IsAndroid(pawn);
            }
            catch
            {
                return false;
            }
        }
    }
}
