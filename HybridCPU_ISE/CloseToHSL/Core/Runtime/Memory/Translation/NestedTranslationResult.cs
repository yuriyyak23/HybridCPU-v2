namespace YAKSys_Hybrid_CPU.Memory
{
    public enum NestedMemoryAccessType : byte
    {
        Read = 0,
        Write = 1,
        Execute = 2,
    }

    public enum NestedTranslationStatus : byte
    {
        Success = 0,
        GuestPageFault = 1,
        SecondStageViolation = 2,
        SecondStageMisconfiguration = 3,
        SingleStage = 4,
    }

    /// <summary>
    /// Typed second-stage translation result: GVA to GPA to HPA, or the exact
    /// fault information needed for compatibility exit projection.
    /// </summary>
    public readonly record struct NestedTranslationResult(
        NestedTranslationStatus Status,
        ulong GuestVirtualAddress,
        ulong GuestPhysicalAddress,
        ulong HostPhysicalAddress,
        NestedMemoryAccessType AccessType,
        int PageWalkLevel,
        byte Permissions,
        byte MemoryType,
        NestedTlbTag TlbTag,
        TranslationViolationInfo Violation)
    {
        public bool Succeeded => Status is NestedTranslationStatus.Success or NestedTranslationStatus.SingleStage;

        public bool IsSecondStageFault =>
            Status is NestedTranslationStatus.SecondStageViolation or NestedTranslationStatus.SecondStageMisconfiguration;

        public static NestedTranslationResult Success(
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            ulong hostPhysicalAddress,
            NestedMemoryAccessType accessType,
            byte permissions,
            byte memoryType,
            NestedTlbTag tag) =>
            new(
                NestedTranslationStatus.Success,
                guestVirtualAddress,
                guestPhysicalAddress,
                hostPhysicalAddress,
                accessType,
                0,
                permissions,
                memoryType,
                tag,
                default);

        public static NestedTranslationResult SingleStage(
            ulong virtualAddress,
            ulong physicalAddress,
            NestedMemoryAccessType accessType,
            byte permissions) =>
            new(
                NestedTranslationStatus.SingleStage,
                virtualAddress,
                physicalAddress,
                physicalAddress,
                accessType,
                0,
                permissions,
                0,
                default,
                default);

        public static NestedTranslationResult GuestPageFault(
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            NestedMemoryAccessType accessType,
            int pageWalkLevel) =>
            new(
                NestedTranslationStatus.GuestPageFault,
                guestVirtualAddress,
                guestPhysicalAddress,
                0,
                accessType,
                pageWalkLevel,
                0,
                0,
                default,
                default);

        public static NestedTranslationResult SecondStageViolation(
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            NestedMemoryAccessType accessType,
            int pageWalkLevel,
            bool causedByPageWalk) =>
            FromSecondStageFault(
                NestedTranslationStatus.SecondStageViolation,
                guestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk,
                misconfiguration: false);

        public static NestedTranslationResult SecondStageMisconfiguration(
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            NestedMemoryAccessType accessType,
            int pageWalkLevel,
            bool causedByPageWalk) =>
            FromSecondStageFault(
                NestedTranslationStatus.SecondStageMisconfiguration,
                guestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk,
                misconfiguration: true);

        private static NestedTranslationResult FromSecondStageFault(
            NestedTranslationStatus status,
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            NestedMemoryAccessType accessType,
            int pageWalkLevel,
            bool causedByPageWalk,
            bool misconfiguration)
        {
            TranslationViolationInfo violation = TranslationViolationInfo.Create(
                guestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                causedByPageWalk,
                misconfiguration);

            return new(
                status,
                guestVirtualAddress,
                guestPhysicalAddress,
                0,
                accessType,
                pageWalkLevel,
                0,
                0,
                default,
                violation);
        }
    }
}
