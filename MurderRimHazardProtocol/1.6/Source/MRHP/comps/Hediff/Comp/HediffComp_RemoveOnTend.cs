using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MRHP
{
    public class HediffComp_RemoveOnTend : HediffComp
    {
        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            // Vanilla logic happens first
            base.CompTended(quality, maxQuality, batchPosition);

            // Our logic: If ANY tend occurred, remove the hediff.
            // Setting severity to 0 causes the hediff to be removed on the next tick check.
            this.parent.Severity = 0f;
        }
    }
}
