using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public enum AcceleratorFaultPublicationDisposition : byte
{
    Published = 0,
    BlockedInvalidOwner = 1,
    DiagnosticOnly = 2,
    Rejected = 3
}

public sealed record AcceleratorFaultPublicationResult
{
    private AcceleratorFaultPublicationResult(
        AcceleratorFaultPublicationDisposition disposition,
        AcceleratorToken? token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorTokenStatusWord statusWord,
        AcceleratorGuardDecision? guardDecision,
        bool hasUserVisibleStatusWord,
        bool userVisiblePublished,
        bool privilegedDiagnosticRecorded,
        string message)
    {
        Disposition = disposition;
        Token = token;
        FaultCode = faultCode;
        StatusWord = statusWord;
        GuardDecision = guardDecision;
        HasUserVisibleStatusWord = hasUserVisibleStatusWord;
        UserVisiblePublished = userVisiblePublished;
        PrivilegedDiagnosticRecorded = privilegedDiagnosticRecorded;
        Message = message;
    }

    public AcceleratorFaultPublicationDisposition Disposition { get; }

    public bool IsPublished => Disposition == AcceleratorFaultPublicationDisposition.Published;

    public bool IsRejected => Disposition == AcceleratorFaultPublicationDisposition.Rejected;

    public AcceleratorToken? Token { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorTokenStatusWord StatusWord { get; }

    public ulong PackedStatusWord => HasUserVisibleStatusWord ? StatusWord.Pack() : 0;

    public AcceleratorGuardDecision? GuardDecision { get; }

    public bool HasUserVisibleStatusWord { get; }

    public bool UserVisiblePublished { get; }

    public bool PrivilegedDiagnosticRecorded { get; }

    public bool CanAuthorizeCommit => false;

    public bool CanAuthorizeCancel => false;

    public bool CanAuthorizeFence => false;

    public string Message { get; }

    public static AcceleratorFaultPublicationResult Published(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision guardDecision,
        string message)
    {
        ArgumentNullException.ThrowIfNull(token);
        AcceleratorTokenStatusWord statusWord =
            AcceleratorTokenStatusWord.FromToken(token);
        AcceleratorTokenFaultCode effectiveFaultCode =
            statusWord.FaultCode == AcceleratorTokenFaultCode.None
                ? faultCode
                : statusWord.FaultCode;
        ThrowIfFaultCodeNone(
            effectiveFaultCode,
            nameof(faultCode));
        return new AcceleratorFaultPublicationResult(
            AcceleratorFaultPublicationDisposition.Published,
            token,
            effectiveFaultCode,
            statusWord,
            guardDecision,
            hasUserVisibleStatusWord: true,
            userVisiblePublished: true,
            privilegedDiagnosticRecorded: false,
            message);
    }

    public static AcceleratorFaultPublicationResult Blocked(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision guardDecision,
        bool privilegedDiagnosticRecorded,
        string message)
    {
        ArgumentNullException.ThrowIfNull(token);
        ThrowIfFaultCodeNone(
            faultCode,
            nameof(faultCode));
        return new AcceleratorFaultPublicationResult(
            privilegedDiagnosticRecorded
                ? AcceleratorFaultPublicationDisposition.DiagnosticOnly
                : AcceleratorFaultPublicationDisposition.BlockedInvalidOwner,
            token,
            faultCode,
            default,
            guardDecision,
            hasUserVisibleStatusWord: false,
            userVisiblePublished: false,
            privilegedDiagnosticRecorded,
            message);
    }

    public static AcceleratorFaultPublicationResult Rejected(
        AcceleratorToken? token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        ThrowIfFaultCodeNone(
            faultCode,
            nameof(faultCode));
        return new AcceleratorFaultPublicationResult(
            AcceleratorFaultPublicationDisposition.Rejected,
            token,
            faultCode,
            default,
            guardDecision,
            hasUserVisibleStatusWord: false,
            userVisiblePublished: false,
            privilegedDiagnosticRecorded: false,
            message);
    }

    private static void ThrowIfFaultCodeNone(
        AcceleratorTokenFaultCode faultCode,
        string paramName)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "L7-SDC fault publication result requires a non-None fault code.",
                paramName);
        }
    }
}

public sealed class AcceleratorExceptionPublication
{
    public AcceleratorFaultPublicationResult TryPublish(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardEvidence? currentGuardEvidence,
        bool recordPrivilegedDiagnostic = false)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            return AcceleratorFaultPublicationResult.Rejected(
                token,
                AcceleratorTokenFaultCode.FaultPublicationRejected,
                guardDecision: null,
                "L7-SDC fault publication requires a non-None token fault code.");
        }

        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            return AcceleratorFaultPublicationResult.Blocked(
                token,
                AcceleratorTokenStore.MapGuardFault(tokenGuardDecision.Fault),
                tokenGuardDecision,
                recordPrivilegedDiagnostic,
                "L7-SDC fault publication blocked user-visible status because token owner/domain or epochs are no longer valid. " +
                tokenGuardDecision.Message);
        }

        AcceleratorGuardDecision publicationGuardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeExceptionPublication(
                token.Descriptor,
                currentGuardEvidence);
        if (!publicationGuardDecision.IsAllowed)
        {
            return AcceleratorFaultPublicationResult.Blocked(
                token,
                AcceleratorTokenStore.MapGuardFault(publicationGuardDecision.Fault),
                publicationGuardDecision,
                recordPrivilegedDiagnostic,
                "L7-SDC fault publication blocked user-visible status because exception publication authority was rejected. " +
                publicationGuardDecision.Message);
        }

        if (tokenGuardDecision.DescriptorOwnerBinding is null ||
            publicationGuardDecision.DescriptorOwnerBinding is null ||
            !tokenGuardDecision.DescriptorOwnerBinding.Equals(publicationGuardDecision.DescriptorOwnerBinding))
        {
            return AcceleratorFaultPublicationResult.Rejected(
                token,
                AcceleratorTokenFaultCode.FaultPublicationRejected,
                publicationGuardDecision,
                "L7-SDC fault publication token guard and exception guard bind different owners.");
        }

        if (token.State == AcceleratorTokenState.Faulted)
        {
            return AcceleratorFaultPublicationResult.Published(
                token,
                token.FaultCode == AcceleratorTokenFaultCode.None
                    ? faultCode
                    : token.FaultCode,
                publicationGuardDecision,
                "L7-SDC fault status was published only after fresh exception-publication guard authority.");
        }

        if (token.IsTerminal)
        {
            return AcceleratorFaultPublicationResult.Rejected(
                token,
                AcceleratorTokenFaultCode.TerminalState,
                publicationGuardDecision,
                $"L7-SDC fault publication cannot rewrite terminal token state {token.State}.");
        }

        AcceleratorTokenTransition fault =
            token.MarkFaulted(
                faultCode,
                currentGuardEvidence!);
        return fault.Succeeded
            ? AcceleratorFaultPublicationResult.Published(
                token,
                faultCode,
                publicationGuardDecision,
                "L7-SDC fault was marked and published only after fresh owner/domain, mapping, and exception-publication guard authority.")
            : AcceleratorFaultPublicationResult.Rejected(
                token,
                fault.FaultCode,
                publicationGuardDecision,
                fault.Message);
    }
}
