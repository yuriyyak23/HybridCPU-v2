using System;

namespace HybridCPU_ISE.Legacy
{
    public static class LegacyObservationServiceFactory
    {
        public static IseObservationService CreateLegacyGlobalCompat(object syncLock)
        {
            ArgumentNullException.ThrowIfNull(syncLock);
            return new IseObservationService(
                new LegacyProcessorMachineStateSource(LegacyMemoryReadPolicy.ZeroPaddedCompatibility),
                syncLock);
        }

        public static IseObservationService CreateLegacyGlobalStrict(object syncLock)
        {
            ArgumentNullException.ThrowIfNull(syncLock);
            return new IseObservationService(
                new LegacyProcessorMachineStateSource(LegacyMemoryReadPolicy.FailClosed),
                syncLock);
        }

        [Obsolete("Use CreateLegacyGlobalCompat(...) or CreateLegacyGlobalStrict(...) to make legacy-global observation selection explicit.")]
        public static IseObservationService CreateProcessorBacked(object syncLock) =>
            CreateLegacyGlobalCompat(syncLock);
    }
}
