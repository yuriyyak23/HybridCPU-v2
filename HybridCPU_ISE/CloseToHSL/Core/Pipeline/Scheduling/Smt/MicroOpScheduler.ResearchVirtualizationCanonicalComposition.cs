#if TESTING
using System;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core;

internal enum ResearchVirtualizationCanonicalCompositionDecision : byte
{
    MaterializedReceipt = 0,
    DeniedNoCanonicalVerifier = 1,
    DeniedStaleRuntimeContextLease = 2,
    DeniedDuplicateMaterialization = 3,
    DeniedPrototypeAdmission = 4,
    DeniedPrototypeExecution = 5,
}

internal readonly record struct ResearchVirtualizationCanonicalCompositionResult(
    ResearchVirtualizationCanonicalCompositionDecision Decision,
    ResearchVirtualizationProbeAdmissionDecision? AdmissionDecision,
    ResearchVirtualizationProbeExecutionDecision? ExecutionDecision,
    ResearchVirtualizationRuntimeOwner.ExecutionReceipt? Receipt,
    string Reason)
{
    internal bool Succeeded =>
        Decision == ResearchVirtualizationCanonicalCompositionDecision.MaterializedReceipt &&
        Receipt is not null;
}

/// <summary>
/// Default-off TESTING-only composition of the Phase 36 probe at the canonical
/// issue/materialization seam. It owns no production dispatch, completion or retire
/// authority; it can only consume a live E1 supplied by that seam.
/// </summary>
internal sealed class ResearchVirtualizationCanonicalIssueComposition
{
    private readonly object _gate = new();
    private readonly ResearchVirtualizationRuntimeOwner _executionOwner;
    private readonly ResearchVirtualizationRuntimeOwner.PolicySnapshot _policy;
    private readonly ResearchVirtualizationOperationContext _context;
    private readonly ResearchVirtualizationOperationContext.MaterializationLease _contextLease;
    private readonly Action<SafetyVerifier>? _afterAdmission;
    private ulong _observedAttemptId;

    internal ResearchVirtualizationCanonicalIssueComposition(
        ResearchVirtualizationRuntimeOwner policyOwner,
        ResearchVirtualizationOperationContext context,
        ResearchVirtualizationRuntimeOwner? executionOwner = null,
        Action<SafetyVerifier>? afterAdmission = null,
        ResearchVirtualizationOperationContext.MaterializationLease? contextLease = null)
    {
        ArgumentNullException.ThrowIfNull(policyOwner);
        ArgumentNullException.ThrowIfNull(context);

        _executionOwner = executionOwner ?? policyOwner;
        _policy = policyOwner.CapturePolicy();
        _context = context;
        _contextLease = contextLease ?? context.CaptureMaterializationLease();
        _afterAdmission = afterAdmission;
    }

    internal ResearchVirtualizationCanonicalCompositionResult Compose(
        SafetyVerifier verifier,
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundleMetadata,
        VmxMicroOp carrier,
        int sourceSlotId,
        int workingSlotId,
        SafetyVerifier.VirtualizationAdmissionCertificate e1)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(e1);

        lock (_gate)
        {
            if (_observedAttemptId != 0)
            {
                return Deny(
                    ResearchVirtualizationCanonicalCompositionDecision.DeniedDuplicateMaterialization,
                    "The TESTING-only canonical composition already observed one E1 attempt.");
            }

            _observedAttemptId = e1.AttemptId;
            ResearchVirtualizationOperationContext.IdentitySnapshot? identity =
                _context.Materialize(_contextLease, e1.AttemptId, e1.ReplayEpoch);
            if (identity is null)
            {
                return Deny(
                    ResearchVirtualizationCanonicalCompositionDecision.DeniedStaleRuntimeContextLease,
                    "The typed runtime context lease was foreign or stale before canonical materialization.");
            }

            ResearchVirtualizationProbeAdmissionResult admission =
                verifier.IssueResearchVirtualizationOperationAdmission(
                    _policy,
                    _context,
                    identity,
                    replayPhase,
                    bundleMetadata,
                    carrier,
                    sourceSlotId,
                    workingSlotId,
                    e1);
            if (!admission.IsIssued || admission.Certificate is null)
            {
                return new(
                    ResearchVirtualizationCanonicalCompositionDecision.DeniedPrototypeAdmission,
                    admission.Decision,
                    null,
                    null,
                    admission.Reason);
            }

            _afterAdmission?.Invoke(verifier);
            ResearchVirtualizationProbeExecutionResult execution =
                _executionOwner.Execute(verifier, admission.Certificate, _context);
            if (!execution.Succeeded || execution.Receipt is null)
            {
                return new(
                    ResearchVirtualizationCanonicalCompositionDecision.DeniedPrototypeExecution,
                    admission.Decision,
                    execution.Decision,
                    null,
                    execution.Reason);
            }

            return new(
                ResearchVirtualizationCanonicalCompositionDecision.MaterializedReceipt,
                admission.Decision,
                execution.Decision,
                execution.Receipt,
                "The canonical TESTING-only issue boundary materialized one no-state/no-payload research receipt.");
        }
    }

    private static ResearchVirtualizationCanonicalCompositionResult Deny(
        ResearchVirtualizationCanonicalCompositionDecision decision,
        string reason) => new(decision, null, null, null, reason);
}

public partial class MicroOpScheduler
{
    private ResearchVirtualizationCanonicalIssueComposition? _researchVirtualizationCanonicalComposition;

    internal ResearchVirtualizationCanonicalCompositionResult? LastResearchVirtualizationCanonicalCompositionResult
        { get; private set; }

    internal void EnableResearchVirtualizationCanonicalComposition(
        ResearchVirtualizationCanonicalIssueComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _researchVirtualizationCanonicalComposition = composition;
        LastResearchVirtualizationCanonicalCompositionResult = null;
    }

    internal void DisableResearchVirtualizationCanonicalComposition()
    {
        _researchVirtualizationCanonicalComposition = null;
        LastResearchVirtualizationCanonicalCompositionResult = null;
    }

    internal SafetyVerifier? ResearchVirtualizationCanonicalVerifierForTesting =>
        (_runtimeLegalityService as RuntimeLegalityService)?.ResearchVirtualizationCanonicalVerifier;

    partial void OnCanonicalVirtualizationAdmissionMaterialized(
        BundleIssuePacket issuePacket,
        IssuePacketLane issueLane,
        SmtBundleMetadata4Way bundleMetadata,
        VmxMicroOp carrier,
        SafetyVerifier.VirtualizationAdmissionCertificate e1)
    {
        _ = issuePacket;
        ResearchVirtualizationCanonicalIssueComposition? composition =
            _researchVirtualizationCanonicalComposition;
        if (composition is null)
            return;

        SafetyVerifier? verifier = ResearchVirtualizationCanonicalVerifierForTesting;
        if (verifier is null)
        {
            LastResearchVirtualizationCanonicalCompositionResult = new(
                ResearchVirtualizationCanonicalCompositionDecision.DeniedNoCanonicalVerifier,
                null,
                null,
                null,
                "The canonical SafetyVerifier was unavailable at the TESTING-only issue seam.");
            return;
        }

        LastResearchVirtualizationCanonicalCompositionResult = composition.Compose(
            verifier,
            _currentReplayPhase,
            bundleMetadata,
            carrier,
            issueLane.SlotIndex,
            issueLane.PhysicalLaneIndex,
            e1);
    }
}
#endif
