using Verse;

namespace MurderRimCore.MRWD
{
    public static class WorkerDroneDependency
    {
        // Primary packageId (About.xml <packageId>Stonerspizzajoint.TheWorkerDrones</packageId>)
        public const string PackageId = "Stonerspizzajoint.TheWorkerDrones";

        public static bool Active
        {
            get
            {
                // Fast path: packageId match
                if (ModLister.GetActiveModWithIdentifier(PackageId) != null)
                    return true;

                return false;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class WorkerDroneSettingsInitLog
    {
        static WorkerDroneSettingsInitLog()
        {
            if (!WorkerDroneDependency.Active)
                Log.Message($"[MRC] Worker Drone dependency '{WorkerDroneDependency.PackageId}' not active. Drone settings UI disabled.");
            else
                Log.Message("[MRC] Worker Drone dependency active. Settings loaded.");
        }
    }
}
