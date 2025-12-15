using System;
using RimWorld;
using RimWorld.BaseGen;

namespace MRHP
{
    // Token: 0x02003910 RID: 14608
    public class SymbolResolver_SentinelComplex : SymbolResolver_AncientComplex_Base
    {
        // Token: 0x17003360 RID: 13152
        // (get) Token: 0x060142B6 RID: 82614 RVA: 0x00601763 File Offset: 0x005FF963
        protected override LayoutDef DefaultLayoutDef
        {
            get
            {
                return MRHP_DefOf.MRHP_Complex_SentinelBunker;
            }
        }

        // Token: 0x060142B7 RID: 82615 RVA: 0x0060176C File Offset: 0x005FF96C
        public override void Resolve(ResolveParams rp)
        {
            ResolveParams resolveParams = rp;
            resolveParams.floorDef = TerrainDefOf.PackedDirt;
            BaseGen.symbolStack.Push("outdoorsPath", resolveParams, null);
            BaseGen.symbolStack.Push("ensureCanReachMapEdge", rp, null);
            base.ResolveComplex(rp);
        }
    }
}