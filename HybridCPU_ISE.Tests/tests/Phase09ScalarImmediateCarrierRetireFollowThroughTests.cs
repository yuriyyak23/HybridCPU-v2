using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class ScalarImmediateCarrierRetireFollowThroughTests
    {
        private static void SeedSchedulerClassTemplate(MicroOpScheduler scheduler)
        {
            var capacityState = new SlotClassCapacityState();
            capacityState.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);
        }

        [Fact]
        public void DecodedAddiNegativeImmediate_OnActiveNonZeroVt_RetiresCanonicalResultWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong pc = 0x4100;
            const ushort sourceRegister = 5;
            const ushort destinationRegister = 7;
            const ulong sourceValue = 9UL;
            const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;
            const ulong expectedResult = 8UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(InstructionsEnum.ADDI, rd: (byte)destinationRegister, rs1: (byte)sourceRegister, immediate: 0xFFFF));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

            var decodeStatus = core.TestReadDecodeStageStatus();
            Assert.True(decodeStatus.Valid);
            Assert.Equal((uint)InstructionsEnum.ADDI, decodeStatus.OpCode);
            Assert.False(decodeStatus.IsVectorOp);
            Assert.False(decodeStatus.IsMemoryOp);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStatus = core.TestReadExecuteStageStatus();
            Assert.True(executeStatus.Valid);
            Assert.True(executeStatus.ResultReady);
            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
            Assert.True(replayPhase.IsActive);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DecodedSlt_OnActiveNonZeroVt_RetiresCanonicalCompareResultWithoutBoundaryPromotion()
        {
            const int vtId = 1;
            const ulong pc = 0x4200;
            const ushort leftRegister = 6;
            const ushort rightRegister = 8;
            const ushort destinationRegister = 9;
            const ulong leftValue = 5UL;
            const ulong rightValue = 7UL;
            const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
            const ulong expectedResult = 1UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, leftRegister, leftValue);
            core.WriteCommittedArch(vtId, rightRegister, rightValue);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(
                    InstructionsEnum.SLT,
                    rd: (byte)destinationRegister,
                    rs1: (byte)leftRegister,
                    rs2: (byte)rightRegister,
                    immediate: 0));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

            var decodeStatus = core.TestReadDecodeStageStatus();
            Assert.True(decodeStatus.Valid);
            Assert.Equal((uint)InstructionsEnum.SLT, decodeStatus.OpCode);
            Assert.False(decodeStatus.IsVectorOp);
            Assert.False(decodeStatus.IsMemoryOp);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStatus = core.TestReadExecuteStageStatus();
            Assert.True(executeStatus.Valid);
            Assert.True(executeStatus.ResultReady);
            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
            Assert.True(replayPhase.IsActive);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DecodedAuipc_OnActiveNonZeroVt_RetiresPcRelativeResultThroughMainlineCarrier()
        {
            const int vtId = 3;
            const ulong pc = 0x5000;
            const ushort destinationRegister = 10;
            const ulong originalDestinationValue = 0x9999_8888_7777_6666UL;
            const ulong expectedResult = 0x7000UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(InstructionsEnum.AUIPC, rd: (byte)destinationRegister, immediate: 2));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

            var decodeStatus = core.TestReadDecodeStageStatus();
            Assert.True(decodeStatus.Valid);
            Assert.Equal((uint)InstructionsEnum.AUIPC, decodeStatus.OpCode);
            Assert.False(decodeStatus.IsVectorOp);
            Assert.False(decodeStatus.IsMemoryOp);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStatus = core.TestReadExecuteStageStatus();
            Assert.True(executeStatus.Valid);
            Assert.True(executeStatus.ResultReady);
            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
            Assert.True(replayPhase.IsActive);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        private static MicroOpScheduler PrimeReplayScheduler(
            ref Processor.CPU_Core core,
            ulong retiredPc,
            out long serializingEpochCountBefore)
        {
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            serializingEpochCountBefore = scheduler.SerializingEpochCount;

            Assert.True(core.GetReplayPhaseContext().IsActive);
            Assert.True(scheduler.TestGetReplayPhaseContext().IsActive);
            return scheduler;
        }

        private static VLIW_Instruction[] CreateBundle(
            params VLIW_Instruction[] slots)
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < slots.Length && slotIndex < rawSlots.Length; slotIndex++)
            {
                rawSlots[slotIndex] = slots[slotIndex];
            }

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
                Immediate = immediate,
                StreamLength = 0,
                Stride = 0,
                VirtualThreadId = 0
            };
        }
    }
}

