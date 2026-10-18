namespace YAKSys_Hybrid_CPU.Core;

public enum HypercallBackendAdmissionDecision : byte
{
    NotEvaluated = 0,
    DeniedRuntimeAdmission = 1,
    DeniedNoNeutralTrap = 2,
    MissingBackendDescriptor = 3,
    DeniedBackendAuthority = 4,
    DeniedDomainValidation = 5,
    DeniedCapability = 6,
    DeniedEvidence = 7,
    DeniedNeutralBackendOwnerMissing = 8,
    DeniedNeutralBackendOwnerAuthority = 9,
    DeniedNeutralBackendOwnerRfcAdr = 10,
    DeniedNeutralBackendOwnerShape = 11,
    DeniedNeutralBackendOwnerProof = 12,
}

public enum HypercallBackendAuthority : byte
{
    Runtime = 0,
    DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class HypercallBackendDescriptor
{
    public HypercallBackendDescriptor()
        : this(
            HypercallBackendAuthority.Runtime,
            CapabilityBoundaryRequirement.None,
            EvidenceBoundaryRequirement.None,
            requiresValidatedDomain: true,
            neutralBackendOwnerMaterialized: false)
    {
    }

    public HypercallBackendDescriptor(
        HypercallBackendAuthority authority,
        CapabilityBoundaryRequirement capabilityRequirement,
        EvidenceBoundaryRequirement evidenceRequirement,
        bool requiresValidatedDomain,
        bool neutralBackendOwnerMaterialized)
        : this(
            authority,
            capabilityRequirement,
            evidenceRequirement,
            requiresValidatedDomain,
            neutralBackendOwnerMaterialized,
            neutralBackendOwner: null)
    {
    }

    public HypercallBackendDescriptor(
        HypercallBackendAuthority authority,
        CapabilityBoundaryRequirement capabilityRequirement,
        EvidenceBoundaryRequirement evidenceRequirement,
        bool requiresValidatedDomain,
        bool neutralBackendOwnerMaterialized,
        NeutralHypercallBackendOwnerDescriptor? neutralBackendOwner)
    {
        Authority = authority;
        CapabilityRequirement = capabilityRequirement;
        EvidenceRequirement = evidenceRequirement;
        RequiresValidatedDomain = requiresValidatedDomain;
        NeutralBackendOwnerMaterialized = neutralBackendOwnerMaterialized;
        NeutralBackendOwner = neutralBackendOwner;
    }

    public static HypercallBackendDescriptor RuntimeOwnedDesignFence(
        CapabilityBoundaryRequirement capabilityRequirement,
        EvidenceBoundaryRequirement evidenceRequirement) =>
        new(
            HypercallBackendAuthority.Runtime,
            capabilityRequirement,
            evidenceRequirement,
            requiresValidatedDomain: true,
            neutralBackendOwnerMaterialized: false);

    public static HypercallBackendDescriptor RuntimeOwnedDraftOwnerFence(
        CapabilityBoundaryRequirement capabilityRequirement,
        EvidenceBoundaryRequirement evidenceRequirement,
        NeutralHypercallBackendOwnerDescriptor neutralBackendOwner) =>
        new(
            HypercallBackendAuthority.Runtime,
            capabilityRequirement,
            evidenceRequirement,
            requiresValidatedDomain: true,
            neutralBackendOwnerMaterialized: true,
            neutralBackendOwner);

    public HypercallBackendAuthority Authority { get; }

    public CapabilityBoundaryRequirement CapabilityRequirement { get; }

    public EvidenceBoundaryRequirement EvidenceRequirement { get; }

    public bool RequiresValidatedDomain { get; }

    public bool NeutralBackendOwnerMaterialized { get; }

    public NeutralHypercallBackendOwnerDescriptor? NeutralBackendOwner { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == HypercallBackendAuthority.Runtime;
}

public readonly record struct HypercallBackendAdmissionRequest(
    NeutralTrapResult TrapResult,
    RuntimeBoundaryAdmissionResult RuntimeAdmission,
    HypercallBackendDescriptor? BackendDescriptor,
    CapabilityDescriptorSet? Capabilities,
    EvidencePolicyDescriptor? EvidencePolicy,
    bool DomainValidated)
{
    public static HypercallBackendAdmissionRequest MissingNeutralOwner(
        NeutralTrapResult trapResult,
        RuntimeBoundaryAdmissionResult runtimeAdmission,
        CapabilityDescriptorSet? capabilities,
        EvidencePolicyDescriptor? evidencePolicy,
        bool domainValidated) =>
        new(
            trapResult,
            runtimeAdmission,
            BackendDescriptor: null,
            capabilities,
            evidencePolicy,
            domainValidated);
}

public readonly record struct HypercallBackendAdmissionResult(
    HypercallBackendAdmissionDecision Decision,
    bool BackendExecutionAuthorized,
    CapabilityBoundaryRequirement CapabilityRequirement,
    EvidenceBoundaryRequirement EvidenceRequirement,
    string Reason)
{
    public bool IsAllowed => BackendExecutionAuthorized;

    public bool DeniesBackendExecution => !BackendExecutionAuthorized;

    public static HypercallBackendAdmissionResult NotEvaluated(string reason) =>
        new(
            HypercallBackendAdmissionDecision.NotEvaluated,
            BackendExecutionAuthorized: false,
            CapabilityBoundaryRequirement.None,
            EvidenceBoundaryRequirement.None,
            reason);

    public static HypercallBackendAdmissionResult Denied(
        HypercallBackendAdmissionDecision decision,
        CapabilityBoundaryRequirement capabilityRequirement,
        EvidenceBoundaryRequirement evidenceRequirement,
        string reason) =>
        new(
            decision,
            BackendExecutionAuthorized: false,
            capabilityRequirement,
            evidenceRequirement,
            reason);
}

public sealed class HypercallBackendAdmissionService
{
    public static HypercallBackendAdmissionService Default { get; } = new();

    public HypercallBackendAdmissionResult Admit(
        HypercallBackendAdmissionRequest request)
    {
        if (!request.RuntimeAdmission.IsAllowed)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedRuntimeAdmission,
                request,
                "Hypercall backend admission requires runtime boundary admission.");
        }

        if (!request.TrapResult.ShouldTrap)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNoNeutralTrap,
                request,
                "Hypercall backend admission requires a neutral trap result.");
        }

        if (request.BackendDescriptor is null)
        {
            return Deny(
                HypercallBackendAdmissionDecision.MissingBackendDescriptor,
                request,
                "Hypercall backend execution remains denied: no neutral runtime backend descriptor is materialized.");
        }

        HypercallBackendDescriptor descriptor = request.BackendDescriptor;
        if (!descriptor.IsRuntimeAuthoritative)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedBackendAuthority,
                request,
                "Compatibility projection cannot own hypercall backend execution.");
        }

        if (descriptor.RequiresValidatedDomain && !request.DomainValidated)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedDomainValidation,
                request,
                "Hypercall backend admission requires a validated runtime domain.");
        }

        if (!descriptor.CapabilityRequirement.IsSatisfiedBy(
                request.Capabilities ?? CapabilityDescriptorSet.Empty))
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedCapability,
                request,
                "Hypercall backend admission requires a typed runtime capability grant.");
        }

        if (!descriptor.EvidenceRequirement.IsSatisfiedBy(request.EvidencePolicy))
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedEvidence,
                request,
                "Hypercall backend admission requires neutral evidence policy approval.");
        }

        if (!descriptor.NeutralBackendOwnerMaterialized)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerMissing,
                request,
                "Hypercall backend execution remains denied: neutral backend owner semantics are not materialized.");
        }

        if (descriptor.NeutralBackendOwner is not { } owner ||
            !owner.IsMaterialized)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerMissing,
                request,
                "Hypercall backend execution remains denied: no neutral backend owner descriptor is materialized.");
        }

        if (!owner.IsNeutralRuntimeOwner)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerAuthority,
                request,
                "Hypercall backend execution remains denied: compatibility projection cannot source neutral backend owner authority.");
        }

        if (!owner.IsCandidateDraft)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerRfcAdr,
                request,
                "Hypercall backend execution remains denied: owner-specific RFC/ADR packet is missing or not the VMCALL no-state draft.");
        }

        if (!owner.HasNoStateCandidateShape)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerShape,
                request,
                "Hypercall backend execution remains denied: owner descriptor is not the no-state, no-payload, domain-local draft shape.");
        }

        if (!owner.NegativeTestsPresent)
        {
            return Deny(
                HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerProof,
                request,
                "Hypercall backend execution remains denied: owner-specific negative conformance proof is missing.");
        }

        return Deny(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerRfcAdr,
            request,
            "Hypercall backend execution remains denied: RFC-HV-VMCALL-NO-STATE-OWNER-0001 is draft only and has no accepted owner semantics.");
    }

    private static HypercallBackendAdmissionResult Deny(
        HypercallBackendAdmissionDecision decision,
        HypercallBackendAdmissionRequest request,
        string reason) =>
        HypercallBackendAdmissionResult.Denied(
            decision,
            request.BackendDescriptor?.CapabilityRequirement ?? CapabilityBoundaryRequirement.None,
            request.BackendDescriptor?.EvidenceRequirement ?? EvidenceBoundaryRequirement.None,
            reason);
}
