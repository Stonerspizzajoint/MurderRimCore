using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace MRHP
{
    public class QuestNode_Root_SentinelBunker : QuestNode_Root_Loot_AncientComplex
    {
        protected override LayoutDef LayoutDef
        {
            get
            {
                // FAST: Uses cached reference
                return MRHP_DefOf.MRHP_Complex_SentinelBunker;
            }
        }

        protected override SitePartDef SitePartDef
        {
            get
            {
                // FAST: Uses cached reference
                return MRHP_DefOf.MRHP_SentinelLair_Wild;
            }
        }

        protected override bool BeforeRunInt()
        {
            // Add any DLC checks here if needed (e.g. ModLister.CheckBiotech)
            return true;
        }

        protected override void RunInt()
        {
            base.RunInt();
        }
    }
}