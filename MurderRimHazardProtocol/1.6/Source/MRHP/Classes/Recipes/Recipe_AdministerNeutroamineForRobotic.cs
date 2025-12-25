using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace MRHP
{
    public class Recipe_AdministerNeutroamineForRobotic : Recipe_AdministerIngestible
    {
        // 1. VISIBILITY LOGIC
        // If they don't have NeutroLoss, the recipe is hidden.
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || !pawn.IsRobotic()) return false;

            // CHECK HEALTH
            HediffDef lossDef = VREA_DefOf.VREA_NeutroLoss;
            if (lossDef != null)
            {
                // Do they have the leak?
                if (!pawn.health.hediffSet.HasHediff(lossDef)) return false;
            }

            return base.AvailableOnNow(thing, part);
        }

        // 2. INGREDIENT CALCULATION
        public override float GetIngredientCount(IngredientCount ing, Bill bill)
        {
            BillStack billStack = bill.billStack;
            Pawn pawn = ((billStack != null) ? billStack.billGiver : null) as Pawn;
            if (pawn == null) return base.GetIngredientCount(ing, bill);

            Hediff loss = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss, false);
            if (loss == null) return 0f; // Should be handled by AvailableOnNow, but safety first.

            ThingDef neutro = ing.filter.AllowedThingDefs.FirstOrDefault();
            if (neutro == null) return 0f;

            // Calculate needed
            // Logic: 1 Neutroamine cures 0.01 Severity.
            // Example: Severity 0.15 needs 15 units.
            float needed = Mathf.Ceil(loss.Severity / 0.01f);

            return needed;
        }

        // 3. APPLY CURE
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff loss = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss, false);
            if (loss != null)
            {
                ThingDef neutro = ingredients.FirstOrDefault()?.def;
                if (neutro != null)
                {
                    int count = ingredients.Where(x => x.def == neutro).Sum(x => x.stackCount);

                    // Reduce severity
                    loss.Severity -= (float)count * 0.01f;

                    if (loss.Severity <= 0.001f)
                    {
                        pawn.health.RemoveHediff(loss);
                    }
                }
            }
            ingredients.ForEach(x => x.Destroy(DestroyMode.Vanish));
        }
    }
}