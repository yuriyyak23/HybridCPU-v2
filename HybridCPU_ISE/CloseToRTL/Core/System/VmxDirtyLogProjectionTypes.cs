using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxDirtyWriteSource : byte
{
    Unknown = 0,
    Cpu = 1,
    Atomic = 2,
    Dma = 3,
    Lane6 = 4,
    Lane7 = 5,
    NptWriteProtect = 6,
}

public enum VmxDirtyLogOverflowPolicy : byte
{
    FailClosed = 0,
}

public readonly record struct VmxDirtyLogConfiguration(
    bool Enabled,
    ulong GuestPhysicalBase,
    ulong GuestPhysicalLimitExclusive,
    uint PageSize,
    uint MaxDirtyPages,
    VmxDirtyLogOverflowPolicy OverflowPolicy)
{
    public const uint DefaultPageSize = 4096;
    public const uint DefaultMaxDirtyPages = 1 << 20;

    public static VmxDirtyLogConfiguration Disabled { get; } =
        new(false, 0, 0, DefaultPageSize, DefaultMaxDirtyPages, VmxDirtyLogOverflowPolicy.FailClosed);

    public static VmxDirtyLogConfiguration EnabledForRange(
        ulong guestPhysicalBase,
        ulong guestPhysicalLimitExclusive,
        uint pageSize = DefaultPageSize,
        uint maxDirtyPages = DefaultMaxDirtyPages) =>
        new(
            true,
            guestPhysicalBase,
            guestPhysicalLimitExclusive,
            pageSize == 0 ? DefaultPageSize : pageSize,
            maxDirtyPages == 0 ? DefaultMaxDirtyPages : maxDirtyPages,
            VmxDirtyLogOverflowPolicy.FailClosed);
}

public readonly record struct VmxDirtyLogStatus(
    bool Enabled,
    uint PageSize,
    ulong Generation,
    ulong WriteSequence,
    bool Overflowed,
    int DirtyPageCount,
    ulong CpuWriteCount,
    ulong AtomicWriteCount,
    ulong DmaWriteCount,
    ulong Lane6WriteCount,
    ulong Lane7WriteCount,
    ulong NptWriteProtectCount,
    ulong DroppedWriteCount)
{
    public ulong TotalAcceptedWrites =>
        CpuWriteCount +
        AtomicWriteCount +
        DmaWriteCount +
        Lane6WriteCount +
        Lane7WriteCount +
        NptWriteProtectCount;
}

public sealed class VmxDirtyLogSnapshot
{
    public VmxDirtyLogSnapshot(
        bool enabled,
        uint pageSize,
        ulong guestPhysicalBase,
        ulong guestPhysicalLimitExclusive,
        ulong generation,
        ulong writeSequence,
        bool overflowed,
        IReadOnlyList<ulong> dirtyPageIndices)
    {
        Enabled = enabled;
        PageSize = pageSize;
        GuestPhysicalBase = guestPhysicalBase;
        GuestPhysicalLimitExclusive = guestPhysicalLimitExclusive;
        Generation = generation;
        WriteSequence = writeSequence;
        Overflowed = overflowed;
        DirtyPageIndices = dirtyPageIndices ?? Array.Empty<ulong>();
    }

    public bool Enabled { get; }

    public uint PageSize { get; }

    public ulong GuestPhysicalBase { get; }

    public ulong GuestPhysicalLimitExclusive { get; }

    public ulong Generation { get; }

    public ulong WriteSequence { get; }

    public bool Overflowed { get; }

    public IReadOnlyList<ulong> DirtyPageIndices { get; }

    public bool ContainsGuestPhysicalAddress(ulong address)
    {
        if (!Enabled ||
            address < GuestPhysicalBase ||
            PageSize == 0)
        {
            return false;
        }

        ulong pageIndex = (address - GuestPhysicalBase) / PageSize;
        for (int index = 0; index < DirtyPageIndices.Count; index++)
        {
            if (DirtyPageIndices[index] == pageIndex)
            {
                return true;
            }
        }

        return false;
    }
}
