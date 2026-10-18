namespace HybridCPU.ExternalRuntime.Contracts;
public abstract class ExternalOriginalDomainBindRequest
{
    protected ExternalOriginalDomainBindRequest(ExternalDomainBindRequest request) => Request = request;
    public ExternalDomainBindRequest Request { get; }
}
public sealed record ExternalOriginalDomainBindObservation(ExternalOriginalDomainBindRequest Request, ExternalDomainBindResult Raw, bool NoEffect);
public interface IHybridCpuOriginalDomainBindRuntimeV1
{
    ExternalOriginalDomainBindRequest? PrepareOriginalDomainBind(ExternalDomainBindRequest request);
    ExternalOriginalDomainBindObservation? BindOriginalDomain(ExternalOriginalDomainBindRequest request);
    ExternalOriginalDomainBindObservation? ResolveOriginalDomainBind(ExternalOriginalDomainBindRequest originalRequest);
}
public interface IHybridCpuOriginalDomainBindRetirementRuntimeV1
{
    ExternalOriginalDomainBindObservation? RetireOriginalDomainBind(ExternalOriginalDomainBindRequest originalRequest);
}
