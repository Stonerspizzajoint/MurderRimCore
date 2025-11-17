using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Your build's PawnRelationWorker_Parent has no InRelation; so patch the base worker's InRelation
    // and only apply when the concrete worker instance is PawnRelationWorker_Parent.
    // This makes the child see each same-gender parent as Mother/Father when the child holds Parent->parent directly.
    [HarmonyPatch]
    public static class SameGenderParentSupport_ParentViaBase
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Patch base: RimWorld.PawnRelationWorker.InRelation(Pawn, Pawn)
            var t = AccessTools.TypeByName("RimWorld.PawnRelationWorker");
            if (t == null) return null;
            return AccessTools.Method(t, "InRelation", new[] { typeof(Pawn), typeof(Pawn) });
        }

        [HarmonyPostfix]
        public static void Postfix(PawnRelationWorker __instance, ref bool __result, Pawn me, Pawn other)
        {
            if (__result) return;
            if (__instance == null || me == null || other == null || me == other) return;

            // Only act when the concrete worker is the Parent worker
            if (__instance.GetType().FullName != "RimWorld.PawnRelationWorker_Parent") return;

            try
            {
                // On the child's Social tab, 'me' is the child and 'other' is the candidate parent.
                // If the child directly lists 'other' as Parent, consider 'other' a parent (label Mother/Father derives from other.gender).
                if (me.relations != null &&
                    me.relations.DirectRelationExists(PawnRelationDefOf.Parent, other))
                {
                    __result = true;
                }
            }
            catch { }
        }
    }
}