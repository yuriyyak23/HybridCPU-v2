namespace YAKSys_Hybrid_CPU.Core;

public enum CompatibilityWriteNoEmissionDecision : byte
{
    Allowed = 0,
    UnsupportedWriteResult = 1,
    MissingCompatibilityFence = 2,
    MissingNoEffectPolicy = 3,
    DescriptorMutated = 4,
    GrantsMutated = 5,
    EvidenceMutated = 6,
    CompletionMutated = 7,
    MemoryGenerationMutated = 8,
    LaneStateMutated = 9,
    MigrationStateMutated = 10,
}

public readonly record struct CompatibilityWriteNoEmissionSnapshot(
    ulong DescriptorFingerprint,
    ulong GrantFingerprint,
    ulong EvidenceFingerprint,
    ulong CompletionFingerprint,
    ulong MemoryGenerationFingerprint,
    ulong LaneStateFingerprint,
    ulong MigrationStateFingerprint);

public readonly record struct CompatibilityWriteNoEmissionResult(
    CompatibilityWriteNoEmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == CompatibilityWriteNoEmissionDecision.Allowed;

    public static CompatibilityWriteNoEmissionResult Allowed { get; } =
        new(CompatibilityWriteNoEmissionDecision.Allowed, "Compatibility write has no emission path.");

    public static CompatibilityWriteNoEmissionResult Denied(
        CompatibilityWriteNoEmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class CompatibilityWriteNoEmissionContract
{
    public CompatibilityWriteNoEmissionResult ValidateVmxCapsWrite(
        VmxCapsProjection projection,
        VmxCapsWriteResult writeResult)
    {
        if (writeResult == VmxCapsWriteResult.Rejected)
        {
            return CompatibilityWriteNoEmissionResult.Allowed;
        }

        if (writeResult != VmxCapsWriteResult.CompatibilityNoEffect)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.UnsupportedWriteResult,
                "VmxCaps compatibility write produced an unsupported result.");
        }

        if (!projection.CompatibilityWriteFenceEnabled)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.MissingCompatibilityFence,
                "Compatibility no-effect write requires an explicit compatibility write fence.");
        }

        if (!projection.PublicationPolicy.IsCompatibilityWriteArchitecturallyNoEffect())
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.MissingNoEffectPolicy,
                "Compatibility no-effect write requires a no-effect publication policy.");
        }

        return CompatibilityWriteNoEmissionResult.Allowed;
    }

    public CompatibilityWriteNoEmissionResult ValidateNoMutation(
        VmxCapsProjection projection,
        VmxCapsWriteResult writeResult,
        CompatibilityWriteNoEmissionSnapshot before,
        CompatibilityWriteNoEmissionSnapshot after)
    {
        CompatibilityWriteNoEmissionResult writePolicyResult =
            ValidateVmxCapsWrite(projection, writeResult);

        if (!writePolicyResult.IsAllowed)
        {
            return writePolicyResult;
        }

        if (before.DescriptorFingerprint != after.DescriptorFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.DescriptorMutated,
                "Compatibility write mutated descriptor-owned state.");
        }

        if (before.GrantFingerprint != after.GrantFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.GrantsMutated,
                "Compatibility write mutated capability grants.");
        }

        if (before.EvidenceFingerprint != after.EvidenceFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.EvidenceMutated,
                "Compatibility write mutated evidence state.");
        }

        if (before.CompletionFingerprint != after.CompletionFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.CompletionMutated,
                "Compatibility write mutated completion state.");
        }

        if (before.MemoryGenerationFingerprint != after.MemoryGenerationFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.MemoryGenerationMutated,
                "Compatibility write mutated memory-domain generation.");
        }

        if (before.LaneStateFingerprint != after.LaneStateFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.LaneStateMutated,
                "Compatibility write mutated lane-owned state.");
        }

        if (before.MigrationStateFingerprint != after.MigrationStateFingerprint)
        {
            return CompatibilityWriteNoEmissionResult.Denied(
                CompatibilityWriteNoEmissionDecision.MigrationStateMutated,
                "Compatibility write mutated migration-owned state.");
        }

        return CompatibilityWriteNoEmissionResult.Allowed;
    }

    public bool IsVmxCapsWriteSatisfied(
        VmxCapsProjection projection,
        VmxCapsWriteResult writeResult) =>
        ValidateVmxCapsWrite(projection, writeResult).IsAllowed;

    public bool IsNoMutationSatisfied(
        VmxCapsProjection projection,
        VmxCapsWriteResult writeResult,
        CompatibilityWriteNoEmissionSnapshot before,
        CompatibilityWriteNoEmissionSnapshot after) =>
        ValidateNoMutation(projection, writeResult, before, after).IsAllowed;
}
