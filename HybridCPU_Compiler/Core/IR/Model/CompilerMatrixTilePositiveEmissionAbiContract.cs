using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR;

public enum CompilerMatrixTilePositiveEmissionKind : byte
{
    MtileLoad = 0,
    MtileStore = 1,
    MtileMacc = 2,
    Mtranspose = 3
}

public readonly record struct CompilerMatrixTileTileOperand(ushort TileId)
{
    public static CompilerMatrixTileTileOperand Create(ushort tileId) => new(tileId);
}

public readonly record struct CompilerMatrixTileDescriptorAbi(
    MatrixTileCanonicalDescriptorAbi CanonicalDescriptor,
    DataTypeEnum ElementType)
{
    public static CompilerMatrixTileDescriptorAbi Create(
        ushort rows,
        ushort columns,
        DataTypeEnum elementType,
        uint strideBytes = 0)
    {
        if (!DataTypeUtils.IsValid(elementType))
        {
            throw new ArgumentOutOfRangeException(nameof(elementType), elementType, "Unknown MTILE element data type.");
        }

        ushort elementSizeBytes = checked((ushort)DataTypeUtils.SizeOf(elementType));
        uint effectiveStride = strideBytes == 0
            ? checked((uint)columns * elementSizeBytes)
            : strideBytes;
        return new CompilerMatrixTileDescriptorAbi(
            MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                columns,
                elementSizeBytes,
                effectiveStride),
            elementType);
    }

    public void Validate(string parameterName)
    {
        if (!DataTypeUtils.IsValid(ElementType))
        {
            throw new ArgumentOutOfRangeException(parameterName, ElementType, "Unknown MTILE element data type.");
        }

        if (!CanonicalDescriptor.IsCanonical)
        {
            throw new ArgumentException(
                "MTILE compiler emission requires a canonical runtime tile descriptor.",
                parameterName);
        }

        ushort elementSizeBytes = checked((ushort)DataTypeUtils.SizeOf(ElementType));
        if (CanonicalDescriptor.ElementSizeBytes != elementSizeBytes)
        {
            throw new ArgumentException(
                "MTILE compiler emission requires descriptor element size to match the encoded data type.",
                parameterName);
        }
    }
}

public readonly record struct CompilerMatrixTileMemoryFaultAbiInputs(
    ulong BaseAddress,
    ushort PageSizeBytes)
{
    public static CompilerMatrixTileMemoryFaultAbiInputs Create(
        ulong baseAddress,
        ushort pageSizeBytes = MatrixTileMemoryShapeAndFaultAbi.DefaultPageSizeBytes) =>
        new(baseAddress, pageSizeBytes);
}

public readonly record struct CompilerMatrixTileAccumulatorPolicyAbi(
    MatrixTileCanonicalDescriptorAbi RightSourceDescriptor,
    MatrixTileCanonicalDescriptorAbi AccumulatorDescriptor,
    MatrixTileNumericElementKind ElementKind,
    MatrixTileAccumulatorPolicyKind AccumulatorPolicy)
{
    public static CompilerMatrixTileAccumulatorPolicyAbi CreateForRuntimeDerivedFootprint(
        MatrixTileCanonicalDescriptorAbi leftSourceDescriptor,
        MatrixTileNumericElementKind elementKind = MatrixTileNumericElementKind.SignedInteger)
    {
        ushort accumulatorElementSize =
            MatrixTileAccumulatorAndTransposePolicyAbi.GetAccumulatorElementSizeBytes(
                leftSourceDescriptor.ElementSizeBytes);
        MatrixTileCanonicalDescriptorAbi rightDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                leftSourceDescriptor.Columns,
                leftSourceDescriptor.Rows,
                leftSourceDescriptor.ElementSizeBytes,
                checked((uint)leftSourceDescriptor.Rows * leftSourceDescriptor.ElementSizeBytes));
        MatrixTileCanonicalDescriptorAbi accumulatorDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                leftSourceDescriptor.Rows,
                rightDescriptor.Columns,
                accumulatorElementSize,
                checked((uint)rightDescriptor.Columns * accumulatorElementSize));

        return new CompilerMatrixTileAccumulatorPolicyAbi(
            rightDescriptor,
            accumulatorDescriptor,
            elementKind,
            MatrixTileAccumulatorPolicyKind.WideningIntegerAccumulatorWithOverflowTrap);
    }
}

