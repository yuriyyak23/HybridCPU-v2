using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE
{
    public interface IIseMachineStateSource
    {
        MachineStateSourceProvenance SourceProvenance { get; }
        int GetCoreCount();
        Processor.CPU_Core GetCore(int coreId);
        int GetPodCount();
        PodController? GetPod(int podIndex);
        byte[] ReadMemory(ulong address, int length);
        long GetTotalMemorySize();
        MemorySubsystem? GetMemorySubsystem();
        PerformanceReport GetPerformanceReport();
        ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId);
        SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId);
        TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash);
        string GetReplayToken();
    }
}
