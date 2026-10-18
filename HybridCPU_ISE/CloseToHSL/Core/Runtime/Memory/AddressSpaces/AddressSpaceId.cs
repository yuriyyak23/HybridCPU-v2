namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Runtime-owned identity for second-stage translation cache entries.
    /// It is not compatibility frontend state and is not serialized through
    /// frozen field-alias projection vocabulary.
    /// </summary>
    public readonly record struct AddressSpaceId(
        ushort DomainTag,
        ushort AddressSpaceTag,
        ulong SecondStageRootIdentity,
        ulong SecondStageEpoch,
        ulong AddressSpaceTagEpoch,
        ulong AddressSpaceGeneration)
    {
        public bool MatchesSecondStageRoot(ulong secondStageRootIdentity) =>
            SecondStageRootIdentity == secondStageRootIdentity;

        public bool MatchesAddressSpaceTag(ushort addressSpaceTag) =>
            AddressSpaceTag == addressSpaceTag;

        public bool IsCurrent(ulong secondStageEpoch, ulong addressSpaceTagEpoch) =>
            SecondStageEpoch == secondStageEpoch &&
            AddressSpaceTagEpoch == addressSpaceTagEpoch;

        public ulong EncodeDescriptor() =>
            ((ulong)DomainTag << 48) |
            ((ulong)AddressSpaceTag << 32) |
            (SecondStageRootIdentity & 0xFFFF_FFFFUL);
    }
}
