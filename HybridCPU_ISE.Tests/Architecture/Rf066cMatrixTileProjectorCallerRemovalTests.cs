using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf066cMatrixTileProjectorCallerRemovalTests
{
    [Fact]
    public void MatrixTileHarnessCanonicalMaterialization_MatchesRemovedLegacyProjectorPath()
    {
        VLIW_Instruction[] slots = CreateLoadBundle();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            slots,
            bundleAddress: 0x6600,
            bundleSerial: 66);

        MicroOp?[] legacy =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(slots, decoded);
        MicroOp?[] canonical =
            MatrixTileFullPipelineHarness.BuildCanonicalMatrixTileCarrierBundle(decoded);

        MatrixTileMicroOp expected = Assert.IsType<MtileLoadMicroOp>(legacy[0]);
        MatrixTileMicroOp actual = Assert.IsType<MtileLoadMicroOp>(canonical[0]);
        Assert.Equal(expected.OpCode, actual.OpCode);
        Assert.Equal(expected.OperationKind, actual.OperationKind);
        Assert.Equal(expected.RuntimeResourceClass, actual.RuntimeResourceClass);
        Assert.Equal(expected.Placement, actual.Placement);
        Assert.Equal(expected.DependencyMetadata, actual.DependencyMetadata);
        Assert.Equal(expected.ReadMemoryRanges, actual.ReadMemoryRanges);
        Assert.Equal(expected.WriteMemoryRanges, actual.WriteMemoryRanges);
        Assert.All(canonical.Skip(1), Assert.Null);
    }

    [Fact]
    public void CanonicalMatrixTileMaterialization_FailsClosedOnBindingOpcodeSubstitution()
    {
        VLIW_Instruction[] slots = CreateLoadBundle();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            slots,
            bundleAddress: 0x6600,
            bundleSerial: 67);
        CanonicalDecodedInstruction canonical = decoded.CanonicalBundle!.GetSlot(0);
        GeneratedStaticBinding exact = Assert.IsType<GeneratedStaticBinding>(canonical.StaticBinding);
        CanonicalDecodedInstruction mutated = canonical with
        {
            StaticBinding = exact with { Opcode = (uint)InstructionsEnum.MTILE_STORE }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            MatrixTileIrProjectionAndMaterializer.MaterializeCanonicalCarrier(
                mutated,
                decoded.GetDecodedSlot(0)));

        Assert.Contains("mismatched decoded slot identity or binding", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixTileHarness_NoLongerCallsLegacyTransportProjector()
    {
        string root = CompatFreezeScanner.FindRepoRoot();
        string harness = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MatrixTileFullPipelineHarness.cs"));
        string materializer = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "ISA",
            "Instructions",
            "NonVmx",
            "Lanes00_03Vector",
            "MatrixTile",
            "MatrixTileIrProjectionAndMaterializer.cs"));

        Assert.DoesNotContain("DecodedBundleTransportProjector", harness, StringComparison.Ordinal);
        Assert.Contains("BuildCanonicalMatrixTileCarrierBundle(decodedBundle)", harness, StringComparison.Ordinal);
        int canonicalMethodStart = materializer.IndexOf(
            "public static MatrixTileMicroOp MaterializeCanonicalCarrier(",
            StringComparison.Ordinal);
        int nextProjectionMethod = materializer.IndexOf(
            "public static bool TryProject(",
            canonicalMethodStart,
            StringComparison.Ordinal);
        Assert.True(canonicalMethodStart >= 0 && nextProjectionMethod > canonicalMethodStart);
        string canonicalMaterializationSurface = materializer[canonicalMethodStart..nextProjectionMethod];
        Assert.DoesNotContain("InstructionRegistry", canonicalMaterializationSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeRegistry", canonicalMaterializationSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("TryFromOpcode", canonicalMaterializationSurface, StringComparison.Ordinal);
    }

    private static VLIW_Instruction[] CreateLoadBundle()
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[0] = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.MTILE_LOAD,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 4,
            stride: 16);
        return slots;
    }
}
