namespace YAKSys_Hybrid_CPU.Core;

public enum PrivilegedExecutionStateProjectionDecision : byte
{
    AllowedReadOnlyProjection = 0,
    DeniedNoReadOnlySource = 1,
    DeniedOwnerAdmission = 2,
    DeniedNoSecureVisibility = 3,
    DeniedNoMigrationClass = 4,
    DeniedNoConformanceProof = 5,
    DeniedUnsupportedRegister = 6,
}

public readonly record struct PrivilegedExecutionStateProjectionRequest(
    PrivilegedControlRegisterKind Register,
    PrivilegedExecutionStateDescriptor? Descriptor,
    ulong RuntimeDomainTag,
    ulong RuntimeAddressSpaceTag,
    PrivilegedExecutionStateEpoch CurrentEpoch,
    bool SecureVisibilityAllowed,
    bool MigrationClassified,
    bool ConformanceProven);

public readonly record struct PrivilegedExecutionStateProjectionResult(
    PrivilegedExecutionStateProjectionDecision Decision,
    PrivilegedExecutionStateOwnerResult OwnerAdmission,
    bool ValueAvailable,
    ulong Value,
    bool BackendSuccessAuthorized,
    bool MutationAuthorized,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == PrivilegedExecutionStateProjectionDecision.AllowedReadOnlyProjection &&
        OwnerAdmission.IsAllowed &&
        ValueAvailable &&
        !BackendSuccessAuthorized &&
        !MutationAuthorized &&
        !CompletionPublicationAuthorized &&
        !RetirePublicationAuthorized;

    public static PrivilegedExecutionStateProjectionResult Denied(
        PrivilegedExecutionStateProjectionDecision decision,
        PrivilegedExecutionStateOwnerResult ownerAdmission,
        string reason) =>
        new(
            decision,
            ownerAdmission,
            ValueAvailable: false,
            Value: 0,
            BackendSuccessAuthorized: false,
            MutationAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed class PrivilegedExecutionStateProjectionService
{
    private readonly PrivilegedExecutionStateOwnerPolicy _ownerPolicy;
    private readonly SecureComputeVmReadVisibilityPolicy _visibilityPolicy;

    public PrivilegedExecutionStateProjectionService()
        : this(
            PrivilegedExecutionStateOwnerPolicy.Default,
            new SecureComputeVmReadVisibilityPolicy())
    {
    }

    public PrivilegedExecutionStateProjectionService(
        PrivilegedExecutionStateOwnerPolicy ownerPolicy,
        SecureComputeVmReadVisibilityPolicy visibilityPolicy)
    {
        _ownerPolicy = ownerPolicy;
        _visibilityPolicy = visibilityPolicy;
    }

    public PrivilegedExecutionStateProjectionResult Project(
        PrivilegedExecutionStateProjectionRequest request)
    {
        if (request.Descriptor is null)
        {
            return Deny(
                PrivilegedExecutionStateProjectionDecision.DeniedNoReadOnlySource,
                default,
                "Guest control-register projection requires a neutral privileged execution-state descriptor value source.");
        }

        if (request.Register is not (
            PrivilegedControlRegisterKind.GuestCr0 or
            PrivilegedControlRegisterKind.GuestCr4))
        {
            return Deny(
                PrivilegedExecutionStateProjectionDecision.DeniedUnsupportedRegister,
                default,
                "Only GuestCr0 and GuestCr4 are admitted by the privileged execution-state projection contract.");
        }

        PrivilegedExecutionStateOwnerResult ownerAdmission = _ownerPolicy.Admit(
            new PrivilegedExecutionStateOwnerRequest(
                request.Descriptor,
                request.RuntimeDomainTag,
                request.RuntimeAddressSpaceTag,
                request.CurrentEpoch));

        if (!ownerAdmission.IsAllowed)
        {
            PrivilegedExecutionStateProjectionDecision decision =
                ownerAdmission.Decision == PrivilegedExecutionStateOwnerDecision.DeniedMigrationClass
                    ? PrivilegedExecutionStateProjectionDecision.DeniedNoMigrationClass
                    : PrivilegedExecutionStateProjectionDecision.DeniedOwnerAdmission;

            return Deny(decision, ownerAdmission, ownerAdmission.Reason);
        }

        SecureComputeVmReadVisibilityDecision visibility = _visibilityPolicy.Validate(
            hasNeutralOwner: true,
            hasReadOnlySource: true,
            hasSecureVisibility: request.SecureVisibilityAllowed,
            hasMigrationClassification: request.MigrationClassified,
            hasConformanceProof: request.ConformanceProven);

        if (visibility != SecureComputeVmReadVisibilityDecision.AllowedReadOnlyProjection)
        {
            return Deny(
                MapDeniedVisibility(visibility),
                ownerAdmission,
                $"Guest control-register projection denied by {visibility}.");
        }

        PrivilegedExecutionStateDescriptor descriptor = request.Descriptor.Value;
        ulong value = request.Register == PrivilegedControlRegisterKind.GuestCr0
            ? descriptor.GuestCr0.Value
            : descriptor.GuestCr4.Value;

        return new PrivilegedExecutionStateProjectionResult(
            PrivilegedExecutionStateProjectionDecision.AllowedReadOnlyProjection,
            ownerAdmission,
            ValueAvailable: true,
            value,
            BackendSuccessAuthorized: false,
            MutationAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            Reason: "Neutral privileged execution-state value is admitted for read-only compatibility projection only.");
    }

    private static PrivilegedExecutionStateProjectionDecision MapDeniedVisibility(
        SecureComputeVmReadVisibilityDecision decision) =>
        decision switch
        {
            SecureComputeVmReadVisibilityDecision.DeniedNoSecureVisibility =>
                PrivilegedExecutionStateProjectionDecision.DeniedNoSecureVisibility,
            SecureComputeVmReadVisibilityDecision.DeniedNoMigrationClass =>
                PrivilegedExecutionStateProjectionDecision.DeniedNoMigrationClass,
            SecureComputeVmReadVisibilityDecision.DeniedNoConformanceProof =>
                PrivilegedExecutionStateProjectionDecision.DeniedNoConformanceProof,
            SecureComputeVmReadVisibilityDecision.DeniedNoReadOnlySource =>
                PrivilegedExecutionStateProjectionDecision.DeniedNoReadOnlySource,
            _ => PrivilegedExecutionStateProjectionDecision.DeniedOwnerAdmission,
        };

    private static PrivilegedExecutionStateProjectionResult Deny(
        PrivilegedExecutionStateProjectionDecision decision,
        PrivilegedExecutionStateOwnerResult ownerAdmission,
        string reason) =>
        PrivilegedExecutionStateProjectionResult.Denied(
            decision,
            ownerAdmission,
            reason);
}
