namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Original software device backing under an admitted external root.
/// LoanCorrelation belongs to the caller's native lifetime interlock, not permission.</summary>
public sealed record ExternalOriginalParentDeviceRequest(ExternalDomainBindReceipt ParentAdmission,
    Guid LoanCorrelation, string DeviceIdentity, ExternalChildVirtualIoRights Rights,
    int MaximumTransferBytes, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalParentDeviceReceipt(ExternalOriginalParentDeviceRequest Request,
    ExternalParentDeviceHandle Handle, ExternalParentDeviceEpoch Epoch,
    HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);
public sealed record ExternalOriginalVirtualIoRequest(ExternalChildDomainLease Child,
    ExternalOriginalParentDeviceReceipt Device, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalVirtualIoCloseRequest(ExternalOriginalVirtualIoRequest Admission,
    ExternalChildVirtualIoBindReceipt Binding, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalVirtualIoClosure(ExternalOriginalVirtualIoCloseRequest Request,
    bool NoFurtherUse, HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);
public sealed record ExternalOriginalParentDeviceClosure(ExternalOriginalParentDeviceReceipt Device,
    bool NoFurtherUse, HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);

public interface IHybridCpuOriginalVirtualIoRuntimeV1
{
    ExternalOriginalParentDeviceReceipt? BindOriginalParentDevice(ExternalOriginalParentDeviceRequest request);
    ExternalOriginalParentDeviceReceipt? ResolveOriginalParentDevice(ExternalOriginalParentDeviceRequest request);
    ExternalChildVirtualIoBindReceipt? BindOriginalVirtualIo(ExternalOriginalVirtualIoRequest request);
    ExternalChildVirtualIoBindReceipt? ResolveOriginalVirtualIo(ExternalOriginalVirtualIoRequest request);
    ExternalOriginalVirtualIoClosure? CloseOriginalVirtualIo(ExternalOriginalVirtualIoCloseRequest request);
    ExternalOriginalVirtualIoClosure? ResolveOriginalVirtualIoClosure(ExternalOriginalVirtualIoCloseRequest request);
    ExternalOriginalParentDeviceClosure? CloseOriginalParentDevice(ExternalOriginalParentDeviceReceipt device);
    ExternalOriginalParentDeviceClosure? ResolveOriginalParentDeviceClosure(ExternalOriginalParentDeviceReceipt device);
    bool TransferOriginalVirtualIo(ExternalOriginalVirtualIoRequest original, ulong offset, byte[] bytes, bool write);
}

/// <summary>Continues only an already retained exact original Close admission.
/// Query never performs closure. Missing admission/reply/history is unknown;
/// continuation cannot create an intent or adopt an equal/copied request.</summary>
public interface IHybridCpuOriginalVirtualIoCloseContinuationRuntimeV1
{
    ExternalOriginalVirtualIoClosure? ContinueOriginalVirtualIoClose(ExternalOriginalVirtualIoCloseRequest request);
}

/// <summary>Continues only a retained Close of this exact original device receipt.
/// Existing live IO users remain independent; all original users must be contained
/// before backing is reclaimed. Query cannot create admission or clear backing.</summary>
public interface IHybridCpuOriginalParentDeviceCloseContinuationRuntimeV1
{
    ExternalOriginalParentDeviceClosure? ContinueOriginalParentDeviceClose(ExternalOriginalParentDeviceReceipt device);
}
