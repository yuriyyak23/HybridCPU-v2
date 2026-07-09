using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileVlmLegalityFaultKind : byte
{
    None = 0,
    NonMatrixTileOpcode = 1,
    MissingRuntimeOwnedVlmRow = 2,
    NonDescriptorBackedContour = 3,
    MemoryShapeFault = 4,
    MaccSemanticFault = 5,
    TransposeSemanticFault = 6,
}

public readonly record struct MatrixTileVlmLegalityValidationResult(
    bool IsLegal,
    InstructionsEnum Opcode,
    MatrixTileVlmLegalityFaultKind FaultKind,
    VectorContourLegalityStatus DescriptorBackedStatus,
    bool OpensDecoderAdmission,
    bool OpensExecution,
    bool UsesFallbackPath)
{
    public static MatrixTileVlmLegalityValidationResult Legal(InstructionsEnum opcode)
    {
        return new MatrixTileVlmLegalityValidationResult(
            IsLegal: true,
            opcode,
            MatrixTileVlmLegalityFaultKind.None,
            VectorContourLegalityStatus.DescriptorOnly,
            OpensDecoderAdmission: false,
            OpensExecution: false,
            UsesFallbackPath: false);
    }

    public static MatrixTileVlmLegalityValidationResult Fault(
        InstructionsEnum opcode,
        MatrixTileVlmLegalityFaultKind faultKind)
    {
        return new MatrixTileVlmLegalityValidationResult(
            IsLegal: false,
            opcode,
            faultKind,
            VectorContourLegalityStatus.FailClosed,
            OpensDecoderAdmission: false,
            OpensExecution: false,
            UsesFallbackPath: false);
    }
}

public static class MatrixTileRuntimeOwnedVlmRows
{
    public const string MatrixTileVlmFamily = "XMatrix";
    public const string RuntimeOwnedVlmRowsDecision = "ClosedRuntimeOwnedMatrixTileVlmRows";
    public const string FeatureGateDecision = "XMatrixExtensionLegalityGateSelected";
    public const string ElementWidthDecision = "MatrixTileElementWidths1_2_4_8Legal";
    public const string TileShapeDecision = "CanonicalTileDescriptorRowsColumnsStrideContourSelected";
    public const string MemoryLayoutDecision = "RowMajorTileMemoryLayoutContourSelected";
    public const string ReservedDisabledDecision = "ReservedDisabledMatrixTileVlmContoursFailClosed";
    public const string DescriptorBackedDecision = "DescriptorBackedMatrixTileContourIsDescriptorOnly";
    public const string MetadataNonAuthorityDecision = "ClassifierAndOptionalDisabledMetadataAreNotVlmAuthority";
    public const string DecoderAdmissionDecision = "DecoderAdmissionOwnedByPhase06DecoderEncoderAbiNotVlmRows";
    public const string FallbackDecision = "NoScalarVectorDscLane7VmxOrBackendFallbackAuthority";

    public const bool HasFeatureGate = true;
    public const bool HasElementWidthLegality = true;
    public const bool HasTileShapeLegality = true;
    public const bool HasMemoryLayoutLegality = true;
    public const bool HasReservedDisabledRows = true;
    public const bool KeepsClassifierMetadataNonAuthority = true;
    public const bool KeepsOptionalDisabledStatusNonAuthority = true;
    public const bool KeepsDecoderAdmissionBlocked = true;
    public const bool KeepsCompilerEmissionIndependent = true;
    public const bool KeepsCompilerHandoffBlocked = true;
    public const bool OpensDecoderAdmission = false;
    public const bool OpensExecution = false;
    public const bool UsesFallbackPath = false;

    public static InstructionsEnum[] Opcodes { get; } =
    [
        InstructionsEnum.MTILE_LOAD,
        InstructionsEnum.MTILE_STORE,
        InstructionsEnum.MTILE_MACC,
        InstructionsEnum.MTRANSPOSE,
    ];

    public static bool HasRuntimeOwnedVlmRows => AllRowsSatisfy(IsRuntimeOwnedMatrixTileRow);

    public static bool HasMatrixTileVlmFamily => AllRowsSatisfy(
        static row => row.FamilyName == MatrixTileVlmFamily);

    public static bool HasDescriptorBackedOnlyContour => AllRowsSatisfy(
        static row => row.GetContourStatus(VectorContourKind.DescriptorBacked) ==
                      VectorContourLegalityStatus.DescriptorOnly);

    public static bool KeepsNonDescriptorContoursFailClosed => AllRowsSatisfy(
        static row =>
            row.OneDimensional == VectorContourLegalityStatus.FailClosed &&
            row.IndexedAddressing == VectorContourLegalityStatus.FailClosed &&
            row.TwoDimensionalAddressing == VectorContourLegalityStatus.FailClosed &&
            row.Masked == VectorContourLegalityStatus.FailClosed &&
            row.TailMaskPolicy == VectorContourLegalityStatus.FailClosed &&
            row.Reduction == VectorContourLegalityStatus.FailClosed);

