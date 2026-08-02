using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Containment for the existing retirement authority and cross-stage
/// publication certificates. RetireCoordinator retains functional authority.
/// </summary>
internal sealed class RetireState
{
    internal RetireCoordinator Coordinator = null!;
    internal PipelineContourCertificate DecodePublicationCertificate;
    internal PipelineContourCertificate ExecuteCompletionCertificate;
    internal PipelineContourCertificate RetireVisibilityCertificate;
}
