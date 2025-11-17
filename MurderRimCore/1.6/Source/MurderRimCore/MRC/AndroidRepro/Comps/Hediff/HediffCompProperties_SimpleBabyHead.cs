using Verse;
using RimWorld;
using UnityEngine;

namespace MurderRimCore.AndroidRepro
{
    public class HediffCompProperties_SimpleBabyHead : HediffCompProperties
    {
        public string headTexPath;
        public string transparentBodyTexPath;

        public float offsetX = 0f;
        public float offsetZ = 0f;
        public float headScale = 1f;
        public float headLayerOffsetY = 0f;

        // Global eye scale
        public float eyeScale = 1f;

        // BASE eye offsets (apply to all facings)
        public float eyeOffsetX = 0f;
        public float eyeOffsetY = 0f;
        public float eyeOffsetZ = 0f;

        // Directional extras added on top of base
        // South (front)
        public float eyeSouthOffsetX = 0f;
        public float eyeSouthOffsetY = 0f;
        public float eyeSouthOffsetZ = 0f;

        // East
        public float eyeEastOffsetX = 0f;
        public float eyeEastOffsetY = 0f;
        public float eyeEastOffsetZ = 0f;

        // West
        public float eyeWestOffsetX = 0f;
        public float eyeWestOffsetY = 0f;
        public float eyeWestOffsetZ = 0f;

        public bool hideBodyApparel = true;
        public bool hideHeadgear = false;

        public HediffCompProperties_SimpleBabyHead()
        {
            compClass = typeof(HediffComp_SimpleBabyHead);
        }
    }

    public class HediffComp_SimpleBabyHead : HediffComp
    {
        public HediffCompProperties_SimpleBabyHead Props => (HediffCompProperties_SimpleBabyHead)props;

        public override void CompPostPostAdd(DamageInfo? dinfo) => ForceRefresh();
        public override void CompPostPostRemoved() => ForceRefresh();

        private void ForceRefresh()
        {
            try
            {
                Pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(Pawn);
            }
            catch { }
        }
    }
}