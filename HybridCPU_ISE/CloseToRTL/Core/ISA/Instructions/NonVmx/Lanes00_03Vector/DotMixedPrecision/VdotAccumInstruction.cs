namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision;

public sealed partial class VdotAccumInstruction
{
    public const string Mnemonic = "VDOT.ACCUM";
    public const string OperandShape = "dot operands, accumulator/result sideband";
    public const string EvidenceBoundary = "VectorDotMatrixDeferredNoExecution";
    public const string AccumulatorPolicy = "accumulating dot requires explicit accumulator precision, destination/result footprint, staged publication, replay, and golden artifacts.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase09NegativeDecisionGate";
    public const bool RequiresDotAbiDecision = true;
    public const bool RequiresAccumulatorPrecisionAbi = true;
    public const bool RequiresAccumulatorResultFootprintAbi = true;
    public const bool RequiresSeparateResultSurfaceAbi = true;
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
