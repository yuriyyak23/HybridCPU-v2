namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Names the original admitted child lifetime; equal receipt copies cannot transfer it.</summary>
public sealed record ExternalOriginalChildCloseRequest(ExternalChildDomainCreateReceipt Admission, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalChildCloseObservation(ExternalOriginalChildCloseRequest Request,
    ExternalChildResult<ExternalChildDomainCloseReceipt> Raw, bool NoEffect, bool NoFurtherUse);
public interface IHybridCpuOriginalChildCloseRuntimeV1
{
    ExternalOriginalChildCloseObservation? CloseOriginalChild(ExternalOriginalChildCloseRequest request);
    ExternalOriginalChildCloseObservation? ResolveOriginalChildClose(ExternalOriginalChildCloseRequest originalRequest);
}
