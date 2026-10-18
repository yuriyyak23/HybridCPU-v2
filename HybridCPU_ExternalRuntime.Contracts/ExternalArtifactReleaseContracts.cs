namespace HybridCPU.ExternalRuntime.Contracts;

public sealed record ExternalArtifactReleaseRequest(
    ExternalChildDomainLease Child, ExternalChildArtifactBindReceipt Artifact,
    ExternalOperationIdentity Operation);

/// <summary>Exact original artifact backing lifetime is ended. No future Start or
/// retained CPU runner can use that backing. This is not mapping closure or OS reclaim.</summary>
public sealed record ExternalArtifactReleaseReceipt(
    ExternalArtifactReleaseRequest Request, HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration, bool NoFurtherUse);

/// <summary>Optional bounded artifact cleanup. Request precedes possible effect;
/// lookup is pure and returns only a surviving exact producing-owner fact. Absence,
/// Denied, failure and transport loss do not establish release or no-effect.</summary>
public interface IHybridCpuArtifactReleaseRuntimeV1
{
    ExternalChildResult<ExternalArtifactReleaseReceipt> ReleaseChildExecutableArtifact(ExternalArtifactReleaseRequest request);
    ExternalArtifactReleaseReceipt? ResolveChildExecutableArtifactRelease(ExternalArtifactReleaseRequest request);
}
