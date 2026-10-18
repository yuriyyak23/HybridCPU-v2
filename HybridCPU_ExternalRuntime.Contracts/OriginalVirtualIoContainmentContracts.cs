namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Cleanup of one original private backing admission, including an
/// accepted intent whose allocation/bind reply did not produce a usable receipt.</summary>
public sealed record ExternalOriginalVirtualIoContainmentRequest(
    ExternalOriginalParentDeviceRequest Admission, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalVirtualIoContainmentClosure(
    ExternalOriginalVirtualIoContainmentRequest Request, bool NoFurtherUse,
    HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);
public interface IHybridCpuOriginalVirtualIoContainmentRuntimeV1
{
    ExternalOriginalVirtualIoContainmentClosure? ContainOriginalVirtualIoBacking(ExternalOriginalVirtualIoContainmentRequest request);
    ExternalOriginalVirtualIoContainmentClosure? ResolveOriginalVirtualIoBackingContainment(ExternalOriginalVirtualIoContainmentRequest request);
}
