namespace HybridCPU.ExternalRuntime.Contracts;
public sealed record ExternalOriginalChildCreateRequest(ExternalDomainBindReceipt Admission, ExternalChildDomainCreateRequest CreationRequest)
{
    public bool AllowNestedCreation { get; init; }
}
public sealed record ExternalOriginalChildCreateObservation(ExternalOriginalChildCreateRequest Request, ExternalChildResult<ExternalChildDomainCreateReceipt> Raw, bool NoEffect)
{
    public bool NestedCreationAllowed { get; init; }
}
public interface IHybridCpuOriginalChildCreateRuntimeV1
{
    ExternalOriginalChildCreateObservation? CreateOriginalChild(ExternalOriginalChildCreateRequest request);
    ExternalOriginalChildCreateObservation? ResolveOriginalChildCreate(ExternalOriginalChildCreateRequest originalRequest);
}
