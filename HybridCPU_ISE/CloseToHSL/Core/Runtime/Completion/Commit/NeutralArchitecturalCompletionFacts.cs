namespace YAKSys_Hybrid_CPU.Core;

internal enum NeutralArchitecturalCompletionClass : byte
{
    None = 0,
    TrapEntry = 1,
    SystemEvent = 2,
    TranslationFault = 3,
}

public enum NeutralFaultAddressSemantic : byte
{
    None = 0,
    VirtualAddress = 1,
    GuestPhysicalAddress = 2,
    HostPhysicalAddress = 3,
}

internal enum NeutralFaultAuxiliarySemantic : byte
{
    None = 0,
    TranslationFault = 1,
    SecondStageTranslationViolation = 2,
    AccessPolicy = 3,
}

internal readonly record struct NeutralScalarFact(bool IsPresent, ulong Value)
{
    internal static NeutralScalarFact Absent => default;

    internal static NeutralScalarFact Present(ulong value) => new(true, value);
}

internal readonly record struct NeutralAddressFact(
    bool IsPresent,
    ulong Value,
    NeutralFaultAddressSemantic Semantic)
{
    internal static NeutralAddressFact Absent => default;

    internal static NeutralAddressFact Present(
        ulong value,
        NeutralFaultAddressSemantic semantic)
    {
        if (semantic == NeutralFaultAddressSemantic.None)
            throw new ArgumentOutOfRangeException(nameof(semantic));
        return new(true, value, semantic);
    }
}

internal readonly record struct NeutralAuxiliaryFact(
    bool IsPresent,
    ulong Value,
    NeutralFaultAuxiliarySemantic Semantic)
{
    internal static NeutralAuxiliaryFact Absent => default;

    internal static NeutralAuxiliaryFact Present(
        ulong value,
        NeutralFaultAuxiliarySemantic semantic)
    {
        if (semantic == NeutralFaultAuxiliarySemantic.None)
            throw new ArgumentOutOfRangeException(nameof(semantic));
        return new(true, value, semantic);
    }
}

internal readonly record struct NeutralArchitecturalCompletionFacts(
    NeutralArchitecturalCompletionClass CompletionClass,
    NeutralScalarFact Reason,
    NeutralScalarFact Qualification,
    NeutralAddressFact FaultAddress,
    NeutralAuxiliaryFact FaultAuxiliary);

internal readonly record struct ArchitecturalCompletionCandidate(
    ulong DomainId,
    int ContextId,
    int VirtualThreadId,
    ulong AttemptId,
    ulong EventId,
    NeutralArchitecturalCompletionFacts Facts);

internal static class ArchitecturalCompletionEventIdentity
{
    internal static ulong Create(ulong bundleSequence, int slotIndex)
    {
        if (bundleSequence == 0 || (uint)slotIndex >= BundleMetadata.BundleSlotCount)
            return 0;

        ulong identity = unchecked(
            (bundleSequence * 0x9E3779B185EBCA87UL) ^
            ((ulong)(uint)slotIndex + 0xD1B54A32D192ED03UL));
        return identity == 0 ? 1UL : identity;
    }
}
