using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class MatMulResourceModel
{
    public static MatMulResourceModel Default { get; } =
        new(
            setupCycles: 10,
            macsPerCycle: 4,
            outputElementsPerWritebackCycle: 8,
            maxQueueOccupancy: 2,
            memoryBandwidthBytesPerCycle: 64);

    public MatMulResourceModel(
        uint setupCycles,
        uint macsPerCycle,
        uint outputElementsPerWritebackCycle,
        uint maxQueueOccupancy,
        ulong memoryBandwidthBytesPerCycle)
    {
        if (macsPerCycle == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(macsPerCycle),
                "MatMul resource model requires a non-zero MAC throughput.");
        }

        if (outputElementsPerWritebackCycle == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputElementsPerWritebackCycle),
                "MatMul resource model requires non-zero writeback throughput.");
        }

        SetupCycles = setupCycles;
        MacsPerCycle = macsPerCycle;
        OutputElementsPerWritebackCycle = outputElementsPerWritebackCycle;
        MaxQueueOccupancy = maxQueueOccupancy;
        MemoryBandwidthBytesPerCycle = memoryBandwidthBytesPerCycle;
    }

    public uint SetupCycles { get; }

    public uint MacsPerCycle { get; }

    public uint OutputElementsPerWritebackCycle { get; }

    public uint MaxQueueOccupancy { get; }

    public ulong MemoryBandwidthBytesPerCycle { get; }

    public ulong EstimateLatencyCycles(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        checked
        {
            ulong macs = (ulong)descriptor.M * descriptor.N * descriptor.K;
            ulong outputElements = (ulong)descriptor.M * descriptor.N;
            ulong computeCycles = DivideRoundUp(macs, MacsPerCycle);
            ulong writebackCycles = DivideRoundUp(outputElements, OutputElementsPerWritebackCycle);
            return SetupCycles + computeCycles + writebackCycles;
        }
    }

    public ulong EstimateScratchBytes(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ushort inputBytes = MatMulDescriptorValidator.DatatypeSizeBytes(
            descriptor.Datatypes.InputDatatype);
        ushort accumulatorBytes = MatMulDescriptorValidator.DatatypeSizeBytes(
            descriptor.Datatypes.AccumulatorDatatype);
        checked
        {
            ulong aTile = (ulong)descriptor.TileM * descriptor.TileK * inputBytes;
            ulong bTile = (ulong)descriptor.TileK * descriptor.TileN * inputBytes;
            ulong cTile = (ulong)descriptor.TileM * descriptor.TileN * accumulatorBytes;
            return aTile + bTile + cTile;
        }
    }

    public AcceleratorResourceModel ToCapabilityResourceModel()
    {
        return new AcceleratorResourceModel(
            SetupCycles,
            cyclesPerElement: MacsPerCycle,
            MaxQueueOccupancy,
            scratchBytes: 4096,
            MemoryBandwidthBytesPerCycle);
    }

    private static ulong DivideRoundUp(ulong value, ulong divisor)
    {
        return value == 0
            ? 0
            : ((value - 1) / divisor) + 1;
    }
}
