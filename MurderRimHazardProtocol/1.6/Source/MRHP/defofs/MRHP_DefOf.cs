using RimWorld;
using Verse;
using VREAndroids;

namespace MRHP
{
    [DefOf]
    public static class MRHP_DefOf
    {
        public static MentalStateDef MRHP_AndroidRage;

        public static FleshTypeDef MRHP_JCJensonRobotFlesh;

        public static PawnKindDef MRHP_Sentinel;
        public static PawnKindDef MRHP_JCJenson_WorkerDrone;
        public static PawnKindDef MRHP_JCJenson_Human;

        public static JobDef MRHP_ExecuteAndroid;
        public static JobDef MRHP_SentinelMaul;
        public static JobDef MRHP_SentinelPounceJob;
        public static JobDef MRHP_RobotSelfSeal;
        public static JobDef MRHP_IngestScrap;

        public static AbilityDef MRHP_SentinelPounce;
        public static AbilityDef MRHP_SentinelFlash;

        public static AndroidGeneDef MRHP_BootLoopImmunity;
        public static AndroidGeneDef MRHP_BootLoopCritical;

        public static HediffDef MRHP_Pinned;
        public static HediffDef MRHP_BootLoop;
        public static HediffDef MRHP_BootLoopPerminent;
        public static HediffDef MRHP_MaterialDeprived;

        public static ComplexLayoutDef MRHP_Complex_SentinelBunker;

        public static SitePartDef MRHP_SentinelLair_Wild;

        public static ComplexThreatDef MRHP_Threat_SleepingSentinels;

        public static SoundDef Longjump_Jump;
        public static SoundDef Pawn_MeleeDodge;

        public static StatDef EMPResistance;

        public static ThingDef MRHP_SentinelLeapFlyer;

        public static ThingDef MRHP_Mote_BootLoop;

        static MRHP_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MRHP_DefOf));
        }
    }
}
