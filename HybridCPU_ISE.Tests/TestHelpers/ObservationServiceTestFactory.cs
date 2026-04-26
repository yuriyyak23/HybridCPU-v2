using System;
using HybridCPU_ISE;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class ObservationServiceTestFactory
{
    internal static object SyncRoot { get; } = new();

    public static IseObservationService CreateEmptyService() =>
        new(NullMachineStateSource.Instance, SyncRoot);

    public static IseObservationService CreateSingleCoreService(
        Processor.CPU_Core core,
        Processor.MainMemoryArea? mainMemory = null,
        MemorySubsystem? memorySubsystem = null,
        PodController? pod = null,
        int podCount = 0)
    {
        int resolvedPodCount = Math.Max(podCount, pod is null ? 0 : 1);
        PodController?[] pods = resolvedPodCount == 0
            ? Array.Empty<PodController?>()
            : new PodController?[resolvedPodCount];
        if (resolvedPodCount > 0)
        {
            pods[0] = pod;
        }

        return new IseObservationService(
            new ObservationTestMachineStateSource(
                cores: new[] { core },
                pods: pods,
                mainMemory: mainMemory,
                memorySubsystem: memorySubsystem),
            SyncRoot);
    }
}

internal sealed class ObservationTestMachineStateSource : IIseMachineStateSource
{
    private readonly Processor.CPU_Core[] _cores;
    private readonly PodController?[] _pods;
    private readonly Processor.MainMemoryArea? _mainMemory;
    private readonly MemorySubsystem? _memorySubsystem;

    public ObservationTestMachineStateSource(
        Processor.CPU_Core[]? cores = null,
        PodController?[]? pods = null,
        Processor.MainMemoryArea? mainMemory = null,
        MemorySubsystem? memorySubsystem = null)
    {
        _cores = cores ?? Array.Empty<Processor.CPU_Core>();
        _pods = pods ?? Array.Empty<PodController?>();
        _mainMemory = mainMemory;
        _memorySubsystem = memorySubsystem;
    }

    public MachineStateSourceProvenance SourceProvenance => MachineStateSourceProvenance.LiveCore;

    public int GetCoreCount() => _cores.Length;

    public Processor.CPU_Core GetCore(int coreId) => _cores[coreId];

    public int GetPodCount() => _pods.Length;

    public PodController? GetPod(int podIndex) => _pods[podIndex];

    public byte[] ReadMemory(ulong address, int length)
    {
        var buffer = new byte[length];
        if (_mainMemory == null)
        {
            throw new InvalidOperationException("ObservationTestMachineStateSource requires initialized main memory.");
        }

        if ((long)address >= _mainMemory.Length)
        {
            return buffer;
        }

        int bytesToRead = (int)Math.Min((long)length, _mainMemory.Length - (long)address);
        bool readCompleted = _mainMemory.TryReadPhysicalRange(address, buffer.AsSpan(0, bytesToRead));
        if (!readCompleted)
        {
            throw new InvalidOperationException(
                $"ObservationTestMachineStateSource failed to read {bytesToRead} byte(s) at IOVA 0x{address:X}.");
        }

        return buffer;
    }

    public long GetTotalMemorySize() => _mainMemory?.Length ?? 0;

    public MemorySubsystem? GetMemorySubsystem() => _memorySubsystem;

    public PerformanceReport GetPerformanceReport() => new();

    public ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId) => GetCore(coreId).GetReplayPhaseMetrics();

    public SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId) => GetCore(coreId).GetSchedulerPhaseMetrics();

    public TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programHash);
        return null;
    }

    public string GetReplayToken() => string.Empty;
}
