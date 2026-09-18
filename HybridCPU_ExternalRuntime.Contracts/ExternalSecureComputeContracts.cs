namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Versioned external SecureCompute contract, independent from transport revisions.</summary>
public static class ExternalSecureComputeContract
{
    public static HybridCpuExternalContractVersion Version { get; } = new(1, 0, 0);
    public static void ValidateVersion(HybridCpuExternalContractVersion version)
    {
        if (version != Version) throw new ArgumentOutOfRangeException(nameof(version));
    }
}

public readonly struct ExternalSecureDomainHandle : IEquatable<ExternalSecureDomainHandle>
{
    public ExternalSecureDomainHandle(Guid value) => Value = value;
    public Guid Value { get; }
    public bool Equals(ExternalSecureDomainHandle other) => Value == other.Value;
    public override bool Equals(object? value) => value is ExternalSecureDomainHandle other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
}
public readonly struct ExternalSecureDomainGeneration { public ExternalSecureDomainGeneration(ulong value) => Value = value; public ulong Value { get; } }
public readonly struct ExternalSecureDomainLease { public ExternalSecureDomainLease(ExternalSecureDomainHandle handle, ExternalSecureDomainGeneration generation) { Handle = handle; Generation = generation; } public ExternalSecureDomainHandle Handle { get; } public ExternalSecureDomainGeneration Generation { get; } }
public readonly struct ExternalSecureRegionHandle { public ExternalSecureRegionHandle(Guid value) => Value = value; public Guid Value { get; } }
public readonly struct ExternalSecureRegionGeneration { public ExternalSecureRegionGeneration(ulong value) => Value = value; public ulong Value { get; } }
public readonly struct ExternalSecureEvidenceContextHandle { public ExternalSecureEvidenceContextHandle(Guid value) => Value = value; public Guid Value { get; } }

public enum ExternalSecureAssuranceClass : byte { Basic = 1, Elevated = 2, High = 3 }

/// <summary>Requested and provider-proven secure-domain properties, never CPU authority.</summary>
public sealed record ExternalSecureDomainProperties
{
    public ExternalSecureDomainProperties(
        ExternalSecureAssuranceClass assurance,
        bool requiresEvidence,
        bool requiresPrivateMemory)
    {
        if (!Enum.IsDefined(assurance)) throw new ArgumentOutOfRangeException(nameof(assurance));
        Assurance = assurance; RequiresEvidence = requiresEvidence; RequiresPrivateMemory = requiresPrivateMemory;
    }
    public ExternalSecureAssuranceClass Assurance { get; }
    public bool RequiresEvidence { get; }
    public bool RequiresPrivateMemory { get; }
}

/// <summary>Provider-neutral creation request scoped to an existing ordinary domain.</summary>
public sealed record ExternalSecureDomainCreateRequest
{
    public ExternalSecureDomainCreateRequest(
        ExternalDomainLease parent,
        ExternalSecureEvidenceContextHandle evidenceContext,
        ExternalSecureDomainProperties requiredProperties,
        HybridCpuExternalContractVersion contractVersion)
    {
        ExternalSecureComputeContract.ValidateVersion(contractVersion);
        ArgumentNullException.ThrowIfNull(requiredProperties);
        if (parent.Handle.Value == Guid.Empty || parent.Epoch.Value == 0 || evidenceContext.Value == Guid.Empty)
            throw new ArgumentException("Exact parent lease and opaque evidence context are required.");
        Parent = parent; EvidenceContext = evidenceContext; RequiredProperties = requiredProperties; ContractVersion = contractVersion;
    }
    public ExternalDomainLease Parent { get; }
    public ExternalSecureEvidenceContextHandle EvidenceContext { get; }
    public ExternalSecureDomainProperties RequiredProperties { get; }
    public HybridCpuExternalContractVersion ContractVersion { get; }
}

