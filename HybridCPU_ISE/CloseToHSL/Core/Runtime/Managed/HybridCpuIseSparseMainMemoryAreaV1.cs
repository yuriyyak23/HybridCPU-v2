using System.Buffers.Binary;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

/// <summary>Bounded page-sparse physical memory for loader-backed guest evidence runs.</summary>
public sealed class HybridCpuIseSparseMainMemoryAreaV1 : Processor.MainMemoryArea
{
    private const int PageSize = 4096;
    private readonly Dictionary<ulong, byte[]> _pages = new();
    private readonly long _length;

    public HybridCpuIseSparseMainMemoryAreaV1(long length = 0x3000_0000)
    {
        if (length <= 0 || length % PageSize != 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        _length = length;
    }

    public override long Length => _length;

    public override bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
    {
        if (!InRange(physicalAddress, buffer.Length)) return false;
        for (int offset = 0; offset < buffer.Length;)
        {
            ulong address = physicalAddress + (ulong)offset;
            ulong pageIndex = address / PageSize;
            int pageOffset = (int)(address % PageSize);
            int count = Math.Min(PageSize - pageOffset, buffer.Length - offset);
            if (_pages.TryGetValue(pageIndex, out byte[]? page))
                page.AsSpan(pageOffset, count).CopyTo(buffer[offset..]);
            else
                buffer.Slice(offset, count).Clear();
            offset += count;
        }
        return true;
    }

    public override bool TryWritePhysicalRange(ulong physicalAddress, ReadOnlySpan<byte> buffer)
    {
        if (!InRange(physicalAddress, buffer.Length)) return false;
        for (int offset = 0; offset < buffer.Length;)
        {
            ulong address = physicalAddress + (ulong)offset;
            ulong pageIndex = address / PageSize;
            int pageOffset = (int)(address % PageSize);
            int count = Math.Min(PageSize - pageOffset, buffer.Length - offset);
            if (!_pages.TryGetValue(pageIndex, out byte[]? page))
                _pages.Add(pageIndex, page = new byte[PageSize]);
            buffer.Slice(offset, count).CopyTo(page.AsSpan(pageOffset, count));
            offset += count;
        }
        return true;
    }

    public override ulong ReadPhysicalWord(ulong physicalAddress)
    {
        Span<byte> bytes = stackalloc byte[8];
        return TryReadPhysicalRange(physicalAddress, bytes) ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : 0;
    }

    private bool InRange(ulong address, int count) =>
        count >= 0 && address <= (ulong)_length && (ulong)count <= (ulong)_length - address;
}
