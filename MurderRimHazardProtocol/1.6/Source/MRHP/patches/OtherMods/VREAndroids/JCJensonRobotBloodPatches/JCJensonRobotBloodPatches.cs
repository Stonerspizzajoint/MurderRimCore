using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using VREAndroids; // We use their definitions directly

namespace MRHP.Patches
{

    // ---------------------------------------------------------
    // PATCH 2: AI - Seek Bed when Leaking
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(HealthAIUtility), "ShouldSeekMedicalRestUrgent")]
    public static class Patch_HealthAIUtility_Sentinel
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            // If vanilla says "No need for bed", check if we are leaking coolant
            if (!__result && pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
            {
                // Check if we have the VRE Neutro Loss hediff
                if (pawn.health.hediffSet.HasHediff(VREA_DefOf.VREA_NeutroLoss))
                {
                    __result = true;
                }
            }
        }
    }

    // ---------------------------------------------------------
    // PATCH 3: UI - Replace "Bleeding" text in Health Tab
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffListing")]
    public static class Patch_HealthCardUtility_Sentinel
    {
        // Use a Transpiler to inject our UI update logic
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                yield return codes[i];

                // Look for where the "Bleeding" text string is stored (Local Variable 9)
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 9)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // Load Pawn
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 9); // Load address of 'text' variable
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_HealthCardUtility_Sentinel), nameof(ReplaceBleedingTextSentinel)));
                }
            }
        }

        // The method that updates the text
        public static void ReplaceBleedingTextSentinel(Pawn pawn, ref string text)
        {
            // Only run for our Sentinels
            if (pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
            {
                float bleedRateTotal = pawn.health.hediffSet.BleedRateTotal;
                Hediff leakHediff = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss, false);

                // If completely empty (Dead/Downed logic from VRE)
                if (leakHediff != null && leakHediff.Severity >= 1f)
                {
                    text = "VREA.NeutroamineLeakedOutCompletely".Translate();
                    return;
                }

                // Standard rate display using VRE translations
                text = "VREA.NeutrolossRate".Translate() + ": " + bleedRateTotal.ToStringPercent() + "/" + "LetterDay".Translate();

                // Calculate time to death/shutdown
                int numTicks = TicksUntilTotalLoss(pawn);
                text += " (" + "VREA.TotalNeutroLoss".Translate(numTicks.ToStringTicksToPeriod(true, false, true, true, false)) + ")";
            }
        }

        [StaticConstructorOnStartup]
        [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffRow")]
        public static class Patch_HealthCardUtility_DrawHediffRow
        {
            private static Texture2D _oilIcon;
            public static Texture2D OilIcon
            {
                get
                {
                    if (_oilIcon == null)
                        _oilIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/OilBlood_BleedingIcon", true);
                    return _oilIcon;
                }
            }

            private static FieldInfo BleedingIconField = AccessTools.Field(typeof(HealthCardUtility), "BleedingIcon");

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();

                for (int i = 0; i < codes.Count; i++)
                {
                    yield return codes[i];

                    // Look for: Stfld (Assigning value to the local 'bleedingIcon' field)
                    // Preceded by: Ldsfld HealthCardUtility.BleedingIcon
                    if (i > 1 &&
                        codes[i].opcode == OpCodes.Stfld &&
                        codes[i - 1].LoadsField(BleedingIconField))
                    {
                        // This is the instruction that sets the icon to the Blood drop.
                        // We want to run code AFTER this to say "Actually, if it's a robot, change it to Oil."

                        FieldInfo fieldInfo = (FieldInfo)codes[i].operand; // The 'bleedingIcon' field on the hidden class
                        CodeInstruction loadDisplayClass = codes[i - 2].Clone(); // The instruction that put the object on stack

                        // INJECT:
                        // 1. Load DisplayClass instance (so we can write to its field)
                        yield return loadDisplayClass;

                        // 2. Load DisplayClass instance AGAIN (so we can read the field if needed, or just to pass to Stfld)
                        // Actually, we need [Instance, NewValue] for Stfld.

                        // 3. Load Pawn
                        yield return new CodeInstruction(OpCodes.Ldarg_1);

                        // 4. Call our helper function. 
                        // It returns the OIL icon if robot, or NULL if not.
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_HealthCardUtility_DrawHediffRow), nameof(GetOilIconOrNull)));

                        // 5. Check if result is null
                        yield return new CodeInstruction(OpCodes.Dup); // Duplicate result [Instance, Texture, Texture]

                        // 6. Branch if null (skip assignment)
                        Label skipLabel = new Label(); // We need a real label generator in a full implementation, but here is the logic:
                                                       // Since we are yielding, we can't easily jump forward without ILGenerator. 
                                                       // ALTERNATIVE: Use a helper that takes (Instance, Pawn) and does the field set via Reflection ONCE.

                        yield return new CodeInstruction(OpCodes.Pop); // Clear the duplicate if we aren't branching logic here
                        yield return new CodeInstruction(OpCodes.Pop); // Clear the instance we loaded

                        // RESTART STRATEGY: 
                        // Just call a void method that takes the Instance and Pawn and uses Reflection. 
                        // It's slightly slower but 100% stable and crash-proof.

                        yield return loadDisplayClass;
                        yield return new CodeInstruction(OpCodes.Ldarg_1); // Pawn
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_HealthCardUtility_DrawHediffRow), nameof(FixIconSafe)));
                    }
                }
            }

            public static Texture2D GetOilIconOrNull(Pawn pawn)
            {
                if (pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh) return OilIcon;
                return null;
            }

            // This method does the heavy lifting safely
            public static void FixIconSafe(object displayClassInstance, Pawn pawn)
            {
                if (pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
                {
                    // We use reflection to find the field "bleedingIcon" on the hidden class instance
                    // We cache this FieldInfo for performance
                    if (_cachedField == null)
                    {
                        _cachedField = AccessTools.Field(displayClassInstance.GetType(), "bleedingIcon");
                    }

                    if (_cachedField != null)
                    {
                        _cachedField.SetValue(displayClassInstance, OilIcon);
                    }
                }
            }
            private static FieldInfo _cachedField;
        }

        // ---------------------------------------------------------
        // PATCH 1: General Death Prevention (Consciousness 0%)
        // ---------------------------------------------------------
        // This stops the pawn from dying when Neutro leaks out completely.
        [HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDead")]
        public static class Patch_Sentinel_ShouldBeDead
        {
            [HarmonyPriority(Priority.Last)] // Run last to override vanilla
            public static void Postfix(ref bool __result, Pawn ___pawn)
            {
                // If the game says "Yes, they are dead"...
                if (__result)
                {
                    // ...check if it is our Sentinel...
                    if (___pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
                    {
                        // ...and ensure they still have a Brain (Head wasn't blown off).
                        // If they have a brain, we force them to stay alive (Downed/Shutdown).
                        if (___pawn.health.hediffSet.GetBrain() != null)
                        {
                            __result = false;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // PATCH 2: Capacity Death Prevention (Organs)
        // ---------------------------------------------------------
        // This stops the pawn from dying if Breathing/Pumping stops (e.g. Reactor destroyed).
        // They will go down, but not die.
        [HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDeadFromRequiredCapacity")]
        public static class Patch_Sentinel_CapacityDeath
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(ref PawnCapacityDef __result, Pawn ___pawn)
            {
                // If the game found a missing capacity that causes death (like Breathing)...
                if (__result != null)
                {
                    if (___pawn.RaceProps.FleshType == MRHP_DefOf.MRHP_JCJensonRobotFlesh)
                    {
                        // ...override it. Null means "No fatal capacity missing".
                        if (___pawn.health.hediffSet.GetBrain() != null)
                        {
                            __result = null;
                        }
                    }
                }
            }
        }

        // Helper math copied from VRE to ensure consistency
        public static int TicksUntilTotalLoss(Pawn pawn)
        {
            float bleedRateTotal = pawn.health.hediffSet.BleedRateTotal;
            if (bleedRateTotal < 0.0001f) return int.MaxValue;

            Hediff leakHediff = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss, false);
            float currentSev = (leakHediff != null) ? leakHediff.Severity : 0f;

            return (int)((1f - currentSev) / bleedRateTotal * 60000f);
        }
    }
}