/// <summary>Opaque provider proof that exact requested properties were established.</summary>
public readonly struct ExternalSecureDomainCreationProof : IEquatable<ExternalSecureDomainCreationProof>
{
    public ExternalSecureDomainCreationProof(Guid value) => Value = value;
    public Guid Value { get; }
    public bool Equals(ExternalSecureDomainCreationProof other) => Value == other.Value;
    public override bool Equals(object? value) => value is ExternalSecureDomainCreationProof other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>Provider-issued creation receipt; a missing proof is not successful creation.</summary>
public sealed record ExternalSecureDomainCreateReceipt
{
    public ExternalSecureDomainCreateReceipt(
        ExternalSecureDomainCreateRequest request,
        ExternalSecureDomainLease domain,
        ExternalSecureDomainProperties provenProperties,
        ExternalSecureDomainCreationProof proof)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provenProperties);
        if (domain.Handle.Value == Guid.Empty || domain.Generation.Value == 0 || proof.Value == Guid.Empty)
            throw new ArgumentException("Exact secure domain generation and non-empty provider proof are required.");
        if (provenProperties != request.RequiredProperties)
            throw new ArgumentException("Provider proof must establish the exact requested secure-domain properties.", nameof(provenProperties));
        Request = request; Domain = domain; ProvenProperties = provenProperties; Proof = proof;
    }
    public ExternalSecureDomainCreateRequest Request { get; }
    public ExternalSecureDomainLease Domain { get; }
    public ExternalSecureDomainProperties ProvenProperties { get; }
    public ExternalSecureDomainCreationProof Proof { get; }
}

/// <summary>Provider-issued terminal secure-domain closure; unavailability is not closure.</summary>
public sealed record ExternalSecureDomainCloseReceipt
{
    public ExternalSecureDomainCloseReceipt(ExternalSecureDomainLease domain, ExternalOperationIdentity operation, bool isTerminal)
    {
        if (domain.Handle.Value == Guid.Empty || domain.Generation.Value == 0 ||
            operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0 || !isTerminal)
            throw new ArgumentException("Exact secure-domain, operation and terminal closure are required.");
        Domain = domain; Operation = operation; IsTerminal = isTerminal;
    }
    public ExternalSecureDomainLease Domain { get; }
    public ExternalOperationIdentity Operation { get; }
    public bool IsTerminal { get; }
}

public enum ExternalSecureDomainProviderStatus : byte
{
    Receipt = 1,
    Unavailable = 2,
    Faulted = 3,
    Stale = 4,
    Rejected = 5,
}

/// <summary>Provider response to secure-domain creation; successful status requires a receipt.</summary>
public sealed record ExternalSecureDomainCreateProviderResult
{
    public ExternalSecureDomainCreateProviderResult(
        ExternalSecureDomainProviderStatus status,
        ExternalSecureDomainCreateReceipt? receipt = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((status == ExternalSecureDomainProviderStatus.Receipt) != (receipt is not null))
            throw new ArgumentException("Receipt status and receipt presence must agree.");
        Status = status; Receipt = receipt;
    }
    public ExternalSecureDomainProviderStatus Status { get; }
    public ExternalSecureDomainCreateReceipt? Receipt { get; }
}

/// <summary>Provider response to terminal secure-domain close; successful status requires a receipt.</summary>
public sealed record ExternalSecureDomainCloseProviderResult
{
    public ExternalSecureDomainCloseProviderResult(
        ExternalSecureDomainProviderStatus status,
        ExternalSecureDomainCloseReceipt? receipt = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((status == ExternalSecureDomainProviderStatus.Receipt) != (receipt is not null))
            throw new ArgumentException("Receipt status and receipt presence must agree.");
        Status = status; Receipt = receipt;
    }
    public ExternalSecureDomainProviderStatus Status { get; }
    public ExternalSecureDomainCloseReceipt? Receipt { get; }
}

/// <summary>
/// Provider-neutral secure-domain seam. Implementations issue all creation and
/// closure facts; HybridCPU can only validate correlation and fail closed.
/// </summary>
public interface IExternalSecureDomainProvider
{
    ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request);
    ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation);
}

public enum ExternalSecureVirtualEventAuthorizationStatus : byte
{
    Receipt = 1,
    Unavailable = 2,
    Faulted = 3,
    Stale = 4,
    Rejected = 5,
}

