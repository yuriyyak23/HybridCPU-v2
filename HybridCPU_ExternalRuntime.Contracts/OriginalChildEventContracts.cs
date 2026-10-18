namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Original external software mailbox admission; does not change machine interrupt semantics.</summary>
public sealed record ExternalOriginalChildEventRequest(
    ExternalChildDomainCreateReceipt Admission, string SourceResourceId,
    ulong Sequence, ExternalOperationIdentity Operation);

public sealed record ExternalOriginalChildEventObservation(
    ExternalOriginalChildEventRequest Request, ExternalChildEventReceipt? Receipt,
    bool NoEffect, bool NoFurtherDelivery,
    HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);

public interface IHybridCpuOriginalChildEventRuntimeV1
{
    ExternalOriginalChildEventObservation? InjectOriginalChildEvent(ExternalOriginalChildEventRequest request);
    ExternalOriginalChildEventObservation? ConsumeOriginalChildEvent(ExternalOriginalChildEventRequest originalRequest);
    ExternalOriginalChildEventObservation? ResolveOriginalChildEvent(ExternalOriginalChildEventRequest originalRequest);
    ExternalOriginalChildEventObservation? ContainOriginalChildEvent(ExternalOriginalChildEventRequest originalRequest);
}
