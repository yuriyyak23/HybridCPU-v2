using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory;

public enum NestedMemoryCompositionStage : byte
{
    None = 0,
    L2GuestPageTable = 1,
    ChildDomainTranslation = 2,
    HostDomainTranslation = 3,
}

public readonly record struct NestedMemoryCompositionContext(
    MemoryDomainTranslationControl ChildTranslationControl,
    MemoryDomainTranslationControl HostTranslationControl,
    ushort ChildAddressSpaceTag,
    ulong ChildTranslationControlEpoch,
    ulong HostTranslationControlEpoch,
    ulong L2Generation)
{
    public bool IsValid => ChildTranslationControl.TranslationEnabled && HostTranslationControl.TranslationEnabled && ChildTranslationControl.IsValid && HostTranslationControl.IsValid;

    public AddressSpaceId ToAddressSpaceId() =>
        new(
            HostTranslationControl.DomainTag,
            ChildAddressSpaceTag,
            MixSecondStageRoots(ChildTranslationControl.SecondStageRoot, HostTranslationControl.SecondStageRoot),
            ChildTranslationControlEpoch ^ HostTranslationControlEpoch,
            HostTranslationControl.AddressSpaceTagEpochOrZero() ^ L2Generation,
            L2Generation);

    private static ulong MixSecondStageRoots(ulong childRoot, ulong hostRoot) =>
        ((childRoot << 17) | (childRoot >> 47)) ^ hostRoot ^ 0x9E37_79B9_7F4A_7C15UL;
}

public readonly record struct NestedMemoryCompositionResult(
    bool Succeeded,
    bool TlbHit,
    NestedMemoryCompositionStage FaultStage,
    NestedTranslationResult Translation,
    ulong L2GuestPhysicalAddress,
    ulong L1GuestPhysicalAddress,
    ulong HostPhysicalAddress,
    AddressSpaceId AddressSpace,
    string Message)
{
    public bool CausesL1Exit => !Succeeded && FaultStage == NestedMemoryCompositionStage.ChildDomainTranslation;

    public bool CausesL0Exit => !Succeeded && FaultStage == NestedMemoryCompositionStage.HostDomainTranslation;

    public static NestedMemoryCompositionResult Success(
        bool tlbHit,
        NestedTranslationResult translation,
        ulong l1GuestPhysicalAddress,
        AddressSpaceId addressSpace) =>
        new(
            true,
            tlbHit,
            NestedMemoryCompositionStage.None,
            translation,
            translation.GuestPhysicalAddress,
            l1GuestPhysicalAddress,
            translation.HostPhysicalAddress,
            addressSpace,
            string.Empty);

    public static NestedMemoryCompositionResult Fault(
        NestedMemoryCompositionStage stage,
        NestedTranslationResult translation,
        AddressSpaceId addressSpace,
        string message) =>
        new(
            false,
            false,
            stage,
            translation,
            translation.GuestPhysicalAddress,
            0,
            0,
            addressSpace,
            message);
}

public sealed class NestedMemoryCompositionService
{
    private const ulong PpnMask = 0xFFFF_FFFF_FFFF_F000UL;
    private const ulong PresentBit = 0x1UL;
    private const ulong ReadBit = 0x2UL;
    private const ulong WriteBit = 0x4UL;
    private const ulong ExecuteBit = 0x8UL;
    private const ulong ReservedMisconfigBit = 1UL << 63;

    private TLB _tlb;

    public int CountCachedTranslations() => _tlb.CountNestedEntries();

    public int InvalidateAll() => _tlb.FlushNestedAll();

    public int InvalidateByAddressSpaceTag(ushort addressSpaceTag) =>
        _tlb.FlushNestedByAddressSpaceTag(addressSpaceTag);

    public int InvalidateBySecondStageRoot(ulong secondStageRootIdentity) =>
        _tlb.FlushNestedBySecondStageRoot(secondStageRootIdentity);

