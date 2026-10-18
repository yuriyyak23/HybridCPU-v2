namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// CPU-owned guard result supplied for one exact external-operation request.
/// This is not a provider admission receipt and never grants permission by itself.
/// </summary>
public enum ExternalCpuGuardStatus : byte
{
    Allowed = 1,
    Rejected = 2,
    Stale = 3
}

/// <summary>
/// Provider-neutral classification for why the external binding gate refused to submit.
/// It is deliberately independent from CPU scheduling <c>RejectKind</c>.
/// </summary>
public enum ExternalOperationBindingRejectKind : byte
{
    None = 0,
    CpuGuardMissing = 1,
    CpuGuardRejected = 2,
    CpuGuardStale = 3,
    CpuGuardCorrelationMismatch = 4,
    AdmissionMissing = 5,
    AdmissionRejected = 6,
    AdmissionStale = 7,
    AdmissionCorrelationMismatch = 8,
    GenerationMismatch = 9
}

public enum ExternalOperationBindingStatus : byte
{
    SubmitEligible = 1,
    Rejected = 2,
    Stale = 3
}

/// <summary>
/// Immutable CPU-side guard evidence for one request. The issuing CPU runtime must
/// recreate it after owner, domain, descriptor, or replay-policy invalidation.
/// </summary>
public sealed record ExternalOperationCpuGuardReceipt
{
    public ExternalOperationCpuGuardReceipt(
        ExternalOperationRequest request,
        ExternalCpuGuardStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        Request = request;
        Status = status;
    }

    public ExternalOperationRequest Request { get; }

    public ExternalCpuGuardStatus Status { get; }
}

/// <summary>
/// The result of combining independent CPU guard and provider admission gates.
/// A submit-eligible result is structural validation only; the provider remains
/// the sole authority for submission and later lifecycle receipts.
/// </summary>
public sealed record ExternalOperationBindingDecision
{
    private ExternalOperationBindingDecision(
        ExternalOperationBindingStatus status,
        ExternalOperationBindingRejectKind rejectKind)
    {
        Status = status;
        RejectKind = rejectKind;
    }

    public ExternalOperationBindingStatus Status { get; }

    public ExternalOperationBindingRejectKind RejectKind { get; }

    public bool IsSubmitEligible => Status == ExternalOperationBindingStatus.SubmitEligible;

    internal static ExternalOperationBindingDecision Eligible() =>
        new(ExternalOperationBindingStatus.SubmitEligible, ExternalOperationBindingRejectKind.None);

    internal static ExternalOperationBindingDecision Reject(ExternalOperationBindingRejectKind rejectKind) =>
        new(ExternalOperationBindingStatus.Rejected, rejectKind);

    internal static ExternalOperationBindingDecision Stale(ExternalOperationBindingRejectKind rejectKind) =>
        new(ExternalOperationBindingStatus.Stale, rejectKind);
}

/// <summary>
/// Fail-closed combiner for independent CPU guard and provider admission gates.
/// It does not issue receipts, call a provider, or reinterpret CPU legality as provider authority.
/// </summary>
public static class ExternalOperationAdmissionBinding
{
    public static ExternalOperationBindingDecision Evaluate(
        ExternalOperationRequest request,
        ExternalGenerationSet? currentGenerations,
        ExternalOperationCpuGuardReceipt? cpuGuard,
        ExternalOperationAdmissionReceipt? admission)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Generations.Equals(currentGenerations))
            return ExternalOperationBindingDecision.Stale(ExternalOperationBindingRejectKind.GenerationMismatch);

        if (cpuGuard is null)
            return ExternalOperationBindingDecision.Reject(ExternalOperationBindingRejectKind.CpuGuardMissing);

        if (cpuGuard.Request != request)
            return ExternalOperationBindingDecision.Stale(ExternalOperationBindingRejectKind.CpuGuardCorrelationMismatch);

        if (cpuGuard.Status == ExternalCpuGuardStatus.Stale)
            return ExternalOperationBindingDecision.Stale(ExternalOperationBindingRejectKind.CpuGuardStale);

        if (cpuGuard.Status != ExternalCpuGuardStatus.Allowed)
            return ExternalOperationBindingDecision.Reject(ExternalOperationBindingRejectKind.CpuGuardRejected);

        if (admission is null)
            return ExternalOperationBindingDecision.Reject(ExternalOperationBindingRejectKind.AdmissionMissing);

        if (admission.Request != request)
            return ExternalOperationBindingDecision.Stale(ExternalOperationBindingRejectKind.AdmissionCorrelationMismatch);

        if (admission.Outcome == ExternalRuntimeOutcome.Stale)
            return ExternalOperationBindingDecision.Stale(ExternalOperationBindingRejectKind.AdmissionStale);

        return admission.Outcome == ExternalRuntimeOutcome.Succeeded
            ? ExternalOperationBindingDecision.Eligible()
            : ExternalOperationBindingDecision.Reject(ExternalOperationBindingRejectKind.AdmissionRejected);
    }
}
