namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeNamedPositivePath : byte
{
    GuestCr0Cr4ReadOnlyProjection = 0,
    HypercallBackendOwnerProofOnly = 1,
    CompletionRetirePublicationGate = 2,
    MigrationOutputManifestClassification = 3,
    DebugAttestationVisibility = 4,
    FutureRestrictedRuntimeExecution = 5,
}

public enum SecureComputeNamedPathVmxZeroAuthorityDecision : byte
{
    ZeroAuthorityClassified = 0,
    DeniedVmxActivation = 1,
    DeniedVmxCapsAuthority = 2,
    DeniedVmcsStateStore = 3,
    DeniedActiveVmcsPointerIdentity = 4,
    DeniedVmreadSecureStateAuthority = 5,
    DeniedVmwriteSecureStateMutation = 6,
    DeniedVmcsCheckpointAuthority = 7,
    DeniedCompletionPublication = 8,
    DeniedRetirePublication = 9,
    DeniedProjectionWithoutNeutralResult = 10,
}

public readonly record struct SecureComputeNamedPathVmxZeroAuthorityRequest(
    SecureComputeNamedPositivePath Path,
    bool HasNeutralRuntimeResult = false,
    bool RequestsCompatibilityProjection = false,
    bool AttemptsVmxActivation = false,
    bool AttemptsVmxCapsGrant = false,
    bool AttemptsVmcsStateStore = false,
    bool AttemptsActiveVmcsPointerIdentity = false,
    bool AttemptsVmreadSecureStateAuthority = false,
    bool AttemptsVmwriteSecureStateMutation = false,
    bool AttemptsVmcsCheckpointAuthority = false,
    bool AttemptsCompletionPublication = false,
    bool AttemptsRetirePublication = false);

public readonly record struct SecureComputeNamedPathVmxZeroAuthorityResult(
    SecureComputeNamedPathVmxZeroAuthorityDecision Decision,
    SecureComputeNamedPositivePath Path,
    bool CompatibilityProjectionAllowed,
    bool VmxActivationAuthorized,
    bool VmxCapsAuthorityAuthorized,
    bool VmcsStateStoreAuthorized,
    bool ActiveVmcsPointerIdentityAuthorized,
    bool VmreadSecureStateAuthorityAuthorized,
    bool VmwriteSecureStateMutationAuthorized,
    bool VmcsCheckpointAuthorityAuthorized,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureComputeNamedPathVmxZeroAuthorityDecision.ZeroAuthorityClassified;

    public bool CreatesAnyVmxAuthority =>
        VmxActivationAuthorized ||
        VmxCapsAuthorityAuthorized ||
        VmcsStateStoreAuthorized ||
        ActiveVmcsPointerIdentityAuthorized ||
        VmreadSecureStateAuthorityAuthorized ||
        VmwriteSecureStateMutationAuthorized ||
        VmcsCheckpointAuthorityAuthorized;

    public static SecureComputeNamedPathVmxZeroAuthorityResult Classified(
        SecureComputeNamedPositivePath path,
        bool compatibilityProjectionAllowed,
        string reason) =>
        new(
            SecureComputeNamedPathVmxZeroAuthorityDecision.ZeroAuthorityClassified,
            path,
            compatibilityProjectionAllowed,
            VmxActivationAuthorized: false,
            VmxCapsAuthorityAuthorized: false,
            VmcsStateStoreAuthorized: false,
            ActiveVmcsPointerIdentityAuthorized: false,
            VmreadSecureStateAuthorityAuthorized: false,
            VmwriteSecureStateMutationAuthorized: false,
            VmcsCheckpointAuthorityAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);

    public static SecureComputeNamedPathVmxZeroAuthorityResult Denied(
        SecureComputeNamedPathVmxZeroAuthorityDecision decision,
        SecureComputeNamedPositivePath path,
        string reason) =>
        new(
            decision,
            path,
            CompatibilityProjectionAllowed: false,
            VmxActivationAuthorized: false,
            VmxCapsAuthorityAuthorized: false,
            VmcsStateStoreAuthorized: false,
            ActiveVmcsPointerIdentityAuthorized: false,
            VmreadSecureStateAuthorityAuthorized: false,
            VmwriteSecureStateMutationAuthorized: false,
            VmcsCheckpointAuthorityAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed class SecureComputeNamedPathVmxZeroAuthorityPolicy
{
    public static SecureComputeNamedPathVmxZeroAuthorityPolicy FailClosed { get; } = new();

    public SecureComputeNamedPathVmxZeroAuthorityResult Classify(
        SecureComputeNamedPathVmxZeroAuthorityRequest request)
    {
        SecureComputeNamedPathVmxZeroAuthorityResult denied = DenyAuthorityShortcut(request);
        if (!denied.IsAllowed)
        {
            return denied;
        }

        if (request.RequestsCompatibilityProjection &&
            !request.HasNeutralRuntimeResult)
        {
            return SecureComputeNamedPathVmxZeroAuthorityResult.Denied(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedProjectionWithoutNeutralResult,
                request.Path,
                "Compatibility projection requires a neutral runtime result and remains zero-authority.");
        }

        return SecureComputeNamedPathVmxZeroAuthorityResult.Classified(
            request.Path,
            compatibilityProjectionAllowed: request.RequestsCompatibilityProjection,
            "Named SecureCompute path remains VMX zero-authority.");
    }

    private static SecureComputeNamedPathVmxZeroAuthorityResult DenyAuthorityShortcut(
        SecureComputeNamedPathVmxZeroAuthorityRequest request)
    {
        if (request.AttemptsVmxActivation)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmxActivation,
                request,
                "VMX cannot activate SecureCompute.");
        }

        if (request.AttemptsVmxCapsGrant)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmxCapsAuthority,
                request,
                "Capability projection cannot grant SecureCompute authority.");
        }

        if (request.AttemptsVmcsStateStore)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmcsStateStore,
                request,
                "Compatibility state cannot store SecureCompute state.");
        }

        if (request.AttemptsActiveVmcsPointerIdentity)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedActiveVmcsPointerIdentity,
                request,
                "Active compatibility pointer identity cannot be SecureCompute domain identity.");
        }

        if (request.AttemptsVmreadSecureStateAuthority)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmreadSecureStateAuthority,
                request,
                "Read projection cannot read SecureCompute state as authority.");
        }

        if (request.AttemptsVmwriteSecureStateMutation)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmwriteSecureStateMutation,
                request,
                "Write projection cannot mutate SecureCompute state.");
        }

        if (request.AttemptsVmcsCheckpointAuthority)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmcsCheckpointAuthority,
                request,
                "Compatibility metadata cannot be SecureCompute checkpoint authority.");
        }

        if (request.AttemptsCompletionPublication)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedCompletionPublication,
                request,
                "Compatibility projection cannot publish SecureCompute completion.");
        }

        if (request.AttemptsRetirePublication)
        {
            return Deny(
                SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedRetirePublication,
                request,
                "Compatibility projection cannot publish SecureCompute retire effects.");
        }

        return SecureComputeNamedPathVmxZeroAuthorityResult.Classified(
            request.Path,
            compatibilityProjectionAllowed: false,
            string.Empty);
    }

    private static SecureComputeNamedPathVmxZeroAuthorityResult Deny(
        SecureComputeNamedPathVmxZeroAuthorityDecision decision,
        SecureComputeNamedPathVmxZeroAuthorityRequest request,
        string reason) =>
        SecureComputeNamedPathVmxZeroAuthorityResult.Denied(
            decision,
            request.Path,
            reason);
}
