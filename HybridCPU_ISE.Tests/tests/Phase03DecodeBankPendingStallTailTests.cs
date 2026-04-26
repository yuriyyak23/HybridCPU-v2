using HybridCPU_ISE;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DecodeBankPendingStallTailTests
{
    [Fact]
    public void DecodeStage_WhenMemoryBankIsPendingForActiveVt_ThenReturnsStructuredStallDecisionWithoutCommittingCycleState()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var (core, pendingBankId) = CreateCorePreparedForDecodeBankPendingStall();
            var decodeResult = core.TestRunDecodeStageWithFetchedBundleResult(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40)),
                pc: 0x4E00);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            var runtimeFacts = core.TestReadDecodedSlotRuntimeIssueFacts(transportFacts.Slots[0]);
            var decodeStage = core.TestReadDecodeStageStatus();
            var pipeState = core.GetPipelineControl();

            Assert.True(runtimeFacts.IsMemoryOp);
            Assert.Equal(0, runtimeFacts.VirtualThreadId);
            Assert.Equal(pendingBankId, runtimeFacts.MemoryBankIntent);
            Assert.False(decodeResult.CanAdvance);
            Assert.Equal(PipelineStallKind.MemoryWait, decodeResult.StallReason);
            Assert.True(decodeResult.BankConflict);
            Assert.Equal(0, decodeResult.IssuedSlots);
            Assert.Equal(1, decodeResult.RejectedSlots);
            Assert.False(decodeStage.Valid);
            Assert.False(pipeState.Stalled);
            Assert.Equal(0UL, pipeState.StallCycles);
            Assert.Equal(0UL, pipeState.MshrScoreboardStalls);
            Assert.Equal(0UL, pipeState.BankConflictStallCycles);
        });
    }

    [Fact]
    public void ObservationService_WhenDecodeBankPendingStallsActiveVt_ThenCoreStateReportsMemoryWait()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var (core, _) = CreateCoreStagedBeforeDecodeBankPendingStall();
            core.ExecutePipelineCycle();
            IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

            CoreStateSnapshot snapshot = service.GetCoreState(0);

            Assert.True(snapshot.IsStalled);
            Assert.True(snapshot.VirtualThreadStalled[0]);
            Assert.Equal("Memory Wait", snapshot.VirtualThreadStallReasons[0]);
            Assert.Equal("None", snapshot.VirtualThreadStallReasons[1]);
        });
    }

    [Fact]
    public void DecodeStage_WhenScalarBundleAdmitsSingleSlot_ThenReturnsStructuredIssueCount()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x5200, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);

            var decodeResult = core.TestRunDecodeStageWithFetchedBundleResult(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 5, rs1: 1, rs2: 2)),
                pc: 0x5200);
            var decodeStage = core.TestReadDecodeStageStatus();

            Assert.True(decodeResult.CanAdvance);
            Assert.Equal(PipelineStallKind.None, decodeResult.StallReason);
            Assert.False(decodeResult.BankConflict);
            Assert.Equal(1, decodeResult.IssuedSlots);
            Assert.Equal(0, decodeResult.RejectedSlots);
            Assert.True(decodeStage.Valid);
        });
    }

    [Fact]
    public void ExecutePipelineCycle_WhenDecodeBankPendingStallsActiveVt_ThenTimelineRecordsStall()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var sink = new TraceSink(TraceFormat.JSON, "phase11-decode-bank-pending-stall.json");
            sink.SetEnabled(true);
            sink.SetLevel(TraceLevel.Full);

            TraceSink? originalTraceSink = Processor.TraceSink;
            try
            {
                Processor.TraceSink = sink;

                var (core, pendingBankId) = CreateCoreStagedBeforeDecodeBankPendingStall();
                MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
                Assert.True(scheduler.IsBankPendingForVT(pendingBankId, 0));

                core.ExecutePipelineCycle();

                var pipeState = core.GetPipelineControl();
                FullStateTraceEvent evt = Assert.Single(sink.GetThreadTrace(0));

                Assert.True(pipeState.Stalled);
                Assert.Equal(PipelineStallKind.MemoryWait, pipeState.StallReason);
                Assert.Equal(1UL, pipeState.StallCycles);
                Assert.Equal(1UL, pipeState.MshrScoreboardStalls);
                Assert.Equal(1UL, pipeState.BankConflictStallCycles);
                Assert.Equal("STALL", evt.PipelineStage);
                Assert.True(evt.Stalled);
                Assert.Equal(
                    PipelineStallText.Render(PipelineStallKind.MemoryWait, PipelineStallTextStyle.Trace),
                    evt.StallReason);
                Assert.True(core.GetFetchStage().Valid);
                Assert.False(core.GetDecodeStage().Valid);
            }
            finally
            {
                Processor.TraceSink = originalTraceSink;
            }
        });
    }

    private static (Processor.CPU_Core Core, int PendingBankId) CreateCorePreparedForDecodeBankPendingStall()
    {
        return ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4E00, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.TestInitializeFSPScheduler();

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40));

            int pendingBankId = ResolveDecodedMemoryBankIntent(rawSlots, bundleAddress: 0x4E00);

            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            scheduler.ClearSmtScoreboard();

            int slotIndex = scheduler.SetSmtScoreboardPendingTyped(
                targetId: pendingBankId,
                virtualThreadId: 0,
                currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad,
                bankId: pendingBankId);
            Assert.True(slotIndex >= 0);

            return (core, pendingBankId);
        });
    }

    private static (Processor.CPU_Core Core, int PendingBankId) CreateCoreStagedBeforeDecodeBankPendingStall()
    {
        return ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4E00, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.TestInitializeFSPScheduler();

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40));

            int pendingBankId = ResolveDecodedMemoryBankIntent(rawSlots, bundleAddress: 0x4E00);

            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            scheduler.ClearSmtScoreboard();

            int slotIndex = scheduler.SetSmtScoreboardPendingTyped(
                targetId: pendingBankId,
                virtualThreadId: 0,
                currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad,
                bankId: pendingBankId);
            Assert.True(slotIndex >= 0);

            core.TestStageFetchedBundleForDecode(rawSlots, pc: 0x4E00);
            return (core, pendingBankId);
        });
    }

    private static int ResolveDecodedMemoryBankIntent(
        VLIW_Instruction[] rawSlots,
        ulong bundleAddress)
    {
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: bundleAddress, bundleSerial: 1);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]));

        return microOp.MemoryBankId;
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