/// <summary>
/// Provider-issued authorization after completion, visibility, security and
/// generation revalidation, and architectural publication. It is required
/// before a secure external effect can enter the existing child event route.
/// </summary>
public sealed record ExternalSecureVirtualEventAuthorizationReceipt
{
    public ExternalSecureVirtualEventAuthorizationReceipt(
        ExternalSecureGuestRegionBinding binding,
        ExternalOperationPublicationReceipt publication,
        ExternalChildEventRequest childEvent)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(childEvent);
        if (publication.Outcome != ExternalRuntimeOutcome.Succeeded ||
            publication.Request.Operation != childEvent.Operation ||
            childEvent.Sequence == 0 || !Enum.IsDefined(childEvent.Kind))
            throw new ArgumentException("Only an exactly published operation may authorize a valid child event.");
        Binding = binding; Publication = publication; ChildEvent = childEvent;
    }
    public ExternalSecureGuestRegionBinding Binding { get; }
    public ExternalOperationPublicationReceipt Publication { get; }
    public ExternalChildEventRequest ChildEvent { get; }
}

public sealed record ExternalSecureVirtualEventAuthorizationResult
{
    public ExternalSecureVirtualEventAuthorizationResult(
        ExternalSecureVirtualEventAuthorizationStatus status,
        ExternalSecureVirtualEventAuthorizationReceipt? receipt = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((status == ExternalSecureVirtualEventAuthorizationStatus.Receipt) != (receipt is not null))
            throw new ArgumentException("Receipt status and receipt presence must agree.");
        Status = status; Receipt = receipt;
    }
    public ExternalSecureVirtualEventAuthorizationStatus Status { get; }
    public ExternalSecureVirtualEventAuthorizationReceipt? Receipt { get; }
}

/// <summary>Provider seam for exact secure/child generation revalidation before guest events.</summary>
public interface IExternalSecureVirtualEventProvider
{
    ExternalSecureVirtualEventAuthorizationResult AuthorizeChildEvent(
        ExternalSecureGuestRegionBinding binding,
        ExternalOperationPublicationReceipt publication,
        ExternalChildEventRequest childEvent);
}

/// <summary>
/// Exact composition of an existing bounded child virtual-I/O receipt with the
/// secure execution binding. It neither grants nor replaces child I/O rights.
/// </summary>
public sealed record ExternalSecureVirtualIoBinding
{
    public ExternalSecureVirtualIoBinding(
        ExternalSecureExecutionBinding execution,
        ExternalChildVirtualIoBindReceipt virtualIo)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(virtualIo);
        if (virtualIo.ChildHandle != execution.Child.Handle ||
            virtualIo.ChildEpoch != execution.Child.Epoch ||
            virtualIo.Parent != execution.Parent ||
            virtualIo.IoHandle.Value == Guid.Empty || virtualIo.IoEpoch.Value == 0 ||
            virtualIo.Rights == ExternalChildVirtualIoRights.None || virtualIo.MaximumTransferBytes == 0)
            throw new ArgumentException("Secure virtual I/O must reuse an exact bounded child virtual-I/O receipt.", nameof(virtualIo));
        Execution = execution; VirtualIo = virtualIo;
    }
    public ExternalSecureExecutionBinding Execution { get; }
    public ExternalChildVirtualIoBindReceipt VirtualIo { get; }
}

/// <summary>Opaque provider proof of secure-I/O admission for one exact composed binding.</summary>
public readonly struct ExternalSecureIoAdmissionProof : IEquatable<ExternalSecureIoAdmissionProof>
{
    public ExternalSecureIoAdmissionProof(Guid value) => Value = value;
    public Guid Value { get; }
    public bool Equals(ExternalSecureIoAdmissionProof other) => Value == other.Value;
    public override bool Equals(object? value) => value is ExternalSecureIoAdmissionProof other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
}

public sealed record ExternalSecureIoAdmissionReceipt
{
    public ExternalSecureIoAdmissionReceipt(
        ExternalSecureVirtualIoBinding binding,
        ExternalOperationIdentity operation,
        ExternalSecureIoAdmissionProof proof)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0 || proof.Value == Guid.Empty)
            throw new ArgumentException("Exact operation and non-empty secure-I/O admission proof are required.");
        Binding = binding; Operation = operation; Proof = proof;
    }
    public ExternalSecureVirtualIoBinding Binding { get; }
    public ExternalOperationIdentity Operation { get; }
    public ExternalSecureIoAdmissionProof Proof { get; }
}

