using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;

namespace HybridCPU_ISE
{
    public sealed partial class IseObservationService
    {
        public PerformanceReport GetPerformanceReport()
        {
            lock (_syncLock)
            {
                return _machineStateSource.GetPerformanceReport();
            }
        }

        public ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                return _machineStateSource.GetReplayPhaseMetrics(coreId);
            }
        }

        public SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                return _machineStateSource.GetSchedulerPhaseMetrics(coreId);
            }
        }

        public TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programHash);

            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                return _machineStateSource.GetTypedSlotTelemetryProfile(coreId, programHash);
            }
        }

        public string GetReplayToken()
        {
            lock (_syncLock)
            {
                return _machineStateSource.GetReplayToken();
            }
        }
    }
}
