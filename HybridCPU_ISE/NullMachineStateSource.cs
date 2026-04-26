using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE
{
    public sealed class MachineStateSourceUnavailableException : InvalidOperationException
    {
        public string SourceKind { get; }
        public string Operation { get; }

        public MachineStateSourceUnavailableException(string sourceKind, string operation)
            : base($"{sourceKind} machine-state source cannot provide {operation}; observation data is unavailable.")
        {
            SourceKind = string.IsNullOrWhiteSpace(sourceKind)
                ? "Unknown"
                : sourceKind;
            Operation = string.IsNullOrWhiteSpace(operation)
                ? "UnknownObservationOperation"
                : operation;
        }
    }

    public sealed class NullMachineStateSource : IIseMachineStateSource
    {
        private const string SourceKind = nameof(MachineStateSourceProvenance.Null);

        public static NullMachineStateSource Instance { get; } = new();

        public MachineStateSourceProvenance SourceProvenance => MachineStateSourceProvenance.Null;

        public int GetCoreCount() => ThrowUnavailable<int>(nameof(GetCoreCount));

        public Processor.CPU_Core GetCore(int coreId) => ThrowUnavailable<Processor.CPU_Core>(nameof(GetCore));

        public int GetPodCount() => ThrowUnavailable<int>(nameof(GetPodCount));

        public PodController? GetPod(int podIndex) => ThrowUnavailable<PodController?>(nameof(GetPod));

        public byte[] ReadMemory(ulong address, int length) => ThrowUnavailable<byte[]>(nameof(ReadMemory));

        public long GetTotalMemorySize() => ThrowUnavailable<long>(nameof(GetTotalMemorySize));

        public MemorySubsystem? GetMemorySubsystem() => ThrowUnavailable<MemorySubsystem?>(nameof(GetMemorySubsystem));

        public PerformanceReport GetPerformanceReport() => ThrowUnavailable<PerformanceReport>(nameof(GetPerformanceReport));

        public ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId) =>
            ThrowUnavailable<ReplayPhaseMetrics>(nameof(GetReplayPhaseMetrics));

        public SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId) =>
            ThrowUnavailable<SchedulerPhaseMetrics>(nameof(GetSchedulerPhaseMetrics));

        public TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash) =>
            ThrowUnavailable<TypedSlotTelemetryProfile?>(nameof(GetTypedSlotTelemetryProfile));

        public string GetReplayToken() => ThrowUnavailable<string>(nameof(GetReplayToken));

        private static T ThrowUnavailable<T>(string operation) =>
            throw new MachineStateSourceUnavailableException(SourceKind, operation);
    }
}
