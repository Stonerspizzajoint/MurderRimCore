using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace MurderRimCore.Patches
{
    [HarmonyPatch(typeof(Toils_LayDown), nameof(Toils_LayDown.LayDown))]
    public static class MurderRimCore_Toils_LayDown_LayDown_Patch
    {
        static void Postfix(Toil __result)
        {
            // Add an extra tick action for our custom need
            var oldTickAction = __result.tickAction;
            __result.tickAction = () =>
            {
                oldTickAction?.Invoke();

                // Get the pawn from the actor
                Pawn pawn = __result.actor;
                if (pawn == null) return;

                // Find our custom need
                var sleepMode = pawn.needs?.AllNeeds?.FirstOrDefault(n => n is MurderRimCore.Need_SleepMode) as MurderRimCore.Need_SleepMode;
                if (sleepMode != null)
                {
                    // Calculate rest effectiveness (mimic vanilla logic)
                    float restEffectiveness = 1f;
                    Building_Bed bed = pawn.CurrentBed();
                    if (bed != null)
                    {
                        restEffectiveness = bed.GetStatValue(StatDefOf.BedRestEffectiveness, true);
                    }
                    sleepMode.TickResting(restEffectiveness);
                }
            };
        }
    }
}

