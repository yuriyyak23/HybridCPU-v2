namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D;

public sealed partial class VgatherIndexed2DContour
{
    public const string Mnemonic = "VGATHER";
    public const string Contour = "Indexed2D";
    public const string OperandShape = "index surface, base pointer, 2D shape sideband";
    public const string EvidenceBoundary = "VectorMemoryContourFailClosed";
    public const string ShapeContract = "indexed+2D shape sideband must define index element meaning, rows, columns, stride, bounds, mask/tail, and fault order before execution.";
    public const string AddressingContourPolicy = "base VGATHER 1D indexed evidence does not authorize the indexed+2D contour.";
    public const string PublicationPolicy = "indexed+2D gather requires staged destination publication after descriptor, bounds, and replay evidence close.";
    public const string ExecutionLaneBinding = "Lanes04_05Memory";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresMemoryShapeAbi = true;
    public const bool RequiresIndexed2DShapeSideband = true;
    public const bool RequiresIndexSurfaceAbi = true;
    public const bool RequiresRowColumnStrideAbi = true;
    public const bool RequiresAddressBoundsPolicy = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresFaultReplayPolicy = true;
    public const bool NoBaseOpcodeDuplication = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMemoryMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenStreamEngineFallback = true;
    public const bool NoHiddenDmaFallback = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
