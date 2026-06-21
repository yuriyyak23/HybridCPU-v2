namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments;

public sealed partial class Vstseg4Instruction
{
    public const string Mnemonic = "VSTSEG4";
    public const string OperandShape = "source surfaces, base pointer, stride/shape sideband";
    public const string EvidenceBoundary = "VectorSegmentMemoryFailClosed";
    public const string ShapeContract = "segment memory shape sideband must define segment count, stride, alignment, byte order, mask/tail, and fault granularity before execution.";
    public const string SegmentOrderingPolicy = "segment store interleaving and source surface order require golden byte-commit evidence.";
    public const string PublicationPolicy = "segment stores require staged byte commit after replay-safe fault ordering closes.";
    public const string ExecutionLaneBinding = "Lanes04_05Memory";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const int SegmentCount = 4;
    public const bool IsSegmentStore = true;
    public const bool RequiresMemoryShapeAbi = true;
    public const bool RequiresSegmentShapeAbi = true;
    public const bool RequiresByteOrderingPolicy = true;
    public const bool RequiresAlignmentFaultPolicy = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresFaultReplayPolicy = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMemoryMicroOp = true;
    public const bool RequiresRetireStagedCommit = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoBaseOpcodeDuplication = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenStreamEngineFallback = true;
    public const bool NoHiddenDmaFallback = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
