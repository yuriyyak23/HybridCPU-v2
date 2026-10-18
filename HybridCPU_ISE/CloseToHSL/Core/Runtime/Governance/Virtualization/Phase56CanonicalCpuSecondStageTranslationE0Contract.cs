namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Machine-readable governance description of the bounded Phase 56 contour.
/// It grants no execution or compatibility authority.
/// </summary>
internal static class Phase56CanonicalCpuSecondStageTranslationE0Contract
{
    internal const string TranslationOwner = "ExistingCpuInstructionTranslationOwner";
    internal const string ConfigurationOwner = "MemoryDomainRuntimeAndMemoryDomainDescriptorOnly";
    internal const string AddressModel = "ExplicitVirtualAddressToGuestPhysicalAddressToCpuPhysicalAddress";
    internal const string Producer = "CanonicalCpuSecondStageTranslationFaultProducer";
    internal const string Arbitration = "ExistingStageAwareOlderStageThenOrderedLaneWinner";
    internal const string Freshness = "IssuerSealedMemoryDomainOwnerBindingAddressSpaceGenerationAndRebindInvalidation";
    internal const string NeutralFacts = "TypedReasonQualificationGuestPhysicalAddressSecondStageViolationAuxiliary";
    internal const string DefaultBehavior = "IdentityAndSecondStageDisabled";
    internal const bool VmReadOpened = false;
    internal const bool VmWriteOpened = false;
    internal const bool VmxAuthorityGranted = false;
    internal const bool NestedOrIommuAuthorityUsed = false;
}
