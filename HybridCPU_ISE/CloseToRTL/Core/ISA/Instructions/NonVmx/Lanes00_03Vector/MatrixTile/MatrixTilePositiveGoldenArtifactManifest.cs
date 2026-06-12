using HybridCPU_ISE.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public readonly record struct MatrixTileGoldenCarrier(
    ulong Word0,
    ulong Word1,
    ulong Word2,
    ulong Word3)
{
    public VLIW_Instruction CreateInstruction()
    {
        return new VLIW_Instruction
        {
            Word0 = Word0,
            Word1 = Word1,
            Word2 = Word2,
            Word3 = Word3
        };
    }
}

public readonly record struct MatrixTileExecutionGoldenVector(
    string Id,
    InstructionsEnum Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileGoldenCarrier Carrier,
    MatrixTileCanonicalDescriptorAbi TileDescriptor,
    MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor,
    MatrixTileCanonicalDescriptorAbi ResultTileDescriptor,
    ushort SourceTileId,
    ushort SecondaryTileId,
    ushort DestinationTileId,
    string SourceTileHex,
    string SecondaryTileHex,
    string InitialDestinationHex,
    string InitialMemoryHex,
    string ExpectedRetiredHex,
    string ExpectedRollbackHex,
    MatrixTileRetirePublicationKind PublicationKind);

public readonly record struct MatrixTileMemoryFaultGoldenVector(
    string Id,
    InstructionsEnum Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileGoldenCarrier Carrier,
    ushort SourceTileId,
    ushort DestinationTileId,
    string SourceTileHex,
    MatrixTileExecutionFaultKind ExpectedExecutionFault,
    MatrixTileRetireFaultKind ExpectedRetireFault,
    MatrixTileMemoryFaultKind ExpectedMemoryFault,
    bool ExpectedFaultPoint,
    ushort ExpectedFaultRow,
    ulong ExpectedFaultAddress);

public readonly record struct MatrixTileDescriptorFaultGoldenVector(
    string Id,
    InstructionsEnum Opcode,
    MatrixTileGoldenCarrier Carrier,
    MatrixTileIrProjectionFaultKind ExpectedProjectionFault);

public readonly record struct MatrixTileReservedCarrierGoldenVector(
    string Id,
    InstructionsEnum Opcode,
    MatrixTileGoldenCarrier Carrier,
    string ExpectedDecodeDecision);

public static class MatrixTilePositiveGoldenArtifactManifest
{
    private static readonly MatrixTileCanonicalDescriptorAbi Tile2X2I8 =
        MatrixTileCanonicalDescriptorAbi.Create(2, 2, 1, 2);

    private static readonly MatrixTileCanonicalDescriptorAbi Accumulator2X2I32 =
        MatrixTileCanonicalDescriptorAbi.Create(2, 2, 4, 8);

    private static readonly MatrixTileExecutionGoldenVector[] ExecutionVectorTable =
    [
        new(
            "MTILE_LOAD_2X2_I8_RETIRE_REPLAY",
            InstructionsEnum.MTILE_LOAD,
            MatrixTileProjectedOperationKind.Load,
            new MatrixTileGoldenCarrier(
                0x00D8000000400002UL,
                0x0000000000000100UL,
                0x0000000000000002UL,
                0x0010000000040001UL),
            Tile2X2I8,
            SecondaryTileDescriptor: default,
            Tile2X2I8,
            SourceTileId: 0,
            SecondaryTileId: 2,
            DestinationTileId: 2,
            SourceTileHex: "",
            SecondaryTileHex: "",
            InitialDestinationHex: "09080706",
            InitialMemoryHex: "01020304",
            ExpectedRetiredHex: "01020304",
            ExpectedRollbackHex: "09080706",
            MatrixTileRetirePublicationKind.TileState),
        new(
            "MTILE_STORE_2X2_I8_RETIRE_REPLAY",
            InstructionsEnum.MTILE_STORE,
            MatrixTileProjectedOperationKind.Store,
            new MatrixTileGoldenCarrier(
                0x00D9000000400002UL,
                0x0000000000000180UL,
                0x0000000000000002UL,
                0x0010000000040001UL),
            Tile2X2I8,
            SecondaryTileDescriptor: default,
            Tile2X2I8,
            SourceTileId: 2,
            SecondaryTileId: 2,
            DestinationTileId: 2,
            SourceTileHex: "05060708",
            SecondaryTileHex: "",
            InitialDestinationHex: "",
            InitialMemoryHex: "15161718",
            ExpectedRetiredHex: "05060708",
            ExpectedRollbackHex: "15161718",
            MatrixTileRetirePublicationKind.MemoryStore),
        new(
            "MTILE_MACC_2X2_I8_I32_RETIRE_REPLAY",
            InstructionsEnum.MTILE_MACC,
            MatrixTileProjectedOperationKind.Macc,
            new MatrixTileGoldenCarrier(
                0x00DA000000400002UL,
                0x0000000000000001UL,
                0x0000000000030002UL,
                0x0010000000040001UL),
            Tile2X2I8,
            Tile2X2I8,
            Accumulator2X2I32,
            SourceTileId: 1,
            SecondaryTileId: 2,
            DestinationTileId: 3,
            SourceTileHex: "01020304",
            SecondaryTileHex: "01000001",
            InitialDestinationHex: "00000000000000000000000000000000",
            InitialMemoryHex: "",
            ExpectedRetiredHex: "01000000020000000300000004000000",
            ExpectedRollbackHex: "00000000000000000000000000000000",
            MatrixTileRetirePublicationKind.Accumulator),
        new(
            "MTRANSPOSE_2X2_I8_RETIRE_REPLAY",
            InstructionsEnum.MTRANSPOSE,
            MatrixTileProjectedOperationKind.Transpose,
            new MatrixTileGoldenCarrier(
                0x00DB000000400002UL,
                0x0000000000000001UL,
                0x0000000000000002UL,
                0x0010000000040001UL),
            Tile2X2I8,
            SecondaryTileDescriptor: default,
            Tile2X2I8,
            SourceTileId: 1,
            SecondaryTileId: 2,
            DestinationTileId: 2,
            SourceTileHex: "01020304",
            SecondaryTileHex: "",
            InitialDestinationHex: "09090909",
            InitialMemoryHex: "",
            ExpectedRetiredHex: "01030204",
            ExpectedRollbackHex: "09090909",
            MatrixTileRetirePublicationKind.TileState)
    ];

