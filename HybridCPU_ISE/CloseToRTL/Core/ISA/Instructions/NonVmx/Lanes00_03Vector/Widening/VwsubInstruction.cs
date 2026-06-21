namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening;

public sealed partial class VwsubInstruction
{
    public const string Mnemonic = "VWSUB";
    public const string OperandShape = "DestSrc1Pointer, Src2Pointer, source/destination width sideband";
    public const string EvidenceBoundary = "VectorWidenNarrowConvertFailClosed";
    public const string WidthTransformPolicy = "Unresolved; source/destination element-width and LMUL/VL contour ABI required";
    public const string SignednessPolicy = "Unresolved; signed widening subtract policy required";
    public const string OverflowPublicationPolicy = "Unresolved; widening overflow/result footprint policy required";
    public const string MaskTailPolicy = "Unresolved; mask/tail behavior requires VLM contour closure";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresSourceDestinationWidthSideband = true;
    public const bool RequiresSignednessAbi = true;
    public const bool RequiresElementWidthLmulVlAbi = true;
    public const bool RequiresWideningOverflowPolicyAbi = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool SeparateFromBaseVectorArithmetic = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
