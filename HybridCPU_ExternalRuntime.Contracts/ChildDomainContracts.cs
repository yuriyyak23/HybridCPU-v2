namespace HybridCPU.ExternalRuntime.Contracts;

[Flags]
public enum ExternalChildAuthority : uint
{
    None = 0,
    Execute = 1 << 0,
    GuestMemory = 1 << 1,
    EventInjection = 1 << 2,
    TrapDelivery = 1 << 3,
    VirtualIo = 1 << 4,
}

public readonly record struct ExternalChildDomainHandle(Guid Value);
public readonly record struct ExternalChildDomainEpoch(ulong Value);
public readonly record struct ExternalChildDomainLease(
    ExternalChildDomainHandle Handle,
    ExternalChildDomainEpoch Epoch,
    ExternalDomainLease Parent);

public enum ExternalChildDomainState : byte
{
    Ready = 0,
    Running = 1,
    Parked = 2,
    Closed = 3,
}

public enum ExternalChildDomainTransition : byte
{
    Start = 0,
    Park = 1,
    Resume = 2,
}

public sealed record ExternalChildDomainCreateRequest(
    Guid RequestId,
    ExternalChildAuthority RequestedAuthority,
    ulong GuestMemoryLimitBytes,
    ExternalOperationIdentity Operation);

public sealed record ExternalChildDomainCreateReceipt(
    ExternalChildDomainLease Lease,
    ExternalChildAuthority GrantedAuthority,
    ulong GuestMemoryLimitBytes,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalChildDomainState State);

public sealed record ExternalChildExecutableImageRequest(
    byte[] PackageBytes,
    int MaximumPipelineCycles,
    ExternalOperationIdentity Operation);

