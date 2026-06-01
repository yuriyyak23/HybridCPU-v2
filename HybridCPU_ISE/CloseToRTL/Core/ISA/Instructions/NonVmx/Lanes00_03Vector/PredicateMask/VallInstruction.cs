namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask;

public sealed partial class VallInstruction
{
    public const string Mnemonic = "VALL";
    public const string OperandShape = "rd, predicate-mask sideband";
    public const string EvidenceBoundary = "VectorScalarResultContourFailClosed";
    public const string ScalarResultDestinationPolicy = "Unresolved; scalar rd publication ABI required";
    public const string EmptyMaskResultPolicy = "Unresolved; empty-mask boolean result policy required";
    public const string BooleanResultEncodingPolicy = "Unresolved; canonical false/true scalar encoding required";
    public const string PredicateMaskSidebandPolicy = "Explicit carrier sideband required; not inferred from VLM";
    public const string ActiveVlTailPolicy = "Unresolved; active VL and tail policy ABI required";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresPredicateMaskSideband = true;
    public const bool RequiresScalarResultAbi = true;
    public const bool RequiresScalarResultDestinationAbi = true;
    public const bool RequiresEmptyMaskResultPolicy = true;
    public const bool RequiresBooleanResultEncodingAbi = true;
    public const bool RequiresActiveVlTailPolicyAbi = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresRetireReplayPublicationAbi = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool MaskSidebandInferredFromVlm = false;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenStreamEngineFallback = true;
    public const bool NoHiddenDmaFallback = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
