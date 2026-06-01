namespace YAKSys_Hybrid_CPU.Core;

public enum SecureIoHypercallAdmissionDecision : byte
{
    AllowedIo = 0,
    AllowedAdmittedDenied = 1,
    DeniedMissingIoOwner = 2,
    DeniedPrivateMemoryAccess = 3,
    DeniedSharedBufferBinding = 4,
    DeniedMissingTypedGrant = 5,
    DeniedMissingHypercallPolicy = 6,
    DeniedHypercallId = 7,
    DeniedRawPrivatePointer = 8,
    DeniedForgedOpaqueHandle = 9,
    DeniedMissingNeutralBackendOwner = 10,
    DeniedDomainValidation = 11,
    DeniedEvidence = 12,
    DeniedCompletionFence = 13,
    DeniedRetireFence = 14,
    DeniedBackendSuccessClosed = 15,
}

public readonly record struct SecureIoHypercallAdmissionResult(
    SecureIoHypercallAdmissionDecision Decision,
    bool BackendExecutionAuthorized,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision is SecureIoHypercallAdmissionDecision.AllowedIo
            or SecureIoHypercallAdmissionDecision.AllowedAdmittedDenied;

    public bool IsAdmittedDenied =>
        Decision == SecureIoHypercallAdmissionDecision.AllowedAdmittedDenied &&
        !BackendExecutionAuthorized;

    public static SecureIoHypercallAdmissionResult AllowedIo(
        bool completionPublicationAuthorized,
        bool retirePublicationAuthorized) =>
        new(
            SecureIoHypercallAdmissionDecision.AllowedIo,
            BackendExecutionAuthorized: false,
            completionPublicationAuthorized,
            retirePublicationAuthorized,
            string.Empty);

    public static SecureIoHypercallAdmissionResult AllowedAdmittedDenied(
        bool completionPublicationAuthorized,
        bool retirePublicationAuthorized) =>
        new(
            SecureIoHypercallAdmissionDecision.AllowedAdmittedDenied,
            BackendExecutionAuthorized: false,
            completionPublicationAuthorized,
            retirePublicationAuthorized,
            "Secure hypercall admission is recognized but backend execution remains closed.");

    public static SecureIoHypercallAdmissionResult Denied(
        SecureIoHypercallAdmissionDecision decision,
        string reason) =>
        new(
            decision,
            BackendExecutionAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed partial class SecureIoHypercallAdmissionPolicy
{
    public static SecureIoHypercallAdmissionPolicy Default { get; } = new();

    private readonly SecureMemoryAdmissionPolicy _memoryAdmission = new();
    private readonly SecureGrantAuthorityPolicy _grantAuthority = new();

    public SecureIoHypercallAdmissionResult AdmitIoDma(
        SecureIoDomainDescriptor? ioPolicy,
        SecureMemoryDomainDescriptor? memory,
        SecureMemoryAccessRequest access,
        SecureCompletionPublicationFence? publicationFence,
        bool requireRetirePublication)
    {
        if (ioPolicy?.NeutralIoOwnerMaterialized != true)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingIoOwner,
                "Secure I/O requires a neutral I/O owner descriptor.");
        }

        SecureMemoryAdmissionResult memoryResult = _memoryAdmission.Admit(
            memory,
            access,
            ioPolicy);
        if (!memoryResult.IsAllowed)
        {
            return Deny(MapMemoryDecision(memoryResult.Decision), memoryResult.Reason);
        }

        SecureIoHypercallAdmissionResult completion =
            AdmitCompletionAndRetire(
                publicationFence,
                requireCompletionPublication: ioPolicy.RequireCompletionFence,
                requireRetirePublication);
        if (!completion.IsAllowed)
        {
            return completion;
        }

        return SecureIoHypercallAdmissionResult.AllowedIo(
            completion.CompletionPublicationAuthorized,
            completion.RetirePublicationAuthorized);
    }

    public SecureIoHypercallAdmissionResult AdmitHypercall(
        SecureHypercallDescriptor? hypercallPolicy,
        ulong hypercallId,
        CapabilityDescriptorSet? capabilities,
        EvidencePolicyDescriptor? evidencePolicy,
        SecureIoDomainDescriptor? ioPolicy,
        SecureRevocationEpoch policyEpoch,
        SecureCompletionPublicationFence? publicationFence,
        bool neutralBackendOwnerMaterialized,
        bool domainValidated)
    {
        if (hypercallPolicy is null || !hypercallPolicy.HasPolicy)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingHypercallPolicy,
                "Secure hypercall requires an explicit secure hypercall policy.");
        }

        if (!hypercallPolicy.AllowsHypercallId(hypercallId))
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedHypercallId,
                "Secure hypercall id is not allowed by policy.");
        }

        foreach (SecureHypercallArgumentDescriptor argument in hypercallPolicy.Arguments)
        {
            SecureIoHypercallAdmissionResult argumentResult =
                AdmitArgument(argument, ioPolicy, policyEpoch);
            if (!argumentResult.IsAllowed)
            {
                return argumentResult;
            }
        }

        if (hypercallPolicy.NeutralBackendOwnerRequired && !neutralBackendOwnerMaterialized)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingNeutralBackendOwner,
                "Secure hypercall backend execution requires a neutral runtime backend owner.");
        }

        if (!domainValidated)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedDomainValidation,
                "Secure hypercall requires a validated secure runtime domain.");
        }

        SecureIoHypercallAdmissionResult capability = AdmitTypedGrant(
            hypercallPolicy.RequiredGrant,
            capabilities,
            policyEpoch);
        if (!capability.IsAllowed)
        {
            return capability;
        }

        if (hypercallPolicy.RequireEvidenceApproval &&
            evidencePolicy?.CanExposeToGuest(EvidenceVisibilityClass.GuestArchitecturalState) != true)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedEvidence,
                "Secure hypercall requires neutral evidence policy approval.");
        }

        SecureIoHypercallAdmissionResult publication =
            AdmitCompletionAndRetire(
                publicationFence,
                hypercallPolicy.RequireCompletionFence,
                hypercallPolicy.RequireRetirePublicationRule);
        if (!publication.IsAllowed)
        {
            return publication;
        }

        if (!hypercallPolicy.AllowBackendExecution)
        {
            return SecureIoHypercallAdmissionResult.AllowedAdmittedDenied(
                publication.CompletionPublicationAuthorized,
                publication.RetirePublicationAuthorized);
        }

        return Deny(
            SecureIoHypercallAdmissionDecision.DeniedBackendSuccessClosed,
            "Secure hypercall backend success remains closed in this phase.");
    }

    private SecureIoHypercallAdmissionResult AdmitArgument(
        SecureHypercallArgumentDescriptor argument,
        SecureIoDomainDescriptor? ioPolicy,
        SecureRevocationEpoch policyEpoch)
    {
        if (argument.IsDeniedRawPrivatePointer)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedRawPrivatePointer,
                "Raw private guest pointer arguments are denied for secure hypercalls.");
        }

        if (argument.ArgumentClass == SecureHypercallArgumentClass.OpaqueHandle)
        {
            return argument.Grant.MatchesEpoch(policyEpoch)
                ? SecureIoHypercallAdmissionResult.AllowedIo(false, false)
                : Deny(
                    SecureIoHypercallAdmissionDecision.DeniedForgedOpaqueHandle,
                    "Secure hypercall opaque handles require provenance and current epoch.");
        }

        if (!argument.RequiresSharedBuffer)
        {
            return SecureIoHypercallAdmissionResult.AllowedIo(false, false);
        }

        if (ioPolicy?.NeutralIoOwnerMaterialized != true)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingIoOwner,
                "Secure hypercall shared-buffer arguments require a neutral I/O owner.");
        }

        if (!ioPolicy.AllowsSharedBuffer(argument.SharedBufferId))
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
                "Secure hypercall shared-buffer argument must reference an explicit shared-buffer descriptor.");
        }

        return argument.Grant.MatchesEpoch(policyEpoch)
            ? SecureIoHypercallAdmissionResult.AllowedIo(false, false)
            : Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
                "Secure hypercall shared-buffer argument requires a current typed grant.");
    }

    private SecureIoHypercallAdmissionResult AdmitTypedGrant(
        SecureGrantHandle requiredGrant,
        CapabilityDescriptorSet? capabilities,
        SecureRevocationEpoch policyEpoch)
    {
        SecureAuthorityBounds hypercallBounds = new(
            allowsPrivateMemory: false,
            allowsSharedMemory: false,
            allowsIo: false,
            allowsDma: false,
            allowsHypercalls: true,
            allowsDebug: false,
            allowsMigration: false,
            allowsCompatibilityProjection: false);
        SecureGrantAuthorityResult authority = _grantAuthority.Validate(
            requiredGrant,
            SecureGrantMaterializationSource.NeutralRuntimeOwner,
            hypercallBounds,
            hypercallBounds,
            SecureGrantEpochSet.Single(policyEpoch),
            runtimeOwnerMaterialized: true,
            capabilities: capabilities ?? CapabilityDescriptorSet.Empty,
            requiredScope: CapabilityGrantScope.DomainGranted);

        return authority.IsAllowed
            ? SecureIoHypercallAdmissionResult.AllowedIo(false, false)
            : Deny(
                SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
                authority.Reason);
    }

    private static SecureIoHypercallAdmissionResult AdmitCompletionAndRetire(
        SecureCompletionPublicationFence? publicationFence,
        bool requireCompletionPublication,
        bool requireRetirePublication)
    {
        if (requireCompletionPublication &&
            publicationFence?.CanPublishCompletion != true)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedCompletionFence,
                "Secure I/O and hypercall completion publication requires a completion fence.");
        }

        if (requireRetirePublication &&
            publicationFence?.CanPublishRetire != true)
        {
            return Deny(
                SecureIoHypercallAdmissionDecision.DeniedRetireFence,
                "Secure I/O and hypercall retire publication requires an explicit retire fence.");
        }

        return SecureIoHypercallAdmissionResult.AllowedIo(
            publicationFence?.CanPublishCompletion == true,
            publicationFence?.CanPublishRetire == true);
    }

    private static SecureIoHypercallAdmissionDecision MapMemoryDecision(
        SecureMemoryAdmissionDecision decision) =>
        decision switch
        {
            SecureMemoryAdmissionDecision.DeniedPrivateDma =>
                SecureIoHypercallAdmissionDecision.DeniedPrivateMemoryAccess,
            SecureMemoryAdmissionDecision.DeniedDmaRequiresTypedGrant =>
                SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
            _ => SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
        };

    private static SecureIoHypercallAdmissionResult Deny(
        SecureIoHypercallAdmissionDecision decision,
        string reason) =>
        SecureIoHypercallAdmissionResult.Denied(decision, reason);
}
