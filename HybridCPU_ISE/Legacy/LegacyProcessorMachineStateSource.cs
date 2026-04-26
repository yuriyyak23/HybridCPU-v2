using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Legacy
{
    public enum LegacyMemoryReadPolicy
    {
        FailClosed = 0,
        ZeroPaddedCompatibility = 1,
    }

    /// <summary>
    /// Explicit legacy bridge over the process-global Processor.* runtime surface.
    /// Host code must opt into this contour deliberately and provide the shared sync root
    /// when constructing <see cref="IseObservationService"/>.
    /// </summary>
    public sealed class LegacyProcessorMachineStateSource : IIseMachineStateSource
    {
        private readonly LegacyMemoryReadPolicy _memoryReadPolicy;

        public static object SharedSyncRoot { get; } = new();

        internal LegacyProcessorMachineStateSource(LegacyMemoryReadPolicy memoryReadPolicy)
        {
            _memoryReadPolicy = memoryReadPolicy;
        }

        public MachineStateSourceProvenance SourceProvenance => MachineStateSourceProvenance.LegacyGlobal;

        public int GetCoreCount()
        {
            Processor.CPU_Core[] cores = Processor.CPU_Cores;
            if (cores == null || cores.Length == 0)
            {
                return 0;
            }

            int initializedCoreCount = 0;
            for (int coreIndex = 0; coreIndex < cores.Length; coreIndex++)
            {
                Processor.CPU_Core core = cores[coreIndex];
                if (core.ArchContexts == null || core.ArchContexts.Length != Processor.CPU_Core.SmtWays)
                {
                    break;
                }

                initializedCoreCount++;
            }

            return initializedCoreCount;
        }

        public Processor.CPU_Core GetCore(int coreId) => Processor.CPU_Cores[coreId];

        public int GetPodCount() => Processor.Pods.Length;

        public PodController? GetPod(int podIndex) => Processor.Pods[podIndex];

        public byte[] ReadMemory(ulong address, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            byte[] buffer = new byte[length];
            Processor.MainMemoryArea? mainMemory = Processor.MainMemory;

            if (mainMemory == null)
            {
                throw new LegacyMachineStateReadException(
                    address,
                    length,
                    memoryLength: -1,
                    LegacyMachineStateReadFailureKind.MainMemoryUnavailable);
            }

            if (address > long.MaxValue)
            {
                throw new LegacyMachineStateReadException(
                    address,
                    length,
                    mainMemory.Length,
                    LegacyMachineStateReadFailureKind.AddressOutOfRange);
            }

            long baseAddress = (long)address;
            if (baseAddress >= mainMemory.Length)
            {
                return HandleOutOfRangeRead(address, length, mainMemory.Length, buffer);
            }

            long availableBytes = mainMemory.Length - baseAddress;
            int bytesToRead = (int)Math.Min(length, availableBytes);
            if (bytesToRead < length && _memoryReadPolicy != LegacyMemoryReadPolicy.ZeroPaddedCompatibility)
            {
                throw new LegacyMachineStateReadException(
                    address,
                    length,
                    mainMemory.Length,
                    LegacyMachineStateReadFailureKind.PartialReadRequiresCompatibilityPadding);
            }

            long originalPosition = mainMemory.Position;

            try
            {
                mainMemory.Position = baseAddress;
                _ = mainMemory.Read(buffer, 0, bytesToRead);
                return buffer;
            }
            catch (Exception ex)
            {
                throw new LegacyMachineStateReadException(
                    address,
                    length,
                    mainMemory.Length,
                    LegacyMachineStateReadFailureKind.ReadFault,
                    ex);
            }
            finally
            {
                mainMemory.Position = originalPosition;
            }
        }

        private byte[] HandleOutOfRangeRead(ulong address, int length, long memoryLength, byte[] buffer)
        {
            if (_memoryReadPolicy == LegacyMemoryReadPolicy.ZeroPaddedCompatibility)
            {
                return buffer;
            }

            throw new LegacyMachineStateReadException(
                address,
                length,
                memoryLength,
                LegacyMachineStateReadFailureKind.AddressOutOfRange);
        }

        public long GetTotalMemorySize() => Processor.MainMemory?.Length ?? 0;

        public MemorySubsystem? GetMemorySubsystem() => Processor.Memory;

        public PerformanceReport GetPerformanceReport() => Processor.GetPerformanceStats();

        public ReplayPhaseMetrics GetReplayPhaseMetrics(int coreId) => GetCore(coreId).GetReplayPhaseMetrics();

        public SchedulerPhaseMetrics GetSchedulerPhaseMetrics(int coreId) => GetCore(coreId).GetSchedulerPhaseMetrics();

        public TypedSlotTelemetryProfile? GetTypedSlotTelemetryProfile(int coreId, string programHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programHash);

            PodController? pod = GetPod(coreId / PodController.CORES_PER_POD);
            if (pod?.Scheduler is not { } scheduler)
            {
                return null;
            }

            return TelemetryExporter.BuildProfile(
                scheduler,
                programHash,
                GetCore(coreId).GetPipelineControl());
        }

        public string GetReplayToken() => Processor.GetReplayToken();
    }
}
