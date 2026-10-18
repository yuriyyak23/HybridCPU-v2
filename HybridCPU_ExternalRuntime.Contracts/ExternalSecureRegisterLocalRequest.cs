namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// Neutral bounded register-local addition intent, not a machine opcode or an
/// execution permission. The host must authorize the existing binding separately.
/// This request cannot select registers, placement, memory or publication effects.
/// </summary>
public sealed class ExternalSecureRegisterLocalRequest
{
    public ExternalSecureRegisterLocalRequest(ExternalSecureExecutionBinding binding,
        ExternalOperationIdentity operation, ulong left, ulong right, uint schemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0)
            throw new ArgumentException("Exact external operation required.", nameof(operation));
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Binding = binding;
        Operation = operation;
        Left = left;
        Right = right;
        SchemaVersion = schemaVersion;
    }

    public ExternalSecureExecutionBinding Binding { get; }
    public ExternalOperationIdentity Operation { get; }
    public ulong Left { get; }
    public ulong Right { get; }
    public uint SchemaVersion { get; }
}

/// <summary>
/// Read-only provider revalidation of the exact child/parent/secure/policy
/// generations and operation. A match is not a reservation or execution permit.
/// Implementations must not submit work or create effects from this query.
/// </summary>
public interface IExternalSecureExecutionBindingProvider
{
    ExternalSecureExecutionBindingObservation Revalidate(ExternalSecureRegisterLocalRequest request);
}

public sealed class ExternalSecureExecutionBindingObservation
{
    public ExternalSecureExecutionBindingObservation(ExternalSecureRegisterLocalRequest request,
        ExternalSecureDomainProviderStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Request = request;
        Status = status;
    }
    public ExternalSecureRegisterLocalRequest Request { get; }
    public ExternalSecureDomainProviderStatus Status { get; }
}
