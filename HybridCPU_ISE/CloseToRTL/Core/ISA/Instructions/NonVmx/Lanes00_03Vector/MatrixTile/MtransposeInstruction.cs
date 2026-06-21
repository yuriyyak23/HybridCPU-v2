namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public sealed partial class MtransposeInstruction
{
    public const string Mnemonic = "MTRANSPOSE";
    public const string OperandShape = "tile descriptor, transpose policy sideband";
    public const string EvidenceBoundary = "MatrixTileRuntimeExecutableAuthority";
    public const string TransposePolicy = "matrix transpose requires explicit tile descriptor ABI, transpose tile policy, execution model, staged publication, replay, and golden artifacts.";
    public const string ExecutionLaneBinding = "MatrixTileComputeLanes00_03";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string ProductionDecision = "Phase14TileStreamResourceContourClosed";
    public const bool OptionalDisabledInIsaV4 = false;
    public const bool OptionalEnabledInIsaV4 = true;
    public const bool RequiresTileExecutionModel = true;
    public const bool RequiresTileDescriptorAbi = true;
    public const bool RequiresTransposeTilePolicyAbi = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool NoDecoderEncoderAbiPublication = true;
    public const bool NoInstructionIrProjectionPublication = false;
    public const bool NoRegistryMaterializerPublication = false;
    public const bool NoTypedMicroOpPublication = false;
    public const bool NoSchedulerLaneBindingPublication = false;
    public const bool NoExecutionCapturePublication = false;
    public const bool NoRetireWritebackPublication = false;
    public const bool NoReplayRollbackPublication = false;
    public const bool NoCompilerHelperEmission = true;
    public const bool RequiresTypedTileMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresFutureRetireReplayEvidence = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoDescriptorFallbackWithoutGenericRuntimeOwnership = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoHiddenVectorLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoLane6Placement = true;
    public const bool NoLane6DscFallback = true;
    public const bool NoLane7Fallback = true;
    public const bool NoExternalBackendFallback = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = true;
    public const bool CompilerHelperAllowed = false;
}
