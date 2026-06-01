// DSC_FENCE metadata anchor: Lane6 queue fence remains deferred until fence scope,
// ordering authority, retire side effects, and replay evidence close.
// No VMX frontend path is added; future execution requires virtualization-boundary policy.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle;

public sealed partial class DscFenceInstruction
{
    public const string Mnemonic = "DSC_FENCE";
    public const string OperandShape = "QueueTokenOrHandle, fence scope sideband";
    public const string ParameterDescriptor = "TokenOrQueueHandle, fence scope, ordering domain sideband";
    public const string MicroOpShape = "Lane6QueueControl.Fence, queue-authority gated, retire-owned ordering side effect";
    public const string ExecutionLaneBinding = "Lane06DmaStream";
    public const string EvidenceBoundary = "Lane6QueueControlNoExecution";
    public const string QueueAuthorityBoundary = "GenericLane6QueueRuntimeOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase11NegativeDecisionGate";

    public const bool IsQueueControlOwned = true;
    public const bool RequiresQueueAuthority = true;
    public const bool RequiresTokenNamespaceAbi = true;
    public const bool RequiresQueueHandleAbi = true;
    public const bool RequiresTokenLifecycleAbi = true;
    public const bool RequiresQueueOwnershipModel = true;
    public const bool RequiresQueueStateModel = true;
    public const bool RequiresQueueRollbackJournal = true;
    public const bool RequiresQueueRuntimeAdmission = true;
    public const bool RequiresTypedQueueMicroOp = true;
    public const bool RequiresQueueCommandEncoding = true;
    public const bool RequiresCommandScopeAbi = true;
    public const bool RequiresQueueOrderingAbi = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresSchedulerLaneBinding = true;
    public const bool RequiresRetireOwnedSideEffect = true;
    public const bool RequiresFutureRetireReplayEvidence = true;
    public const bool RequiresReplayDeterminism = true;
    public const bool RequiresRollbackPolicy = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoGuestVisibleHostEvidence = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool ExistingDscStatusEvidenceIsInsufficient = true;
    public const bool ExistingDscQueryCapsEvidenceIsInsufficient = true;
    public const bool Dsc2ParserEvidenceIsInsufficient = true;
    public const bool NoScalarOpcodePublication = true;
    public const bool NoDecoderEncoderAbiPublication = true;
    public const bool NoInstructionIrProjectionPublication = true;
    public const bool NoRegistryMaterializerPublication = true;
    public const bool NoTypedMicroOpPublication = true;
    public const bool NoSchedulerLaneBindingPublication = true;
    public const bool NoExecutionCapturePublication = true;
    public const bool NoRetirePublicationBeforeQueueAuthority = true;
    public const bool NoReplayRollbackPublication = true;
    public const bool NoCompilerHelperEmission = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoHiddenVectorLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoDmaStreamComputeFallback = true;
    public const bool NoDscStatusFallback = true;
    public const bool NoDscQueryCapsFallback = true;
    public const bool NoDsc2Fallback = true;
    public const bool NoLane7Fallback = true;
    public const bool NoExternalBackendFallback = true;
    public const bool NoVmxSpecificPath = true;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool HasScalarOpcodeAllocation = false;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
