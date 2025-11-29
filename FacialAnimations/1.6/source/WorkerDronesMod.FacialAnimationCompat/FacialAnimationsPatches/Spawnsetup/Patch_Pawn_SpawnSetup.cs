using HarmonyLib;
using Verse;
using System.Linq;
using System.Reflection;

namespace MurderRimCore.FacialAnimationCompat
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null) return;

            // Sometimes SpawnSetup is called in contexts where Spawned isn't yet set — still attempt to run,
            // but exit early if there are no comps.
            var comps = __instance.AllComps;
            if (comps == null || comps.Count == 0) return;

            foreach (var comp in comps)
            {
                if (comp == null) continue;

                // Quick heuristic: only call SafeReload on comps that look like facial animation comps.
                // We check for the method ReloadIfNeed (non-invasive, no hard dependency).
                var reloadMethod = comp.GetType().GetMethod("ReloadIfNeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (reloadMethod == null) continue;

                // Defensive call: SafeReload will manage per-type reflection and handle exceptions.
                try
                {
                    FacialAnimationGeneUtil.SafeReload(comp);
                }
                catch (System.Exception ex)
                {
                    // Make sure a single bad comp doesn't crash the game.
                    Log.Error($"[MurderRimCore] FacialAnimation SpawnSetup patch: SafeReload threw for comp type {comp.GetType().FullName}: {ex}");
                }
            }
        }
    }
}