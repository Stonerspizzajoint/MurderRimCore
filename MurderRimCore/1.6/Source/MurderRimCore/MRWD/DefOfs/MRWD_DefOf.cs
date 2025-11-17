using RimWorld;
using Verse;
using Verse.AI;

namespace MurderRimCore.MRWD
{
    [DefOf]
    public static class MRWD_DefOf
    {
        //  MurderRim: Worker Drones
        // GeneDefs
        public static GeneDef MRWD_DroneBody;
        public static GeneDef MRWD_AlwaysHardhat;
        public static GeneDef MRWD_ObservationalLearning;

        // TraitDefs
        public static TraitDef MRWD_DoorEnthusiast;

        // NeedDefs
        public static NeedDef MRWD_DoorSatisfaction;

        // ThoughtDefs
        public static ThoughtDef MRWD_DoorEnthusiast_Thought;
        public static ThoughtDef MRWD_DoorLost;

        // ThingDefs
        public static ThingDef MRWD_Headgear_Hardhat;

        static MRWD_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MRWD_DefOf));
        }
    }
}