    private static readonly MatrixTileMemoryFaultGoldenVector[] MemoryFaultVectorTable =
    [
        new(
            "MTILE_LOAD_PARTIAL_ROW_FAULT",
            InstructionsEnum.MTILE_LOAD,
            MatrixTileProjectedOperationKind.Load,
            new MatrixTileGoldenCarrier(
                0x00D8000000400002UL,
                0x00000000000003FFUL,
                0x0000000000000004UL,
                0x0010000000040001UL),
            SourceTileId: 0,
            DestinationTileId: 4,
            SourceTileHex: "",
            MatrixTileExecutionFaultKind.InvalidMemoryShape,
            MatrixTileRetireFaultKind.CapturedExecutionFault,
            MatrixTileMemoryFaultKind.PartialMemoryFault,
            ExpectedFaultPoint: true,
            ExpectedFaultRow: 0,
            ExpectedFaultAddress: 0x3FF),
        new(
            "MTILE_STORE_PARTIAL_COMMIT_FAULT",
            InstructionsEnum.MTILE_STORE,
            MatrixTileProjectedOperationKind.Store,
            new MatrixTileGoldenCarrier(
                0x00D9000000400002UL,
                0x0000000000000180UL,
                0x0000000000000004UL,
                0x0010000000040001UL),
            SourceTileId: 4,
            DestinationTileId: 4,
            SourceTileHex: "0A0B0C0D",
            MatrixTileExecutionFaultKind.None,
            MatrixTileRetireFaultKind.MemoryCommitFault,
            MatrixTileMemoryFaultKind.None,
            ExpectedFaultPoint: false,
            ExpectedFaultRow: 0,
            ExpectedFaultAddress: 0)
    ];

    private static readonly MatrixTileDescriptorFaultGoldenVector[] DescriptorFaultVectorTable =
    [
        CreateDescriptorFault(
            "MTILE_LOAD_MALFORMED_2D_SHAPE",
            InstructionsEnum.MTILE_LOAD,
            0x00D8000000400002UL),
        CreateDescriptorFault(
            "MTILE_STORE_MALFORMED_2D_SHAPE",
            InstructionsEnum.MTILE_STORE,
            0x00D9000000400002UL),
        CreateDescriptorFault(
            "MTILE_MACC_MALFORMED_2D_SHAPE",
            InstructionsEnum.MTILE_MACC,
            0x00DA000000400002UL),
        CreateDescriptorFault(
            "MTRANSPOSE_MALFORMED_2D_SHAPE",
            InstructionsEnum.MTRANSPOSE,
            0x00DB000000400002UL)
    ];

    private static readonly MatrixTileReservedCarrierGoldenVector[] ReservedCarrierVectorTable =
    [
        CreateReservedCarrier(
            "MTILE_LOAD_RESERVED_WORD0",
            InstructionsEnum.MTILE_LOAD,
            0x00D8010000400002UL),
        CreateReservedCarrier(
            "MTILE_STORE_RESERVED_WORD0",
            InstructionsEnum.MTILE_STORE,
            0x00D9010000400002UL),
        CreateReservedCarrier(
            "MTILE_MACC_RESERVED_WORD0",
            InstructionsEnum.MTILE_MACC,
            0x00DA010000400002UL),
        CreateReservedCarrier(
            "MTRANSPOSE_RESERVED_WORD0",
            InstructionsEnum.MTRANSPOSE,
            0x00DB010000400002UL)
    ];

