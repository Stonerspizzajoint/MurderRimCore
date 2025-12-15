using System;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using VEF.AnimalBehaviours;

namespace MRHP.Patches
{
    [HarmonyPatch(typeof(JobDriver_IngestWeird), "FinalizeIngestAnything")]
    public static class Patch_FinalizeIngestAnything
    {
        public static void Postfix(Pawn ingester, TargetIndex ingestibleInd, CompEatWeirdFood comp, ref Toil __result)
        {
            // Capture local variable for delegate safety
            Toil workingToil = __result;
            Action originalInit = workingToil.initAction;

            workingToil.initAction = delegate ()
            {
                Pawn actor = workingToil.actor;

                if (actor.CurJob != null)
                {
                    Thing thing = actor.CurJob.GetTarget(ingestibleInd).Thing;

                    if (thing != null)
                    {
                        bool willDestroy = false;

                        // === PREDICT DESTRUCTION ===
                        if (comp.Props.fullyDestroyThing)
                        {
                            willDestroy = true;
                        }
                        else
                        {
                            if (thing.def.useHitPoints && !comp.Props.ignoreUseHitPoints)
                            {
                                int damage = (int)((float)thing.MaxHitPoints * comp.Props.percentageOfDestruction);
                                if (thing.HitPoints - damage <= 0) willDestroy = true;
                            }
                            else
                            {
                                int consumeAmount = (int)(comp.Props.percentageOfDestruction * (float)thing.def.stackLimit);
                                if (thing.stackCount - consumeAmount < 10) willDestroy = true;
                            }
                        }

                        // === EXECUTE DESTRUCTION LOGIC ===
                        if (willDestroy)
                        {
                            // Check if it's our custom component
                            if (comp is CompScrapEater scrapEater)
                            {
                                // Call our custom method (Handles Stripping + Forbidding + Scrap)
                                scrapEater.TrySpawnScrap(thing);
                            }
                            // Fallback: Normal stripping for non-ScrapEater creatures
                            else if (thing is Corpse c)
                            {
                                c.Strip();
                            }
                        }
                    }
                }

                // === RUN ORIGINAL VEF LOGIC ===
                // This will perform the actual destroy/damage
                if (originalInit != null)
                {
                    originalInit();
                }
            };
        }
    }
}