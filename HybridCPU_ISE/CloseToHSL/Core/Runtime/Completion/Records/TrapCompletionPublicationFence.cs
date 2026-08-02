namespace YAKSys_Hybrid_CPU.Core;

public enum TrapCompletionPublicationDecision : byte
{
    DeniedRuntimeAdmission = 0,
    DeniedNoNeutralTrap = 1,
    DeniedBackendExecution = 2,
    DeniedRetirePublication = 3,
    Allowed = 4,
    CompletionPublishedRetireDenied = 5,
}

public enum TrapCompletionMigrationClass : byte
{
    Unclassified = 0,
    RecomputedAfterRestore = 1,
    GuestArchitecturalState = 2,
    HostOwnedNonMigratable = 3,
}

public readonly record struct TrapCompletionPublicationFenceResult(
    TrapCompletionPublicationDecision Decision,
    NeutralTrapResult TrapResult,
    CompletionRecord Completion,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool CompletionPublicationAllowed =>
        (Decision is TrapCompletionPublicationDecision.Allowed or
            TrapCompletionPublicationDecision.CompletionPublishedRetireDenied) &&
        !Completion.IsEmpty;

    public bool RetirePublicationAllowed =>
        Decision == TrapCompletionPublicationDecision.Allowed &&
        RetirePublicationAuthorized;

    public bool IsDenied => !CompletionPublicationAllowed;

    public bool IsCompletionOnly =>
        Decision == TrapCompletionPublicationDecision.CompletionPublishedRetireDenied &&
        CompletionPublicationAllowed &&
        !RetirePublicationAllowed;

    public bool IsRetireDenied =>
        CompletionPublicationAllowed &&
        !RetirePublicationAllowed;

    public bool DeniesBackendExecution =>
        Decision == TrapCompletionPublicationDecision.DeniedBackendExecution;
}

public sealed class TrapCompletionPublicationFence
{
    public static TrapCompletionPublicationFence Default { get; } = new();

    public TrapCompletionPublicationFenceResult DenyProjectionOnly(
        NeutralTrapResult result,
        bool runtimeAdmissionAllowed) =>
        Evaluate(
            result,
            runtimeAdmissionAllowed,
            completionPublicationAuthorized: false,
            retirePublicationAuthorized: false,
            neutralReasonCode: (uint)result.Kind,
            qualification: 0,
            faultAddress: 0,
            faultAux: 0);

    public TrapCompletionPublicationFenceResult Evaluate(
        NeutralTrapResult result,
        bool runtimeAdmissionAllowed,
        bool completionPublicationAuthorized,
        bool retirePublicationAuthorized,
        uint neutralReasonCode,
        ulong qualification = 0,
        ulong faultAddress = 0,
        ulong faultAux = 0,
        EvidenceVisibilityClass evidenceClass =
            EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
        TrapCompletionMigrationClass migrationClass =
            TrapCompletionMigrationClass.Unclassified)
    {
        if (!runtimeAdmissionAllowed)
        {
            return Denied(
                TrapCompletionPublicationDecision.DeniedRuntimeAdmission,
                result,
                "Trap publication requires runtime boundary admission.");
        }

        if (!result.ShouldTrap)
        {
            return Denied(
                TrapCompletionPublicationDecision.DeniedNoNeutralTrap,
                result,
                "Trap publication requires a neutral trap result.");
        }

        if (!completionPublicationAuthorized)
        {
            return Denied(
                TrapCompletionPublicationDecision.DeniedBackendExecution,
                result,
                "Trap projection is admitted, but backend completion publication is denied.");
        }

        if (!retirePublicationAuthorized)
        {
            return PublishedCompletion(
                result,
                neutralReasonCode,
                qualification,
                faultAddress,
                faultAux,
                retirePublicationAuthorized: false,
                "Trap completion was authorized, but retire publication is denied.");
        }

        if (!CanPublishRetireEvidence(evidenceClass))
        {
            return PublishedCompletion(
                result,
                neutralReasonCode,
                qualification,
                faultAddress,
                faultAux,
                retirePublicationAuthorized: false,
                "Trap completion was published, but retire publication cannot expose host-owned evidence.");
        }

        if (!CanPublishRetireMigrationClass(migrationClass))
        {
            return PublishedCompletion(
                result,
                neutralReasonCode,
                qualification,
                faultAddress,
                faultAux,
                retirePublicationAuthorized: false,
                "Trap completion was published, but retire publication requires an explicit migration class.");
        }

        return PublishedCompletion(
            result,
            neutralReasonCode,
            qualification,
            faultAddress,
            faultAux,
            retirePublicationAuthorized: true,
            reason: string.Empty);
    }

    private static TrapCompletionPublicationFenceResult PublishedCompletion(
        NeutralTrapResult result,
        uint neutralReasonCode,
        ulong qualification,
        ulong faultAddress,
        ulong faultAux,
        bool retirePublicationAuthorized,
        string reason) =>
        new(
            retirePublicationAuthorized
                ? TrapCompletionPublicationDecision.Allowed
                : TrapCompletionPublicationDecision.CompletionPublishedRetireDenied,
            result,
            new CompletionRecord(
                CompletionRecordClass.Trap,
                neutralReasonCode,
                qualification,
                faultAddress,
                faultAux),
            retirePublicationAuthorized,
            reason);

    private static bool CanPublishRetireEvidence(
        EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.GuestArchitecturalState or
            EvidenceVisibilityClass.CompatibilityAlias;

    private static bool CanPublishRetireMigrationClass(
        TrapCompletionMigrationClass migrationClass) =>
        migrationClass is TrapCompletionMigrationClass.RecomputedAfterRestore or
            TrapCompletionMigrationClass.GuestArchitecturalState;

    private static TrapCompletionPublicationFenceResult Denied(
        TrapCompletionPublicationDecision decision,
        NeutralTrapResult result,
        string reason) =>
        new(
            decision,
            result,
            CompletionRecord.None,
            RetirePublicationAuthorized: false,
            reason);
}
