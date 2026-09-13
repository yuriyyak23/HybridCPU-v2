using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Immutable RF-10.10 publication payload for one completed canonical
/// VLOAD/VSTORE source read. The bounded retire window remains the only owner
/// allowed to publish the packed bytes to the destination surface.
/// </summary>
public sealed class VectorTransferRetireEffect
{
    private readonly byte[] _packedData;

    internal VectorTransferRetireEffect(
        uint opcode,
        ulong sourceAddress,
        ulong destinationAddress,
        ulong elementCount,
        int elementSize,
        ushort stride,
        ReadOnlySpan<byte> packedData)
    {
        Opcode = opcode;
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        ElementCount = elementCount;
        ElementSize = elementSize;
        Stride = stride;
        _packedData = packedData.ToArray();
    }

    public uint Opcode { get; }
    public ulong SourceAddress { get; }
    public ulong DestinationAddress { get; }
    public ulong ElementCount { get; }
    public int ElementSize { get; }
    public ushort Stride { get; }
    public ReadOnlyMemory<byte> PackedData => _packedData;

    internal byte[] CopyPackedData() => (byte[])_packedData.Clone();
}