public readonly record struct CompilerMatrixTileTransposePolicyAbi(
    MatrixTileCanonicalDescriptorAbi DestinationDescriptor,
    MatrixTileTransposeAliasPolicyKind AliasPolicy)
{
    public static CompilerMatrixTileTransposePolicyAbi CreateForRuntimeDerivedDestination(
        MatrixTileCanonicalDescriptorAbi sourceDescriptor)
    {
        MatrixTileCanonicalDescriptorAbi destinationDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                sourceDescriptor.Columns,
                sourceDescriptor.Rows,
                sourceDescriptor.ElementSizeBytes,
                checked((uint)sourceDescriptor.Rows * sourceDescriptor.ElementSizeBytes));

        return new CompilerMatrixTileTransposePolicyAbi(
            destinationDescriptor,
            MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly);
    }
}

public sealed record CompilerMatrixTileEmissionRequest
{
    private CompilerMatrixTileEmissionRequest(
        CompilerMatrixTilePositiveEmissionKind kind,
        CompilerMatrixTileDescriptorAbi descriptor,
        CompilerMatrixTileTileOperand primaryTile,
        CompilerMatrixTileTileOperand secondaryTile,
        CompilerMatrixTileTileOperand destinationTile,
        CompilerMatrixTileMemoryFaultAbiInputs? memoryFaultAbi,
        CompilerMatrixTileAccumulatorPolicyAbi? accumulatorPolicyAbi,
        CompilerMatrixTileTransposePolicyAbi? transposePolicyAbi)
    {
        Kind = kind;
        Descriptor = descriptor;
        PrimaryTile = primaryTile;
        SecondaryTile = secondaryTile;
        DestinationTile = destinationTile;
        MemoryFaultAbi = memoryFaultAbi;
        AccumulatorPolicyAbi = accumulatorPolicyAbi;
        TransposePolicyAbi = transposePolicyAbi;
    }

    public CompilerMatrixTilePositiveEmissionKind Kind { get; }

    public CompilerMatrixTileDescriptorAbi Descriptor { get; }

    public CompilerMatrixTileTileOperand PrimaryTile { get; }

    public CompilerMatrixTileTileOperand SecondaryTile { get; }

    public CompilerMatrixTileTileOperand DestinationTile { get; }

    public CompilerMatrixTileMemoryFaultAbiInputs? MemoryFaultAbi { get; }

    public CompilerMatrixTileAccumulatorPolicyAbi? AccumulatorPolicyAbi { get; }

    public CompilerMatrixTileTransposePolicyAbi? TransposePolicyAbi { get; }

    public InstructionsEnum Opcode => CompilerMatrixTilePositiveEmissionAbiContract.GetOpcode(Kind);

    public string Mnemonic => CompilerMatrixTilePositiveEmissionAbiContract.GetMnemonic(Kind);

    public static CompilerMatrixTileEmissionRequest MtileLoad(
        CompilerMatrixTileTileOperand destinationTile,
        CompilerMatrixTileDescriptorAbi descriptor,
        CompilerMatrixTileMemoryFaultAbiInputs memoryFaultAbi) =>
        new(
            CompilerMatrixTilePositiveEmissionKind.MtileLoad,
            descriptor,
            primaryTile: default,
            secondaryTile: destinationTile,
            destinationTile,
            memoryFaultAbi,
            accumulatorPolicyAbi: null,
            transposePolicyAbi: null);

    public static CompilerMatrixTileEmissionRequest MtileStore(
        CompilerMatrixTileTileOperand sourceTile,
        CompilerMatrixTileDescriptorAbi descriptor,
        CompilerMatrixTileMemoryFaultAbiInputs memoryFaultAbi) =>
        new(
            CompilerMatrixTilePositiveEmissionKind.MtileStore,
            descriptor,
            primaryTile: sourceTile,
            secondaryTile: sourceTile,
            destinationTile: default,
            memoryFaultAbi,
            accumulatorPolicyAbi: null,
            transposePolicyAbi: null);

    public static CompilerMatrixTileEmissionRequest MtileMacc(
        CompilerMatrixTileTileOperand leftSourceTile,
        CompilerMatrixTileTileOperand rightSourceTile,
        CompilerMatrixTileTileOperand accumulatorTile,
        CompilerMatrixTileDescriptorAbi leftSourceDescriptor,
        CompilerMatrixTileAccumulatorPolicyAbi accumulatorPolicyAbi) =>
        new(
            CompilerMatrixTilePositiveEmissionKind.MtileMacc,
            leftSourceDescriptor,
            primaryTile: leftSourceTile,
            secondaryTile: rightSourceTile,
            destinationTile: accumulatorTile,
            memoryFaultAbi: null,
            accumulatorPolicyAbi,
            transposePolicyAbi: null);

