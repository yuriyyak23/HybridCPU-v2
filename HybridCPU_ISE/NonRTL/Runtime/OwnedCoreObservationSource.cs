using System;
using System.Threading;
using HybridCPU_ISE.Machine;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.NonRTL.Runtime;

/// <summary>
/// Observation of one existing core on its execution-owner thread, between cycles.
/// The host must not execute this core concurrently or transfer ownership while bound.
/// This source neither executes nor stops the CPU and never reads global Processor state.
/// </summary>
public sealed class OwnedCoreObservationSource : IIseMachineStateSource, IDisposable
{
    private readonly Processor.CPU_Core core;
    private readonly Thread owner = Thread.CurrentThread;
    private bool closed;

    public OwnedCoreObservationSource(Processor.CPU_Core core)
    {
        ArgumentNullException.ThrowIfNull(core);
        if (core.CoreID != 0) throw new ArgumentException("Only runner core zero is supported.", nameof(core));
        this.core = core;
    }

    public MachineStateSourceProvenance SourceProvenance => MachineStateSourceProvenance.LiveCore;

    public int GetCoreCount() { EnsureOwner(); return 1; }

    public CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId)
    {
        EnsureOwner();
        if (coreId != 0 || core.CoreID != 0)
            throw new ArgumentOutOfRangeException(nameof(coreId), "This binding supports only the runner's core zero.");
        return CpuCoreDiagnosticSnapshot.Capture(core);
    }

    public void Dispose() { EnsureOwnerThread(); closed = true; }

    private void EnsureOwnerThread()
    {
        if (!ReferenceEquals(Thread.CurrentThread, owner))
            throw new InvalidOperationException("Core observation must execute on the bound CPU-owner thread.");
    }

    private void EnsureOwner()
    {
        EnsureOwnerThread();
        ObjectDisposedException.ThrowIf(closed, this);
    }

    private static T Unavailable<T>(string operation) =>
        throw new MachineStateSourceUnavailableException(nameof(OwnedCoreObservationSource), operation);

    public int GetPodCount() => Unavailable<int>(nameof(GetPodCount));
    public PodController? GetPod(int podIndex) => Unavailable<PodController?>(nameof(GetPod));
    public byte[] ReadMemory(ulong address, int length) => Unavailable<byte[]>(nameof(ReadMemory));
    public long GetTotalMemorySize() => Unavailable<long>(nameof(GetTotalMemorySize));
    public MemorySubsystem? GetMemorySubsystem() => Unavailable<MemorySubsystem?>(nameof(GetMemorySubsystem));
    public PerformanceReport GetPerformanceReport() => Unavailable<PerformanceReport>(nameof(GetPerformanceReport));
    public ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId) => Unavailable<ReplayPhaseMetrics>(nameof(GetReplayPhaseMetrics));
    public SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId) => Unavailable<SchedulerPhaseMetrics>(nameof(GetSchedulerPhaseMetrics));
    public TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash) =>
        Unavailable<TypedSlotTelemetryProfile?>(nameof(GetTypedSlotTelemetryProfile));
    public string GetReplayToken() => Unavailable<string>(nameof(GetReplayToken));
}
