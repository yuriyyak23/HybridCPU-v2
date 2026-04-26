using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase03;

public sealed class Phase03DecodeStateKindContractTests
{
    [Fact]
    public void CanonicalDecodePublication_PublishesCanonicalStateKindAndOrigin()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5100);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x5100, bundleSerial: 81);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();

        Assert.Equal(DecodedBundleStateKind.Canonical, transportFacts.StateKind);
        Assert.Equal(DecodedBundleStateOrigin.CanonicalDecode, transportFacts.StateOrigin);
        Assert.Equal(DecodedBundleStateOwnerKind.BaseRuntimePublication, runtimeState.StateOwnerKind);
        Assert.True(runtimeState.StateEpoch > 0);
        Assert.True(runtimeState.StateVersion > 0);
        Assert.Equal(runtimeState.StateVersion, runtimeState.LineageStateVersion);
        Assert.False(core.GetCurrentDecodedInstructionBundle().IsEmpty);
        Assert.False(core.GetCurrentBundleLegalityDescriptor().IsEmpty);
    }

    [Fact]
    public void FallbackTrapPublication_PublishesDecodeFaultStateKindAndOrigin()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5180);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x5180);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();

        Assert.Equal(DecodedBundleStateKind.DecodeFault, transportFacts.StateKind);
        Assert.Equal(DecodedBundleStateOrigin.DecodeFallbackTrap, transportFacts.StateOrigin);
        Assert.True(core.GetCurrentDecodedInstructionBundle().HasDecodeFault);
        Assert.True(core.GetCurrentBundleLegalityDescriptor().HasDecodeFault);
    }

    [Fact]
    public void ReplayBundleLoad_PublishesReplayStateKindAndOrigin()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5280);

        core.TestLoadReplayDecodedBundleSlotCarrier(
            0x5280,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6));

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();

        Assert.Equal(DecodedBundleStateKind.Replay, transportFacts.StateKind);
        Assert.Equal(DecodedBundleStateOrigin.ReplayBundleLoad, transportFacts.StateOrigin);
        Assert.True(core.GetCurrentDecodedInstructionBundle().IsEmpty);
        Assert.True(core.GetCurrentBundleLegalityDescriptor().IsEmpty);
        Assert.True(runtimeState.TransformHistory.Contains(DecodedBundleTransformKind.ReplayBundleLoad));
        Assert.Equal((byte)1, runtimeState.TransformHistory.MutationDepth);
        Assert.Equal(DecodedBundleStateOrigin.ReplayBundleLoad, runtimeState.TransformHistory.LatestOrigin);
    }

    [Fact]
    public void ResetDecodedBundleRuntimeState_PublishesEmptyResetStateAndClearsCanonicalSnapshots()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x52A0);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x52A0, bundleSerial: 183);
        core.TestResetDecodedBundleRuntimeState(0x52C0);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();

        Assert.Equal(DecodedBundleStateKind.Empty, transportFacts.StateKind);
        Assert.Equal(DecodedBundleStateOrigin.Reset, transportFacts.StateOrigin);
        Assert.Equal((ulong)0x52C0, transportFacts.PC);
        Assert.True(core.GetCurrentDecodedInstructionBundle().IsEmpty);
        Assert.True(core.GetCurrentBundleLegalityDescriptor().IsEmpty);
    }

    [Fact]
    public void ForegroundSlotMutation_PreservesCanonicalBase_AndAppendsSingleSlotMutationTransformHistory()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x52E0);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x52E0, bundleSerial: 184);
        MicroOp replacement = MicroOpTestHelper.CreateScalarALU(0, destReg: 9, src1Reg: 10, src2Reg: 11);

        core.TestPublishCurrentForegroundSlotMutation(0, replacement);

        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();

        Assert.Equal(DecodedBundleStateOwnerKind.BaseRuntimePublication, runtimeState.StateOwnerKind);
        Assert.True(runtimeState.StateEpoch > 0);
        Assert.True(runtimeState.StateVersion > 0);
        Assert.Equal(runtimeState.StateVersion, runtimeState.LineageStateVersion);
        Assert.Equal(DecodedBundleStateOrigin.SingleSlotMutation, runtimeState.StateOrigin);
        Assert.True(runtimeState.HasCanonicalDecode);
        Assert.True(runtimeState.HasCanonicalLegality);
        Assert.True(runtimeState.TransformHistory.Contains(DecodedBundleTransformKind.CanonicalDecode));
        Assert.True(runtimeState.TransformHistory.Contains(DecodedBundleTransformKind.SingleSlotMutation));
        Assert.Equal((byte)1, runtimeState.TransformHistory.MutationDepth);
        Assert.Equal(DecodedBundleStateOrigin.SingleSlotMutation, runtimeState.TransformHistory.LatestOrigin);
    }

    [Fact]
    public void FspPacking_PreservesCanonicalBasePublication_AndProjectsForegroundDerivedIssuePlan()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x5300);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
            var annotations = new VliwBundleAnnotations(
                new InstructionSlotMetadata[0],
                new BundleMetadata { FspBoundary = false });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x5300, bundleSerial: 83);
            scheduler.Nominate(1, candidate);

            DecodedBundleRuntimeState baseRuntimeState = core.TestReadCurrentDecodedBundleRuntimeState();
            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts baseTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleTransportFacts foregroundTransportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            DecodedBundleRuntimeState foregroundRuntimeState = core.TestReadCurrentForegroundDecodedBundleRuntimeState();

            Assert.Equal(DecodedBundleStateKind.Canonical, baseTransportFacts.StateKind);
            Assert.Equal(DecodedBundleStateOrigin.CanonicalDecode, baseTransportFacts.StateOrigin);
            Assert.Equal(DecodedBundleStateKind.ForegroundMutated, foregroundTransportFacts.StateKind);
            Assert.Equal(DecodedBundleStateOrigin.FspPacking, foregroundTransportFacts.StateOrigin);
            Assert.Equal(DecodedBundleStateOwnerKind.BaseRuntimePublication, baseRuntimeState.StateOwnerKind);
            Assert.Equal(DecodedBundleStateOwnerKind.DerivedIssuePlanPublication, foregroundRuntimeState.StateOwnerKind);
            Assert.Equal(baseRuntimeState.BundleSerial, foregroundRuntimeState.BundleSerial);
            Assert.Equal(baseRuntimeState.StateEpoch, foregroundRuntimeState.StateEpoch);
            Assert.True(foregroundRuntimeState.StateVersion > baseRuntimeState.StateVersion);
            Assert.Equal(baseRuntimeState.StateVersion, foregroundRuntimeState.LineageStateVersion);
            Assert.True(baseTransportFacts.Slots[0].IsEmptyOrNop);
            Assert.Same(candidate, foregroundTransportFacts.Slots[0].MicroOp);
            Assert.True(candidate.IsFspInjected);
            Assert.True(foregroundRuntimeState.TransformHistory.Contains(DecodedBundleTransformKind.CanonicalDecode));
            Assert.True(foregroundRuntimeState.TransformHistory.Contains(DecodedBundleTransformKind.FspPacking));
            Assert.Equal((byte)1, foregroundRuntimeState.TransformHistory.MutationDepth);
            Assert.Equal(DecodedBundleStateOrigin.FspPacking, foregroundRuntimeState.TransformHistory.LatestOrigin);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] occupiedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < occupiedSlots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = occupiedSlots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
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
            VirtualThreadId = virtualThreadId
        };
    }
}