    public const string ManifestDecision = "ClosedMatrixTilePositiveExecutableGoldenArtifacts";
    public const string VerificationDecision = "CanonicalCarrierResourceContourTypedStreamExecuteRetireReplayGoldenVerification";
    public const string NoFallbackDecision = "ClosedRuntimeNoFallbackNoHiddenLoweringRegressionEvidence";
    public const string ProvenanceDecision = "RuntimeOwnedHandAuthoredGoldenInputsNoCompilerProvenance";
    public const string ReservedCarrierDecision = "ReservedMatrixTileCarrierRowsFailClosed";
    public const string StatusBoundaryDecision = "GoldenEvidenceIsNonAuthorityPhase14OwnsFinalStatusPromotion";
    public const string CompilerBoundaryDecision = "CompilerCodeUnmodifiedRuntimeHandoffOnly";

    public const bool HasPositiveExecutableGoldenArtifacts = true;
    public const bool HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence = true;
    public const bool HasLegalDecodeEncodeRoundTripVectors = true;
    public const bool HasLegalIrMaterializerProjectionVectors = true;
    public const bool HasLegalExecuteRetireVectors = true;
    public const bool HasMemoryFaultVectors = true;
    public const bool HasDescriptorFaultVectors = true;
    public const bool HasAccumulatorVectors = true;
    public const bool HasTransposeVectors = true;
    public const bool HasReplayRollbackVectors = true;
    public const bool HasNegativeReservedCarrierVectors = true;
    public const bool UsesCompilerGeneratedInputs = false;
    public const bool UsesHostOwnedArchitecturalEvidence = false;
    public const bool UsesFallbackPath = false;
    public const bool KeepsStatusCatalogOptionalDisabled = false;
    public const bool KeepsPositiveCompilerEmissionBlocked = false;

    public static ReadOnlySpan<MatrixTileExecutionGoldenVector> ExecutionVectors =>
        ExecutionVectorTable;

    public static ReadOnlySpan<MatrixTileMemoryFaultGoldenVector> MemoryFaultVectors =>
        MemoryFaultVectorTable;

    public static ReadOnlySpan<MatrixTileDescriptorFaultGoldenVector> DescriptorFaultVectors =>
        DescriptorFaultVectorTable;

    public static ReadOnlySpan<MatrixTileReservedCarrierGoldenVector> ReservedCarrierVectors =>
        ReservedCarrierVectorTable;

    private static MatrixTileDescriptorFaultGoldenVector CreateDescriptorFault(
        string id,
        InstructionsEnum opcode,
        ulong word0)
    {
        return new MatrixTileDescriptorFaultGoldenVector(
            id,
            opcode,
            new MatrixTileGoldenCarrier(
                word0,
                0x0000000000000100UL,
                0x0000000000000002UL,
                0x0010000000030001UL),
            MatrixTileIrProjectionFaultKind.InvalidShapeEncoding);
    }

    private static MatrixTileReservedCarrierGoldenVector CreateReservedCarrier(
        string id,
        InstructionsEnum opcode,
        ulong word0)
    {
        return new MatrixTileReservedCarrierGoldenVector(
            id,
            opcode,
            new MatrixTileGoldenCarrier(
                word0,
                0x0000000000000100UL,
                0x0000000000000002UL,
                0x0010000000040001UL),
            "ReservedWord0BitsRejected");
    }
}

public static class MatrixTileNoFallbackEvidenceContract
{
    private static readonly string[] AuditedRuntimeTypeTable =
    [
        nameof(MatrixTileIrProjectionAndMaterializer),
        nameof(MatrixTileExecuteCaptureAbi),
        nameof(MatrixTileStreamTransferAbi),
        nameof(MatrixTileRetirePublicationAbi),
        nameof(MatrixTileReplayRollbackAbi),
        "MatrixTileMicroOp",
        "MtileLoadMicroOp",
        "MtileStoreMicroOp",
        "MtileMaccMicroOp",
        "MtransposeMicroOp"
    ];

    private static readonly string[] ForbiddenCallTargetTable =
    [
        "ScalarMicroOp",
        "VectorMicroOp",
        "DotProduct",
        "DmaStreamCompute",
        "Lane07",
        "ExternalAccelerator",
        "StreamExecutionRequest",
        "ExecuteStream",
        "BurstWrite",
        ".ISA.Instructions.Vmx.",
        "VmxMicroOp",
        "VmxInstruction",
        "HybridCPU_Compiler"
    ];

    public const string EvidenceDecision = "ClosedMatrixTileRuntimeNoFallbackAndNoHiddenLowering";
    public const string CallGraphDecision = "MtileRuntimeIlCallTargetsExcludeForbiddenHelperFamilies";
    public const string MaterializerDecision = "CanonicalProjectorProducesOnlyTypedMatrixTileMicroOps";
    public const string MemoryDecision = "TileMemoryAccessUsesRuntimeOwnedMatrixTileMemoryMethods";
    public const string CompilerDecision = "NoCompilerAssemblyOrHelperCallTarget";

    public const bool HasIlCallTargetAudit = true;
    public const bool HasTypedCarrierAudit = true;
    public const bool HasRuntimeOwnedMemoryAudit = true;
    public const bool HasCompilerBoundaryAudit = true;
    public const bool UsesFallbackPath = false;

    public static ReadOnlySpan<string> AuditedRuntimeTypes => AuditedRuntimeTypeTable;

    public static ReadOnlySpan<string> ForbiddenCallTargetFragments => ForbiddenCallTargetTable;
}