/// <summary>
/// Secure region binds an already admitted parent mapping exactly; it never
/// supplies an address or creates a second memory authority.
/// </summary>
public sealed record ExternalSecureRegionBinding
{
    public ExternalSecureRegionBinding(
        ExternalSecureDomainLease domain,
        ExternalGuestMappingLease parentMapping,
        ExternalSecureRegionHandle regionHandle,
        ExternalSecureRegionGeneration regionGeneration,
        HybridCpuExternalContractVersion contractVersion)
    {
        ExternalSecureComputeContract.ValidateVersion(contractVersion);
        if (domain.Handle.Value == Guid.Empty || domain.Generation.Value == 0 ||
            parentMapping.Handle.Value == Guid.Empty || parentMapping.Epoch.Value == 0 ||
            regionHandle.Value == Guid.Empty || regionGeneration.Value == 0)
            throw new ArgumentException("Exact secure domain, parent mapping, and region generations are required.");
        Domain = domain; ParentLease = parentMapping; RegionHandle = regionHandle;
        RegionGeneration = regionGeneration; ContractVersion = contractVersion;
    }
    public ExternalSecureDomainLease Domain { get; }
    public ExternalGuestMappingLease ParentLease { get; }
    public ExternalSecureRegionHandle RegionHandle { get; }
    public ExternalSecureRegionGeneration RegionGeneration { get; }
    public HybridCpuExternalContractVersion ContractVersion { get; }
}

/// <summary>Provider-issued terminal closure evidence; unavailable is not closure.</summary>
public sealed record ExternalSecureRegionCloseReceipt
{
    public ExternalSecureRegionCloseReceipt(ExternalSecureRegionBinding binding, ExternalOperationIdentity operation, bool isTerminal)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0)
            throw new ArgumentException("Exact operation required.", nameof(operation));
        if (!isTerminal) throw new ArgumentException("Secure region closure must be terminal.", nameof(isTerminal));
        Binding = binding; Operation = operation; IsTerminal = isTerminal;
    }
    public ExternalSecureRegionBinding Binding { get; }
    public ExternalOperationIdentity Operation { get; }
    public bool IsTerminal { get; }
}

/// <summary>
/// Exact child/secure/parent composition receipt. It is not an authority root:
/// every component remains independently revalidated by its owner.
/// </summary>
public sealed record ExternalSecureExecutionBinding
{
    public ExternalSecureExecutionBinding(
        ExternalChildDomainLease child,
        ExternalSecureDomainLease secureDomain,
        ExternalDomainLease parent,
        ulong policyGeneration,
        HybridCpuExternalContractVersion contractVersion)
    {
        ExternalSecureComputeContract.ValidateVersion(contractVersion);
        if (child.Handle.Value == Guid.Empty || child.Epoch.Value == 0 ||
            secureDomain.Handle.Value == Guid.Empty || secureDomain.Generation.Value == 0 ||
            parent.Handle.Value == Guid.Empty || parent.Epoch.Value == 0 || policyGeneration == 0)
            throw new ArgumentException("Exact child, secure, parent and policy generations are required.");
        if (child.Parent != parent)
            throw new ArgumentException("Child must compose with its exact parent lease.", nameof(parent));
        Child = child; SecureDomain = secureDomain; Parent = parent;
        PolicyGeneration = policyGeneration; ContractVersion = contractVersion;
    }
    public ExternalChildDomainLease Child { get; }
    public ExternalSecureDomainLease SecureDomain { get; }
    public ExternalDomainLease Parent { get; }
    public ulong PolicyGeneration { get; }
    public HybridCpuExternalContractVersion ContractVersion { get; }
}

/// <summary>Secure guest-region composition over the same already admitted guest lease.</summary>
public sealed record ExternalSecureGuestRegionBinding
{
    public ExternalSecureGuestRegionBinding(
        ExternalSecureExecutionBinding execution,
        ExternalSecureRegionBinding region)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(region);
        if (!region.Domain.Equals(execution.SecureDomain) ||
            region.ParentLease.Child != execution.Child ||
            region.ParentLease.Child.Parent != execution.Parent)
            throw new ArgumentException("Secure guest region must reuse the exact composed child and parent lease lineage.", nameof(region));
        Execution = execution; Region = region;
    }
    public ExternalSecureExecutionBinding Execution { get; }
    public ExternalSecureRegionBinding Region { get; }
}