    public NestedMemoryCompositionResult Translate(
        NestedMemoryCompositionContext context,
        ulong l2GuestVirtualAddress,
        NestedMemoryAccessType accessType,
        Processor.MainMemoryArea? mainMemory = null)
    {
        Processor.MainMemoryArea memory = mainMemory ?? Processor.MainMemory;
        AddressSpaceId addressSpace = context.ToAddressSpaceId();

        if (!context.IsValid)
        {
            NestedTranslationResult invalidControlFault = NestedTranslationResult.SecondStageMisconfiguration(
                l2GuestVirtualAddress,
                context.ChildTranslationControl.SecondStageRoot,
                accessType,
                pageWalkLevel: 0,
                causedByPageWalk: false);
            return NestedMemoryCompositionResult.Fault(
                NestedMemoryCompositionStage.HostDomainTranslation,
                invalidControlFault,
                addressSpace,
                "Nested memory composition requires valid child and host translation controls.");
        }

        if (_tlb.TryTranslateNested(
                l2GuestVirtualAddress,
                addressSpace,
                out ulong cachedHpa,
                out ulong cachedL2Gpa,
                out byte cachedPermissions,
                out byte cachedMemoryType,
                out NestedTlbTag cachedTag))
        {
            if (!NestedPageWalker.HasAccessPermission(cachedPermissions, accessType))
            {
                NestedTranslationResult permissionFault = NestedTranslationResult.SecondStageViolation(
                    l2GuestVirtualAddress,
                    cachedL2Gpa,
                    accessType,
                    pageWalkLevel: 0,
                    causedByPageWalk: false);
                return NestedMemoryCompositionResult.Fault(
                    NestedMemoryCompositionStage.ChildDomainTranslation,
                    permissionFault,
                    addressSpace,
                    "Nested TLB hit did not satisfy the requested access type.");
            }

            NestedTranslationResult hit = NestedTranslationResult.Success(
                l2GuestVirtualAddress,
                cachedL2Gpa,
                cachedHpa,
                accessType,
                cachedPermissions,
                cachedMemoryType,
                cachedTag);
            return NestedMemoryCompositionResult.Success(
                tlbHit: true,
                hit,
                l1GuestPhysicalAddress: 0,
                addressSpace);
        }

        if (!TryWalkL2GuestPageTables(
                context,
                l2GuestVirtualAddress,
                accessType,
                memory,
                out ulong l2GuestPhysicalAddress,
                out byte guestPermissions,
                out NestedMemoryCompositionResult fault))
        {
            return fault;
        }

        if (!TryTranslateL2GpaToHpa(
                context,
                l2GuestVirtualAddress,
                l2GuestPhysicalAddress,
                accessType,
                causedByPageWalk: false,
                pageWalkLevel: 0,
                memory,
                out ulong l1GuestPhysicalAddress,
                out ulong hostPhysicalAddress,
                out byte l1Permissions,
                out byte l0Permissions,
                out fault))
        {
            return fault;
        }

        byte permissions = (byte)(guestPermissions & l1Permissions & l0Permissions);
        if (!NestedPageWalker.HasAccessPermission(permissions, accessType))
        {
            NestedTranslationResult permissionFault = NestedTranslationResult.SecondStageViolation(
                l2GuestVirtualAddress,
                l2GuestPhysicalAddress,
                accessType,
                pageWalkLevel: 0,
                causedByPageWalk: false);
            return NestedMemoryCompositionResult.Fault(
                NestedMemoryCompositionStage.ChildDomainTranslation,
                permissionFault,
                addressSpace,
                "Composed L2 guest, child-domain, and host-domain translation permissions deny the access.");
        }

        NestedTlbTag tag = NestedTlbTag.Create(
            l2GuestVirtualAddress,
            addressSpace,
            permissions,
            context.HostTranslationControl.DefaultMemoryType);
        NestedTranslationResult translation = NestedTranslationResult.Success(
            l2GuestVirtualAddress,
            l2GuestPhysicalAddress,
            hostPhysicalAddress,
            accessType,
            permissions,
            context.HostTranslationControl.DefaultMemoryType,
            tag);

        _tlb.InsertNested(
            l2GuestVirtualAddress,
            l2GuestPhysicalAddress,
            hostPhysicalAddress,
            permissions,
            context.HostTranslationControl.DefaultMemoryType,
            addressSpace);

        return NestedMemoryCompositionResult.Success(
            tlbHit: false,
            translation,
            l1GuestPhysicalAddress,
            addressSpace);
    }

