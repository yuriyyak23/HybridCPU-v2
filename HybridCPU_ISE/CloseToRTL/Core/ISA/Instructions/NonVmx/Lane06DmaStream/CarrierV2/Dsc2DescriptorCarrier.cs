// DSC2 metadata anchor: descriptor-v2 carrier remains parser-only until a
// descriptor-v2 ADR closes parser, runtime admission, retire, replay, and ABI evidence.
// It is Lane6 descriptor/carrier-owned and does not become a scalar opcode or VMX path.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.CarrierV2;

public sealed partial class Dsc2DescriptorCarrier
{
    public const string Mnemonic = "DSC2";
    public const string OperandShape = "DescriptorV2CarrierSideband, no architectural scalar operands";
    public const string ParameterDescriptor = "Descriptor-v2 header, extension table, owner/domain, shape/type policy sideband";
    public const string MicroOpShape = "ParserOnlyLane6DescriptorCarrierV2, no runtime MicroOp until descriptor-v2 ADR closes";
    public const string ExecutionLaneBinding = "Lane06DmaStream";
    public const string EvidenceBoundary = "ParserOnlyCarrierNoExecution";
    public const string CarrierAuthorityBoundary = "ParserOnlyLane6DescriptorV2Carrier";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase11NegativeDecisionGate";

    public const bool IsDescriptorOwned = true;
    public const bool IsCarrierOnly = true;
    public const bool IsParserOnly = true;
    public const bool RequiresDescriptorV2Adr = true;
    public const bool RequiresDescriptorV2ParserManifest = true;
    public const bool RequiresBackwardCompatibleDecoder = true;
    public const bool RequiresDescriptorV2ExecutionPolicy = true;
    public const bool RequiresDescriptorV2AdmissionPolicy = true;
    public const bool RequiresRuntimeAdmission = true;
    public const bool RequiresRetireCommitAuthority = true;
    public const bool RequiresDescriptorV2RetireReplayPolicy = true;
    public const bool RequiresReplayDeterminism = true;
    public const bool RequiresParserOnlyConformance = true;
    public const bool RequiresDescriptorV2GoldenArtifacts = true;
    public const bool NoDsc2ExecutionBeforeAdr = true;
    public const bool NoDescriptorV2ExecutionBeforeAdr = true;
    public const bool ParserAcceptanceIsNotExecutionEvidence = true;
    public const bool NoGuestVisibleHostEvidence = true;
    public const bool NoHostEvidenceLeak = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool ExistingDmaStreamComputeEvidenceIsInsufficient = true;
    public const bool ExistingDscStatusEvidenceIsInsufficient = true;
    public const bool ExistingDscQueryCapsEvidenceIsInsufficient = true;
    public const bool Phase10DescriptorOpEvidenceIsInsufficient = true;
    public const bool NoScalarOpcodePublication = true;
    public const bool NoExecutableDecoderEncoderAbiPublication = true;
    public const bool NoInstructionIrProjectionPublication = true;
    public const bool NoRegistryMaterializerPublication = true;
    public const bool NoTypedMicroOpPublication = true;
    public const bool NoSchedulerLaneBindingPublication = true;
    public const bool NoRuntimeAdmissionPublication = true;
    public const bool NoExecutionCapturePublication = true;
    public const bool NoRetireCommitPublication = true;
    public const bool NoReplayRollbackPublication = true;
    public const bool NoCompilerHelperEmission = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoHiddenVectorLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoParserToExecutionPromotion = true;
    public const bool NoDmaStreamComputeFallback = true;
    public const bool NoDscStatusFallback = true;
    public const bool NoDscQueryCapsFallback = true;
    public const bool NoQueueRuntimeFallback = true;
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
