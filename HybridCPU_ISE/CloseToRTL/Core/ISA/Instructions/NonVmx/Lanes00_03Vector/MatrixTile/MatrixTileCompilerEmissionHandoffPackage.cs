using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public readonly record struct MatrixTileCompilerEmissionHandoffRow(
    string Mnemonic,
    InstructionsEnum Opcode,
    IsaInstructionStatus RuntimeStatus,
    RuntimeInstructionEvidence RuntimeEvidence,
    MatrixTileRuntimeResourceClass RuntimeResourceClass,
    SlotClass RequiredSlotClass,
    byte PhysicalLaneMask,
    string RuntimePublicationKind,
    bool HasRuntimeExecutableAuthority,
    bool RequiresDedicatedCompilerImplementation,
    bool HasCurrentCompilerHelper,
    bool HasCurrentCompilerEmission);

public static class MatrixTileCompilerEmissionHandoffPackage
{
    public const string HandoffDecision = "ClosedPhase14PositiveCompilerEmissionHandoffPackage";
    public const string StatusCatalogDecision = "ClosedOptionalEnabledConformanceTestedStatusCatalogPromotion";
    public const string RuntimeAuthorityDecision = "RuntimeOwnedLegalityAndRetirePublicationRemainFinalIsaAuthority";
    public const string CompilerBoundaryDecision = "RuntimeHandoffConsumedByExistingPositiveCompilerImplementationCompilerCodeUnchangedByPhase14";
    public const string NoFallbackDecision = "CompilerMustTargetTypedMatrixTileRuntimePathWithoutFallbackOrHiddenLowering";

    public const bool HasPositiveStatusCatalogPromotion = true;
    public const bool HasPositiveCompilerEmissionHandoffPackage = true;
    public const bool RuntimeAuthorityReadyForSeparateCompilerImplementation = true;
    public const bool CurrentCompilerImplementationExists = true;
    public const bool CurrentCompilerHelperExists = true;
    public const bool CurrentCompilerEmissionExists = true;
    public const bool ModifiesCompilerCode = false;
    public const bool AllowsCompilerToOverrideRuntimeLegality = false;
    public const bool AllowsFallbackOrHiddenLowering = false;

    private static readonly MatrixTileCompilerEmissionHandoffRow[] RowTable =
    [
        Create("MTILE_LOAD", InstructionsEnum.MTILE_LOAD, "RetireOwnedTilePublication"),
        Create("MTILE_STORE", InstructionsEnum.MTILE_STORE, "RetireOwnedAllOrNoneMemoryCommit"),
        Create("MTILE_MACC", InstructionsEnum.MTILE_MACC, "RetireOwnedAccumulatorPublication"),
        Create("MTRANSPOSE", InstructionsEnum.MTRANSPOSE, "RetireOwnedDestinationTilePublication")
    ];

    public static MatrixTileCompilerEmissionHandoffRow[] Rows =>
        (MatrixTileCompilerEmissionHandoffRow[])RowTable.Clone();

    public static MatrixTileCompilerEmissionHandoffRow GetRow(string mnemonic)
    {
        foreach (MatrixTileCompilerEmissionHandoffRow row in RowTable)
        {
            if (string.Equals(row.Mnemonic, mnemonic, StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(mnemonic),
            mnemonic,
            "Unknown matrix/tile compiler handoff row.");
    }

    public static void RequireRuntimeExecutableAuthority(string mnemonic)
    {
        MatrixTileCompilerEmissionHandoffRow row = GetRow(mnemonic);
        if (!row.HasRuntimeExecutableAuthority ||
            row.RuntimeStatus != IsaInstructionStatus.OptionalEnabled ||
            row.RuntimeEvidence != RuntimeInstructionEvidence.ConformanceTested)
        {
            throw new InvalidOperationException(
                $"{row.Mnemonic} does not have complete runtime executable authority.");
        }
    }

    private static MatrixTileCompilerEmissionHandoffRow Create(
        string mnemonic,
        InstructionsEnum opcode,
        string runtimePublicationKind) =>
        new(
            mnemonic,
            opcode,
            IsaInstructionStatus.OptionalEnabled,
            RuntimeInstructionEvidence.ConformanceTested,
            MatrixTileResourceContour.Classify((uint)opcode),
            MatrixTileResourceContour.ResolveSlotClass(
                MatrixTileResourceContour.Classify((uint)opcode)),
            SlotClassLaneMap.GetLaneMask(
                MatrixTileResourceContour.ResolveSlotClass(
                    MatrixTileResourceContour.Classify((uint)opcode))),
            runtimePublicationKind,
            HasRuntimeExecutableAuthority: true,
            RequiresDedicatedCompilerImplementation: true,
            HasCurrentCompilerHelper: true,
            HasCurrentCompilerEmission: true);
}
