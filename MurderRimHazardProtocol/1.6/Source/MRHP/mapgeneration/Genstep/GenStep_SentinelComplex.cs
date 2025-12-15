using System.Reflection;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace MRHP
{
    public class GenStep_SentinelComplex : GenStep_AncientComplex
    {
        protected override void GenerateComplex(Map map, ResolveParams parms)
        {
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("SentinelComplex", parms, null);
            BaseGen.Generate();
        }
    }
}