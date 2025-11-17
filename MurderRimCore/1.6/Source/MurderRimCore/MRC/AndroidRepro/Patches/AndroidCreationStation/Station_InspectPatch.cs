using HarmonyLib;
using RimWorld;
using System.Text;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    [HarmonyPatch(typeof(Thing), "GetInspectString")]
    public static class Station_InspectPatch
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            var station = __instance as VREAndroids.Building_AndroidCreationStation;
            if (station == null) return;

            if (!AndroidFusionRuntime.TryGetProcess(station, out FusionProcess proc) || proc == null) return;

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(__result))
            {
                sb.Append(__result);
                sb.AppendLine();
            }

            switch (proc.Stage)
            {
                case FusionStage.Fusion:
                    sb.Append("Fusion: ")
                      .Append(proc.FusionPercent.ToStringPercent())
                      .Append(" (Hooked Up: ")
                      .Append(proc.ParentsInSlots ? "yes" : "no")
                      .Append(")");
                    break;

                case FusionStage.Gestation:
                    sb.Append("Gestation: ").Append(proc.GestationPercent.ToStringPercent());
                    break;

                case FusionStage.Assembly:
                    int p = AndroidFusionRuntime.CountInFootprint(station, ThingDefOf.Plasteel);
                    int u = AndroidFusionRuntime.CountInFootprintFlexible(station, AndroidFusionRuntime.UraniumDefNames);
                    int a = AndroidFusionRuntime.CountInFootprintFlexible(station, AndroidFusionRuntime.AdvancedComponentDefNames);
                    sb.Append("Assembly: materials inside station (")
                      .Append($"Plasteel {p}/{AndroidFusionRuntime.PlasteelReq}, Uranium {u}/{AndroidFusionRuntime.UraniumReq}, Adv. Comp. {a}/{AndroidFusionRuntime.AdvCompReq}")
                      .Append(")");
                    break;

                case FusionStage.Complete:
                    sb.Append("Fusion complete.");
                    break;

                case FusionStage.Aborted:
                    sb.Append("Fusion aborted.");
                    break;
            }

            __result = sb.ToString();
        }
    }
}