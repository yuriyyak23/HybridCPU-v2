namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// Provider-local backend capacity held for one original neutral request.
/// This is neither semantic authorization nor a certificate or issue permit.
/// </summary>
public sealed class ExternalSecureExecutionReservation
{
    public ExternalSecureExecutionReservation(ExternalSecureRegisterLocalRequest request, Guid handle)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (handle == Guid.Empty) throw new ArgumentException("Nonempty reservation handle required.", nameof(handle));
        Request = request;
        Handle = handle;
    }
    public ExternalSecureRegisterLocalRequest Request { get; }
    public Guid Handle { get; }
}

/// <summary>Exact original reservation observation; absence never proves release.</summary>
public sealed class ExternalSecureExecutionReservationResult
{
    private ExternalSecureExecutionReservationResult(ExternalSecureRegisterLocalRequest refusedRequest)
    {
        ArgumentNullException.ThrowIfNull(refusedRequest);
        Status = ExternalSecureDomainProviderStatus.Rejected;
        RefusedRequest = refusedRequest;
    }

    /// <summary>
    /// Provider's terminal original decision: no backend hold was ever created
    /// for this exact request, and Reserve will never be executed for it later.
    /// This is not inferred from missing history, current capacity or Rejected.
    /// It cannot close a previously observed reservation or a release obligation.
    /// </summary>
    public static ExternalSecureExecutionReservationResult RefusedWithoutReservation(
        ExternalSecureRegisterLocalRequest request) => new(request);

    public ExternalSecureExecutionReservationResult(ExternalSecureDomainProviderStatus status,
        ExternalSecureExecutionReservation? reservation = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        Reservation = reservation;
    }
    public ExternalSecureDomainProviderStatus Status { get; }
    public ExternalSecureExecutionReservation? Reservation { get; }
    public ExternalSecureRegisterLocalRequest? RefusedRequest { get; }
}

/// <summary>
/// Optional backend reservation lifecycle, separate from read-only binding checks.
/// Reserve holds capacity once and must not execute work. Release closes only that
/// original hold. Queries are read-only original-result lookups, never retries or
/// fresh grants. Receipt from QueryRelease means the exact hold is terminally freed.
/// Implementations retain original objects and exact request/generation correlation.
/// No method receives architectural effect sinks or permission to submit work.
/// </summary>
public interface IExternalSecureExecutionReservationProvider
{
    ExternalSecureExecutionReservationResult Reserve(ExternalSecureRegisterLocalRequest request);
    ExternalSecureExecutionReservationResult QueryReservation(ExternalSecureRegisterLocalRequest request);
    ExternalSecureExecutionReservationResult Release(ExternalSecureExecutionReservation reservation);
    ExternalSecureExecutionReservationResult QueryRelease(ExternalSecureExecutionReservation reservation);
}
