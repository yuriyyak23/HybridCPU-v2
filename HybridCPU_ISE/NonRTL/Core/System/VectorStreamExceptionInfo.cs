namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct VectorStreamExceptionInfo(
    ushort ExecutionDomainTag,
    ushort AddressSpaceTag,
    ushort OwnerVirtualThreadId,
    uint ExceptionMask,
    uint ExceptionPriority,
    byte HighestExceptionIndex,
    ulong FaultingPc,
    ulong FaultingLane,
    uint FaultingOpcode,
    VectorExceptionAction Action,
    ulong Sequence)
{
    public bool IsValid => ExecutionDomainTag != 0 && HighestExceptionIndex < 5;

    public ulong EncodeCompatibilityQualification() =>
        ((ulong)HighestExceptionIndex & 0x7UL) |
        (((ulong)Action & 0x3UL) << 8) |
        (((ulong)OwnerVirtualThreadId & 0xFFFFUL) << 16) |
        (((ulong)AddressSpaceTag & 0xFFFFUL) << 32);

    public bool RequiresCompatibilityExitProjection =>
        Action == VectorExceptionAction.ReflectAsCompatibilityExit;
}
