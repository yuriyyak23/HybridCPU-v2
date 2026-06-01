namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint;

public sealed partial class VsraSatInstruction
{
    public const string Mnemonic = "VSRA.SAT";
    public const string OperandShape = "DestSrc1Pointer, shift source/immediate, saturating policy sideband";
    public const string EvidenceBoundary = "VectorFixedPointSaturatingFailClosed";
    public const string SaturationPolicy = "saturating arithmetic right shift requires an explicit meaningfulness decision plus shift operand, signedness, element width, clamp, mask/tail, and staged publication policy.";
    public const string ShiftMeaningPolicy = "arithmetic right-shift saturation may remain reserved if the policy is non-meaningful.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresSaturatingPolicyAbi = true;
    public const bool RequiresElementWidthAbi = true;
    public const bool RequiresSignednessAbi = true;
    public const bool RequiresClampPolicyAbi = true;
    public const bool RequiresOverflowPolicyAbi = true;
    public const bool RequiresShiftOperandAbi = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresSaturatingShiftMeaningDecision = true;
    public const bool MayRemainReservedIfNonMeaningful = true;
    public const bool SeparateFromClosedVaddSat = true;
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
