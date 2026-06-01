namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint;

public sealed partial class VclipInstruction
{
    public const string Mnemonic = "VCLIP";
    public const string OperandShape = "DestSrc1Pointer, clip-bounds/result-width sideband";
    public const string EvidenceBoundary = "VectorFixedPointSaturatingFailClosed";
    public const string ClipPolicy = "fixed-point clip requires explicit bounds encoding, result width, signedness, narrowing/truncation behavior, mask/tail, and staged publication policy.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresClipBoundsAbi = true;
    public const bool RequiresNarrowingPolicyAbi = true;
    public const bool RequiresResultWidthAbi = true;
    public const bool RequiresSignednessAbi = true;
    public const bool RequiresRoundingTruncationPolicyAbi = true;
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
