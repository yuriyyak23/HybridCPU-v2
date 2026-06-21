namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint;

public sealed partial class VavgRInstruction
{
    public const string Mnemonic = "VAVG.R";
    public const string OperandShape = "DestSrc1Pointer, Src2Pointer, rounded average policy sideband";
    public const string EvidenceBoundary = "VectorFixedPointSaturatingFailClosed";
    public const string AveragePolicy = "rounded fixed-point average requires explicit signedness, element width, rounding mode, tie behavior, overflow, mask/tail, and staged publication policy.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresAveragePolicyAbi = true;
    public const bool RequiresElementWidthAbi = true;
    public const bool RequiresSignednessAbi = true;
    public const bool RequiresRoundingTruncationPolicyAbi = true;
    public const bool RequiresRoundingPolicyAbi = true;
    public const bool RequiresOverflowPolicyAbi = true;
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
