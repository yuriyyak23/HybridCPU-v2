using HybridCPU.ManagedRuntime;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

/// <summary>
/// Explicit production binding between ManagedRuntime heap storage and the exact ISE
/// MainMemory instance used by a CPU execution context. It never falls back to the
/// mutable Processor.MainMemory global.
/// </summary>
public sealed class HybridCpuIseHeapMemoryV1 : IHybridCpuManagedHeapMemoryV1
{
    private readonly Processor.MainMemoryArea _memory;

    public HybridCpuIseHeapMemoryV1(Processor.MainMemoryArea memory, ulong baseAddress, ulong sizeBytes)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        if (baseAddress == 0 || sizeBytes == 0 || baseAddress > ulong.MaxValue - sizeBytes ||
            baseAddress + sizeBytes > checked((ulong)memory.Length))
            throw new ArgumentOutOfRangeException(nameof(sizeBytes),
                "Managed heap range must be fully materializable in the explicitly bound ISE memory instance.");
        BaseAddress = baseAddress;
        SizeBytes = sizeBytes;
    }

    public ulong BaseAddress { get; }
    public ulong SizeBytes { get; }

    public bool Clear(ulong address, int byteCount)
    {
        if (!Range(address, byteCount)) return false;
        byte[] zeroes = new byte[Math.Min(byteCount, 64 * 1024)];
        int remaining = byteCount;
        ulong cursor = address;
        while (remaining != 0)
        {
            int count = Math.Min(remaining, zeroes.Length);
            if (!_memory.TryWritePhysicalRange(cursor, zeroes.AsSpan(0, count))) return false;
            cursor = checked(cursor + (ulong)count);
            remaining -= count;
        }
        return true;
    }

    public bool Read(ulong address, Span<byte> destination) =>
        Range(address, destination.Length) && _memory.TryReadPhysicalRange(address, destination);

    public bool Write(ulong address, ReadOnlySpan<byte> source) =>
        Range(address, source.Length) && _memory.TryWritePhysicalRange(address, source);

    private bool Range(ulong address, int byteCount)
    {
        if (byteCount < 0 || address < BaseAddress) return false;
        ulong relative = address - BaseAddress;
        return relative <= SizeBytes && (ulong)byteCount <= SizeBytes - relative;
    }
}
