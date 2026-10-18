namespace HybridCPU.ExternalRuntime.Contracts;
public sealed record ExternalOriginalGuestMapRequest(ExternalChildDomainCreateReceipt Admission, ExternalGuestMemoryMapRequest MappingRequest);
public sealed record ExternalOriginalGuestMapObservation(ExternalOriginalGuestMapRequest Request,
    ExternalChildResult<ExternalGuestMemoryMapReceipt> Raw, bool NoEffect);
public interface IHybridCpuOriginalGuestMapRuntimeV1
{
    ExternalOriginalGuestMapObservation? MapOriginalGuest(ExternalOriginalGuestMapRequest request);
    ExternalOriginalGuestMapObservation? ResolveOriginalGuestMap(ExternalOriginalGuestMapRequest request);
}
