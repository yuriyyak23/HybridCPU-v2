namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments;

public sealed partial class Vldseg8Instruction
{
    public const string Mnemonic = "VLDSEG8";
    public const string OperandShape = "destination surfaces, base pointer, stride/shape sideband";
    public const string EvidenceBoundary = "VectorSegmentMemoryFailClosed";
    public const string ShapeContract = "segment memory shape sideband must define segment count, stride, alignment, byte order, mask/tail, and fault granularity before execution.";
    public const string SegmentOrderingPolicy = "segment load deinterleaving and destination surface order require golden byte-order evidence.";
    public const string PublicationPolicy = "segment loads require staged destination publication after replay-safe fault ordering closes.";
    public const string ExecutionLaneBinding = "Lanes04_05Memory";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const int SegmentCount = 8;
    public const bool IsSegmentLoad = true;
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
    public const bool RequiresRetireStagedPublication = true;
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
