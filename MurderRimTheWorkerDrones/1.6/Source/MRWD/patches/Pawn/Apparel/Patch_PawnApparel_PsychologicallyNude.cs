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
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "get_PsychologicallyNude")]
    static class Patch_PawnApparel_PsychologicallyNude
    {
        static bool Prefix(Pawn_ApparelTracker __instance, ref bool __result)
        {
            var pawn = __instance?.pawn;
            if (pawn == null) return true;

            if (pawn.genes != null && pawn.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("MRWD_DroneBody", false)))
            {
                __result = false; // treat as clothed
                return false; // skip original
            }

            return true;
        }
    }
}
