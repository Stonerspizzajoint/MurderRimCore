using RimWorld;
using Verse;
using UnityEngine;

namespace MRWD
{
    public class Gene_DroneBody : Gene
    {
        public override void PostRemove()
        {
            base.PostRemove();

            // Only apply if the pawn has a story and is humanlike
            if (pawn?.story != null && pawn.def.race?.Humanlike == true)
            {
                // Set to a random human skin color
                float melanin = Random.Range(0f, 1f);
                pawn.story.SkinColorBase = PawnSkinColors.GetSkinColor(melanin);
            }
        }
    }
}

