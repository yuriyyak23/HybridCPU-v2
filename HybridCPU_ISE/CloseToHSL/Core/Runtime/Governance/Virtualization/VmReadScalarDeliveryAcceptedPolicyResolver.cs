using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal readonly record struct VmReadScalarDeliveryPolicyLookup(
    string DecisionId,
    string OperationNamespace,
    string OperationId,
    ImmutableArray<ushort> FieldIds,
    string SourceOwner,
    VirtualizationDecisionResultAbiV2 ResultAbi,
    VirtualizationDecisionEffectClassV2 EffectClass,
    VirtualizationCapabilityRequirementV2 CapabilityRequirement,
    VirtualizationOperationMigrationPolicyV2 MigrationPolicy,
    bool Revoked);

internal readonly record struct VmReadScalarDeliveryPolicyResolution(
    AcceptedVmReadScalarDeliveryDecisionV2? Policy,
    string Reason)
{
    internal bool IsResolved => Policy is not null;
    internal bool RuntimeAuthorityGranted => false;
    internal bool SourceValueAvailable => false;
    internal bool ResultReceiptIssued => false;
}

internal static class VmReadScalarDeliveryAcceptedPolicyResolver
{
    internal const string AcceptanceContainingCommitSha =
        "44fc1c3daf7d9359ffb5e4b00ccfa35953670515";

    internal static VmReadScalarDeliveryPolicyResolution Resolve(
        in VmReadScalarDeliveryPolicyLookup lookup)
    {
        if (lookup.Revoked ||
            lookup.DecisionId != VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId ||
            lookup.OperationNamespace != VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace ||
            lookup.OperationId != VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId ||
            lookup.SourceOwner != "PrivilegedExecutionStateOwnerPolicy" ||
            lookup.ResultAbi != VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister ||
            lookup.EffectClass != VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly ||
            lookup.CapabilityRequirement != VirtualizationCapabilityRequirementV2.None ||
            lookup.MigrationPolicy != VirtualizationOperationMigrationPolicyV2.DrainOnly ||
            lookup.FieldIds.IsDefaultOrEmpty ||
            !lookup.FieldIds.SequenceEqual(Phase41VmReadScalarDeliveryE0Contract.ExactFieldIds))
        {
            return new(null, "Exact scalar-delivery D2 lookup mismatched or was revoked.");
        }

        VmReadScalarDeliveryDecisionValidationResultV2 validation =
            Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                AcceptanceContainingCommitSha);
        return validation.IsAcceptedPolicyObject
            ? new(validation.AcceptedDecision, "Exact accepted scalar-delivery governance policy resolved without runtime authority.")
            : new(null, validation.Reason);
    }

    internal static VmReadScalarDeliveryPolicyLookup ExactLookup(bool revoked = false) => new(
        VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
        VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
        VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
        Phase41VmReadScalarDeliveryE0Contract.ExactFieldIds,
        "PrivilegedExecutionStateOwnerPolicy",
        VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
        VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
        VirtualizationCapabilityRequirementV2.None,
        VirtualizationOperationMigrationPolicyV2.DrainOnly,
        revoked);
}
