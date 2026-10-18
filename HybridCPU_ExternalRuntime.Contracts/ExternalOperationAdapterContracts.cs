namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// Immutable semantic input supplied by a CPU-side caller to an external-operation
/// provider. It deliberately contains no descriptor bytes, address, transport, or
/// provider identity. The provider owns creation of the exact operation request.
/// </summary>
public sealed record ExternalOperationSemanticRequest
{
    public ExternalOperationSemanticRequest(
        HybridCpuExternalContractVersion contractVersion,
        ExternalRequestCorrelation correlation,
        ExternalEffectClass effectClass,
        ExternalVisibilityRequirement visibilityRequirement,
        ExternalCancellationMode cancellationMode,
        ExternalReplayEffectClass replayEffectClass)
    {
        ExternalOperationContract.ValidateVersion(contractVersion);
        if (correlation.Value == Guid.Empty)
            throw new ArgumentException("Correlation required.", nameof(correlation));
        if (!Enum.IsDefined(effectClass) || !Enum.IsDefined(visibilityRequirement) ||
            !Enum.IsDefined(cancellationMode) || !Enum.IsDefined(replayEffectClass))
            throw new ArgumentOutOfRangeException(nameof(effectClass));

        ContractVersion = contractVersion;
        Correlation = correlation;
        EffectClass = effectClass;
        VisibilityRequirement = visibilityRequirement;
        CancellationMode = cancellationMode;
        ReplayEffectClass = replayEffectClass;
    }

    public HybridCpuExternalContractVersion ContractVersion { get; }
    public ExternalRequestCorrelation Correlation { get; }
    public ExternalEffectClass EffectClass { get; }
    public ExternalVisibilityRequirement VisibilityRequirement { get; }
    public ExternalCancellationMode CancellationMode { get; }
    public ExternalReplayEffectClass ReplayEffectClass { get; }
}

/// <summary>Provider's explicit status for a non-blocking lifecycle observation.</summary>
public enum ExternalOperationProviderPollStatus : byte
{
    Pending = 1,
    Receipt = 2,
    Unavailable = 3,
    Faulted = 4,
    Stale = 5
}

/// <summary>
/// Immutable provider observation. Pending is the only no-receipt status; a null
/// receipt for every other status is invalid. This is a transport-free seam, not
/// provider authentication and not a CPU permission to publish effects.
/// </summary>
public sealed record ExternalOperationProviderPollResult
{
    public ExternalOperationProviderPollResult(
        ExternalOperationProviderPollStatus status,
        ExternalGenerationSet currentGenerations,
        ExternalOperationReceipt? receipt = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ArgumentNullException.ThrowIfNull(currentGenerations);
        if ((status == ExternalOperationProviderPollStatus.Pending) != (receipt is null))
            throw new ArgumentException("Only Pending observations omit a receipt.", nameof(receipt));
        if (receipt is not null && receipt.Request.Generations.ContractVersion != currentGenerations.ContractVersion)
            throw new ArgumentException("Receipt and current generations have incompatible versions.", nameof(receipt));

        Status = status;
        CurrentGenerations = currentGenerations;
        Receipt = receipt;
    }

    public ExternalOperationProviderPollStatus Status { get; }
    public ExternalGenerationSet CurrentGenerations { get; }
    public ExternalOperationReceipt? Receipt { get; }
}

/// <summary>
/// Provider-neutral asynchronous operation seam. Implementations issue their own
/// operation identity and opaque scope, and remain the authority for all receipts.
/// Consumers must validate every result against the exact returned request.
/// </summary>
public interface IExternalOperationProvider
{
    ExternalOperationProviderPollResult Admit(ExternalOperationSemanticRequest request);
    ExternalOperationProviderPollResult Submit(ExternalOperationRequest request);
    ExternalOperationProviderPollResult Poll(ExternalOperationRequest request);
    ExternalOperationProviderPollResult Cancel(ExternalOperationRequest request);
}

/// <summary>
/// Opaque host-established service handle. Its value has no CPU-interpretable
/// structure and descriptor bytes cannot create one.
/// </summary>
public readonly struct ExternalOperationServiceHandle : IEquatable<ExternalOperationServiceHandle>
{
    public ExternalOperationServiceHandle(Guid value) => Value = value;
    public Guid Value { get; }
    public bool IsValid => Value != Guid.Empty;
    public bool Equals(ExternalOperationServiceHandle other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ExternalOperationServiceHandle other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ExternalOperationServiceHandle left, ExternalOperationServiceHandle right) => left.Equals(right);
    public static bool operator !=(ExternalOperationServiceHandle left, ExternalOperationServiceHandle right) => !left.Equals(right);
}

/// <summary>
/// Exact, generation-bound host integration binding for a provider service.
/// The lease remains opaque to the CPU and is revalidated by the provider.
/// </summary>
public sealed record ExternalOperationServiceBinding
{
    public ExternalOperationServiceBinding(
        HybridCpuExternalContractVersion contractVersion,
        ExternalOperationServiceHandle handle,
        ExternalDomainLease scope,
        ExternalGenerationSet generations)
    {
        ExternalOperationContract.ValidateVersion(contractVersion);
        if (!handle.IsValid) throw new ArgumentException("Service handle required.", nameof(handle));
        if (scope.Handle.Value == Guid.Empty || scope.Epoch.Value == 0)
            throw new ArgumentException("Exact lease scope required.", nameof(scope));
        ArgumentNullException.ThrowIfNull(generations);
        if (generations.ContractVersion != contractVersion)
            throw new ArgumentException("Generation version mismatch.", nameof(generations));
        ContractVersion = contractVersion;
        Handle = handle;
        Scope = scope;
        Generations = generations;
    }

