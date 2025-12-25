using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VREAndroids;

namespace MRHP.Patches
{
    // A shared state controller to communicate between patches
    public static class ReputationGuard
    {
        public static bool SuppressGoodwillChange = false;
    }

    // PATCH 1: Detect the Murder
    [HarmonyPatch(typeof(Faction), "Notify_MemberDied")]
    public static class Patch_Faction_Notify_MemberDied
    {
        [HarmonyPrefix]
        public static void Prefix(Faction __instance, Pawn member, DamageInfo? dinfo, bool wasWorldPawn, Map map)
        {
            // Reset safety flag just in case
            ReputationGuard.SuppressGoodwillChange = false;

            // 1. Validate Context
            if (map == null || !map.IsPlayerHome) return;
            if (member == null || dinfo == null) return;

            // 2. Is the Victim an Android? (As per your request)
            if (!Utils.IsAndroid(member)) return;

            Pawn killer = dinfo.Value.Instigator as Pawn;
            if (killer == null) return;

            // 3. Is the Killer a Sentinel/Murder Drone?
            // (Assuming IsRobotic checks for your flesh type)
            if (!killer.IsRobotic()) return;

            // 4. CRITICAL CHECK: Is the Killer NOT the player?
            // If the player orders their drone to kill a guest, they DESERVE the penalty.
            if (killer.Faction == Faction.OfPlayer) return;

            // 5. Check if Killer is Wild OR Foreign
            // Vanilla blames player if Killer.Faction is null (Wild).
            // Vanilla also might blame player depending on complex guest logic.
            // We want to exempt ALL Non-Player Drone kills.

            // "It wasn't me. It was the Solver."
            ReputationGuard.SuppressGoodwillChange = true;
            // Log.Message($"[MRHP] Suppressing Reputation Loss: {member} killed by {killer} (Wild/Foreign Drone)");
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            // Always turn the safety off after the method finishes
            ReputationGuard.SuppressGoodwillChange = false;
        }
    }

    // PATCH 2: Block the Penalty
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith")]
    public static class Patch_Faction_TryAffectGoodwillWith
    {
        [HarmonyPrefix]
        public static bool Prefix(Faction other, int goodwillChange, bool canSendMessage = true, bool canSendHostilityLetter = true, string reason = null, GlobalTargetInfo? lookTarget = null)
        {
            // If our guard is up...
            if (ReputationGuard.SuppressGoodwillChange)
            {
                // ...and the change is negative (penalty)...
                if (goodwillChange < 0)
                {
                    // BLOCK IT.
                    // Return false skips the original method.
                    // Log.Message($"[MRHP] Blocked Goodwill Change of {goodwillChange} with {other.Name}");
                    return false;
                }
            }

            // Otherwise, run normal logic
            return true;
        }
    }
}