using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    public static class FusedNewbornMarkerUtil
    {
        private const string MarkerDefName = "MRC_FusedNewbornMarkerHediff";

        public static void MarkWithHediff(Pawn p)
        {
            if (p == null || p.health == null) return;

            HediffDef def = MRC_AndroidRepro_DefOf.MRC_FusedNewbornMarkerHediff
                            ?? DefDatabase<HediffDef>.GetNamedSilentFail(MarkerDefName);

            if (def == null)
            {
                Log.Warning("[MRC-Repro] Marker HediffDef not found: " + MarkerDefName);
                return;
            }

            if (p.health.hediffSet.HasHediff(def)) return;

            try
            {
                var hd = HediffMaker.MakeHediff(def, p);
                p.health.AddHediff(hd);
            }
            catch
            {
                Log.Warning("[MRC-Repro] Failed to add fused newborn marker hediff.");
            }
        }

        public static bool IsMarkedByHediff(Pawn p)
        {
            if (p == null || p.health == null || p.health.hediffSet == null) return false;
            HediffDef def = MRC_AndroidRepro_DefOf.MRC_FusedNewbornMarkerHediff
                            ?? DefDatabase<HediffDef>.GetNamedSilentFail(MarkerDefName);
            return def != null && p.health.hediffSet.HasHediff(def);
        }

        public static void RemoveMarkerHediff(Pawn p)
        {
            if (p == null || p.health == null || p.health.hediffSet == null) return;
            HediffDef def = MRC_AndroidRepro_DefOf.MRC_FusedNewbornMarkerHediff
                            ?? DefDatabase<HediffDef>.GetNamedSilentFail(MarkerDefName);
            if (def == null) return;

            var h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null)
            {
                try { p.health.RemoveHediff(h); } catch { }
            }
        }
    }
}