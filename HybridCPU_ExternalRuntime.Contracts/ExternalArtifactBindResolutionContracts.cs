namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Pure surviving-original Bind lookup for cleanup. Absence is unknown;
/// the returned admission does not release backing or permit further execution.
/// The exact request instance and captured bytes identify the original invocation.</summary>
public interface IHybridCpuOriginalArtifactBindRuntimeV1
{
    ExternalChildArtifactBindReceipt? ResolveChildExecutableArtifactBind(
        ExternalChildDomainLease child, ExternalChildArtifactBindRequest originalRequest);
}

/// <summary>Surviving original invocation only. NoEffect proves refusal before
/// native materialization; missing/Denied/loader failure alone proves nothing.</summary>
public sealed record ExternalOriginalArtifactBindDisposition(
    ExternalChildDomainLease Child, ExternalChildArtifactBindRequest Request,
    ExternalChildResult<ExternalChildArtifactBindReceipt> Raw, bool NoEffect,
    HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration, string PackageSha256)
{
    /// <summary>Exact private backing from failed materialization, usable only for
    /// original Release. This is neither successful Bind nor NoEffect/closure.</summary>
    public ExternalChildArtifactBindReceipt? CleanupArtifact { get; init; }
}

public interface IHybridCpuOriginalArtifactBindDispositionRuntimeV1
{
    ExternalOriginalArtifactBindDisposition? ResolveOriginalArtifactBindDisposition(
        ExternalChildDomainLease child, ExternalChildArtifactBindRequest originalRequest);
}
