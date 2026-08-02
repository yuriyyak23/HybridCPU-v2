using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace YAKSys_Hybrid_CPU.Core;

public enum Lane6HostEvidenceRestoreDecision : byte
{
    RebuildRequired = 0,
    Rebuilt = 1,
    RestoreRejected = 2,
}

public readonly record struct Lane6HostEvidenceRestoreResult(
    Lane6HostEvidenceRestoreDecision Decision,
    string Reason)
{
    public bool RequiresRebuild => Decision == Lane6HostEvidenceRestoreDecision.RebuildRequired;

    public static Lane6HostEvidenceRestoreResult RebuildRequired { get; } =
        new(
            Lane6HostEvidenceRestoreDecision.RebuildRequired,
            "Lane6 native token evidence was cleared for restore and must be rebuilt from host runtime state.");

    public static Lane6HostEvidenceRestoreResult Rebuilt { get; } =
        new(
            Lane6HostEvidenceRestoreDecision.Rebuilt,
            "Lane6 native token evidence was rebuilt from host runtime state.");

    public static Lane6HostEvidenceRestoreResult Rejected(string reason) =>
        new(Lane6HostEvidenceRestoreDecision.RestoreRejected, reason);
}

public sealed class Lane6HostOwnedEvidenceStore
{
    private readonly Dictionary<Lane6VirtualToken, DmaStreamComputeTokenHandle> _nativeTokenBindings = new();

    public int ActiveBindingCount => _nativeTokenBindings.Count;

    public bool TryBind(
        Lane6VirtualToken virtualToken,
        DmaStreamComputeTokenHandle hostHandle)
    {
        if (!virtualToken.IsValid || hostHandle.IsDefault)
        {
            return false;
        }

        _nativeTokenBindings[virtualToken] = hostHandle;
        return true;
    }

    public bool TryResolve(
        Lane6VirtualToken virtualToken,
        out DmaStreamComputeTokenHandle hostHandle) =>
        _nativeTokenBindings.TryGetValue(virtualToken, out hostHandle);

    public Lane6HostEvidenceRestoreResult PrepareForRestore(
        EvidencePolicyDescriptor evidencePolicy)
    {
        var boundary = new HostOwnedEvidenceBoundary();
        HostOwnedEvidenceBoundaryResult validation = boundary.ValidateRestore(
            evidencePolicy,
            EvidenceVisibilityClass.NativeTokenEvidence,
            EvidenceRestorePolicy.RecomputeAfterRestore);
        if (validation.Decision != HostOwnedEvidenceBoundaryDecision.RestoreRequiresRecompute)
        {
            return Lane6HostEvidenceRestoreResult.Rejected(
                "Lane6 native token evidence cannot be imported as restored state.");
        }

        _nativeTokenBindings.Clear();
        return Lane6HostEvidenceRestoreResult.RebuildRequired;
    }

    public Lane6HostEvidenceRestoreResult RebuildAfterRestore(
        Lane6VirtualToken virtualToken,
        DmaStreamComputeTokenHandle hostHandle) =>
        TryBind(virtualToken, hostHandle)
            ? Lane6HostEvidenceRestoreResult.Rebuilt
            : Lane6HostEvidenceRestoreResult.Rejected(
                "Lane6 host evidence rebuild requires a valid virtual token and fresh host token handle.");
}
