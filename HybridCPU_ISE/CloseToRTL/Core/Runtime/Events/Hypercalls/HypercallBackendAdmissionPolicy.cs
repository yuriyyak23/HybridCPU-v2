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
    {
        Authority = authority;
        CapabilityRequirement = capabilityRequirement;
        EvidenceRequirement = evidenceRequirement;
        RequiresValidatedDomain = requiresValidatedDomain;
        NeutralBackendOwnerMaterialized = neutralBackendOwnerMaterialized;
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

    public HypercallBackendAuthority Authority { get; }

    public CapabilityBoundaryRequirement CapabilityRequirement { get; }

    public EvidenceBoundaryRequirement EvidenceRequirement { get; }

    public bool RequiresValidatedDomain { get; }

    public bool NeutralBackendOwnerMaterialized { get; }

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

        return Deny(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerMissing,
            request,
            "Hypercall backend execution remains denied until a neutral execution owner is explicitly admitted.");
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
