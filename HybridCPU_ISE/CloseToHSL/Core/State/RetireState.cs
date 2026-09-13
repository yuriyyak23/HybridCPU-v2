using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Containment for the existing retirement authority and cross-stage
/// publication certificates. RetireCoordinator retains functional authority.
/// </summary>
internal sealed class RetireState
{
    internal RetireCoordinator Coordinator = null!;
    internal ArchitecturalCompletionCommitOwner CompletionCommitOwner = null!;
    internal ArchitecturalCompletionCommitOwner.ProducerRegistration
        CanonicalPipelineCompletionProducer = null!;
    internal ArchitecturalCompletionCommitOwner.ProducerRegistration
        CanonicalCpuTranslationFaultCompletionProducer = null!;
    internal ArchitecturalCompletionCommitOwner.ProducerRegistration
        CanonicalCpuSecondStageTranslationFaultCompletionProducer = null!;
    internal PipelineContourCertificate DecodePublicationCertificate;
    internal PipelineContourCertificate ExecuteCompletionCertificate;
    internal PipelineContourCertificate RetireVisibilityCertificate;
    internal ulong LastRetiredBundlePc;
    internal ulong RetiredBundleSequence;
    internal byte LastRetiredLaneIndex;
    internal uint LastRetiredOpcode;
    internal bool LastRetiredWritesRegister;
    internal ushort LastRetiredDestinationRegister;
    internal ulong LastRetiredResultValue;
    internal string LastRetiredMicroOpType = string.Empty;
    internal string LastRetiredMicroOpDescription = string.Empty;
}
