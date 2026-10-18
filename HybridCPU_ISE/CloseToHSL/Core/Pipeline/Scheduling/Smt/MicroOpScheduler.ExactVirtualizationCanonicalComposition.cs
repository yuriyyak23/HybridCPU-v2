namespace YAKSys_Hybrid_CPU.Core;

using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

public partial class MicroOpScheduler
{
    private DomainHypercallCanonicalComposition? _exactVirtualizationComposition;

    internal DomainHypercallCompositionResult? LastExactVirtualizationCompositionResult
        { get; private set; }

    internal SafetyVerifier? ExactVirtualizationCanonicalVerifier =>
        (_runtimeLegalityService as RuntimeLegalityService)?.CanonicalVirtualizationVerifier;

    internal bool HasExactVirtualizationComposition => _exactVirtualizationComposition is not null;

    internal void ConfigureExactVirtualizationComposition(
        DomainHypercallCanonicalComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (_exactVirtualizationComposition is not null)
            throw new InvalidOperationException("Exact virtualization composition is already configured.");

        _exactVirtualizationComposition = composition;
        LastExactVirtualizationCompositionResult = null;
    }

    internal void DisableExactVirtualizationComposition()
    {
        _exactVirtualizationComposition?.Disable();
        _exactVirtualizationComposition = null;
        LastExactVirtualizationCompositionResult = null;
    }

    partial void OnCanonicalVirtualizationOperandMaterialized(
        BundleIssuePacket issuePacket,
        IssuePacketLane issueLane,
        SmtBundleMetadata4Way bundleMetadata,
        VmxMicroOp carrier,
        SafetyVerifier.VirtualizationAdmissionCertificate e1,
        VirtualizationOperandSnapshot operand)
    {
        _ = issuePacket;
        DomainHypercallCanonicalComposition? composition = _exactVirtualizationComposition;
        if (composition is null)
            return;

        SafetyVerifier? verifier = ExactVirtualizationCanonicalVerifier;
        if (verifier is null)
        {
            LastExactVirtualizationCompositionResult = new(
                DomainHypercallCompositionDecision.MissingCanonicalVerifier,
                null,
                "Only the canonical RuntimeLegalityService SafetyVerifier may compose E4.");
            return;
        }

        LastExactVirtualizationCompositionResult = composition.Prepare(
            verifier,
            _currentReplayPhase,
            bundleMetadata,
            carrier,
            issueLane.SlotIndex,
            issueLane.PhysicalLaneIndex,
            e1,
            operand);
    }
}