    public HybridCpuExternalContractVersion ContractVersion { get; }
    public ExternalOperationServiceHandle Handle { get; }
    public ExternalDomainLease Scope { get; }
    public ExternalGenerationSet Generations { get; }
}

[Flags]
public enum ExternalOperationCapability : ushort
{
    None = 0,
    ExternalAcceleratorService = 1 << 0,
    DmaRead = 1 << 1,
    DmaWrite = 1 << 2,
    CoherentHostDeviceAccess = 1 << 3,
    StagedPublication = 1 << 4,
    DirectOutput = 1 << 5,
    Cancellation = 1 << 6
}

public enum ExternalOperationCapabilityStatus : byte
{
    Available = 1,
    Unavailable = 2,
    Stale = 3,
    ContractVersionMismatch = 4,
    ProviderFault = 5,
    TransportUnavailable = 6
}

/// <summary>Provider-authoritative semantic capability observation.</summary>
public sealed record ExternalOperationCapabilityResult
{
    public ExternalOperationCapabilityResult(
        ExternalOperationCapabilityStatus status,
        ExternalGenerationSet currentGenerations,
        ExternalOperationCapability capabilities = ExternalOperationCapability.None)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ArgumentNullException.ThrowIfNull(currentGenerations);
        const ExternalOperationCapability known =
            ExternalOperationCapability.ExternalAcceleratorService |
            ExternalOperationCapability.DmaRead |
            ExternalOperationCapability.DmaWrite |
            ExternalOperationCapability.CoherentHostDeviceAccess |
            ExternalOperationCapability.StagedPublication |
            ExternalOperationCapability.DirectOutput |
            ExternalOperationCapability.Cancellation;
        if ((capabilities & ~known) != 0) throw new ArgumentOutOfRangeException(nameof(capabilities));
        if (status != ExternalOperationCapabilityStatus.Available && capabilities != ExternalOperationCapability.None)
            throw new ArgumentException("Only an available provider may advertise capabilities.", nameof(capabilities));
        Status = status;
        CurrentGenerations = currentGenerations;
        Capabilities = capabilities;
    }

    public ExternalOperationCapabilityStatus Status { get; }
    public ExternalGenerationSet CurrentGenerations { get; }
    public ExternalOperationCapability Capabilities { get; }
}

/// <summary>Optional provider extension for semantic capability observation.</summary>
public interface IExternalOperationCapabilityProvider
{
    ExternalOperationCapabilityResult QueryCapabilities(ExternalOperationServiceBinding binding);
}

/// <summary>Semantic cancellation support declared by a provider; never inferred from local state.</summary>
public enum ExternalCancellationCapability : byte
{
    NotSupported = 1,
    BeforeSubmitOnly = 2,
    BestEffortAfterSubmit = 3,
    GuaranteedBeforeDeviceStart = 4,
    ProviderDefinedIdempotentRetry = 5
}

/// <summary>
/// Provider-observable invalidation categories. Reconfiguration remains opaque:
/// no topology, fabric, mapping, or hardware identity is exposed.
/// </summary>
public enum ExternalOperationInvalidationReason : byte
{
    DeviceReset = 1,
    GenerationChanged = 2,
    DomainOrMappingChanged = 3,
    ServiceReconfigured = 4,
    TransportSessionLost = 5,
    VisibilityFailed = 6,
    PublicationRejected = 7,
    ReleaseCleanupFailed = 8
}

/// <summary>Provider-issued cancellation outcome; local token changes are not evidence.</summary>
public enum ExternalOperationCancellationOutcome : byte
{
    ConfirmedBeforeSubmit = 1,
    ConfirmedTerminalAfterSubmit = 2,
    Unsupported = 3,
    Ambiguous = 4,
    Stale = 5
}

/// <summary>Exact acknowledgement for cancellation, independent from release.</summary>
public sealed record ExternalOperationCancellationReceipt
{
    public ExternalOperationCancellationReceipt(
        ExternalOperationRequest request,
        ExternalOperationCancellationOutcome outcome,
        ExternalGenerationSet currentGenerations)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(currentGenerations);
        if (currentGenerations.ContractVersion != request.ContractVersion)
            throw new ArgumentException("Generation version mismatch.", nameof(currentGenerations));
        Request = request;
        Outcome = outcome;
        CurrentGenerations = currentGenerations;
    }
    public ExternalOperationRequest Request { get; }
    public ExternalOperationCancellationOutcome Outcome { get; }
    public ExternalGenerationSet CurrentGenerations { get; }
    public bool IsConfirmed => Outcome is ExternalOperationCancellationOutcome.ConfirmedBeforeSubmit or
        ExternalOperationCancellationOutcome.ConfirmedTerminalAfterSubmit;
}

/// <summary>Optional provider extension that alone may acknowledge cancellation.</summary>
public interface IExternalOperationCancellationProvider
{
    ExternalOperationCancellationReceipt RequestCancellation(ExternalOperationRequest request);
}
