namespace YAKSys_Hybrid_CPU.Core;

internal static partial class RuntimeCapabilityIds
{
    internal const int VmCallProbeNoStateV1Bit = 41;
    internal const ulong VmCallProbeNoStateV1Mask = 1UL << VmCallProbeNoStateV1Bit;
}

internal sealed record VirtualizationCapabilityAllocation(
    ulong CapabilityMask,
    uint AllocationPolicyVersion,
    ulong AllocationGeneration,
    string DecisionId,
    VirtualizationDelegationPolicyV2 DelegationPolicy,
    VirtualizationRevocationPolicyV2 RevocationPolicy,
    VirtualizationCapabilityMigrationClassV2 MigrationClass,
    VirtualizationEvidenceVisibilityV2 EvidenceVisibility,
    VirtualizationProjectionPolicyV2 ProjectionPolicy)
{
    internal bool IsGrant => false;
    internal bool RuntimeCapabilityGranted => false;
}

/// <summary>
/// Stable capability-number allocation metadata. It creates no CapabilityGrant
/// and is not loaded into a CapabilityDescriptorSet.
/// </summary>
internal static class VirtualizationCapabilityAllocationRegistry
{
    internal static VirtualizationCapabilityAllocation Phase38Probe { get; } = new(
        RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
        1,
        1,
        VirtualizationDecisionValidatorV2.ExpectedDecisionId,
        VirtualizationDelegationPolicyV2.NonDelegable,
        VirtualizationRevocationPolicyV2.RuntimeRevocable,
        VirtualizationCapabilityMigrationClassV2.DomainLocal,
        VirtualizationEvidenceVisibilityV2.HostOnly,
        VirtualizationProjectionPolicyV2.NeverProject);
}
