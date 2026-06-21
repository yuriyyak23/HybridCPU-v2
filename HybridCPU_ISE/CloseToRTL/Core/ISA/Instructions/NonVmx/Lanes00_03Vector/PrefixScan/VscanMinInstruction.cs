namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan;

public sealed partial class VscanMinInstruction
{
    public const string Mnemonic = "VSCAN.MIN";
    public const string OperandShape = "DestSrc1Pointer, element-type/prefix-policy sideband";
    public const string EvidenceBoundary = "VectorScanContourFailClosed";
    public const string ScanPolicy = "prefix min requires explicit inclusive/exclusive policy, element ordering, signedness/element type, tail behavior, replay determinism, and staged publication.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresPrefixScanPolicyAbi = true;
    public const bool RequiresElementTypeSideband = true;
    public const bool RequiresSignednessAbi = true;
    public const bool RequiresTailPolicyAbi = true;
    public const bool RequiresMaskPolicyAbi = true;
    public const bool SeparateFromClosedVscanSum = true;
    public const bool RequiresReplayDeterminism = true;
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
