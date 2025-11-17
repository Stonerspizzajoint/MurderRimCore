using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Ensure a parent sees the baby as Son/Daughter when the baby holds a direct Parent link to them.
    // Targets PawnRelationWorker_Child.InRelation dynamically (works across versions).
    [HarmonyPatch]
    public static class SameGenderParentSupport_ChildWorker
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("RimWorld.PawnRelationWorker_Child");
            if (t == null) return null;
            return AccessTools.Method(t, "InRelation", new[] { typeof(Pawn), typeof(Pawn) });
        }

        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn me, Pawn other)
        {
            if (__result) return;
            if (me == null || other == null || me == other) return;

            try
            {
                // On a parent's Social tab, 'me' is the parent, 'other' is the child.
                // If the child directly lists 'me' as Parent, treat as Son/Daughter.
                if (other.relations != null &&
                    other.relations.DirectRelationExists(PawnRelationDefOf.Parent, me))
                {
                    __result = true;
                }
            }
            catch { }
        }
    }
}
