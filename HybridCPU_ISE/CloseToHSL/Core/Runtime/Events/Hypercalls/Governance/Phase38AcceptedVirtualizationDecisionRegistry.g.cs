namespace YAKSys_Hybrid_CPU.Core;

internal sealed record AcceptedVirtualizationDecisionRegistryEntry(
    string OperationNamespace,
    ushort NumericLeaf,
    string OperationId,
    AcceptedVirtualizationDecision Policy)
{
    internal bool RuntimeCapabilityGranted => false;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Generated exact policy lookup for the one attributable D2 decision. The entry
/// is governance metadata only and cannot invoke, admit, complete or retire work.
/// </summary>
internal static class Phase38AcceptedVirtualizationDecisionRegistry
{
    internal static AcceptedVirtualizationDecisionRegistryEntry ExactEntry { get; } = Create();

    internal static bool TryResolvePolicy(
        string operationNamespace,
        ushort numericLeaf,
        out AcceptedVirtualizationDecisionRegistryEntry entry)
    {
        if (string.Equals(
                operationNamespace,
                ExactEntry.OperationNamespace,
                StringComparison.Ordinal) &&
            numericLeaf == ExactEntry.NumericLeaf)
        {
            entry = ExactEntry;
            return true;
        }

        entry = null!;
        return false;
    }

    private static AcceptedVirtualizationDecisionRegistryEntry Create()
    {
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase38VirtualizationDecisionAcceptanceV2.Record;
        var policy = new AcceptedVirtualizationDecision(
            spec.DecisionId,
            spec.SpecDigest,
            acceptance.AcceptanceDigest,
            acceptance.SpecCommitSha,
            spec.OperationNamespace,
            spec.NumericLeaf,
            spec.OwnerId,
            spec.OwnerPolicyVersion,
            spec.OwnerEpoch,
            spec.EffectClass,
            new(spec.AdjacentLeafPolicy, spec.CrossNamespacePolicy));

        return new(spec.OperationNamespace, spec.NumericLeaf, spec.OperationId, policy);
    }
}
