namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision;

public sealed partial class VdotWideI16Instruction
{
    public const string Mnemonic = "VDOT.WIDE.I16";
    public const string OperandShape = "dot operands, i16 wide contour sideband";
    public const string EvidenceBoundary = "VectorDotMatrixDeferredNoExecution";
    public const string WiderIntegerContourPolicy = "i16 wide-dot contour is separate from scoped VDOT.WIDE and requires explicit integer widening, accumulator, VLM, retire/replay, and golden evidence.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase09NegativeDecisionGate";
    public const bool RequiresDotAbiDecision = true;
    public const bool RequiresWiderIntegerContourAbi = true;
    public const bool RequiresAccumulatorPrecisionAbi = true;
    public const bool SeparateFromScopedVdotWide = true;
    public const bool NoNameOnlyVdotWideExtension = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool NoDecoderEncoderAbiPublication = true;
    public const bool NoInstructionIrProjectionPublication = true;
    public const bool NoRegistryMaterializerPublication = true;
    public const bool NoTypedMicroOpPublication = true;
    public const bool NoSchedulerLaneBindingPublication = true;
    public const bool NoExecutionCapturePublication = true;
    public const bool NoRetireWritebackPublication = true;
    public const bool NoReplayRollbackPublication = true;
    public const bool NoCompilerHelperEmission = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresFutureRetireReplayEvidence = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoDescriptorFallbackWithoutGenericRuntimeOwnership = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoHiddenVectorLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
