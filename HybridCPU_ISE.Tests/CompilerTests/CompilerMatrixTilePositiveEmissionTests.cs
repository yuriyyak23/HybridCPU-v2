using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerMatrixTilePositiveEmissionTests
{
    private static readonly CompilerMatrixTileDescriptorAbi Tile2X2I8 =
        new(MatrixTileCanonicalDescriptorAbi.Create(2, 2, 1, 2), DataTypeEnum.INT8);

    public static IEnumerable<object[]> ExecutionGoldenVectors()
    {
        MatrixTileExecutionGoldenVector[] vectors =
            MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors.ToArray();
        foreach (MatrixTileExecutionGoldenVector vector in vectors)
        {
            yield return [vector];
        }
    }

    [Theory]
    [MemberData(nameof(ExecutionGoldenVectors))]
    public void FacadeHelpers_EmitDirectMatrixTileGoldenCarrierThroughCompilerIrAndRuntimeProjection(
        MatrixTileExecutionGoldenVector vector)
    {
        var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        EmitGoldenVector(facade, vector);
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        AssertCarrier(vector.Carrier, raw);
        Assert.Equal(vector.Opcode, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT8, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.True(raw.Is2D);
        Assert.False(raw.Indexed);
        Assert.False(raw.Reduction);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(vector.Opcode, ir.Opcode);
        Assert.NotNull(ir.MatrixTileEmission);
        CompilerMatrixTileEmissionPlan plan = ir.MatrixTileEmission!;
        Assert.Equal(vector.Opcode, plan.Request.Opcode);
        Assert.Equal(vector.OperationKind, plan.RuntimeProjection.OperationKind);
        Assert.Equal(vector.OperationKind, plan.RuntimeMaterialization.OperationKind);
        Assert.False(plan.UsesFallbackPath);
        Assert.False(plan.UsesAliasPromotion);
        Assert.False(plan.UsesScalarVectorDotOrBackendFallback);
        Assert.Equal(
            MatrixTileCompilerEmissionHandoffPackage.GetRow(vector.Opcode.ToString()).Opcode,
            plan.RuntimeHandoffRow.Opcode);
        AssertCarrier(vector.Carrier, plan.EncodedInstruction);
        Assert.Equal(vector.TileDescriptor, plan.Request.Descriptor.CanonicalDescriptor);
        Assert.Contains(ir.Operands, operand => operand.Kind == IrOperandKind.Tile);
        Assert.Contains(ir.Operands, operand => operand.Name == "tileColumns" && operand.Value == vector.TileDescriptor.Columns);
        Assert.Contains(ir.Operands, operand => operand.Name == "tileRowStrideBytes" && operand.Value == vector.TileDescriptor.StrideBytes);
        AssertMatrixTileOperandContract(ir, vector);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        AssertCarrier(vector.Carrier, lowered);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);
        MatrixTileMicroOp tileMicroOp = Assert.IsAssignableFrom<MatrixTileMicroOp>(carrier);
        Assert.Equal(vector.Opcode, (InstructionsEnum)tileMicroOp.OpCode);
        Assert.Equal(vector.OperationKind, tileMicroOp.OperationKind);
        Assert.Equal(vector.TileDescriptor, tileMicroOp.TileDescriptor);
        Assert.Equal(vector.ResultTileDescriptor, tileMicroOp.ResultTileDescriptor);
        Assert.False(tileMicroOp.UsesFallbackPath);
        Assert.True(tileMicroOp.PublishesTypedTileMicroOp);
    }

    [Fact]
    public void MalformedHelperInputs_FailClosedBeforeEmission()
    {
        var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
#pragma warning restore CS0618

        var nonCanonicalDescriptor =
            new CompilerMatrixTileDescriptorAbi(
                MatrixTileCanonicalDescriptorAbi.Create(0, 2, 1, 2),
                DataTypeEnum.INT8);
        Assert.Throws<ArgumentException>(() =>
            facade.MtileLoad(
                CompilerMatrixTileTileOperand.Create(1),
                nonCanonicalDescriptor,
                CompilerMatrixTileMemoryFaultAbiInputs.Create(0x100)));
        Assert.Equal(0, context.InstructionCount);

        Assert.Throws<ArgumentException>(() =>
            facade.MtileLoad(
                CompilerMatrixTileTileOperand.Create(1),
                Tile2X2I8,
                CompilerMatrixTileMemoryFaultAbiInputs.Create(0x100, pageSizeBytes: 2048)));
        Assert.Equal(0, context.InstructionCount);

        var malformedAccumulatorPolicy =
            new CompilerMatrixTileAccumulatorPolicyAbi(
                Tile2X2I8.CanonicalDescriptor,
                Tile2X2I8.CanonicalDescriptor,
                MatrixTileNumericElementKind.SignedInteger,
                MatrixTileAccumulatorPolicyKind.WideningIntegerAccumulatorWithOverflowTrap);
        Assert.Throws<ArgumentException>(() =>
            facade.MtileMacc(
                CompilerMatrixTileTileOperand.Create(1),
                CompilerMatrixTileTileOperand.Create(2),
                CompilerMatrixTileTileOperand.Create(3),
                Tile2X2I8,
                malformedAccumulatorPolicy));
        Assert.Equal(0, context.InstructionCount);

        var twoByThree =
            new CompilerMatrixTileDescriptorAbi(
                MatrixTileCanonicalDescriptorAbi.Create(2, 3, 1, 3),
                DataTypeEnum.INT8);
        var inPlaceNonSquareTranspose =
            new CompilerMatrixTileTransposePolicyAbi(
                MatrixTileCanonicalDescriptorAbi.Create(3, 2, 1, 2),
                MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly);
        Assert.Throws<ArgumentException>(() =>
            facade.Mtranspose(
                CompilerMatrixTileTileOperand.Create(1),
                CompilerMatrixTileTileOperand.Create(1),
                twoByThree,
                inPlaceNonSquareTranspose));
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void RawMatrixTileIngress_IsRejectedInFavorOfTypedHelperAbi()
    {
        var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);

        InvalidOperationException directException = Assert.Throws<InvalidOperationException>(() =>
            context.CompileInstruction(
                (uint)InstructionsEnum.MTILE_LOAD,
                (byte)DataTypeEnum.INT8,
                predicate: 0,
                immediate: 2,
                destSrc1: 0x100,
                src2: 2,
                streamLength: 4,
                stride: 1,
                stealabilityPolicy: StealabilityPolicy.NotStealable));
        Assert.Contains("typed matrix/tile helper ABI", directException.Message, StringComparison.Ordinal);
        Assert.Equal(0, context.InstructionCount);

#pragma warning disable CS0618
        var expert = new ExpertBackendFacade(0, context);
#pragma warning restore CS0618
        InvalidOperationException expertException = Assert.Throws<InvalidOperationException>(() =>
            expert.EmitRawInstruction(
                (uint)InstructionsEnum.MTILE_STORE,
                (byte)DataTypeEnum.INT8,
                predicate: 0,
                immediate: 2,
                destSrc1: 0x180,
                src2: 2,
                streamLength: 4,
                stride: 1));
        Assert.Contains("typed matrix/tile helper ABI", expertException.Message, StringComparison.Ordinal);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void MatrixTileCompilerOwnedSources_DoNotReferenceFallbackOrAliasOpcodeFamilies()
    {
        string source = ReadCompilerOwnedMatrixTileSource();
        string[] forbiddenFragments =
        [
            "InstructionsEnum.VLOAD",
            "InstructionsEnum.VSTORE",
            "InstructionsEnum.VDOT",
            "InstructionsEnum.VTRANSPOSE",
            "InstructionsEnum.FMAC",
            "InstructionsEnum.DmaStreamCompute",
            "CompileVector",
            "CompileVdot",
            "CompileDmaStreamCompute",
            "CompileAccelerator",
            "EmitRawInstruction(",
            "ExpertBackendFacade",
            "ExternalBackend",
            ".ISA.Instructions.Vmx."
        ];

        foreach (string forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(forbiddenFragment, source, StringComparison.Ordinal);
        }

        Assert.Contains(nameof(HybridCpuThreadCompilerContext.CompileMtileLoad), source);
        Assert.Contains(nameof(HybridCpuThreadCompilerContext.CompileMtileStore), source);
        Assert.Contains(nameof(HybridCpuThreadCompilerContext.CompileMtileMacc), source);
        Assert.Contains(nameof(HybridCpuThreadCompilerContext.CompileMtranspose), source);
        Assert.Contains(CompilerMatrixTilePositiveEmissionAbiContract.NoFallbackDecision, source);
    }

    private static void EmitGoldenVector(
        AppAsmFacade facade,
        MatrixTileExecutionGoldenVector vector)
    {
        CompilerMatrixTileDescriptorAbi descriptor =
            new(vector.TileDescriptor, DataTypeEnum.INT8);
        switch (vector.Opcode)
        {
            case InstructionsEnum.MTILE_LOAD:
                facade.MtileLoad(
                    CompilerMatrixTileTileOperand.Create(vector.DestinationTileId),
                    descriptor,
                    CompilerMatrixTileMemoryFaultAbiInputs.Create(vector.Carrier.Word1));
                break;
            case InstructionsEnum.MTILE_STORE:
                facade.MtileStore(
                    CompilerMatrixTileTileOperand.Create(vector.SourceTileId),
                    descriptor,
                    CompilerMatrixTileMemoryFaultAbiInputs.Create(vector.Carrier.Word1));
                break;
            case InstructionsEnum.MTILE_MACC:
                facade.MtileMacc(
                    CompilerMatrixTileTileOperand.Create(vector.SourceTileId),
                    CompilerMatrixTileTileOperand.Create(vector.SecondaryTileId),
                    CompilerMatrixTileTileOperand.Create(vector.DestinationTileId),
                    descriptor,
                    new CompilerMatrixTileAccumulatorPolicyAbi(
                        vector.SecondaryTileDescriptor,
                        vector.ResultTileDescriptor,
                        MatrixTileNumericElementKind.SignedInteger,
                        MatrixTileAccumulatorPolicyKind.WideningIntegerAccumulatorWithOverflowTrap));
                break;
            case InstructionsEnum.MTRANSPOSE:
                facade.Mtranspose(
                    CompilerMatrixTileTileOperand.Create(vector.SourceTileId),
                    CompilerMatrixTileTileOperand.Create(vector.DestinationTileId),
                    descriptor,
                    new CompilerMatrixTileTransposePolicyAbi(
                        vector.ResultTileDescriptor,
                        MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(vector), vector.Opcode, "Unsupported MTILE golden vector.");
        }
    }

    private static void AssertMatrixTileOperandContract(
        IrInstruction ir,
        MatrixTileExecutionGoldenVector vector)
    {
        switch (vector.Opcode)
        {
            case InstructionsEnum.MTILE_LOAD:
                Assert.Contains(ir.Operands, operand => operand.Kind == IrOperandKind.MemoryAddress && operand.Name == "tileMemoryBase");
                Assert.Contains(ir.Annotation.Defs, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileDestination");
                Assert.Contains(ir.Annotation.Uses, operand => operand.Kind == IrOperandKind.MemoryAddress && operand.Name == "tileMemoryBase");
                Assert.Null(ir.Annotation.MemoryWriteRegion);
                Assert.NotNull(ir.Annotation.MemoryReadRegion);
                break;
            case InstructionsEnum.MTILE_STORE:
                Assert.Contains(ir.Operands, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileSource");
                Assert.Empty(ir.Annotation.Defs);
                Assert.Contains(ir.Annotation.Uses, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileSource");
                Assert.Null(ir.Annotation.MemoryReadRegion);
                Assert.NotNull(ir.Annotation.MemoryWriteRegion);
                break;
            case InstructionsEnum.MTILE_MACC:
                Assert.Contains(ir.Annotation.Defs, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileAccumulator");
                Assert.Contains(ir.Annotation.Uses, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileLeftSource");
                Assert.Contains(ir.Annotation.Uses, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileRightSource");
                Assert.Null(ir.Annotation.MemoryReadRegion);
                Assert.Null(ir.Annotation.MemoryWriteRegion);
                break;
            case InstructionsEnum.MTRANSPOSE:
                Assert.Contains(ir.Annotation.Defs, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileDestination");
                Assert.Contains(ir.Annotation.Uses, operand => operand.Kind == IrOperandKind.Tile && operand.Name == "tileSource");
                Assert.Null(ir.Annotation.MemoryReadRegion);
                Assert.Null(ir.Annotation.MemoryWriteRegion);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(vector), vector.Opcode, "Unsupported MTILE golden vector.");
        }
    }

    private static void AssertCarrier(
        MatrixTileGoldenCarrier expected,
        VLIW_Instruction actual)
    {
        Assert.Equal(expected.Word0, actual.Word0);
        Assert.Equal(expected.Word1, actual.Word1);
        Assert.Equal(expected.Word2, actual.Word2);
        Assert.Equal(expected.Word3, actual.Word3);
    }

    private static string ReadCompilerOwnedMatrixTileSource()
    {
        string[] ownedFileNames =
        [
            "CompilerMatrixTilePositiveEmissionAbiContract.cs",
            "CompilerMatrixTileEmissionLowerer.cs",
            "ThreadCompilerContext.MatrixTile.cs"
        ];

        return string.Join(
            Environment.NewLine,
            CompilerSourceScanner.EnumerateCompilerSourceFiles()
                .Where(path => ownedFileNames.Contains(Path.GetFileName(path), StringComparer.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
