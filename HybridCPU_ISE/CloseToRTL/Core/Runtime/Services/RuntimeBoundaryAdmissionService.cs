// Description: Generic runtime admission service that joins domain, capability, evidence, and root-authority gates.
namespace YAKSys_Hybrid_CPU.Core;

public enum RuntimeBoundaryAdmissionDecision : byte
{
    Allowed = 0,
    MissingDomainContext = 1,
    DomainBoundaryDenied = 2,
    CapabilityBoundaryDenied = 3,
    EvidenceBoundaryDenied = 4,
    FrontendAuthoritativeMutationDenied = 5,
    RuntimeAuthorityDenied = 6,
    SecureDomainBoundaryDenied = 7,
}

public readonly record struct RuntimeBoundaryAdmissionRequest(
    DomainRuntimeContext? Context,
    RootAuthorityDescriptor? RootAuthority,
    EvidencePolicyDescriptor? EvidencePolicy,
    DomainRuntimeOperation Operation,
    DomainBoundaryDescriptor DomainBoundary,
    CapabilityBoundaryRequirement CapabilityRequirement,
    EvidenceBoundaryRequirement EvidenceRequirement,
    SecureComputeDomainDescriptor? SecureDescriptor = null,
    SecureDomainOperationClass SecureOperationClass = SecureDomainOperationClass.Ordinary,
    DomainMeasurementDescriptor? SecureMeasurement = null,
    SecureMemoryDomainDescriptor? SecureMemory = null,
    SecureMemoryAccessRequest? SecureMemoryAccess = null);

public readonly record struct RuntimeBoundaryAdmissionResult(
    RuntimeBoundaryAdmissionDecision Decision,
    string Message,
    DomainRuntimeAuthorityResult AuthorityResult)
{
    public bool IsAllowed => Decision == RuntimeBoundaryAdmissionDecision.Allowed;

    public static RuntimeBoundaryAdmissionResult Allowed(
        DomainRuntimeAuthorityResult authorityResult) =>
        new(
            RuntimeBoundaryAdmissionDecision.Allowed,
            string.Empty,
            authorityResult);

    public static RuntimeBoundaryAdmissionResult Denied(
        RuntimeBoundaryAdmissionDecision decision,
        string message,
        DomainRuntimeAuthorityResult authorityResult = default) =>
        new(decision, message, authorityResult);
}

public sealed partial class RuntimeBoundaryAdmissionService
{
    private readonly DomainRuntimeAuthority _authority;
    private readonly SecureDomainAdmissionService _secureAdmission;
    private readonly SecureMemoryAdmissionPolicy _secureMemoryAdmission;

    public RuntimeBoundaryAdmissionService()
        : this(
            new DomainRuntimeAuthority(),
            new SecureDomainAdmissionService(),
            new SecureMemoryAdmissionPolicy())
    {
    }

    public RuntimeBoundaryAdmissionService(DomainRuntimeAuthority authority)
        : this(
            authority,
            new SecureDomainAdmissionService(),
            new SecureMemoryAdmissionPolicy())
    {
    }

    public RuntimeBoundaryAdmissionService(
        DomainRuntimeAuthority authority,
        SecureDomainAdmissionService secureAdmission)
        : this(authority, secureAdmission, new SecureMemoryAdmissionPolicy())
    {
    }

    public RuntimeBoundaryAdmissionService(
        DomainRuntimeAuthority authority,
        SecureDomainAdmissionService secureAdmission,
        SecureMemoryAdmissionPolicy secureMemoryAdmission)
    {
        _authority = authority;
        _secureAdmission = secureAdmission;
        _secureMemoryAdmission = secureMemoryAdmission;
    }

    public RuntimeBoundaryAdmissionResult Validate(
        RuntimeBoundaryAdmissionRequest request)
    {
        DomainRuntimeContext? context = request.Context;
        if (context is null)
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.MissingDomainContext,
                "Runtime boundary admission requires a domain runtime context.");
        }

        if (!request.DomainBoundary.IsSatisfiedBy(context))
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.DomainBoundaryDenied,
                "Runtime boundary admission requires descriptor-owned execution, memory and I/O domains.");
        }

        if (!request.CapabilityRequirement.IsSatisfiedBy(context.Capabilities))
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.CapabilityBoundaryDenied,
                "Runtime boundary admission requires a typed capability grant.");
        }

        if (!request.EvidenceRequirement.IsSatisfiedBy(request.EvidencePolicy))
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.EvidenceBoundaryDenied,
                "Runtime boundary admission requires evidence policy approval.");
        }

        if (IsFrontendAuthoritativeMutation(request.Operation))
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.FrontendAuthoritativeMutationDenied,
                "Compatibility frontend cannot directly mutate authoritative runtime state.");
        }

        SecureComputeDomainDescriptor? secureDescriptor =
            context.SecureCompute ?? request.SecureDescriptor;
        if (request.SecureOperationClass != SecureDomainOperationClass.Ordinary &&
            secureDescriptor is { IsEnabled: true })
        {
            SecureDomainAdmissionResult secureAdmission = _secureAdmission.Admit(
                secureDescriptor,
                request.SecureOperationClass,
                request.SecureMeasurement,
                request.SecureMemory);

            if (!secureAdmission.IsAllowed)
            {
                return Deny(
                    RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
                    secureAdmission.Reason);
            }

            if (!context.IsBoundToDomain(secureDescriptor.DomainTag))
            {
                return Deny(
                    RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
                    "Secure domain descriptor must match the neutral runtime domain tag.");
            }

            if (request.SecureMemory is { IsMaterialized: true } secureMemory &&
                !secureMemory.IsBoundTo(secureDescriptor.DomainTag, context.AddressSpaceTag))
            {
                return Deny(
                    RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
                    "Secure memory descriptor must match the secure domain and neutral address-space tags.");
            }

            if (request.SecureMemoryAccess is { } secureMemoryAccess)
            {
                SecureMemoryAdmissionResult memoryAdmission = _secureMemoryAdmission.Admit(
                    request.SecureMemory,
                    secureMemoryAccess,
                    secureDescriptor.IoPolicy);

                if (!memoryAdmission.IsAllowed)
                {
                    return Deny(
                        RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
                        memoryAdmission.Reason);
                }
            }
        }

        DomainRuntimeAuthorityResult authority = _authority.Validate(
            request.RootAuthority,
            context,
            request.Operation,
            request.CapabilityRequirement);

        if (!authority.IsAllowed)
        {
            return Deny(
                RuntimeBoundaryAdmissionDecision.RuntimeAuthorityDenied,
                authority.Message,
                authority);
        }

        return RuntimeBoundaryAdmissionResult.Allowed(authority);
    }

    public bool CanAdmit(RuntimeBoundaryAdmissionRequest request) =>
        Validate(request).IsAllowed;

    private static bool IsFrontendAuthoritativeMutation(
        DomainRuntimeOperation operation) =>
        operation.Source == DomainRuntimeOperationSource.CompatibilityFrontend &&
        operation.CanMutateAuthoritativeState &&
        operation.Kind is not DomainRuntimeOperationKind.ActivateCompatibilityFrontend
            and not DomainRuntimeOperationKind.DeactivateCompatibilityFrontend;

    private static RuntimeBoundaryAdmissionResult Deny(
        RuntimeBoundaryAdmissionDecision decision,
        string message,
        DomainRuntimeAuthorityResult authorityResult = default) =>
        RuntimeBoundaryAdmissionResult.Denied(decision, message, authorityResult);
}
