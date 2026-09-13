using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

// Historical Phase-34 request remains fail-closed. In particular its mandatory
// address-space/evidence booleans are not the PROBE_NO_STATE_V1 contract.
internal enum VirtualizationOperationAdmissionDecision : byte
{
    DeniedD2DecisionArtifact = 0,
    DeniedOwnerInterfaceDisabled = 1,
    DeniedCanonicalLeafCaptureMissing = 2,
    DeniedRuntimeIdentitiesMissing = 3,
}

internal readonly record struct VirtualizationOperationAdmissionRequest(
    VirtualizationOperationDecisionManifest DecisionManifest,
    VirtualizationDecisionAttributionEvidence Attribution,
    bool CanonicalRuntimeLeafCaptured,
    bool CapabilityGrantIdentityPresent,
    bool EvidencePolicyIdentityPresent,
    bool AddressSpaceIdentityPresent,
    bool RestoreGenerationPresent);

internal readonly record struct VirtualizationOperationAdmissionResult(
    VirtualizationOperationAdmissionDecision Decision,
    SafetyVerifier.VirtualizationOperationAdmissionCertificate? Certificate,
    string Reason)
{
    internal bool IsIssued => false;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

internal enum VirtualizationE2State : byte
{
    Issued = 0,
    ConsumedByExecutor = 1,
    Revoked = 2,
}

internal enum VirtualizationE2Decision : byte
{
    Issued = 0,
    MissingInput = 1,
    InvalidD2OrOwnerPolicy = 2,
    InvalidE1 = 3,
    InvalidOperand = 4,
    DomainIdentityMismatch = 5,
    CapabilityLeaseNotLive = 6,
    CapabilityPolicyMismatch = 7,
    RootAuthorityMismatch = 8,
    CommonRuntimeAdmissionDenied = 9,
    RestoreGenerationMismatch = 10,
    DuplicateAttempt = 11,
    CertificateIssuerMismatch = 12,
    CertificateNotLive = 13,
    CertificateBindingMismatch = 14,
    CertificateDigestMismatch = 15,
    Revoked = 16,
    LifecycleGateDenied = 17,
}

internal readonly record struct VirtualizationE2IssueRequest(
    ReplayPhaseContext ReplayPhase,
    SmtBundleMetadata4Way BundleMetadata,
    VmxMicroOp? Carrier,
    int SourceSlotId,
    int WorkingSlotId,
    SafetyVerifier.VirtualizationAdmissionCertificate? E1,
    VirtualizationOperationOwnerSnapshot? OwnerPolicy,
    VirtualizationOperandSnapshot? Operand,
    DomainRuntimeContext? DomainContext,
    RootAuthorityDescriptor? RootAuthority,
    RuntimeCapabilityGrantOwner? CapabilityOwner,
    RuntimeCapabilityGrantLease? CapabilityLease,
    VirtualizationRestoreGenerationOwner? RestoreOwner,
    DomainHypercallLifecycleGate? LifecycleGate);

internal readonly record struct VirtualizationE2Result(
    VirtualizationE2Decision Decision,
    SafetyVerifier.VirtualizationOperationAdmissionCertificate? Certificate,
    string Reason)
{
    internal bool IsIssued => Decision == VirtualizationE2Decision.Issued && Certificate is not null;
    internal bool IsLive => IsIssued;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

internal enum VirtualizationE2ConsumptionDecision : byte
{
    Consumed = 0,
    InvalidConsumer = 1,
    InvalidCertificate = 2,
}

internal readonly record struct VirtualizationE2ConsumptionResult(
    VirtualizationE2ConsumptionDecision Decision,
    string Reason)
{
    internal bool IsConsumed => Decision == VirtualizationE2ConsumptionDecision.Consumed;
}

public partial class SafetyVerifier
{
    /// <summary>
    /// Opaque, non-serializable E2 for exactly one accepted no-state probe attempt.
    /// It is admission evidence only and deliberately has no backend consumer in PR-D.
    /// </summary>
    internal sealed class VirtualizationOperationAdmissionCertificate
    {
        private readonly object _issuerSeal;

        private VirtualizationOperationAdmissionCertificate(
            object issuerSeal,
            ulong issuanceSequence,
            ulong attemptId,
            ulong e1IssuerGeneration,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag,
            ulong bundleIdentity,
            ulong replayEpoch,
            string decisionId,
            string specDigest,
            string acceptanceDigest,
            ulong ownerId,
            uint ownerPolicyVersion,
            uint ownerEpoch,
            string operationNamespace,
            string operationId,
            ushort numericLeaf,
            string ownerPolicyDigest,
            string operandDigest,
            ulong capabilityGrantIdentity,
            ulong capabilityGeneration,
            ulong rootAuthorityEpoch,
            ulong restoreGeneration,
            string certificateDigest)
        {
            _issuerSeal = issuerSeal;
            IssuanceSequence = issuanceSequence;
            AttemptId = attemptId;
            E1IssuerGeneration = e1IssuerGeneration;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            DomainTag = domainTag;
            BundleIdentity = bundleIdentity;
            ReplayEpoch = replayEpoch;
            DecisionId = decisionId;
            SpecDigest = specDigest;
            AcceptanceDigest = acceptanceDigest;
            OwnerId = ownerId;
            OwnerPolicyVersion = ownerPolicyVersion;
            OwnerEpoch = ownerEpoch;
            OperationNamespace = operationNamespace;
            OperationId = operationId;
            NumericLeaf = numericLeaf;
            OwnerPolicyDigest = ownerPolicyDigest;
            OperandDigest = operandDigest;
            CapabilityGrantIdentity = capabilityGrantIdentity;
            CapabilityGeneration = capabilityGeneration;
            RootAuthorityEpoch = rootAuthorityEpoch;
            RestoreGeneration = restoreGeneration;
            CertificateDigest = certificateDigest;
        }

        internal uint SchemaVersion => 2;
        internal ulong IssuanceSequence { get; }
        internal ulong AttemptId { get; }
        internal ulong E1IssuerGeneration { get; }
        internal int VirtualThreadId { get; }
        internal int OwnerContextId { get; }
        internal ulong DomainTag { get; }
        internal ulong BundleIdentity { get; }
        internal ulong ReplayEpoch { get; }
        internal string DecisionId { get; }
        internal string SpecDigest { get; }
        internal string AcceptanceDigest { get; }
        internal ulong OwnerId { get; }
        internal uint OwnerPolicyVersion { get; }
        internal uint OwnerEpoch { get; }
        internal string OperationNamespace { get; }
        internal string OperationId { get; }
        internal ushort NumericLeaf { get; }
        internal string OwnerPolicyDigest { get; }
        internal string OperandDigest { get; }
        internal ulong CapabilityGrantIdentity { get; }
        internal ulong CapabilityGeneration { get; }
        internal ulong RootAuthorityEpoch { get; }
        internal ulong RestoreGeneration { get; }
        internal string CertificateDigest { get; }
        internal bool HasAddressSpaceIdentity => false;
        internal bool HasEvidenceIdentity => false;
        internal bool BackendExecutionAuthorized => false;
        internal bool CompletionPublicationAuthorized => false;
        internal bool RetirePublicationAuthorized => false;

        internal bool WasIssuedBy(object seal) => ReferenceEquals(_issuerSeal, seal);

        internal static VirtualizationOperationAdmissionCertificate Create(
            object issuerSeal,
            ulong issuanceSequence,
            VirtualizationE2IssueRequest request)
        {
            VirtualizationAdmissionCertificate e1 = request.E1!;
            VirtualizationOperationOwnerSnapshot owner = request.OwnerPolicy!;
            VirtualizationOperandSnapshot operand = request.Operand!;
            RuntimeCapabilityGrantLease lease = request.CapabilityLease!;
            RootAuthorityDescriptor root = request.RootAuthority!;
            string digest = VirtualizationE2Digest.Compute(
                issuanceSequence, e1.AttemptId, e1.IssuerGeneration,
                e1.VirtualThreadId, e1.OwnerContextId, e1.DomainTag,
                e1.BundleIdentity, e1.ReplayEpoch, owner.DecisionId,
                owner.SpecDigest, owner.AcceptanceDigest, owner.OwnerId,
                owner.OwnerPolicyVersion, owner.OwnerEpoch, owner.OperationNamespace,
                owner.OperationId, owner.NumericLeaf, owner.PolicyDigest,
                operand.OperandDigest, lease.GrantIdentity, lease.Generation,
                root.AuthorityEpoch, request.RestoreOwner!.CurrentGeneration);
            return new(
                issuerSeal, issuanceSequence, e1.AttemptId, e1.IssuerGeneration,
                e1.VirtualThreadId, e1.OwnerContextId, e1.DomainTag,
                e1.BundleIdentity, e1.ReplayEpoch, owner.DecisionId,
                owner.SpecDigest, owner.AcceptanceDigest, owner.OwnerId,
                owner.OwnerPolicyVersion, owner.OwnerEpoch, owner.OperationNamespace,
                owner.OperationId, owner.NumericLeaf, owner.PolicyDigest,
                operand.OperandDigest, lease.GrantIdentity, lease.Generation,
                root.AuthorityEpoch, request.RestoreOwner.CurrentGeneration, digest);
        }
    }

    private sealed class LiveVirtualizationE2
    {
        internal LiveVirtualizationE2(VirtualizationE2IssueRequest request)
        {
            Request = request;
        }

        internal VirtualizationE2IssueRequest Request { get; }
        internal VirtualizationE2State State { get; set; } = VirtualizationE2State.Issued;
    }

    private readonly object _virtualizationE2IssuerSeal = new();
    private readonly ConditionalWeakTable<VirtualizationOperationAdmissionCertificate, LiveVirtualizationE2> _liveVirtualizationE2 = new();
    private readonly HashSet<VirtualizationOperationAdmissionCertificate> _issuedVirtualizationE2 = new();
    private readonly ConditionalWeakTable<VirtualizationOperandSnapshot, VirtualizationOperationAdmissionCertificate> _issuedVirtualizationE2ByOperand = new();
    private readonly RuntimeBoundaryAdmissionService _virtualizationE2BoundaryAdmission = new();
    private readonly object _virtualizationE2Sync = new();
    private ulong _nextVirtualizationE2Sequence = 1;

    internal VirtualizationE2Result IssueVirtualizationE2(VirtualizationE2IssueRequest request)
    {
        if (request.Carrier is null || request.E1 is null || request.OwnerPolicy is null ||
            request.Operand is null || request.DomainContext is null || request.RootAuthority is null ||
            request.CapabilityOwner is null || request.CapabilityLease is null || request.RestoreOwner is null ||
            request.LifecycleGate is null)
            return DenyE2(VirtualizationE2Decision.MissingInput, "E2 requires live E1, O1, operand, domain, root, capability, restore, and lifecycle inputs.");

        if (!ReferenceEquals(request.OwnerPolicy, Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot) ||
            !string.Equals(request.OwnerPolicy.DecisionId, VirtualizationDecisionValidatorV2.ExpectedDecisionId, StringComparison.Ordinal) ||
            !string.Equals(request.OwnerPolicy.SpecDigest, Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, StringComparison.Ordinal) ||
            !string.Equals(request.OwnerPolicy.AcceptanceDigest, Phase38VirtualizationDecisionAcceptanceV2.Record.AcceptanceDigest, StringComparison.Ordinal) ||
            request.OwnerPolicy.OwnerId != VirtualizationDecisionValidatorV2.ExpectedOwnerId ||
            request.OwnerPolicy.OwnerPolicyVersion != 1 || request.OwnerPolicy.OwnerEpoch != 1 ||
            !string.Equals(request.OwnerPolicy.OperationNamespace, VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, StringComparison.Ordinal) ||
            !string.Equals(request.OwnerPolicy.OperationId, VirtualizationDecisionValidatorV2.ExpectedOperationId, StringComparison.Ordinal) ||
            request.OwnerPolicy.NumericLeaf != 1)
            return DenyE2(VirtualizationE2Decision.InvalidD2OrOwnerPolicy, "E2 requires the exact current accepted D2/O1 instance.");

        VirtualizationAdmissionValidationResult e1Validation = ValidateVirtualizationAdmission(
            request.ReplayPhase, request.BundleMetadata, request.Carrier,
            request.SourceSlotId, request.WorkingSlotId, request.E1);
        if (!e1Validation.IsValidForFaultOnlyTransport)
            return DenyE2(VirtualizationE2Decision.InvalidE1, e1Validation.Reason);

        VirtualizationOperandValidationResult operandValidation =
            VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                request.Operand, request.Carrier, request.E1, request.OwnerPolicy,
                request.RestoreOwner.CurrentGeneration);
        if (!operandValidation.IsValidForE2Input)
            return DenyE2(VirtualizationE2Decision.InvalidOperand, operandValidation.Reason);

        if (request.RestoreOwner.CurrentGeneration == 0 ||
            request.Operand.RestoreGeneration != request.RestoreOwner.CurrentGeneration)
            return DenyE2(VirtualizationE2Decision.RestoreGenerationMismatch, "E2 restore generation is absent or stale.");

        if (request.DomainContext.DomainTag == 0 ||
            request.DomainContext.DomainTag != request.E1.DomainTag ||
            request.DomainContext.AddressSpaceTag != 0)
            return DenyE2(VirtualizationE2Decision.DomainIdentityMismatch, "Exact no-state E2 requires matching execution domain and absent address-space identity.");

        if (!request.CapabilityOwner.IsLive(request.CapabilityLease))
            return DenyE2(VirtualizationE2Decision.CapabilityLeaseNotLive, "Typed capability lease is absent, forged, revoked or stale.");

        CapabilityGrant grant = request.CapabilityLease.Grant;
        if (!ReferenceEquals(ResolveExactGrant(request.DomainContext), grant) ||
            grant.CapabilityMask != RuntimeCapabilityIds.VmCallProbeNoStateV1Mask ||
            grant.Scope != CapabilityGrantScope.DomainGranted || !grant.IsGranted ||
            grant.OwnerDomainId != request.DomainContext.DomainTag ||
            grant.DelegationPolicy != CapabilityDelegationPolicy.NonDelegable ||
            grant.RevocationPolicy != CapabilityRevocationPolicy.RuntimeRevocable ||
            grant.MigrationClass != CapabilityMigrationClass.DomainLocal ||
            grant.EvidenceVisibility != CapabilityEvidenceVisibility.HostOnly ||
            grant.FrontendProjectionPolicy != CapabilityFrontendProjectionPolicy.NeverProject)
            return DenyE2(VirtualizationE2Decision.CapabilityPolicyMismatch, "Capability lease does not match the exact D2 owner policy.");

        RootAuthorityDescriptor root = request.RootAuthority;
        if (!root.IsRuntimeRoot || root.AuthorityEpoch == 0 ||
            !root.HasCapability(RuntimeCapabilityIds.VmCallProbeNoStateV1Mask) ||
            root.AllowCompatibilityFrontendActivation || root.AllowAuthoritativeStateMutation)
            return DenyE2(VirtualizationE2Decision.RootAuthorityMismatch, "E2 requires a non-mutating runtime root with the exact capability and non-zero epoch.");

        DomainRuntimeOperation operation = new(
            DomainRuntimeOperationKind.InvokeHypercall,
            DomainRuntimeOperationSource.RuntimeService,
            requiresCapabilityGrant: true,
            DomainRuntimeOperationAuthorityClass.NoStateExecution);
        RuntimeBoundaryAdmissionResult commonAdmission = _virtualizationE2BoundaryAdmission.Validate(new(
            request.DomainContext, root, EvidencePolicy: null, operation,
            DomainBoundaryDescriptor.ExecutionOnly,
            CapabilityBoundaryRequirement.TypedGrant(
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                CapabilityGrantScope.DomainGranted),
            EvidenceBoundaryRequirement.None));
        if (!commonAdmission.IsAllowed)
            return DenyE2(VirtualizationE2Decision.CommonRuntimeAdmissionDenied, commonAdmission.Message);

        if (request.LifecycleGate.DomainTag != request.DomainContext.DomainTag ||
            !request.LifecycleGate.TryBeginTransition(
                request.DomainContext.DomainTag,
                DomainHypercallTransitionKind.NewE2,
                out DomainHypercallLifecycleGate.TransitionLease? transition) ||
            transition is null)
            return DenyE2(VirtualizationE2Decision.LifecycleGateDenied, "The exact per-domain lifecycle gate is disabled or draining.");

        using (transition)
        {
#if TESTING
            request.LifecycleGate.NotifyTransitionGapForTesting(DomainHypercallTransitionKind.NewE2);
#endif

            lock (_virtualizationE2Sync)
            {
                if (_issuedVirtualizationE2ByOperand.TryGetValue(request.Operand, out _))
                    return DenyE2(VirtualizationE2Decision.DuplicateAttempt, "One operand snapshot may receive only one E2 issuance.");

                ulong sequence = AllocateE2Sequence();
                VirtualizationOperationAdmissionCertificate certificate =
                    VirtualizationOperationAdmissionCertificate.Create(_virtualizationE2IssuerSeal, sequence, request);
                _liveVirtualizationE2.Add(certificate, new LiveVirtualizationE2(request));
                _issuedVirtualizationE2.Add(certificate);
                _issuedVirtualizationE2ByOperand.Add(request.Operand, certificate);
                return new(VirtualizationE2Decision.Issued, certificate, "SafetyVerifier issued exact fault-only E2 admission.");
            }
        }
    }

    internal VirtualizationE2Result ValidateVirtualizationE2(
        VirtualizationOperationAdmissionCertificate? certificate,
        VirtualizationRestoreGenerationOwner? restoreOwner)
    {
        if (certificate is null)
            return DenyE2(VirtualizationE2Decision.MissingInput, "E2 certificate is missing.");
        if (!certificate.WasIssuedBy(_virtualizationE2IssuerSeal))
            return DenyE2(VirtualizationE2Decision.CertificateIssuerMismatch, "E2 issuer mismatch.");
        if (!_liveVirtualizationE2.TryGetValue(certificate, out LiveVirtualizationE2? live))
            return DenyE2(VirtualizationE2Decision.CertificateNotLive, "E2 is not present in the issuer live registry.");
        if (live.State == VirtualizationE2State.Revoked)
            return DenyE2(VirtualizationE2Decision.Revoked, "E2 was revoked.");
        if (live.State != VirtualizationE2State.Issued)
            return DenyE2(VirtualizationE2Decision.CertificateNotLive, "E2 is no longer in the issued state.");

        VirtualizationE2IssueRequest request = live.Request;
        if (!ReferenceEquals(request.OwnerPolicy, Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot))
            return DenyE2(VirtualizationE2Decision.InvalidD2OrOwnerPolicy, "E2 no longer resolves to the exact accepted O1.");
        if (restoreOwner is null || !ReferenceEquals(restoreOwner, request.RestoreOwner) ||
            restoreOwner.CurrentGeneration == 0 ||
            certificate.RestoreGeneration != restoreOwner.CurrentGeneration)
            return DenyE2(VirtualizationE2Decision.RestoreGenerationMismatch, "E2 was invalidated by restore generation change.");
        if (!request.CapabilityOwner!.IsLive(request.CapabilityLease))
            return DenyE2(VirtualizationE2Decision.CapabilityLeaseNotLive, "E2 capability generation is no longer live.");
        VirtualizationAdmissionValidationResult e1Validation = ValidateVirtualizationAdmission(
            request.ReplayPhase, request.BundleMetadata, request.Carrier!,
            request.SourceSlotId, request.WorkingSlotId, request.E1);
        if (!e1Validation.IsValidForFaultOnlyTransport)
            return DenyE2(VirtualizationE2Decision.InvalidE1, e1Validation.Reason);
        VirtualizationOperandValidationResult operandValidation =
            VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                request.Operand, request.Carrier, request.E1, request.OwnerPolicy,
                restoreOwner.CurrentGeneration);
        if (!operandValidation.IsValidForE2Input)
            return DenyE2(VirtualizationE2Decision.InvalidOperand, operandValidation.Reason);
        if (certificate.AttemptId != request.E1!.AttemptId ||
            certificate.E1IssuerGeneration != request.E1.IssuerGeneration ||
            certificate.VirtualThreadId != request.E1.VirtualThreadId ||
            certificate.OwnerContextId != request.E1.OwnerContextId ||
            certificate.DomainTag != request.E1.DomainTag ||
            certificate.BundleIdentity != request.E1.BundleIdentity ||
            certificate.ReplayEpoch != request.E1.ReplayEpoch ||
            certificate.NumericLeaf != request.Operand!.Rs1Value ||
            certificate.CapabilityGrantIdentity != request.CapabilityLease!.GrantIdentity ||
            certificate.CapabilityGeneration != request.CapabilityLease.Generation ||
            certificate.RootAuthorityEpoch != request.RootAuthority!.AuthorityEpoch)
            return DenyE2(VirtualizationE2Decision.CertificateBindingMismatch, "E2 binding no longer matches its live inputs.");

        string digest = VirtualizationE2Digest.Compute(
            certificate.IssuanceSequence, certificate.AttemptId, certificate.E1IssuerGeneration,
            certificate.VirtualThreadId, certificate.OwnerContextId, certificate.DomainTag,
            certificate.BundleIdentity, certificate.ReplayEpoch, certificate.DecisionId,
            certificate.SpecDigest, certificate.AcceptanceDigest, certificate.OwnerId,
            certificate.OwnerPolicyVersion, certificate.OwnerEpoch, certificate.OperationNamespace,
            certificate.OperationId, certificate.NumericLeaf, certificate.OwnerPolicyDigest,
            certificate.OperandDigest, certificate.CapabilityGrantIdentity,
            certificate.CapabilityGeneration, certificate.RootAuthorityEpoch,
            certificate.RestoreGeneration);
        if (!string.Equals(certificate.CertificateDigest, digest, StringComparison.Ordinal))
            return DenyE2(VirtualizationE2Decision.CertificateDigestMismatch, "E2 canonical digest mismatch.");

        return new(VirtualizationE2Decision.Issued, certificate, "E2 is live and valid; no backend consumer exists in PR-D.");
    }

    internal bool RevokeVirtualizationE2(VirtualizationOperationAdmissionCertificate? certificate)
    {
        lock (_virtualizationE2Sync)
        {
            if (certificate is null || !certificate.WasIssuedBy(_virtualizationE2IssuerSeal) ||
                !_liveVirtualizationE2.TryGetValue(certificate, out LiveVirtualizationE2? live) ||
                live.State != VirtualizationE2State.Issued)
                return false;
            live.State = VirtualizationE2State.Revoked;
            _issuedVirtualizationE2.Remove(certificate);
            return true;
        }
    }

    internal VirtualizationE2ConsumptionResult ConsumeVirtualizationE2FromExactExecutor(
        VirtualizationOperationAdmissionCertificate? certificate,
        VirtualizationRestoreGenerationOwner? restoreOwner,
        DomainHypercallLifecycleGate lifecycleGate,
        object consumerSeal)
    {
        if (!DomainHypercallRuntimeExecutor.IsExactConsumerSeal(consumerSeal))
            return new(
                VirtualizationE2ConsumptionDecision.InvalidConsumer,
                "Only the exact neutral runtime executor may consume E2.");

        lock (_virtualizationE2Sync)
        {
            VirtualizationE2Result validation = ValidateVirtualizationE2(certificate, restoreOwner);
            if (!validation.IsLive || certificate is null ||
                !_liveVirtualizationE2.TryGetValue(certificate, out LiveVirtualizationE2? live) ||
                live.State != VirtualizationE2State.Issued ||
                !ReferenceEquals(live.Request.LifecycleGate, lifecycleGate))
            {
                return new(
                    VirtualizationE2ConsumptionDecision.InvalidCertificate,
                    validation.Reason);
            }

            live.State = VirtualizationE2State.ConsumedByExecutor;
            _issuedVirtualizationE2.Remove(certificate);
            return new(
                VirtualizationE2ConsumptionDecision.Consumed,
                "Exact neutral runtime executor consumed E2 once.");
        }
    }

    internal VirtualizationE2State GetVirtualizationE2State(
        VirtualizationOperationAdmissionCertificate certificate) =>
        _liveVirtualizationE2.TryGetValue(certificate, out LiveVirtualizationE2? live)
            ? live.State
            : VirtualizationE2State.Revoked;

    internal int CountLiveVirtualizationE2(ulong domainTag)
    {
        lock (_virtualizationE2Sync)
            return _issuedVirtualizationE2.Count(certificate => certificate.DomainTag == domainTag);
    }

    internal int CancelLiveVirtualizationE2ForDrain(ulong domainTag)
    {
        lock (_virtualizationE2Sync)
        {
            VirtualizationOperationAdmissionCertificate[] cancelled =
                _issuedVirtualizationE2.Where(certificate => certificate.DomainTag == domainTag).ToArray();
            foreach (VirtualizationOperationAdmissionCertificate certificate in cancelled)
            {
                if (_liveVirtualizationE2.TryGetValue(certificate, out LiveVirtualizationE2? live) &&
                    live.State == VirtualizationE2State.Issued)
                    live.State = VirtualizationE2State.Revoked;
                _issuedVirtualizationE2.Remove(certificate);
            }
            return cancelled.Length;
        }
    }

    internal VirtualizationOperationAdmissionResult EvaluateVirtualizationOperationAdmission(
        VirtualizationOperationAdmissionRequest request,
        INeutralVirtualizationOperationOwner? owner = null)
    {
        VirtualizationOperationDecisionValidationResult d2 =
            VirtualizationOperationDecisionManifestValidator.Validate(request.DecisionManifest, request.Attribution);
        if (!d2.IsStructurallyValidGovernanceEvidence)
            return DenyLegacy(VirtualizationOperationAdmissionDecision.DeniedD2DecisionArtifact, d2.Reason);
        _ = owner;
        return DenyLegacy(
            VirtualizationOperationAdmissionDecision.DeniedOwnerInterfaceDisabled,
            "Phase-34 boolean E2 evaluation remains disabled; use the typed SafetyVerifier-only v2 contour.");
    }

    private static CapabilityGrant? ResolveExactGrant(DomainRuntimeContext context) =>
        context.Capabilities.TypedGrants.TryGetGrant(
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            CapabilityGrantScope.DomainGranted,
            out CapabilityGrant grant)
                ? grant
                : null;

    private ulong AllocateE2Sequence()
    {
        ulong sequence = _nextVirtualizationE2Sequence++;
        if (sequence == 0)
            sequence = _nextVirtualizationE2Sequence++;
        return sequence;
    }

    private static VirtualizationE2Result DenyE2(VirtualizationE2Decision decision, string reason) =>
        new(decision, null, reason);

    private static VirtualizationOperationAdmissionResult DenyLegacy(
        VirtualizationOperationAdmissionDecision decision,
        string reason) => new(decision, null, reason);
}

