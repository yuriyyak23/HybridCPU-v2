namespace YAKSys_Hybrid_CPU.Core;

internal sealed class CompletionSecondStageVmReadScalarDeliveryCanonicalComposition :
    IVmReadScalarResultReceiptOwner
{
    private readonly object _sync = new();
    private readonly ArchitecturalCompletionCommitOwner _completionOwner;
    private readonly NeutralCompletionSecondStageProjectionOwner _projection;
    private readonly bool _enabled;
    private ulong _generation = 1;

    internal CompletionSecondStageVmReadScalarDeliveryCanonicalComposition(
        ArchitecturalCompletionCommitOwner completionOwner,
        ArchitecturalCompletionCommitOwner.ProducerRegistration exactProducer,
        bool enabled)
    {
        _completionOwner = completionOwner ?? throw new ArgumentNullException(nameof(completionOwner));
        _projection = new NeutralCompletionSecondStageProjectionOwner(exactProducer);
        _enabled = enabled;
        PolicyAccepted = Phase57CompletionSecondStageVmReadDecisionAcceptanceV2
            .ValidateRepositoryArtifact("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .IsAcceptedPolicyObject;
    }

    internal bool PolicyAccepted { get; }
    internal bool IsEnabled => _enabled && PolicyAccepted;

    internal VmReadScalarDeliveryResult Prepare(
        ReplayPhaseContext replayPhase,
        VmxMicroOp carrier,
        VmReadScalarAttemptBinding attempt,
        ulong fieldSelector,
        ulong restoreGeneration)
    {
        lock (_sync)
        {
            if (!IsEnabled)
                return Deny(VmReadScalarDeliveryDecision.Disabled, "Exact second-stage completion projection is default-disabled.");
            if (!replayPhase.IsActive || attempt.Operation != VmxOperationKind.VmRead ||
                attempt.AttemptId == 0 || attempt.ReplayEpoch != replayPhase.EpochId ||
                carrier.VirtualizationAdmission != attempt.Certificate)
                return Deny(VmReadScalarDeliveryDecision.AdmissionDenied, "Live canonical VMREAD E1 attempt is required.");
            if (fieldSelector > ushort.MaxValue)
                return Deny(VmReadScalarDeliveryDecision.FieldDenied, "Completion field selector is not exact.");
            VmcsField field = unchecked((VmcsField)(ushort)fieldSelector);
            if (field is not (VmcsField.GuestPhysicalAddress or VmcsField.EptViolationQualification))
                return Deny(VmReadScalarDeliveryDecision.FieldDenied, "Only exact GPA and EPT violation qualification are admitted.");
            if (carrier.Rd is 0 or > 31 || carrier.Rs2 != 0)
                return Deny(VmReadScalarDeliveryDecision.DestinationDenied, "VMREAD scalar delivery requires x1-x31 and reserved x0 Rs2.");

            var scope = new CompletionObservationScope(
                attempt.DomainTag, carrier.OwnerContextId, carrier.VirtualThreadId);
            CompletionObservationResult observation = _completionOwner.ObservationOwner.Observe(scope);
            if (!observation.IsObserved || observation.Snapshot is not { } snapshot)
                return Deny(VmReadScalarDeliveryDecision.SourceDenied, "No committed neutral completion exists for the exact carrier scope.");
            if (snapshot.RestoreGeneration != restoreGeneration ||
                restoreGeneration != _completionOwner.CurrentRestoreGeneration)
                return Deny(VmReadScalarDeliveryDecision.StaleReceipt, "Completion snapshot belongs to a stale restore generation.");

            NeutralCompletionSecondStageProjection mapped = _projection.Project(snapshot);
            if (!mapped.IsProjected)
                return Deny(VmReadScalarDeliveryDecision.ProjectionDenied, mapped.Reason);
            ulong value = field == VmcsField.GuestPhysicalAddress
                ? mapped.GuestPhysicalAddress
                : mapped.EptViolationQualification;
            var receipt = new VmReadScalarResultReceipt(
                this, attempt,
                CompletionSecondStageVmReadDecisionValidatorV2.ExpectedDecisionId,
                _completionOwner.ObservationOwner,
                snapshot.CompletionGeneration,
                snapshot,
                _generation,
                attempt.AttemptId,
                attempt.IssuerGeneration,
                attempt.BundleIdentity,
                attempt.ReplayEpoch,
                restoreGeneration,
                attempt.DomainTag,
                field,
                carrier.Rd,
                value);
            return new(VmReadScalarDeliveryDecision.Prepared, receipt,
                "Exact committed neutral CPU second-stage tuple captured for compatibility-only scalar delivery.");
        }
    }

    public bool ValidateLive(VmReadScalarResultReceipt receipt, ulong? currentRestoreGeneration = null)
    {
        lock (_sync) return Validate(receipt, currentRestoreGeneration) && !receipt.IsConsumed;
    }

    public bool ValidateConsumedBinding(VmReadScalarResultReceipt receipt, ulong currentRestoreGeneration)
    {
        lock (_sync) return Validate(receipt, currentRestoreGeneration);
    }

    private bool Validate(VmReadScalarResultReceipt receipt, ulong? currentRestoreGeneration)
    {
        if (!IsEnabled || receipt.ProfileGeneration != _generation ||
            receipt.DecisionId != CompletionSecondStageVmReadDecisionValidatorV2.ExpectedDecisionId ||
            receipt.CompletionObservationCapture is not { } capture ||
            !capture.TranslationProvenance.HasSecondStageIdentity ||
            !ReferenceEquals(receipt.SourceOwner, _completionOwner.ObservationOwner) ||
            receipt.SourceEpoch != capture.CompletionGeneration ||
            receipt.RestoreGeneration != capture.RestoreGeneration ||
            (currentRestoreGeneration.HasValue && receipt.RestoreGeneration != currentRestoreGeneration.Value))
            return false;
        CompletionObservationResult current = _completionOwner.ObservationOwner.Observe(capture.Scope);
        return current.IsObserved && current.Snapshot is { } live && live == capture;
    }

    private static VmReadScalarDeliveryResult Deny(
        VmReadScalarDeliveryDecision decision, string reason) => new(decision, null, reason);
}