    public static CompilerMatrixTileEmissionRequest Mtranspose(
        CompilerMatrixTileTileOperand sourceTile,
        CompilerMatrixTileTileOperand destinationTile,
        CompilerMatrixTileDescriptorAbi sourceDescriptor,
        CompilerMatrixTileTransposePolicyAbi transposePolicyAbi) =>
        new(
            CompilerMatrixTilePositiveEmissionKind.Mtranspose,
            sourceDescriptor,
            primaryTile: sourceTile,
            secondaryTile: destinationTile,
            destinationTile,
            memoryFaultAbi: null,
            accumulatorPolicyAbi: null,
            transposePolicyAbi);
}

public sealed record CompilerMatrixTileEmissionPlan(
    CompilerMatrixTileEmissionRequest Request,
    MatrixTileCompilerEmissionHandoffRow RuntimeHandoffRow,
    VLIW_Instruction EncodedInstruction,
    MatrixTileInstructionIrProjection RuntimeProjection,
    MatrixTileMaterializedInstruction RuntimeMaterialization,
    bool UsesFallbackPath,
    bool UsesAliasPromotion,
    bool UsesScalarVectorDotOrBackendFallback)
{
    public MatrixTileMemoryShapeValidationResult? MemoryValidation =>
        RuntimeProjection.MemoryValidation;

    public MatrixTileSemanticValidationResult? SemanticValidation =>
        RuntimeProjection.SemanticValidation;
}

public readonly record struct CompilerMatrixTilePositiveEmissionRow(
    string Mnemonic,
    InstructionsEnum Opcode,
    ushort NumericOpcode,
    CompilerMatrixTilePositiveEmissionKind Kind,
    string HelperName,
    string RequiredTypedOperandContract,
    bool UsesPhase13RuntimeHandoff,
    bool RuntimeOwnedLegalityIsFinal,
    bool EmitsDirectMatrixTileOpcode,
    bool UsesFallbackPath,
    bool UsesAliasPromotion);

public static class CompilerMatrixTilePositiveEmissionAbiContract
{
    private static readonly CompilerMatrixTilePositiveEmissionRow[] RowTable =
    [
        Create(
            "MTILE_LOAD",
            InstructionsEnum.MTILE_LOAD,
            CompilerMatrixTilePositiveEmissionKind.MtileLoad,
            nameof(HybridCpuThreadCompilerContext.CompileMtileLoad),
            "tile destination, canonical tile descriptor, tile memory/fault ABI inputs"),
        Create(
            "MTILE_STORE",
            InstructionsEnum.MTILE_STORE,
            CompilerMatrixTilePositiveEmissionKind.MtileStore,
            nameof(HybridCpuThreadCompilerContext.CompileMtileStore),
            "tile source, canonical tile descriptor, tile memory/fault ABI inputs"),
        Create(
            "MTILE_MACC",
            InstructionsEnum.MTILE_MACC,
            CompilerMatrixTilePositiveEmissionKind.MtileMacc,
            nameof(HybridCpuThreadCompilerContext.CompileMtileMacc),
            "source tiles, accumulator tile/result footprint, accumulator policy ABI"),
        Create(
            "MTRANSPOSE",
            InstructionsEnum.MTRANSPOSE,
            CompilerMatrixTilePositiveEmissionKind.Mtranspose,
            nameof(HybridCpuThreadCompilerContext.CompileMtranspose),
            "source/destination tile operands, transpose policy ABI")
    ];

    public const string CompilerPositiveEmissionDecision = "CompilerOwnedMatrixTilePositiveEmissionOpenedFromPhase13Handoff";
    public const string RuntimeHandoffAuthorityDecision = "Phase13RuntimeIsaHandoffPackageIsOpcodeAndLegalityAuthority";
    public const string LegacyOptionalDisabledBoundaryDecision = "LegacyOptionalDisabledCompilerMatrixTileContractIsNotPositiveHelperAuthority";
    public const string NoFallbackDecision = "DirectMatrixTileEmissionNoScalarVectorDotLane6Lane7VmxOrBackendFallback";