    public static bool KeepsExecutableContoursClosed => AllRowsSatisfy(
        static row =>
            row.OneDimensional != VectorContourLegalityStatus.Executable &&
            row.IndexedAddressing != VectorContourLegalityStatus.Executable &&
            row.TwoDimensionalAddressing != VectorContourLegalityStatus.Executable &&
            row.Masked != VectorContourLegalityStatus.Executable &&
            row.TailMaskPolicy != VectorContourLegalityStatus.Executable &&
            row.Reduction != VectorContourLegalityStatus.Executable &&
            row.DescriptorBacked != VectorContourLegalityStatus.Executable);

    public static bool IsMatrixTileOpcode(InstructionsEnum opcode) =>
        opcode is InstructionsEnum.MTILE_LOAD or
            InstructionsEnum.MTILE_STORE or
            InstructionsEnum.MTILE_MACC or
            InstructionsEnum.MTRANSPOSE;

    public static bool TryGetRuntimeOwnedRow(
        InstructionsEnum opcode,
        out VectorLegalityMatrixRow? row)
    {
        if (!IsMatrixTileOpcode(opcode))
        {
            row = null;
            return false;
        }

        return VectorLegalityMatrix.TryGetRow(opcode, out row) &&
               row.FamilyName == MatrixTileVlmFamily;
    }

    public static MatrixTileVlmLegalityValidationResult ValidateLoad(
        MatrixTileMemoryShapeContract contract)
    {
        if (!TryGetRuntimeOwnedRow(InstructionsEnum.MTILE_LOAD, out _))
        {
            return MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_LOAD,
                MatrixTileVlmLegalityFaultKind.MissingRuntimeOwnedVlmRow);
        }

        return MatrixTileMemoryShapeAndFaultAbi.Validate(contract).IsMemoryShapeAbiAccepted
            ? MatrixTileVlmLegalityValidationResult.Legal(InstructionsEnum.MTILE_LOAD)
            : MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_LOAD,
                MatrixTileVlmLegalityFaultKind.MemoryShapeFault);
    }

    public static MatrixTileVlmLegalityValidationResult ValidateStore(
        MatrixTileMemoryShapeContract contract)
    {
        if (!TryGetRuntimeOwnedRow(InstructionsEnum.MTILE_STORE, out _))
        {
            return MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_STORE,
                MatrixTileVlmLegalityFaultKind.MissingRuntimeOwnedVlmRow);
        }

        return MatrixTileMemoryShapeAndFaultAbi.Validate(contract).IsMemoryShapeAbiAccepted
            ? MatrixTileVlmLegalityValidationResult.Legal(InstructionsEnum.MTILE_STORE)
            : MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_STORE,
                MatrixTileVlmLegalityFaultKind.MemoryShapeFault);
    }

    public static MatrixTileVlmLegalityValidationResult ValidateMacc(
        MatrixTileMaccSemanticContract contract)
    {
        if (!TryGetRuntimeOwnedRow(InstructionsEnum.MTILE_MACC, out _))
        {
            return MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_MACC,
                MatrixTileVlmLegalityFaultKind.MissingRuntimeOwnedVlmRow);
        }

        return MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(contract).IsSemanticAbiAccepted
            ? MatrixTileVlmLegalityValidationResult.Legal(InstructionsEnum.MTILE_MACC)
            : MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTILE_MACC,
                MatrixTileVlmLegalityFaultKind.MaccSemanticFault);
    }

    public static MatrixTileVlmLegalityValidationResult ValidateTranspose(
        MatrixTileTransposeSemanticContract contract)
    {
        if (!TryGetRuntimeOwnedRow(InstructionsEnum.MTRANSPOSE, out _))
        {
            return MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTRANSPOSE,
                MatrixTileVlmLegalityFaultKind.MissingRuntimeOwnedVlmRow);
        }

        return MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(contract).IsSemanticAbiAccepted
            ? MatrixTileVlmLegalityValidationResult.Legal(InstructionsEnum.MTRANSPOSE)
            : MatrixTileVlmLegalityValidationResult.Fault(
                InstructionsEnum.MTRANSPOSE,
                MatrixTileVlmLegalityFaultKind.TransposeSemanticFault);
    }

    private static bool IsRuntimeOwnedMatrixTileRow(VectorLegalityMatrixRow row) =>
        row.FamilyName == MatrixTileVlmFamily &&
        row.GetContourStatus(VectorContourKind.DescriptorBacked) ==
        VectorContourLegalityStatus.DescriptorOnly;

    private static bool AllRowsSatisfy(System.Func<VectorLegalityMatrixRow, bool> predicate)
    {
        foreach (InstructionsEnum opcode in Opcodes)
        {
            if (!TryGetRuntimeOwnedRow(opcode, out VectorLegalityMatrixRow? row) ||
                !predicate(row))
            {
                return false;
            }
        }

        return true;
    }
}
