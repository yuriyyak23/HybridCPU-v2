namespace YAKSys_Hybrid_CPU.Core;

internal sealed record HypercallRuntimeOwnerAllocation(
    ulong OwnerId,
    uint OwnerPolicyVersion,
    uint OwnerEpoch,
    string DecisionId,
    string SpecDigest,
    string OwnerRole)
{
    internal bool RuntimeOwnerLoaded => false;
    internal bool BackendExecutionAuthorized => false;
}

/// <summary>
/// Repository allocation registry only. It binds the accepted HCOWNR identity and
/// exposes no resolution to an executable owner service.
/// </summary>
internal static class HypercallRuntimeOwnerRegistry
{
    internal static HypercallRuntimeOwnerAllocation Phase38ProbeOwner { get; } = new(
        VirtualizationDecisionValidatorV2.ExpectedOwnerId,
        1,
        1,
        VirtualizationDecisionValidatorV2.ExpectedDecisionId,
        Phase38VirtualizationDecisionAcceptanceV2.ExpectedSpecDigest,
        "DomainHypercallRuntimeOwner");

    internal static bool TryGetAllocation(
        ulong ownerId,
        out HypercallRuntimeOwnerAllocation allocation)
    {
        if (ownerId == Phase38ProbeOwner.OwnerId)
        {
            allocation = Phase38ProbeOwner;
            return true;
        }

        allocation = null!;
        return false;
    }
}
