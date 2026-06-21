namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing;

public sealed partial class VnsrlInstruction
{
    public const string Mnemonic = "VNSRL";
    public const string OperandShape = "DestSrc1Pointer, shift source/immediate sideband, narrowing policy sideband";
    public const string EvidenceBoundary = "VectorWidenNarrowConvertFailClosed";
    public const string WidthTransformPolicy = "Unresolved; source/destination narrowing width and LMUL/VL contour ABI required";
    public const string ShiftOperandPolicy = "Unresolved; logical shift source/immediate ABI required";
    public const string NarrowingResultPolicy = "Unresolved; truncation, rounding, saturation, and trap policy required";
    public const string MaskTailPolicy = "Unresolved; mask/tail behavior requires VLM contour closure";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresSourceDestinationWidthSideband = true;
    public const bool RequiresNarrowingPolicyAbi = true;
    public const bool RequiresRoundingSaturationTrapPolicy = true;
    public const bool RequiresShiftOperandAbi = true;
    public const bool RequiresElementWidthLmulVlAbi = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
