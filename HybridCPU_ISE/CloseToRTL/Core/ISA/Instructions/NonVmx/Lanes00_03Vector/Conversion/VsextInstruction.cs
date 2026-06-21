namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion;

public sealed partial class VsextInstruction
{
    public const string Mnemonic = "VSEXT";
    public const string OperandShape = "DestSrc1Pointer, source-width sideband";
    public const string EvidenceBoundary = "VectorWidenNarrowConvertFailClosed";
    public const string WidthTransformPolicy = "Unresolved; sign-extension source/destination width and LMUL/VL contour ABI required";
    public const string SignednessPolicy = "Unresolved; signed source interpretation and result footprint ABI required";
    public const string MaskTailPolicy = "Unresolved; mask/tail behavior requires VLM contour closure";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresSourceDestinationWidthSideband = true;
    public const bool RequiresSignednessAbi = true;
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
    public const bool SeparateFromClosedVzext = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
