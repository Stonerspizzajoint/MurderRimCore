using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MRWD.patches
{
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "get_AnyClothing")]
    static class Patch_PawnApparel_AnyClothing
    {
        static bool Prefix(Pawn_ApparelTracker __instance, ref bool __result)
        {
            var pawn = __instance?.pawn;
            if (pawn == null) return true; // run original

            // If pawn has the drone body gene, treat as having clothing for nudity checks
            if (pawn.genes != null && pawn.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("MRWD_DroneBody", false)))
            {
                __result = true;
                return false; // skip original getter
            }

            return true; // run original getter
        }
    }
}
