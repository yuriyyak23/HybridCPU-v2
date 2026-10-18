using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Runtime page walker for composed address-space and second-stage translation.
    /// </summary>
    public static partial class NestedPageWalker
    {
        public const int NESTED_WALK_LATENCY_CYCLES = 32;

        public const byte ReadPermission = 0x02;
        public const byte WritePermission = 0x04;
        public const byte ExecutePermission = 0x08;

        private const ulong PPN_MASK = 0xFFFFFFFFFFFFF000UL;
        private const ulong PRESENT_BIT = 0x1UL;
        private const ulong READ_BIT = 0x2UL;
        private const ulong WRITE_BIT = 0x4UL;
        private const ulong EXECUTE_BIT = 0x8UL;
        private const ulong RESERVED_MISCONFIG_BIT = 1UL << 63;

        public static bool HasAccessPermission(byte permissions, NestedMemoryAccessType accessType) =>
            accessType switch
            {
                NestedMemoryAccessType.Read => (permissions & ReadPermission) != 0,
                NestedMemoryAccessType.Write => (permissions & WritePermission) != 0,
                NestedMemoryAccessType.Execute => (permissions & ExecutePermission) != 0,
                _ => false,
            };

        private static bool WalkGuestPageTable(
            MemoryDomainTranslationControl control,
            ulong guestVirtualAddress,
            NestedMemoryAccessType accessType,
            Processor.MainMemoryArea resolvedMemory,
            out ulong guestPhysicalAddress,
            out byte permissions,
            out NestedTranslationResult fault)
        {
            guestPhysicalAddress = 0;
            permissions = 0;
            fault = default;

            if (!WalkSecondStage(
                    control,
                    control.AddressSpaceRoot,
                    guestVirtualAddress,
                    NestedMemoryAccessType.Read,
                    causedByPageWalk: true,
                    pageWalkLevel: 2,
                    resolvedMemory,
                    out ulong cr3Hpa,
                    out _,
                    out _,
                    out fault))
            {
                return false;
            }

            uint guestDirIndex = (uint)((guestVirtualAddress >> 22) & 0x3FF);
            ulong pdeAddrHpa = cr3Hpa + guestDirIndex * 8;
            ulong pde = resolvedMemory.ReadPhysicalWord(pdeAddrHpa);
            if ((pde & PRESENT_BIT) == 0)
            {
                fault = NestedTranslationResult.GuestPageFault(
                    guestVirtualAddress,
                    control.AddressSpaceRoot,
                    accessType,
                    pageWalkLevel: 2);
                return false;
            }

            ulong ptBaseGpa = pde & PPN_MASK;
            if (!WalkSecondStage(
                    control,
                    ptBaseGpa,
                    guestVirtualAddress,
                    NestedMemoryAccessType.Read,
                    causedByPageWalk: true,
                    pageWalkLevel: 1,
                    resolvedMemory,
                    out ulong ptBaseHpa,
                    out _,
                    out _,
                    out fault))
            {
                return false;
            }

            uint guestTableIndex = (uint)((guestVirtualAddress >> 12) & 0x3FF);
            ulong pteAddrHpa = ptBaseHpa + guestTableIndex * 8;
            ulong pte = resolvedMemory.ReadPhysicalWord(pteAddrHpa);
            if ((pte & PRESENT_BIT) == 0)
            {
                fault = NestedTranslationResult.GuestPageFault(
                    guestVirtualAddress,
                    ptBaseGpa,
                    accessType,
                    pageWalkLevel: 1);
                return false;
            }

            guestPhysicalAddress = (pte & PPN_MASK) | (guestVirtualAddress & 0xFFF);
            permissions = DecodeGuestPermissions(pte);
            return true;
        }

        private static bool WalkSecondStage(
            MemoryDomainTranslationControl control,
            ulong guestPhysicalAddress,
            ulong guestVirtualAddress,
            NestedMemoryAccessType accessType,
            bool causedByPageWalk,
            int pageWalkLevel,
            Processor.MainMemoryArea resolvedMemory,
            out ulong hostPhysicalAddress,
            out byte permissions,
            out byte memoryType,
            out NestedTranslationResult fault)
        {
            hostPhysicalAddress = 0;
            permissions = 0;
            memoryType = control.DefaultMemoryType;
            fault = default;

            uint dirIndex = (uint)((guestPhysicalAddress >> 22) & 0x3FF);
            ulong epdeAddress = control.SecondStageRoot + dirIndex * 8;
            ulong epde = resolvedMemory.ReadPhysicalWord(epdeAddress);
            if (IsMisconfiguredEntry(epde))
            {
                fault = NestedTranslationResult.SecondStageMisconfiguration(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel,
                    causedByPageWalk);
                return false;
            }

            if ((epde & PRESENT_BIT) == 0)
            {
                fault = NestedTranslationResult.SecondStageViolation(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel,
                    causedByPageWalk);
                return false;
            }

            ulong secondStageBase = epde & PPN_MASK;
            uint tableIndex = (uint)((guestPhysicalAddress >> 12) & 0x3FF);
            ulong epteAddress = secondStageBase + tableIndex * 8;
            ulong epte = resolvedMemory.ReadPhysicalWord(epteAddress);
            if (IsMisconfiguredEntry(epte))
            {
                fault = NestedTranslationResult.SecondStageMisconfiguration(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel,
                    causedByPageWalk);
                return false;
            }

            if ((epte & PRESENT_BIT) == 0)
            {
                fault = NestedTranslationResult.SecondStageViolation(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel,
                    causedByPageWalk);
                return false;
            }

            permissions = DecodeSecondStagePermissions(epte);
            if (!HasAccessPermission(permissions, accessType))
            {
                fault = NestedTranslationResult.SecondStageViolation(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel,
                    causedByPageWalk);
                return false;
            }

            hostPhysicalAddress = (epte & PPN_MASK) | (guestPhysicalAddress & 0xFFF);
            return true;
        }

        private static byte DecodeGuestPermissions(ulong entry)
        {
            byte permissions = 0;
            if ((entry & READ_BIT) != 0) permissions |= ReadPermission;
            if ((entry & WRITE_BIT) != 0) permissions |= WritePermission;
            if ((entry & PRESENT_BIT) != 0) permissions |= ExecutePermission;
            return permissions;
        }

        private static byte DecodeSecondStagePermissions(ulong entry)
        {
            byte permissions = 0;
            if ((entry & READ_BIT) != 0) permissions |= ReadPermission;
            if ((entry & WRITE_BIT) != 0) permissions |= WritePermission;
            if ((entry & EXECUTE_BIT) != 0) permissions |= ExecutePermission;
            return permissions;
        }

        private static bool IsMisconfiguredEntry(ulong entry) =>
            (entry & RESERVED_MISCONFIG_BIT) != 0;
    }
}
