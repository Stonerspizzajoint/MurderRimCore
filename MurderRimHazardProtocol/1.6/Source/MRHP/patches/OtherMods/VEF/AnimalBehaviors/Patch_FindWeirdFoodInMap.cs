using HarmonyLib;
using Verse;
using Verse.AI;
using VEF.AnimalBehaviours;
using RimWorld;
using System;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(JobGiver_GetWeirdFood), "FindWeirdFoodInMap")]
    public static class Patch_FindWeirdFoodInMap
    {
        // PREFIX: We run this BEFORE the original method.
        // If we return 'false', the original method is skipped entirely.
        public static bool Prefix(ThingDef thingDef, Pawn pawn, ref Thing __result)
        {
            // 1. Check if this is OUR pawn (CompScrapEater). 
            // If not, let VEF handle it normally (return true).
            CompScrapEater comp = pawn.TryGetComp<CompScrapEater>();
            if (comp == null) return true;

            // 2. Perform a BETTER Search
            // We want to find the closest thing that is:
            // - The right Def
            // - Reachable
            // - In Allowed Area
            // - NOT Forbidden (Respects the X)
            // - Reservable (So two bugs don't fight over one corpse)

            ThingRequest thingReq = ThingRequest.ForDef(thingDef);

            // Define the validator
            Predicate<Thing> validator = (Thing t) =>
            {
                // Must be spawned
                if (t.Spawned == false) return false;

                // Must be in allowed area
                if (!t.Position.InAllowedArea(pawn)) return false;

                // Must NOT be forbidden (The key fix!)
                if (t.IsForbidden(pawn)) return false;

                // Must be reservable (Prevents bugs getting stuck trying to eat the same thing)
                if (!pawn.CanReserve(t)) return false;

                return true;
            };

            // Search
            Thing foundThing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                thingReq,
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false),
                9999f,      // Range
                validator   // Our strict rules
            );

            // 3. Set the result
            __result = foundThing;

            // 4. Return FALSE to skip the original VEF method.
            // We have done the work for them.
            return false;
        }
    }
}
