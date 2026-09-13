namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct MemoryDomainReadOnlyTranslationView(
    bool TranslationEnabled,
    bool AddressSpaceTaggingEnabled,
    bool OwnsSecondStageTranslation,
    ulong AddressSpaceRoot,
    ulong SecondStageRoot,
    ushort DomainTag,
    ushort AddressSpaceTag,
    ulong AddressSpaceGeneration,
    byte DefaultMemoryType,
    byte AddressSpaceTargetCount)
{
    public static MemoryDomainReadOnlyTranslationView From(
        MemoryDomainTranslationControl control,
        bool ownsSecondStageTranslation) =>
        new(
            control.TranslationEnabled,
            control.AddressSpaceTaggingEnabled,
            ownsSecondStageTranslation,
            control.AddressSpaceRoot,
            control.SecondStageRoot,
            control.DomainTag,
            control.AddressSpaceTag,
            control.AddressSpaceGeneration,
            control.DefaultMemoryType,
            control.AddressSpaceTargetCount);
}
