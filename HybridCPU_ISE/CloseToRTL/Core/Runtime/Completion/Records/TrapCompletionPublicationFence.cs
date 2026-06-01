namespace YAKSys_Hybrid_CPU.Core;

public enum TrapCompletionPublicationDecision : byte
{
    DeniedRuntimeAdmission = 0,
    DeniedNoNeutralTrap = 1,
    DeniedBackendExecution = 2,
    DeniedRetirePublication = 3,
    Allowed = 4,
}

public readonly record struct TrapCompletionPublicationFenceResult(
    TrapCompletionPublicationDecision Decision,
    NeutralTrapResult TrapResult,
    CompletionRecord Completion,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool CompletionPublicationAllowed =>
        Decision == TrapCompletionPublicationDecision.Allowed &&
        !Completion.IsEmpty;

    public bool RetirePublicationAllowed =>
        CompletionPublicationAllowed &&
        RetirePublicationAuthorized;

    public bool IsDenied => Decision != TrapCompletionPublicationDecision.Allowed;

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
        ulong faultAux = 0)
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
            return Denied(
                TrapCompletionPublicationDecision.DeniedRetirePublication,
                result,
                "Trap completion was authorized, but retire publication is denied.");
        }

        return new TrapCompletionPublicationFenceResult(
            TrapCompletionPublicationDecision.Allowed,
            result,
            new CompletionRecord(
                CompletionRecordClass.Trap,
                neutralReasonCode,
                qualification,
                faultAddress,
                faultAux),
            RetirePublicationAuthorized: true,
            Reason: string.Empty);
    }

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