internal static class VirtualizationE2Digest
{
    private static readonly byte[] Envelope = Encoding.ASCII.GetBytes("HCPUE2V2\0");

    internal static string Compute(
        ulong sequence, ulong attemptId, ulong e1Generation, int vt, int ownerContext,
        ulong domain, ulong bundle, ulong replay, string decisionId, string specDigest,
        string acceptanceDigest, ulong ownerId, uint ownerPolicyVersion, uint ownerEpoch,
        string operationNamespace, string operationId, ushort leaf, string ownerPolicyDigest,
        string operandDigest, ulong grantIdentity, ulong grantGeneration, ulong rootEpoch,
        ulong restoreGeneration)
    {
        using var stream = new MemoryStream();
        stream.Write(Envelope);
        WriteU64(stream, sequence); WriteU64(stream, attemptId); WriteU64(stream, e1Generation);
        WriteU32(stream, unchecked((uint)vt)); WriteU32(stream, unchecked((uint)ownerContext));
        WriteU64(stream, domain); WriteU64(stream, bundle); WriteU64(stream, replay);
        WriteText(stream, decisionId); stream.Write(Convert.FromHexString(specDigest));
        stream.Write(Convert.FromHexString(acceptanceDigest)); WriteU64(stream, ownerId);
        WriteU32(stream, ownerPolicyVersion); WriteU32(stream, ownerEpoch);
        WriteText(stream, operationNamespace); WriteText(stream, operationId); WriteU16(stream, leaf);
        stream.Write(Convert.FromHexString(ownerPolicyDigest));
        stream.Write(Convert.FromHexString(operandDigest));
        WriteU64(stream, grantIdentity); WriteU64(stream, grantGeneration);
        WriteU64(stream, rootEpoch); WriteU64(stream, restoreGeneration);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteText(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteU32(stream, checked((uint)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteU16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value); stream.Write(bytes);
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes);
    }

    private static void WriteU64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value); stream.Write(bytes);
    }
}
