using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03FspDerivedIssuePlanProgressTests
{
    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_AfterForegroundConsumedSlot_PreservesProgressProjection()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x6100);
            core.VectorConfig.FSP_Enabled = 1;

            VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
            var annotations = new VliwBundleAnnotations(
                [],
                new BundleMetadata { FspBoundary = false });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x6100, bundleSerial: 101);
            scheduler.Nominate(1, candidate);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts initialForeground = core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            Assert.Same(candidate, initialForeground.Slots[0].MicroOp);
            Assert.Equal((uint)InstructionsEnum.ADDI, initialForeground.Slots[1].OpCode);

            core.TestConsumeForegroundBundleSlot(0);

            DecodedBundleTransportFacts afterConsume = core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            Assert.True(afterConsume.Slots[0].IsEmptyOrNop);
            Assert.Equal((uint)InstructionsEnum.ADDI, afterConsume.Slots[1].OpCode);

            BundleProgressState progressState = core.TestReadCurrentDecodedBundleProgressState();
            Assert.False(progressState.Contains(0));
            Assert.True(progressState.Contains(1));

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts refreshedForeground = core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            Assert.True(refreshedForeground.Slots[0].IsEmptyOrNop);
            Assert.Equal((uint)InstructionsEnum.ADDI, refreshedForeground.Slots[1].OpCode);

            progressState = core.TestReadCurrentDecodedBundleProgressState();
            Assert.False(progressState.Contains(0));
            Assert.True(progressState.Contains(1));
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
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
