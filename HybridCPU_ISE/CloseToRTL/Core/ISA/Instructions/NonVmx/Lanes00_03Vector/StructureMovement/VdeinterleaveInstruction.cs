namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement;

public sealed partial class VdeinterleaveInstruction
{
    public const string Mnemonic = "VDEINTERLEAVE";
    public const string OperandShape = "DestSrc1Pointer, Src2Pointer, structure shape sideband";
    public const string EvidenceBoundary = "VectorStructureMovementFailClosed";
    public const string ShapeContract = "structure shape sideband must define element order, lane grouping, aliasing, mask/tail, and publication footprint before execution.";
    public const string PublicationPolicy = "structure movement requires staged vector publication with replay-safe all-or-fail visibility.";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresStructureShapeAbi = true;
    public const bool RequiresShapeOrderingPolicy = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool NoHiddenStreamEngineFallback = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoBaseOpcodeDuplication = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenDmaFallback = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