    public const bool HasCurrentCompilerImplementation = true;
    public const bool HasCurrentCompilerHelper = true;
    public const bool HasCurrentCompilerEmission = true;
    public const bool UsesPhase13RuntimeHandoff = true;
    public const bool RuntimeOwnedLegalityIsFinal = true;
    public const bool AllowsCompilerToOverrideRuntimeLegality = false;
    public const bool UsesOldOptionalDisabledMetadataAsAuthority = false;
    public const bool UsesFallbackPath = false;
    public const bool UsesAliasPromotion = false;
    public const bool EmitsDirectMatrixTileOpcodes = true;

    public static IReadOnlyList<CompilerMatrixTilePositiveEmissionRow> Rows => RowTable;

    public static IReadOnlySet<string> PublicHelperNames { get; } =
        new HashSet<string>(
            RowTable.Select(static row => row.HelperName)
                .Concat(
                [
                    nameof(IAppAsmFacade.MtileLoad),
                    nameof(IAppAsmFacade.MtileStore),
                    nameof(IAppAsmFacade.MtileMacc),
                    nameof(IAppAsmFacade.Mtranspose)
                ]),
            StringComparer.Ordinal);

    public static bool IsMatrixTilePositiveOpcode(uint opCode) =>
        opCode <= ushort.MaxValue &&
        Enum.IsDefined(typeof(InstructionsEnum), (ushort)opCode) &&
        IsMatrixTilePositiveOpcode((InstructionsEnum)opCode);

    public static bool IsMatrixTilePositiveOpcode(InstructionsEnum opcode) =>
        opcode is InstructionsEnum.MTILE_LOAD or
            InstructionsEnum.MTILE_STORE or
            InstructionsEnum.MTILE_MACC or
            InstructionsEnum.MTRANSPOSE;

    public static InstructionsEnum GetOpcode(CompilerMatrixTilePositiveEmissionKind kind) =>
        kind switch
        {
            CompilerMatrixTilePositiveEmissionKind.MtileLoad => InstructionsEnum.MTILE_LOAD,
            CompilerMatrixTilePositiveEmissionKind.MtileStore => InstructionsEnum.MTILE_STORE,
            CompilerMatrixTilePositiveEmissionKind.MtileMacc => InstructionsEnum.MTILE_MACC,
            CompilerMatrixTilePositiveEmissionKind.Mtranspose => InstructionsEnum.MTRANSPOSE,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MTILE compiler helper kind.")
        };

    public static string GetMnemonic(CompilerMatrixTilePositiveEmissionKind kind) =>
        kind switch
        {
            CompilerMatrixTilePositiveEmissionKind.MtileLoad => "MTILE_LOAD",
            CompilerMatrixTilePositiveEmissionKind.MtileStore => "MTILE_STORE",
            CompilerMatrixTilePositiveEmissionKind.MtileMacc => "MTILE_MACC",
            CompilerMatrixTilePositiveEmissionKind.Mtranspose => "MTRANSPOSE",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MTILE compiler helper kind.")
        };

    public static CompilerMatrixTilePositiveEmissionRow GetRow(string mnemonic)
    {
        foreach (CompilerMatrixTilePositiveEmissionRow row in RowTable)
        {
            if (string.Equals(row.Mnemonic, mnemonic, StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(mnemonic), mnemonic, "Unknown MTILE compiler emission row.");
    }

    public static void RequireRuntimeHandoffAuthority(string mnemonic)
    {
        MatrixTileRuntimeIsaPackageContract.RequirePositiveCompilerEmissionReadiness();
        MatrixTileCompilerEmissionHandoffPackage.RequireRuntimeExecutableAuthority(mnemonic);
    }

    private static CompilerMatrixTilePositiveEmissionRow Create(
        string mnemonic,
        InstructionsEnum opcode,
        CompilerMatrixTilePositiveEmissionKind kind,
        string helperName,
        string requiredTypedOperandContract)
    {
        return new CompilerMatrixTilePositiveEmissionRow(
            mnemonic,
            opcode,
            checked((ushort)opcode),
            kind,
            helperName,
            requiredTypedOperandContract,
            UsesPhase13RuntimeHandoff: true,
            RuntimeOwnedLegalityIsFinal: true,
            EmitsDirectMatrixTileOpcode: true,
            UsesFallbackPath: false,
            UsesAliasPromotion: false);
    }
}
