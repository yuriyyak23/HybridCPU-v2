using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public enum RuntimeEpochAdvanceDecision : byte
{
    Advanced = 0,
    DeniedWraparound = 1,
}

public readonly record struct RuntimeEpochAdvanceResult(
    RuntimeEpochAdvanceDecision Decision,
    ulong Epoch,
    bool RequiresDomainFlush)
{
    public bool Succeeded => Decision == RuntimeEpochAdvanceDecision.Advanced;

    public static RuntimeEpochAdvanceResult Advanced(ulong epoch) =>
        new(RuntimeEpochAdvanceDecision.Advanced, epoch, RequiresDomainFlush: false);

    public static RuntimeEpochAdvanceResult DeniedWraparound(ulong currentEpoch) =>
        new(RuntimeEpochAdvanceDecision.DeniedWraparound, currentEpoch, RequiresDomainFlush: true);
}

public static class MemoryDomainTranslationEpoch
{
    public static RuntimeEpochAdvanceResult TryAdvanceFailClosed(ulong current)
    {
        if (current == ulong.MaxValue)
        {
            return RuntimeEpochAdvanceResult.DeniedWraparound(current);
        }

        return RuntimeEpochAdvanceResult.Advanced(current + 1);
    }
}

public readonly record struct MemoryDomainTranslationControl(
    bool TranslationEnabled,
    bool AddressSpaceTaggingEnabled,
    ulong AddressSpaceRoot,
    ulong SecondStageRoot,
    ushort DomainTag,
    ushort AddressSpaceTag,
    ulong AddressSpaceGeneration,
    byte DefaultMemoryType,
    byte AddressSpaceTargetCount = 0)
{
    public const byte WriteBackMemoryType = 6;
    public const byte MaxAddressSpaceTargetCount = 4;

    public bool IsValid =>
        !TranslationEnabled ||
        ((AddressSpaceRoot & 0xFFFUL) == 0 &&
         (SecondStageRoot & 0xFFFUL) == 0 &&
         (!AddressSpaceTaggingEnabled || AddressSpaceTag != 0) &&
         AddressSpaceTargetCount <= MaxAddressSpaceTargetCount);

    public static MemoryDomainTranslationControl Disabled { get; } =
        new(
            TranslationEnabled: false,
            AddressSpaceTaggingEnabled: false,
            AddressSpaceRoot: 0,
            SecondStageRoot: 0,
            DomainTag: 0,
            AddressSpaceTag: 0,
            AddressSpaceGeneration: 0,
            DefaultMemoryType: WriteBackMemoryType,
            AddressSpaceTargetCount: 0);

    public AddressSpaceId ToAddressSpaceId(
        ulong secondStageEpoch,
        ulong addressSpaceTagEpoch) =>
        new(
            DomainTag,
            AddressSpaceTaggingEnabled ? AddressSpaceTag : (ushort)0,
            SecondStageRoot,
            secondStageEpoch,
            addressSpaceTagEpoch,
            AddressSpaceGeneration);
}
