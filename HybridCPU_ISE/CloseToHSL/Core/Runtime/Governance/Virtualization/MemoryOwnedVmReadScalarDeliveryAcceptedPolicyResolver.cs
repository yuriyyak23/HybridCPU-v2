using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal readonly record struct MemoryOwnedVmReadScalarDeliveryPolicyLookup(
    string DecisionId,
    string OperationNamespace,
    string OperationId,
    ImmutableArray<ushort> FieldIds,
    string SourceOwner,
    string ValueSource,
    VirtualizationDecisionResultAbiV2 ResultAbi,
    VirtualizationDecisionEffectClassV2 EffectClass,
    VirtualizationCapabilityRequirementV2 CapabilityRequirement,
    VirtualizationOperationMigrationPolicyV2 MigrationPolicy,
    bool Revoked);

internal readonly record struct MemoryOwnedVmReadScalarDeliveryPolicyResolution(
    AcceptedVmReadScalarDeliveryDecisionV2? Policy,
    string Reason)
{
    internal bool IsResolved => Policy is not null;
    internal bool RuntimeAuthorityGranted => false;
    internal bool SourceValueAvailable => false;
    internal bool ResultReceiptIssued => false;
}

internal static class MemoryOwnedVmReadScalarDeliveryAcceptedPolicyResolver
{
    internal const string AcceptanceContainingCommitSha =
        "8b3675b5eb4a1a83a7feff95e02c6d7b8e8f1920";

    internal static MemoryOwnedVmReadScalarDeliveryPolicyResolution Resolve(
        in MemoryOwnedVmReadScalarDeliveryPolicyLookup lookup)
    {
        if (lookup.Revoked ||
            lookup.DecisionId != MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId ||
            lookup.OperationNamespace != MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace ||
            lookup.OperationId != MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId ||
            lookup.SourceOwner != "MemoryDomainDescriptor" ||
            lookup.ValueSource != "MemoryDomainReadOnlyTranslationView" ||
            lookup.ResultAbi != VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister ||
            lookup.EffectClass != VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly ||
            lookup.CapabilityRequirement != VirtualizationCapabilityRequirementV2.None ||
            lookup.MigrationPolicy != VirtualizationOperationMigrationPolicyV2.DrainOnly ||
            lookup.FieldIds.IsDefaultOrEmpty ||
            !lookup.FieldIds.SequenceEqual(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ExactFieldIds))
        {
            return new(null,
                "Exact GuestCr3/EptPointer/Vpid/Cr3TargetCount scalar-delivery D2 lookup mismatched or was revoked.");
        }

        VmReadScalarDeliveryDecisionValidationResultV2 validation =
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                AcceptanceContainingCommitSha);
        return validation.IsAcceptedPolicyObject
            ? new(validation.AcceptedDecision,
                "Exact accepted memory-owned scalar-delivery governance policy resolved without runtime authority.")
            : new(null, validation.Reason);
    }

    internal static MemoryOwnedVmReadScalarDeliveryPolicyLookup ExactLookup(bool revoked = false) => new(
        MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
        MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
        MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
        Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ExactFieldIds,
        "MemoryDomainDescriptor",
        "MemoryDomainReadOnlyTranslationView",
        VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
        VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
        VirtualizationCapabilityRequirementV2.None,
        VirtualizationOperationMigrationPolicyV2.DrainOnly,
        revoked);
}
