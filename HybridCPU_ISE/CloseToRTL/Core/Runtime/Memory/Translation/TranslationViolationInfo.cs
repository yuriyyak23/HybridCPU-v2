namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Typed second-stage translation fault payload carried to compatibility exit projection.
    /// </summary>
    public readonly record struct TranslationViolationInfo(
        ulong GuestVirtualAddress,
        ulong GuestPhysicalAddress,
        NestedMemoryAccessType AccessType,
        int PageWalkLevel,
        ulong QualificationBits,
        bool IsMisconfiguration)
    {
        public const ulong ReadAccessBit = 1UL << 0;
        public const ulong WriteAccessBit = 1UL << 1;
        public const ulong ExecuteAccessBit = 1UL << 2;
        public const ulong GuestLinearAddressValidBit = 1UL << 7;
        public const ulong CausedByPageWalkBit = 1UL << 8;
        public const ulong MisconfigurationBit = 1UL << 9;

        public static TranslationViolationInfo Create(
            ulong guestVirtualAddress,
            ulong guestPhysicalAddress,
            NestedMemoryAccessType accessType,
            int pageWalkLevel,
            bool causedByPageWalk,
            bool misconfiguration)
        {
            ulong qualification = GuestLinearAddressValidBit;
            qualification |= accessType switch
            {
                NestedMemoryAccessType.Read => ReadAccessBit,
                NestedMemoryAccessType.Write => WriteAccessBit,
                NestedMemoryAccessType.Execute => ExecuteAccessBit,
                _ => 0,
            };

            if (causedByPageWalk)
            {
                qualification |= CausedByPageWalkBit;
            }

            if (misconfiguration)
            {
                qualification |= MisconfigurationBit;
            }

            qualification |= ((ulong)(uint)pageWalkLevel & 0xFUL) << 12;

            return new(
                guestVirtualAddress,
                guestPhysicalAddress,
                accessType,
                pageWalkLevel,
                qualification,
                misconfiguration);
        }
    }
}
