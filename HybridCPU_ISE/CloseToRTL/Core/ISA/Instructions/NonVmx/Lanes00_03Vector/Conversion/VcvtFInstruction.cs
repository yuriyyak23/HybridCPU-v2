namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion;

public sealed partial class VcvtFInstruction
{
    public const string Mnemonic = "VCVT.F";
    public const string OperandShape = "DestSrc1Pointer, floating-point conversion/rounding sideband";
    public const string EvidenceBoundary = "VectorWidenNarrowConvertFailClosed";
    public const string ConversionPolicy = "Unresolved; source/destination type conversion ABI required";
    public const string ResultFootprintPolicy = "Unresolved; floating-point result footprint ABI required";
    public const string NanPolicy = "Unresolved; NaN, invalid conversion, and FP exception policy required";
    public const string RoundingSaturationTrapPolicy = "Unresolved; rounding, saturation, and trap policy required";
    public const string MaskTailPolicy = "Unresolved; mask/tail behavior requires VLM contour closure";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresConversionPolicyAbi = true;
    public const bool RequiresRoundingSaturationTrapPolicy = true;
    public const bool RequiresResultFootprintAbi = true;
    public const bool RequiresNanPolicyAbi = true;
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
