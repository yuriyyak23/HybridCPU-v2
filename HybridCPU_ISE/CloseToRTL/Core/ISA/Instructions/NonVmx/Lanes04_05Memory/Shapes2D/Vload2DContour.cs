namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D;

public sealed partial class Vload2DContour
{
    public const string Mnemonic = "VLOAD";
    public const string Contour = "2D";
    public const string OperandShape = "base pointer, row/column/stride shape sideband";
    public const string EvidenceBoundary = "VectorMemoryContourFailClosed";
    public const string ShapeContract = "2D memory shape sideband must define rows, columns, element size, row stride, mask/tail, and fault granularity before execution.";
    public const string AddressingContourPolicy = "base VLOAD 1D transfer evidence does not authorize the 2D contour.";
    public const string PublicationPolicy = "2D loads require staged destination publication after replay-safe row/column fault ordering closes.";
    public const string ExecutionLaneBinding = "Lanes04_05Memory";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresMemoryShapeAbi = true;
    public const bool Requires2DShapeSideband = true;
    public const bool RequiresRowColumnStrideAbi = true;
    public const bool RequiresMaskTailPolicyAbi = true;
    public const bool RequiresAddressBoundsPolicy = true;
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
