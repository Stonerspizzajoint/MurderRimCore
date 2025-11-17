using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MurderRimCore
{
    // Strict configuration: only explicit defName lists.
    public class SilhouetteGeneExtension : DefModExtension
    {
        public List<string> faceDependentThoughtDefNames;
        public List<string> silhouetteTargetThingDefNames;
    }
}
