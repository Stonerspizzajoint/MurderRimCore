using System.Linq;
using RimWorld;
using Verse;

namespace MurderRimCore.MRWD
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
