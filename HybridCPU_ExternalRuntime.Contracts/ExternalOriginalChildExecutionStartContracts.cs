namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Exact original admitted artifact and invocation; equal copies cannot transfer ownership.</summary>
public sealed record ExternalOriginalChildExecutionStartRequest(ExternalChildDomainLease Child,
    ExternalChildArtifactBindReceipt Artifact, ExternalChildExecutionStartRequest Start);

/// <summary>Surviving original observation. NoEffect is not backing release or containment.</summary>
public sealed record ExternalOriginalChildExecutionStartObservation(ExternalOriginalChildExecutionStartRequest Request,
    ExternalChildResult<ExternalChildExecutionReceipt> Raw, bool NoEffect);

public interface IHybridCpuOriginalChildExecutionStartRuntimeV1
{
    ExternalOriginalChildExecutionStartObservation? StartOriginalChildExecution(ExternalOriginalChildExecutionStartRequest request);
    ExternalOriginalChildExecutionStartObservation? ResolveOriginalChildExecutionStart(ExternalOriginalChildExecutionStartRequest originalRequest);
}