    private bool TryWalkL2GuestPageTables(
        NestedMemoryCompositionContext context,
        ulong l2GuestVirtualAddress,
        NestedMemoryAccessType finalAccessType,
        Processor.MainMemoryArea memory,
        out ulong l2GuestPhysicalAddress,
        out byte permissions,
        out NestedMemoryCompositionResult fault)
    {
        l2GuestPhysicalAddress = 0;
        permissions = 0;

        ulong pdeAddress = context.ChildTranslationControl.AddressSpaceRoot + (((l2GuestVirtualAddress >> 22) & 0x3FFUL) * 8UL);
        if (!TryReadL2GuestPhysicalWord(
                context,
                l2GuestVirtualAddress,
                pdeAddress,
                pageWalkLevel: 2,
                memory,
                out ulong pde,
                out fault))
        {
            return false;
        }

        if ((pde & PresentBit) == 0)
        {
            fault = GuestPageFault(context, l2GuestVirtualAddress, context.ChildTranslationControl.AddressSpaceRoot, finalAccessType, 2);
            return false;
        }

        ulong ptBaseL2Gpa = pde & PpnMask;
        ulong pteAddress = ptBaseL2Gpa + (((l2GuestVirtualAddress >> 12) & 0x3FFUL) * 8UL);
        if (!TryReadL2GuestPhysicalWord(
                context,
                l2GuestVirtualAddress,
                pteAddress,
                pageWalkLevel: 1,
                memory,
                out ulong pte,
                out fault))
        {
            return false;
        }

        if ((pte & PresentBit) == 0)
        {
            fault = GuestPageFault(context, l2GuestVirtualAddress, ptBaseL2Gpa, finalAccessType, 1);
            return false;
        }

        permissions = DecodeGuestPermissions(pte);
        l2GuestPhysicalAddress = (pte & PpnMask) | (l2GuestVirtualAddress & 0xFFFUL);
        fault = default;
        return true;
    }

    private bool TryReadL2GuestPhysicalWord(
        NestedMemoryCompositionContext context,
        ulong l2GuestVirtualAddress,
        ulong l2GuestPhysicalAddress,
        int pageWalkLevel,
        Processor.MainMemoryArea memory,
        out ulong value,
        out NestedMemoryCompositionResult fault)
    {
        if (!TryTranslateL2GpaToHpa(
                context,
                l2GuestVirtualAddress,
                l2GuestPhysicalAddress,
                NestedMemoryAccessType.Read,
                causedByPageWalk: true,
                pageWalkLevel,
                memory,
                out _,
                out ulong hostPhysicalAddress,
                out _,
                out _,
                out fault))
        {
            value = 0;
            return false;
        }

        value = memory.ReadPhysicalWord(hostPhysicalAddress);
        return true;
    }

    private bool TryTranslateL2GpaToHpa(
        NestedMemoryCompositionContext context,
        ulong l2GuestVirtualAddress,
        ulong l2GuestPhysicalAddress,
        NestedMemoryAccessType accessType,
        bool causedByPageWalk,
        int pageWalkLevel,
        Processor.MainMemoryArea memory,
        out ulong l1GuestPhysicalAddress,
        out ulong hostPhysicalAddress,
        out byte l1Permissions,
        out byte l0Permissions,
        out NestedMemoryCompositionResult fault)
    {
        AddressSpaceId addressSpace = context.ToAddressSpaceId();
        if (!TryWalkSecondStage(
                context.ChildTranslationControl,
                l2GuestVirtualAddress,
                l2GuestPhysicalAddress,
                accessType,
                causedByPageWalk,
                pageWalkLevel,
                memory,
                out l1GuestPhysicalAddress,
                out l1Permissions,
                out NestedTranslationResult l1Fault))
        {
            hostPhysicalAddress = 0;
            l0Permissions = 0;
            fault = NestedMemoryCompositionResult.Fault(
                NestedMemoryCompositionStage.ChildDomainTranslation,
                l1Fault,
                addressSpace,
                "L1 second-stage translation rejected the L2 GPA.");
            return false;
        }

        if (!TryWalkSecondStage(
                context.HostTranslationControl,
                l2GuestVirtualAddress,
                l1GuestPhysicalAddress,
                accessType,
                causedByPageWalk,
                pageWalkLevel,
                memory,
                out hostPhysicalAddress,
                out l0Permissions,
                out NestedTranslationResult l0Fault))
        {
            fault = NestedMemoryCompositionResult.Fault(
                NestedMemoryCompositionStage.HostDomainTranslation,
                l0Fault,
                addressSpace,
                "L0 second-stage/IOMMU policy rejected the L1 GPA.");
            return false;
        }

        fault = default;
        return true;
    }

