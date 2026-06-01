// DSC_QUERY_BACKEND metadata anchor: Lane6 backend capability query remains
// deferred until capability ABI, bounded publication, and replay evidence close.
// It must never publish hidden host-owned evidence as guest architectural state.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries;

public sealed partial class DscQueryBackendInstruction
{
    public const string Mnemonic = "DSC_QUERY_BACKEND";
    public const string OperandShape = "Capability query sideband, scalar/status destination sideband";
    public const string ParameterDescriptor = "Query selector, optional queue/device scope, retire-visible bounded capability result";
    public const string MicroOpShape = "Lane6CapabilityQuery.Backend, read-only capability query, retire-owned scalar publication";
    public const string ExecutionLaneBinding = "Lane06DmaStream";
    public const string EvidenceBoundary = "Lane6CapabilityQueryNoExecution";
    public const string QueryAuthorityBoundary = "GenericLane6CapabilityQueryRuntimeOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase11NegativeDecisionGate";

    public const bool IsCapabilityQuery = true;
    public const bool IsReadOnlyQuery = true;
    public const bool RequiresCapabilityQueryAbi = true;
    public const bool RequiresBackendCapabilityAbi = true;
    public const bool RequiresQuerySelectorAbi = true;
    public const bool RequiresCapabilityResultAbi = true;
    public const bool RequiresResultScrubbingPolicy = true;
    public const bool RequiresQueryRuntimeAdmission = true;
    public const bool RequiresTypedQueryMicroOp = true;
    public const bool RequiresBoundedResultFootprint = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresSchedulerLaneBinding = true;
    public const bool RequiresRetireOwnedPublication = true;
    public const bool RequiresFutureRetireReplayEvidence = true;
    public const bool RequiresReplayStableResult = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoHostEvidenceLeak = true;
    public const bool NoGuestVisibleHostEvidence = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool ExistingDscQueryCapsEvidenceIsInsufficient = true;
    public const bool ExistingDscStatusEvidenceIsInsufficient = true;
    public const bool Dsc2ParserEvidenceIsInsufficient = true;
    public const bool NoScalarOpcodePublication = true;
    public const bool NoDecoderEncoderAbiPublication = true;
    public const bool NoInstructionIrProjectionPublication = true;
    public const bool NoRegistryMaterializerPublication = true;
    public const bool NoTypedMicroOpPublication = true;
    public const bool NoSchedulerLaneBindingPublication = true;
    public const bool NoExecutionCapturePublication = true;
    public const bool NoRetirePublicationBeforeQueryAuthority = true;
    public const bool NoReplayRollbackPublication = true;
    public const bool NoCompilerHelperEmission = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoHiddenVectorLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoDmaStreamComputeFallback = true;
    public const bool NoDscQueryCapsFallback = true;
    public const bool NoDscStatusFallback = true;
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
