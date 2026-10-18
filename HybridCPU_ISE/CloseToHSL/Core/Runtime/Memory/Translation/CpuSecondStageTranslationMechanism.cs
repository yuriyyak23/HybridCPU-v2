namespace YAKSys_Hybrid_CPU.Core.Memory;

internal enum CpuSecondStageFaultKind : byte
{
    None = 0,
    NotPresent = 1,
    PermissionDenied = 2,
    Misconfiguration = 3,
    OutsideOwnedAddressSpace = 4,
    SourceStale = 5,
}

internal readonly record struct CpuSecondStageTranslationResult(
    bool Succeeded,
    ulong CpuPhysicalAddress,
    CpuSecondStageFaultKind FaultKind,
    ulong NeutralAuxiliary)
{
    internal static CpuSecondStageTranslationResult Success(ulong cpuPhysicalAddress) =>
        new(true, cpuPhysicalAddress, CpuSecondStageFaultKind.None, 0);

    internal static CpuSecondStageTranslationResult Failed(
        CpuSecondStageFaultKind kind,
        ulong auxiliary) => new(false, 0, kind, auxiliary);
}

/// <summary>
/// Neutral typed encoding owned by the CPU translation contour. It is not a
/// VMX/EPT qualification value. Compatibility projection must map each member
/// explicitly and may not return this encoding unchanged.
/// </summary>
internal static class CpuSecondStageNeutralAuxiliary
{
    internal static ulong Encode(
        CpuInstructionTranslationAccessKind accessKind,
        ushort accessSize,
        CpuSecondStageFaultKind kind,
        byte pageWalkLevel) =>
        ((ulong)(byte)kind << 56) |
        ((ulong)(byte)accessKind << 48) |
        ((ulong)accessSize << 32) |
        ((ulong)pageWalkLevel << 24);

    internal static bool TryDecode(
        ulong value,
        out CpuSecondStageFaultKind kind,
        out CpuInstructionTranslationAccessKind accessKind,
        out ushort accessSize,
        out byte pageWalkLevel)
    {
        kind = (CpuSecondStageFaultKind)((value >> 56) & 0xFF);
        accessKind = (CpuInstructionTranslationAccessKind)((value >> 48) & 0xFF);
        accessSize = (ushort)((value >> 32) & 0xFFFF);
        pageWalkLevel = (byte)((value >> 24) & 0xFF);
        return (value & 0x00FF_FFFFUL) == 0 &&
            kind is >= CpuSecondStageFaultKind.NotPresent and <= CpuSecondStageFaultKind.SourceStale &&
            accessKind is CpuInstructionTranslationAccessKind.InstructionFetch or
                CpuInstructionTranslationAccessKind.ScalarLoad or
                CpuInstructionTranslationAccessKind.ScalarStore &&
            accessSize != 0 && pageWalkLevel is 1 or 2;
    }
}

internal static class CpuSecondStageTranslationMechanism
{
    private const ulong PageFrameMask = 0xFFFF_FFFF_FFFF_F000UL;
    private const ulong PresentBit = 1UL << 0;
    private const ulong ReadBit = 1UL << 1;
    private const ulong WriteBit = 1UL << 2;
    private const ulong ExecuteBit = 1UL << 3;
    private const ulong ReservedMisconfigurationBit = 1UL << 63;

    internal static CpuSecondStageTranslationResult Translate(
        in Core.CpuSecondStageTranslationSnapshot snapshot,
        ulong guestPhysicalAddress,
        CpuInstructionTranslationAccessKind accessKind,
        ushort accessSize,
        Processor.MainMemoryArea physicalMemory)
    {
        if (!snapshot.IsValid || accessSize == 0 ||
            !snapshot.AddressSpace.AllowsRange(guestPhysicalAddress, accessSize))
        {
            return Fault(
                CpuSecondStageFaultKind.OutsideOwnedAddressSpace,
                accessKind,
                accessSize,
                pageWalkLevel: 2);
        }

        ulong end = guestPhysicalAddress + accessSize - 1;
        if (end < guestPhysicalAddress ||
            (guestPhysicalAddress >> 12) != (end >> 12))
        {
            return Fault(
                CpuSecondStageFaultKind.OutsideOwnedAddressSpace,
                accessKind,
                accessSize,
                pageWalkLevel: 1);
        }

        uint directoryIndex = (uint)((guestPhysicalAddress >> 22) & 0x3FF);
        ulong directoryEntry = ReadCpuPhysicalWord(
            physicalMemory,
            snapshot.SecondStageRoot + directoryIndex * 8UL);
        CpuSecondStageFaultKind directoryFault = ValidateEntry(
            directoryEntry,
            accessKind,
            requireLeafPermission: false);
        if (directoryFault != CpuSecondStageFaultKind.None)
            return Fault(directoryFault, accessKind, accessSize, pageWalkLevel: 2);

        ulong tableBase = directoryEntry & PageFrameMask;
        uint tableIndex = (uint)((guestPhysicalAddress >> 12) & 0x3FF);
        ulong tableEntry = ReadCpuPhysicalWord(
            physicalMemory,
            tableBase + tableIndex * 8UL);
        CpuSecondStageFaultKind tableFault = ValidateEntry(
            tableEntry,
            accessKind,
            requireLeafPermission: true);
        if (tableFault != CpuSecondStageFaultKind.None)
            return Fault(tableFault, accessKind, accessSize, pageWalkLevel: 1);

        return CpuSecondStageTranslationResult.Success(
            (tableEntry & PageFrameMask) | (guestPhysicalAddress & 0xFFFUL));
    }

    private static CpuSecondStageFaultKind ValidateEntry(
        ulong entry,
        CpuInstructionTranslationAccessKind accessKind,
        bool requireLeafPermission)
    {
        if ((entry & ReservedMisconfigurationBit) != 0)
            return CpuSecondStageFaultKind.Misconfiguration;
        if ((entry & PresentBit) == 0)
            return CpuSecondStageFaultKind.NotPresent;
        if (!requireLeafPermission)
            return CpuSecondStageFaultKind.None;

        bool allowed = accessKind switch
        {
            CpuInstructionTranslationAccessKind.InstructionFetch => (entry & ExecuteBit) != 0,
            CpuInstructionTranslationAccessKind.ScalarLoad => (entry & ReadBit) != 0,
            CpuInstructionTranslationAccessKind.ScalarStore => (entry & WriteBit) != 0,
            _ => false,
        };
        return allowed ? CpuSecondStageFaultKind.None : CpuSecondStageFaultKind.PermissionDenied;
    }

    private static ulong ReadCpuPhysicalWord(
        Processor.MainMemoryArea physicalMemory,
        ulong physicalAddress)
    {
        Span<byte> bytes = stackalloc byte[8];
        return physicalMemory.TryReadPhysicalRange(physicalAddress, bytes)
            ? BitConverter.ToUInt64(bytes)
            : 0;
    }

    private static CpuSecondStageTranslationResult Fault(
        CpuSecondStageFaultKind kind,
        CpuInstructionTranslationAccessKind accessKind,
        ushort accessSize,
        byte pageWalkLevel) =>
        CpuSecondStageTranslationResult.Failed(
            kind,
            CpuSecondStageNeutralAuxiliary.Encode(
                accessKind,
                accessSize,
                kind,
                pageWalkLevel));
}
