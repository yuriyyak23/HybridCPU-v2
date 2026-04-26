using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE
{
    public sealed partial class IseObservationService
    {
        private readonly IIseMachineStateSource _machineStateSource;
        private readonly object _syncLock;

        public IseObservationService(IIseMachineStateSource machineStateSource, object syncLock)
        {
            ArgumentNullException.ThrowIfNull(machineStateSource);
            ArgumentNullException.ThrowIfNull(syncLock);

            _machineStateSource = machineStateSource;
            _syncLock = syncLock;
        }

        public MachineStateSourceProvenance SourceProvenance => _machineStateSource.SourceProvenance;

        private int GetCoreCount() => GetCoreCount(_machineStateSource);

        private static int GetCoreCount(IIseMachineStateSource machineStateSource) => machineStateSource.GetCoreCount();

        private Processor.CPU_Core GetCore(int coreId) => GetCore(_machineStateSource, coreId);

        private static Processor.CPU_Core GetCore(IIseMachineStateSource machineStateSource, int coreId) => machineStateSource.GetCore(coreId);

        private int GetPodCount() => GetPodCount(_machineStateSource);

        private static int GetPodCount(IIseMachineStateSource machineStateSource) => machineStateSource.GetPodCount();

        private PodController? GetPodOrNull(int podIndex) => GetPodOrNull(_machineStateSource, podIndex);

        private static PodController? GetPodOrNull(IIseMachineStateSource machineStateSource, int podIndex) => machineStateSource.GetPod(podIndex);

        private byte[] ReadMachineMemory(ulong address, int length) => ReadMachineMemory(_machineStateSource, address, length);

        private static byte[] ReadMachineMemory(IIseMachineStateSource machineStateSource, ulong address, int length) =>
            machineStateSource.ReadMemory(address, length);

        private long GetTotalMachineMemorySize() => GetTotalMachineMemorySize(_machineStateSource);

        private static long GetTotalMachineMemorySize(IIseMachineStateSource machineStateSource) =>
            machineStateSource.GetTotalMemorySize();

        private MemorySubsystem? GetMemorySubsystem() => GetMemorySubsystem(_machineStateSource);

        private static MemorySubsystem? GetMemorySubsystem(IIseMachineStateSource machineStateSource) =>
            machineStateSource.GetMemorySubsystem();
    }
}
