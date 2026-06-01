namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask;

public sealed partial class VmergeInstruction
{
    public const string Mnemonic = "VMERGE";
    public const string OperandShape = "DestSrc1Pointer, Src2Pointer, predicate-mask sideband";
    public const string EvidenceBoundary = "VectorContourFailClosed";
    public const string AliasPolicy = "Distinct mnemonic until a Phase05 ABI decision proves aliasing";
    public const string ResultSourcePolarityPolicy = "Unresolved; no canonical result/source polarity";
    public const string MaskedOffTailPolicy = "Unresolved; requires explicit mask/tail ABI";
    public const string PredicateMaskSidebandPolicy = "Explicit carrier sideband required; not inferred from VLM";
    public const string ElementWidthLmulVlPolicy = "Unresolved; requires element-width, LMUL, and VL contour closure";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresPredicateMaskSideband = true;
    public const bool RequiresAliasPolarityDecision = true;
    public const bool RequiresMaskedOffTailPolicyAbi = true;
    public const bool RequiresElementWidthLmulVlAbi = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
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
