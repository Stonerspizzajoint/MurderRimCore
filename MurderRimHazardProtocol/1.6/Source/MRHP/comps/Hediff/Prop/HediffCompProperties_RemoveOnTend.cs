using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MRHP
{
    public class HediffCompProperties_RemoveOnTend : HediffCompProperties
    {
        public HediffCompProperties_RemoveOnTend()
        {
            this.compClass = typeof(HediffComp_RemoveOnTend);
        }
    }
}
