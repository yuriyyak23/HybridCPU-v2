#if TESTING
using System;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core;

public partial class SafetyVerifier
{
    internal sealed class ResearchVirtualizationOperationAdmissionCertificate
    {
        internal ResearchVirtualizationOperationAdmissionCertificate(
            object verifierSeal,
            object ownerSeal,
            ulong ownerPolicyGeneration,
            object contextSeal,
            ulong contextGeneration,
            ulong verifierGeneration,
            ResearchVirtualizationProbeOperation operation,
            ResearchVirtualizationProbeIdentity identity,
            VirtualizationAdmissionCertificate e1Carrier)
        {
            _verifierSeal = verifierSeal;
            OwnerSeal = ownerSeal;
            OwnerPolicyGeneration = ownerPolicyGeneration;
            ContextSeal = contextSeal;
            ContextGeneration = contextGeneration;
            VerifierGeneration = verifierGeneration;
            Operation = operation;
            Identity = identity;
            E1Carrier = e1Carrier;
        }

        private readonly object _verifierSeal;
        internal object OwnerSeal { get; }
        internal ulong OwnerPolicyGeneration { get; }
        internal object ContextSeal { get; }
        internal ulong ContextGeneration { get; }
        internal ulong VerifierGeneration { get; }
        internal ResearchVirtualizationProbeOperation Operation { get; }
        internal ResearchVirtualizationProbeIdentity Identity { get; }
        internal VirtualizationAdmissionCertificate E1Carrier { get; }
        internal bool HasNumericLeaf => false;
        internal bool CompletionPublicationAuthorized => false;
        internal bool RetirePublicationAuthorized => false;

        internal bool WasIssuedBy(object verifierSeal) =>
            ReferenceEquals(_verifierSeal, verifierSeal);
    }

    internal ResearchVirtualizationProbeAdmissionResult IssueResearchVirtualizationOperationAdmission(
        ResearchVirtualizationRuntimeOwner.PolicySnapshot policy,
        ResearchVirtualizationOperationContext context,
        ResearchVirtualizationOperationContext.IdentitySnapshot identitySnapshot,
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundle,
        VmxMicroOp carrier,
        int sourceSlotId,
        int workingSlotId,
        VirtualizationAdmissionCertificate e1)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(identitySnapshot);
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(e1);

        if (policy.Operation != ResearchVirtualizationProbeOperation.StateMinimalLiveness)
            return DenyResearchVirtualizationAdmission(
                ResearchVirtualizationProbeAdmissionDecision.DeniedUnsupportedOperation,
                "The research policy does not name the state-minimal probe.");

        if (!context.IsLive(identitySnapshot))
            return DenyResearchVirtualizationAdmission(
                ResearchVirtualizationProbeAdmissionDecision.DeniedRuntimeIdentitySnapshot,
                "The operation identity snapshot is foreign or invalidated.");

        ResearchVirtualizationProbeIdentity identity = identitySnapshot.Identity;
        if (!identity.IsComplete)
            return DenyResearchVirtualizationAdmission(
                ResearchVirtualizationProbeAdmissionDecision.DeniedIncompleteIdentity,
                "Every E2 research identity must be materialized.");

        VirtualizationAdmissionValidationResult e1Validation = ValidateVirtualizationAdmission(
            replayPhase,
            bundle,
            carrier,
            sourceSlotId,
            workingSlotId,
            e1);
        if (!e1Validation.IsValidForFaultOnlyTransport)
            return DenyResearchVirtualizationAdmission(
                ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
                e1Validation.Reason);

        if (identity.VirtualThreadId != e1.VirtualThreadId ||
            identity.OwnerContextId != e1.OwnerContextId ||
            identity.DomainTag != e1.DomainTag ||
            identity.CarrierAttemptId != e1.AttemptId ||
            identity.ReplayEpoch != e1.ReplayEpoch)
            return DenyResearchVirtualizationAdmission(
                ResearchVirtualizationProbeAdmissionDecision.DeniedCarrierIdentityMismatch,
                "The research identities do not match the live E1 carrier attempt.");

        return new(
            ResearchVirtualizationProbeAdmissionDecision.IssuedForResearchExecution,
            new ResearchVirtualizationOperationAdmissionCertificate(
                _virtualizationAdmissionIssuerSeal,
                policy.OwnerSeal,
                policy.PolicyGeneration,
                identitySnapshot.ContextSeal,
                identitySnapshot.ContextGeneration,
                e1.IssuerGeneration,
                policy.Operation,
                identity,
                e1),
            "SafetyVerifier issued one TESTING-only operation certificate over a live E1 carrier.");
    }

    internal bool IsResearchVirtualizationOperationAdmissionLive(
        ResearchVirtualizationOperationAdmissionCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (!certificate.WasIssuedBy(_virtualizationAdmissionIssuerSeal) ||
            certificate.VerifierGeneration != _virtualizationAdmissionIssuerGeneration)
            return false;

        return _liveVirtualizationAdmissions.TryGetValue(
                   certificate.E1Carrier,
                   out LiveVirtualizationAdmission? live) &&
               live.AttemptId == certificate.Identity.CarrierAttemptId &&
               live.IssuerGeneration == _virtualizationAdmissionIssuerGeneration;
    }

    private static ResearchVirtualizationProbeAdmissionResult DenyResearchVirtualizationAdmission(
        ResearchVirtualizationProbeAdmissionDecision decision,
        string reason) => new(decision, null, reason);
}
#endif
