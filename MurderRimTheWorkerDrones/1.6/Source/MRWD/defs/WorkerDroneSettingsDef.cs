using System.Linq;
using RimWorld;
using Verse;
using MurderRimCore;

namespace MRWD
{
    public class WorkerDroneSettingsDef : Def
    {
        public WorkerDroneBackstorySettings BackstorySettings = new WorkerDroneBackstorySettings();
        public WorkerDroneSpawnModSettings SpawnSettings = new WorkerDroneSpawnModSettings();

        public static WorkerDroneSettingsDef Current =>
            DefDatabase<WorkerDroneSettingsDef>.AllDefsListForReading.FirstOrDefault();

        public static WorkerDroneBackstorySettings Backstory => Current?.BackstorySettings;
        public static WorkerDroneSpawnModSettings Spawn => Current?.SpawnSettings;
    }
}
