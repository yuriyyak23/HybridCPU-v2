using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class AcceleratorResourceModel
{
    public AcceleratorResourceModel(
        uint baseLatencyCycles,
        uint cyclesPerElement,
        uint maxQueueOccupancy,
        ulong scratchBytes,
        ulong memoryBandwidthBytesPerCycle)
    {
        BaseLatencyCycles = baseLatencyCycles;
        CyclesPerElement = cyclesPerElement;
        MaxQueueOccupancy = maxQueueOccupancy;
        ScratchBytes = scratchBytes;
        MemoryBandwidthBytesPerCycle = memoryBandwidthBytesPerCycle;
    }

    public uint BaseLatencyCycles { get; }

    public uint CyclesPerElement { get; }

    public uint MaxQueueOccupancy { get; }

    public ulong ScratchBytes { get; }

    public ulong MemoryBandwidthBytesPerCycle { get; }

    public ulong EstimateLatencyCycles(ulong elementCount)
    {
        checked
        {
            return BaseLatencyCycles + (elementCount * CyclesPerElement);
        }
    }

    public uint EstimateQueueOccupancy(uint commandCount)
    {
        return Math.Min(commandCount, MaxQueueOccupancy);
    }
}
