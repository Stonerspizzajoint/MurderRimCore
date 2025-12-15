using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MRHP
{
    public class HediffCompProperties_Pinned : HediffCompProperties
    {
        public HediffCompProperties_Pinned()
        {
            compClass = typeof(HediffComp_Pinned);
        }
    }
}
