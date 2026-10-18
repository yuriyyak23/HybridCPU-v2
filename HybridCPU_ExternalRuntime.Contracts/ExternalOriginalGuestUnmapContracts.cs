namespace HybridCPU.ExternalRuntime.Contracts;

public sealed record ExternalOriginalGuestUnmapRequest(ExternalGuestMemoryMapReceipt Admission, ExternalOperationIdentity Operation);
public sealed record ExternalOriginalGuestUnmapObservation(ExternalOriginalGuestUnmapRequest Request,
    ExternalChildResult<ExternalGuestMemoryUnmapReceipt> Raw, bool NoEffect, bool NoFurtherUse);
public interface IHybridCpuOriginalGuestUnmapRuntimeV1
{
    ExternalOriginalGuestUnmapObservation? UnmapOriginalGuest(ExternalOriginalGuestUnmapRequest request);
    ExternalOriginalGuestUnmapObservation? ResolveOriginalGuestUnmap(ExternalOriginalGuestUnmapRequest request);
}
