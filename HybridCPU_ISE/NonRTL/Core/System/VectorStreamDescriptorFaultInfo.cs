namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct VectorStreamDescriptorFaultInfo(
    ushort ExecutionDomainTag,
    ushort AddressSpaceTag,
    ushort OwnerVirtualThreadId,
    StreamDescriptorFaultKind Kind,
    ulong GuestDescriptorAddress,
    uint DescriptorLength,
    ulong StreamReplayEpoch,
    ulong Sequence,
    string Message)
{
    public bool IsFaulted => Kind != StreamDescriptorFaultKind.None;

    public ulong EncodeCompatibilityQualification() =>
        ((ulong)Kind & 0xFFUL) |
        (((ulong)OwnerVirtualThreadId & 0xFFFFUL) << 16) |
        (((ulong)AddressSpaceTag & 0xFFFFUL) << 32);

    public static VectorStreamDescriptorFaultInfo None { get; } =
        new(
            ExecutionDomainTag: 0,
            AddressSpaceTag: 0,
            OwnerVirtualThreadId: 0,
            StreamDescriptorFaultKind.None,
            GuestDescriptorAddress: 0,
            DescriptorLength: 0,
            StreamReplayEpoch: 0,
            Sequence: 0,
            Message: string.Empty);
}