    private static bool TryWalkSecondStage(
        MemoryDomainTranslationControl control,
        ulong l2GuestVirtualAddress,
        ulong guestPhysicalAddress,
        NestedMemoryAccessType accessType,
        bool causedByPageWalk,
        int pageWalkLevel,
        Processor.MainMemoryArea memory,
        out ulong translatedPhysicalAddress,
        out byte permissions,
        out NestedTranslationResult fault)
    {
        translatedPhysicalAddress = 0;
        permissions = 0;
        fault = default;

        if (!control.TranslationEnabled || !control.IsValid)
        {
            fault = NestedTranslationResult.SecondStageMisconfiguration(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        ulong epdeAddress = control.SecondStageRoot + (((guestPhysicalAddress >> 22) & 0x3FFUL) * 8UL);
        ulong epde = memory.ReadPhysicalWord(epdeAddress);
        if (IsMisconfiguredEntry(epde))
        {
            fault = NestedTranslationResult.SecondStageMisconfiguration(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        if ((epde & PresentBit) == 0)
        {
            fault = NestedTranslationResult.SecondStageViolation(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        ulong tableBase = epde & PpnMask;
        ulong epteAddress = tableBase + (((guestPhysicalAddress >> 12) & 0x3FFUL) * 8UL);
        ulong epte = memory.ReadPhysicalWord(epteAddress);
        if (IsMisconfiguredEntry(epte))
        {
            fault = NestedTranslationResult.SecondStageMisconfiguration(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        if ((epte & PresentBit) == 0)
        {
            fault = NestedTranslationResult.SecondStageViolation(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        permissions = DecodeSecondStagePermissions(epte);
        if (!NestedPageWalker.HasAccessPermission(permissions, accessType))
        {
            fault = NestedTranslationResult.SecondStageViolation(
                l2GuestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk);
            return false;
        }

        translatedPhysicalAddress = (epte & PpnMask) | (guestPhysicalAddress & 0xFFFUL);
        return true;
    }

    private NestedMemoryCompositionResult GuestPageFault(
        NestedMemoryCompositionContext context,
        ulong l2GuestVirtualAddress,
        ulong guestPhysicalAddress,
        NestedMemoryAccessType accessType,
        int pageWalkLevel)
    {
        NestedTranslationResult fault = NestedTranslationResult.GuestPageFault(
            l2GuestVirtualAddress,
            guestPhysicalAddress,
            accessType,
            pageWalkLevel);
        return NestedMemoryCompositionResult.Fault(
            NestedMemoryCompositionStage.L2GuestPageTable,
            fault,
            context.ToAddressSpaceId(),
            "L2 guest page tables rejected the L2 GVA.");
    }

    private static byte DecodeGuestPermissions(ulong entry)
    {
        byte permissions = 0;
        if ((entry & ReadBit) != 0)
        {
            permissions |= NestedPageWalker.ReadPermission;
        }

        if ((entry & WriteBit) != 0)
        {
            permissions |= NestedPageWalker.WritePermission;
        }

        if ((entry & PresentBit) != 0)
        {
            permissions |= NestedPageWalker.ExecutePermission;
        }

        return permissions;
    }

    private static byte DecodeSecondStagePermissions(ulong entry)
    {
        byte permissions = 0;
        if ((entry & ReadBit) != 0)
        {
            permissions |= NestedPageWalker.ReadPermission;
        }

        if ((entry & WriteBit) != 0)
        {
            permissions |= NestedPageWalker.WritePermission;
        }

        if ((entry & ExecuteBit) != 0)
        {
            permissions |= NestedPageWalker.ExecutePermission;
        }

        return permissions;
    }

    private static bool IsMisconfiguredEntry(ulong entry) =>
        (entry & ReservedMisconfigBit) != 0;
}

internal static class MemoryDomainTranslationControlNestedExtensions
{
    public static ulong AddressSpaceTagEpochOrZero(this MemoryDomainTranslationControl control) =>
        control.AddressSpaceTaggingEnabled ? control.AddressSpaceGeneration : 0;
}