/// <summary>Immutable image-admission evidence. It is neither execution nor child authority.</summary>
public sealed record ExternalChildExecutableImageReceipt(
    ExternalChildDomainHandle ChildHandle,
    ExternalChildDomainEpoch ChildEpoch,
    ExternalDomainLease Parent,
    string PackageSha256,
    int MaximumPipelineCycles,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public sealed record ExternalChildDomainTransitionReceipt(
    ExternalChildDomainLease Lease,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalChildDomainTransition Transition,
    ExternalChildDomainState ResultingState);

public readonly record struct ExternalGuestMappingHandle(Guid Value);
public readonly record struct ExternalGuestMappingEpoch(ulong Value);
public readonly record struct ExternalGuestMappingLease(
    ExternalGuestMappingHandle Handle,
    ExternalGuestMappingEpoch Epoch,
    ExternalChildDomainLease Child);

public sealed record ExternalGuestMemoryMapRequest(
    ulong ChildOffsetBytes,
    ulong LengthBytes,
    ExternalOperationIdentity Operation);

public sealed record ExternalGuestMemoryMapReceipt(
    ExternalGuestMappingLease Mapping,
    ulong ChildOffsetBytes,
    ulong LengthBytes,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public sealed record ExternalGuestMemoryUnmapReceipt(
    ExternalGuestMappingLease Mapping,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    bool IsTerminal);

public enum ExternalChildEventKind : byte
{
    Wake = 0,
    Timer = 1,
    External = 2,
}

public sealed record ExternalChildEventRequest(
    ExternalChildEventKind Kind,
    ulong Sequence,
    ExternalOperationIdentity Operation);

public sealed record ExternalChildEventReceipt(
    ExternalChildDomainLease Lease,
    ExternalChildEventKind Kind,
    ulong Sequence,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public enum ExternalChildTrapKind : byte
{
    MemoryFault = 0,
    IllegalInstruction = 1,
    Hypercall = 2,
    ExternalEvent = 3,
    Timer = 4,
    Preemption = 5,
    DeviceOrIoFault = 6,
}

public enum ExternalChildTrapDisposition : byte
{
    Parked = 0,
    ResumePermitted = 1,
    TerminationRequired = 2,
}

public sealed record ExternalChildTrapRequest(
    ExternalChildTrapKind Kind,
    ulong Sequence,
    ExternalOperationIdentity Operation);

public sealed record ExternalChildTrapReceipt(
    ExternalChildDomainLease Lease,
    ExternalChildTrapKind Kind,
    ulong Sequence,
    ExternalChildTrapDisposition Disposition,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public sealed record ExternalChildDomainCloseReceipt(
    ExternalChildDomainLease Lease,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalChildDomainState ResultingState,
    bool IsTerminal,
    uint ClosedGuestMappings);

public sealed record ExternalChildResult<TReceipt>(
    ExternalRuntimeOutcome Outcome,
    TReceipt? Receipt,
    string Reason) where TReceipt : class;

public interface IHybridCpuChildDomainRuntimeV1
{
    ExternalChildResult<ExternalChildDomainCreateReceipt> CreateChildDomain(
        ExternalDomainLease parent,
        ExternalChildDomainCreateRequest request);
    ExternalChildResult<ExternalChildDomainTransitionReceipt> TransitionChildDomain(
        ExternalChildDomainLease child,
        ExternalChildDomainTransition transition,
        ExternalOperationIdentity operation);
    ExternalChildResult<ExternalGuestMemoryMapReceipt> MapChildGuestMemory(
        ExternalChildDomainLease child,
        ExternalGuestMemoryMapRequest request);
    ExternalChildResult<ExternalGuestMemoryUnmapReceipt> UnmapChildGuestMemory(
        ExternalGuestMappingLease mapping,
        ExternalOperationIdentity operation);
    ExternalChildResult<ExternalChildEventReceipt> InjectChildEvent(
        ExternalChildDomainLease child,
        ExternalChildEventRequest request);
    ExternalChildResult<ExternalChildTrapReceipt> ReportChildTrap(
        ExternalChildDomainLease child,
        ExternalChildTrapRequest request);
    ExternalChildResult<ExternalChildDomainCloseReceipt> CloseChildDomain(
        ExternalChildDomainLease child,
        ExternalOperationIdentity operation);
}

public interface IHybridCpuChildDomainRuntimeV2 : IHybridCpuChildDomainRuntimeV1
{
    ExternalChildResult<ExternalChildExecutableImageReceipt> LoadChildExecutableImage(
        ExternalChildDomainLease child,
        ExternalChildExecutableImageRequest request);
}

public readonly record struct ExternalChildArtifactHandle(Guid Value);
public readonly record struct ExternalChildArtifactEpoch(ulong Value);
public readonly record struct ExternalChildExecutionGeneration(ulong Value);

public sealed record ExternalChildArtifactBindRequest(
    ExternalGuestMappingLease GuestMapping,
    byte[] PackageBytes,
    int MaximumPipelineCycles,
    ExternalOperationIdentity Operation);

/// <summary>Artifact admission evidence only. It is not child, mapping, or completion authority.</summary>
public sealed record ExternalChildArtifactBindReceipt(
    ExternalChildArtifactHandle ArtifactHandle,
    ExternalChildArtifactEpoch ArtifactEpoch,
    ExternalChildDomainHandle ChildHandle,
    ExternalChildDomainEpoch ChildEpoch,
    ExternalGuestMappingHandle MappingHandle,
    ExternalGuestMappingEpoch MappingEpoch,
    ExternalDomainLease Parent,
    string PackageSha256,
    int MaximumPipelineCycles,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public sealed record ExternalChildExecutionStartRequest(
    ExternalChildArtifactHandle ArtifactHandle,
    ExternalChildArtifactEpoch ArtifactEpoch,
    ExternalOperationIdentity Operation);

/// <summary>Exact terminal retired-work evidence. It conveys no capability or reclaim authority.</summary>
public sealed record ExternalChildExecutionReceipt(
    ExternalChildArtifactHandle ArtifactHandle,
    ExternalChildArtifactEpoch ArtifactEpoch,
    ExternalChildDomainHandle ChildHandle,
    ExternalChildDomainEpoch ChildEpoch,
    ExternalGuestMappingHandle MappingHandle,
    ExternalGuestMappingEpoch MappingEpoch,
    ExternalDomainLease Parent,
    string PackageSha256,
    ExternalChildExecutionGeneration ExecutionGeneration,
    int RetiredPipelineCycles,
    ulong LastRetireSequence,
    ulong LastRetiredBundleOffset,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    bool IsTerminal);

[Flags]
public enum ExternalChildVirtualIoRights : uint
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Configure = 1 << 2,
}

public readonly record struct ExternalParentDeviceHandle(Guid Value);
public readonly record struct ExternalParentDeviceEpoch(ulong Value);
public readonly record struct ExternalChildVirtualIoHandle(Guid Value);
public readonly record struct ExternalChildVirtualIoEpoch(ulong Value);

public sealed record ExternalChildVirtualIoBindRequest(
    ExternalParentDeviceHandle ParentDeviceHandle,
    ExternalParentDeviceEpoch ParentDeviceEpoch,
    ExternalChildVirtualIoRights Rights,
    ulong MaximumTransferBytes,
    ExternalOperationIdentity Operation);

public sealed record ExternalChildVirtualIoBindReceipt(
    ExternalChildVirtualIoHandle IoHandle,
    ExternalChildVirtualIoEpoch IoEpoch,
    ExternalChildDomainHandle ChildHandle,
    ExternalChildDomainEpoch ChildEpoch,
    ExternalDomainLease Parent,
    ExternalParentDeviceHandle ParentDeviceHandle,
    ExternalParentDeviceEpoch ParentDeviceEpoch,
    ExternalChildVirtualIoRights Rights,
    ulong MaximumTransferBytes,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration);

public sealed record ExternalChildVirtualIoCloseReceipt(
    ExternalChildVirtualIoHandle IoHandle,
    ExternalChildVirtualIoEpoch IoEpoch,
    ExternalChildDomainHandle ChildHandle,
    ExternalChildDomainEpoch ChildEpoch,
    ExternalDomainLease Parent,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    bool IsTerminal);

public interface IHybridCpuChildDomainRuntimeV3 : IHybridCpuChildDomainRuntimeV2
{
    ExternalChildResult<ExternalChildArtifactBindReceipt> BindChildExecutableArtifact(
        ExternalChildDomainLease child,
        ExternalChildArtifactBindRequest request);
    ExternalChildResult<ExternalChildExecutionReceipt> StartChildExecution(
        ExternalChildDomainLease child,
        ExternalChildExecutionStartRequest request);
    ExternalChildResult<ExternalChildVirtualIoBindReceipt> BindChildVirtualIo(
        ExternalChildDomainLease child,
        ExternalChildVirtualIoBindRequest request);
    ExternalChildResult<ExternalChildVirtualIoCloseReceipt> CloseChildVirtualIo(
        ExternalChildDomainLease child,
        ExternalChildVirtualIoHandle ioHandle,
        ExternalChildVirtualIoEpoch ioEpoch,
        ExternalOperationIdentity operation);
}
