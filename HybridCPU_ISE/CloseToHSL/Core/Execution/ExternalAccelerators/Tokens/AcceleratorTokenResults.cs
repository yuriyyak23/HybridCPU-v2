using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public enum AcceleratorTokenAdmissionResultKind : byte
{
    Accepted = 0,
    NonTrappingReject = 1,
    PreciseFault = 2
}

public enum AcceleratorTokenAdmissionRejectPolicy : byte
{
    NonTrappingReject = 0,
    PreciseFault = 1
}

public enum AcceleratorTokenLookupIntent : byte
{
    Observe = 0,
    Wait = 1,
    Cancel = 2,
    Fence = 3,
    CommitPublication = 4
}

public sealed record AcceleratorTokenTransition
{
    private AcceleratorTokenTransition(
        bool succeeded,
        AcceleratorTokenState fromState,
        AcceleratorTokenState toState,
        AcceleratorTokenFaultCode faultCode,
        string message)
    {
        Succeeded = succeeded;
        FromState = fromState;
        ToState = toState;
        FaultCode = faultCode;
        Message = message;
    }

    public bool Succeeded { get; }

    public bool Rejected => !Succeeded;

    public AcceleratorTokenState FromState { get; }

    public AcceleratorTokenState ToState { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public string Message { get; }

    public static AcceleratorTokenTransition Success(
        AcceleratorTokenState fromState,
        AcceleratorTokenState toState,
        string message) =>
        new(
            succeeded: true,
            fromState,
            toState,
            AcceleratorTokenFaultCode.None,
            message);

    public static AcceleratorTokenTransition Reject(
        AcceleratorTokenState fromState,
        AcceleratorTokenState toState,
        AcceleratorTokenFaultCode faultCode,
        string message)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC token transitions require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorTokenTransition(
            succeeded: false,
            fromState,
            toState,
            faultCode,
            message);
    }
}

public sealed record AcceleratorTokenAdmissionResult
{
    private AcceleratorTokenAdmissionResult(
        AcceleratorTokenAdmissionResultKind kind,
        AcceleratorToken? token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        Kind = kind;
        Token = token;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        Message = message;
    }

    public AcceleratorTokenAdmissionResultKind Kind { get; }

    public bool IsAccepted => Kind == AcceleratorTokenAdmissionResultKind.Accepted && Token is not null;

    public bool IsNonTrappingReject => Kind == AcceleratorTokenAdmissionResultKind.NonTrappingReject;

    public bool RequiresPreciseFault => Kind == AcceleratorTokenAdmissionResultKind.PreciseFault;

    public AcceleratorToken? Token { get; }

    public AcceleratorTokenHandle Handle => Token?.Handle ?? AcceleratorTokenHandle.Invalid;

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public string Message { get; }

    public static AcceleratorTokenAdmissionResult Accepted(AcceleratorToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new AcceleratorTokenAdmissionResult(
            AcceleratorTokenAdmissionResultKind.Accepted,
            token,
            AcceleratorTokenFaultCode.None,
            token.SubmitGuardDecision,
            "ACCEL_SUBMIT accepted by descriptor, capability, and submit guards; token handle is evidence only.");
    }

    public static AcceleratorTokenAdmissionResult Reject(
        AcceleratorTokenFaultCode faultCode,
        string message,
        AcceleratorGuardDecision? guardDecision = null,
        AcceleratorTokenAdmissionRejectPolicy rejectPolicy =
            AcceleratorTokenAdmissionRejectPolicy.NonTrappingReject)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected token admission requires a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorTokenAdmissionResult(
            rejectPolicy == AcceleratorTokenAdmissionRejectPolicy.PreciseFault
                ? AcceleratorTokenAdmissionResultKind.PreciseFault
                : AcceleratorTokenAdmissionResultKind.NonTrappingReject,
            token: null,
            faultCode,
            guardDecision,
            message);
    }
}

public sealed record AcceleratorTokenLookupResult
{
    private AcceleratorTokenLookupResult(
        bool isAllowed,
        AcceleratorTokenLookupIntent intent,
        AcceleratorToken? token,
        AcceleratorTokenStatusWord statusWord,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        bool userVisiblePublicationAllowed,
        bool sideEffectsAllowed,
        string message)
    {
        IsAllowed = isAllowed;
        Intent = intent;
        Token = token;
        StatusWord = statusWord;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        UserVisiblePublicationAllowed = userVisiblePublicationAllowed;
        SideEffectsAllowed = sideEffectsAllowed;
        Message = message;
    }

    public bool IsAllowed { get; }

    public bool IsRejected => !IsAllowed;

    public AcceleratorTokenLookupIntent Intent { get; }

    public AcceleratorToken? Token { get; }

    public AcceleratorTokenStatusWord StatusWord { get; }

    public ulong PackedStatusWord => StatusWord.Pack();

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public bool UserVisiblePublicationAllowed { get; }

    public bool SideEffectsAllowed { get; }

    public string Message { get; }

    public static AcceleratorTokenLookupResult Allowed(
        AcceleratorTokenLookupIntent intent,
        AcceleratorToken token,
        AcceleratorGuardDecision guardDecision,
        string message)
    {
        ArgumentNullException.ThrowIfNull(token);
        return AllowedWithStatus(
            intent,
            token,
            guardDecision,
            AcceleratorTokenStatusWord.FromToken(token),
            AcceleratorTokenFaultCode.None,
            sideEffectsAllowed: intent == AcceleratorTokenLookupIntent.Cancel,
            message);
    }

    public static AcceleratorTokenLookupResult AllowedWithStatus(
        AcceleratorTokenLookupIntent intent,
        AcceleratorToken token,
        AcceleratorGuardDecision guardDecision,
        AcceleratorTokenStatusWord statusWord,
        AcceleratorTokenFaultCode faultCode,
        bool sideEffectsAllowed,
        string message)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new AcceleratorTokenLookupResult(
            isAllowed: true,
            intent,
            token,
            statusWord,
            faultCode,
            guardDecision,
            userVisiblePublicationAllowed: false,
            sideEffectsAllowed,
            message);
    }

    public static AcceleratorTokenLookupResult Rejected(
        AcceleratorTokenLookupIntent intent,
        AcceleratorTokenState visibleState,
        AcceleratorTokenFaultCode faultCode,
        string message,
        AcceleratorToken? token = null,
        AcceleratorGuardDecision? guardDecision = null)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected token lookup requires a fault code.",
                nameof(faultCode));
        }

        AcceleratorTokenStatusFlags flags =
            AcceleratorTokenStatusFlags.GuardRejected |
            AcceleratorTokenStatusFlags.ModelOnly;
        if (IsTerminalState(visibleState))
        {
            flags |= AcceleratorTokenStatusFlags.Terminal;
        }

        if (intent == AcceleratorTokenLookupIntent.CommitPublication)
        {
            flags |= AcceleratorTokenStatusFlags.CommitPublicationForbidden;
        }

        return new AcceleratorTokenLookupResult(
            isAllowed: false,
            intent,
            token,
            new AcceleratorTokenStatusWord(
                visibleState,
                faultCode,
                flags,
                token?.StatusSequence ?? 0),
            faultCode,
            guardDecision,
            userVisiblePublicationAllowed: false,
            sideEffectsAllowed: false,
            message);
    }

    private static bool IsTerminalState(AcceleratorTokenState state) =>
        state is AcceleratorTokenState.Committed
            or AcceleratorTokenState.Faulted
            or AcceleratorTokenState.Canceled
            or AcceleratorTokenState.TimedOut
            or AcceleratorTokenState.Abandoned;
}
