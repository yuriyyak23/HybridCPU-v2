using System.Buffers.Binary;
using YAKSys_Hybrid_CPU;

namespace HybridCPU.ExternalRuntime;

// Private data pages only. This is an external MainMemoryArea implementation;
// instruction legality and the machine memory model remain owned by the ISE.
internal sealed class OwnedExecutableDataMemory : Processor.MainMemoryArea
{
    internal const int PageSize = 4096;
    private readonly object sync = new();
    private readonly Dictionary<ulong, byte[]> pages = new();
    private readonly long length;
    private readonly ulong maximumDataBytes;
    private bool retired;

    internal OwnedExecutableDataMemory(long length = 0x3000_0000, ulong maximumDataBytes = 0x3000_0000)
    {
        if (length <= 0 || length % PageSize != 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (maximumDataBytes == 0 || maximumDataBytes % PageSize != 0 || maximumDataBytes > (ulong)length)
            throw new ArgumentOutOfRangeException(nameof(maximumDataBytes));
        this.length = length;
        this.maximumDataBytes = maximumDataBytes;
    }

    public override long Length => length;
    internal ulong MaximumDataBytes => maximumDataBytes;
    internal ulong AllocatedDataBytes { get { lock (sync) return checked((ulong)pages.Count * PageSize); } }
    internal bool Retired { get { lock (sync) return retired; } }

    // The execution owner calls this only after its runner has proved terminal.
    // Retained references cannot allocate/read/write pages after this boundary.
    internal void Retire()
    {
        lock (sync)
        {
            retired = true;
            foreach (var page in pages.Values) Array.Clear(page);
            pages.Clear();
        }
    }

    public override bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
    {
        lock (sync)
        {
            if (retired || !InRange(physicalAddress, buffer.Length)) return false;
            for (int offset = 0; offset < buffer.Length;)
            {
                ulong address = physicalAddress + (ulong)offset;
                int pageOffset = (int)(address % PageSize);
                int count = Math.Min(PageSize - pageOffset, buffer.Length - offset);
                if (pages.TryGetValue(address / PageSize, out var page))
                    page.AsSpan(pageOffset, count).CopyTo(buffer[offset..]);
                else buffer.Slice(offset, count).Clear();
                offset += count;
            }
            return true;
        }
    }

    public override bool TryWritePhysicalRange(ulong physicalAddress, ReadOnlySpan<byte> buffer)
    {
        lock (sync)
        {
            if (retired || !InRange(physicalAddress, buffer.Length)) return false;
            if (buffer.Length == 0) return true;
            ulong first = physicalAddress / PageSize;
            ulong last = (physicalAddress + (ulong)buffer.Length - 1) / PageSize;
            ulong missing = 0;
            for (ulong index = first; index <= last; index++)
                if (!pages.ContainsKey(index)) missing++;
            // Capacity refusal precedes both allocation and mutation of existing pages.
            if (missing > maximumDataBytes / PageSize - (ulong)pages.Count) return false;
            // An allocation failure can retain a prefix of owned pages, but never
            // loses their authoritative inventory or publishes a successful write.
            for (ulong index = first; index <= last; index++)
                if (!pages.ContainsKey(index)) pages.Add(index, new byte[PageSize]);
            for (int offset = 0; offset < buffer.Length;)
            {
                ulong address = physicalAddress + (ulong)offset;
                int pageOffset = (int)(address % PageSize);
                int count = Math.Min(PageSize - pageOffset, buffer.Length - offset);
                buffer.Slice(offset, count).CopyTo(pages[address / PageSize].AsSpan(pageOffset, count));
                offset += count;
            }
            return true;
        }
    }

    public override ulong ReadPhysicalWord(ulong physicalAddress)
    {
        Span<byte> bytes = stackalloc byte[8];
        return TryReadPhysicalRange(physicalAddress, bytes) ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : 0;
    }

    private bool InRange(ulong address, int count) =>
        count >= 0 && address <= (ulong)length && (ulong)count <= (ulong)length - address;
}
