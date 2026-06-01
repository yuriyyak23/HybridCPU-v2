namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Complete TLB tag for runtime-owned second-stage translations.
    /// </summary>
    public readonly record struct NestedTlbTag(
        AddressSpaceId AddressSpace,
        ulong VirtualPageNumber,
        ulong SecondStageRootIdentity,
        byte Permissions,
        byte MemoryType,
        ulong TranslationEpoch)
    {
        public static NestedTlbTag Create(
            ulong virtualAddress,
            AddressSpaceId addressSpace,
            byte permissions,
            byte memoryType) =>
            new(
                addressSpace,
                virtualAddress >> 12,
                addressSpace.SecondStageRootIdentity,
                permissions,
                memoryType,
                addressSpace.SecondStageEpoch ^
                addressSpace.AddressSpaceTagEpoch ^
                addressSpace.AddressSpaceGeneration);

        public bool MatchesLookup(ulong virtualAddress, AddressSpaceId addressSpace) =>
            VirtualPageNumber == (virtualAddress >> 12) &&
            AddressSpace == addressSpace &&
            SecondStageRootIdentity == addressSpace.SecondStageRootIdentity;
    }
}
