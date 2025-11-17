using RimWorld;
using Verse;

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
                // Use GetGene + Active for compatibility across versions/mods
                var gene = pawn.genes.GetGene(MRWD.MRWD_DefOf.MRWD_DroneBody);
                return gene != null && gene.Active;
            }
            catch
            {
                return false;
            }
        }
    }
}
