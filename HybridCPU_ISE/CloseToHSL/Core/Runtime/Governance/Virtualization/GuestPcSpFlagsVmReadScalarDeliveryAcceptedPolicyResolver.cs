using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal readonly record struct GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup(
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

internal readonly record struct GuestPcSpFlagsVmReadScalarDeliveryPolicyResolution(
    AcceptedVmReadScalarDeliveryDecisionV2? Policy,
    string Reason)
{
    internal bool IsResolved => Policy is not null;
    internal bool RuntimeAuthorityGranted => false;
    internal bool SourceValueAvailable => false;
    internal bool ResultReceiptIssued => false;
}

internal static class GuestPcSpFlagsVmReadScalarDeliveryAcceptedPolicyResolver
{
    internal const string AcceptanceContainingCommitSha =
        "cf0a634f94e5d13c67cc4499635b66994abd57d9";

    internal static GuestPcSpFlagsVmReadScalarDeliveryPolicyResolution Resolve(
        in GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup lookup)
    {
        if (lookup.Revoked ||
            lookup.DecisionId != GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId ||
            lookup.OperationNamespace != GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace ||
            lookup.OperationId != GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId ||
            lookup.SourceOwner != "ExecutionDomainDescriptor" ||
            lookup.ValueSource != "ExecutionDomainReadOnlyStateView" ||
            lookup.ResultAbi != VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister ||
            lookup.EffectClass != VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly ||
            lookup.CapabilityRequirement != VirtualizationCapabilityRequirementV2.None ||
            lookup.MigrationPolicy != VirtualizationOperationMigrationPolicyV2.DrainOnly ||
            lookup.FieldIds.IsDefaultOrEmpty ||
            !lookup.FieldIds.SequenceEqual(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ExactFieldIds))
        {
            return new(null, "Exact GuestPc/GuestSp/GuestFlags scalar-delivery D2 lookup mismatched or was revoked.");
        }

        VmReadScalarDeliveryDecisionValidationResultV2 validation =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                AcceptanceContainingCommitSha);
        return validation.IsAcceptedPolicyObject
            ? new(validation.AcceptedDecision,
                "Exact accepted GuestPc/GuestSp/GuestFlags scalar-delivery governance policy resolved without runtime authority.")
            : new(null, validation.Reason);
    }

    internal static GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup ExactLookup(bool revoked = false) => new(
        GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
        GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
        GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
        Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ExactFieldIds,
        "ExecutionDomainDescriptor",
        "ExecutionDomainReadOnlyStateView",
        VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
        VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
        VirtualizationCapabilityRequirementV2.None,
        VirtualizationOperationMigrationPolicyV2.DrainOnly,
        revoked);
}
