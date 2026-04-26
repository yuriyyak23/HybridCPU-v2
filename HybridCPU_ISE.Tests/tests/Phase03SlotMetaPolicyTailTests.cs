using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03SlotMetaPolicyTailTests
{
    [Fact]
    public void LegacySlotCarrierMaterializer_NonZeroVt_DefaultSlotMetadata_DoesNotClearIntrinsicNonStealablePolicy()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.FENCE));
        var annotations = new VliwBundleAnnotations(
            new[]
            {
                new InstructionSlotMetadata(VtId.Create(1), SlotMetadata.Default)
            });

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0x1000, bundleSerial: 7);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(carrierBundle[0]);
        Assert.Equal(1, microOp.VirtualThreadId);
        Assert.False(microOp.AdmissionMetadata.IsStealable);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CanonicalNotStealableMetadata_DoesNotSeedSlotMetaShell()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 1));
        var annotations = new VliwBundleAnnotations(
            new[]
            {
                new InstructionSlotMetadata(
                    VtId.Create(2),
                    new SlotMetadata
                    {
                        StealabilityPolicy = StealabilityPolicy.NotStealable,
                        BranchHint = BranchHint.Likely
                    })
            });

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0x2000, bundleSerial: 11);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal(2, microOp.VirtualThreadId);
        Assert.False(microOp.AdmissionMetadata.IsStealable);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CanonicalLocalityHint_SeedsAssistWithoutSlotMetaFallback()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40));
        var annotations = new VliwBundleAnnotations(
            new[]
            {
                new InstructionSlotMetadata(
                    VtId.Create(1),
                    new SlotMetadata
                    {
                        LocalityHint = LocalityHint.Cold
                    })
            });

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0x3000, bundleSerial: 19);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(carrierBundle[0]);
        Assert.Equal(LocalityHint.Cold, microOp.MemoryLocalityHint);

        Assert.True(AssistMicroOp.TryCreateFromSeed(
            microOp,
            carrierVirtualThreadId: 0,
            replayEpochId: 1,
            assistEpochId: 2,
            out AssistMicroOp assist));
        Assert.Equal(AssistKind.Ldsa, assist.Kind);
        Assert.Equal(LocalityHint.Cold, assist.LocalityHint);
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}


