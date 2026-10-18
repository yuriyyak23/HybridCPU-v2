namespace HybridCPU.ExternalRuntime.Contracts;

public sealed record ExternalOriginalDomainCloseRequest(
    ExternalDomainBindReceipt Admission, ExternalOperationIdentity Operation);

/// <summary>Canonical producing-owner outcome. NoEffect is exact pre-effect
/// refusal; NoFurtherUse is exact terminal closure of the admitted root lifetime.
/// Neither absence nor failure implies either guarantee.</summary>
public sealed record ExternalOriginalDomainCloseObservation(
    ExternalOriginalDomainCloseRequest Request, ExternalDomainCloseResult Raw,
    bool NoEffect, bool NoFurtherUse);

public interface IHybridCpuOriginalDomainCloseRuntimeV1
{
    ExternalOriginalDomainCloseObservation? CloseOriginalDomain(ExternalOriginalDomainCloseRequest request);
    ExternalOriginalDomainCloseObservation? ResolveOriginalDomainClose(ExternalOriginalDomainCloseRequest originalRequest);
}
