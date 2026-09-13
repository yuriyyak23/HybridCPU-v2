using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072lScalarStoreSpeculativeSuppressionTests
{
    [Fact]
    public void RealSingleLaneSpeculativeStoreFault_PreservesFspOwnedNotReadyCarrierAndProjectsStructuralBlocked()
    {
        const ulong address = 0x400UL;
        const ulong sentinelAddress = 0x40UL;
        byte[] sentinelBefore = { 0xC3, 0xC3, 0xC3, 0xC3, 0xC3, 0xC3, 0xC3, 0xC3 };
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.MainMemory = mainMemory;
            Assert.True(mainMemory.TryWritePhysicalRange(sentinelAddress, sentinelBefore));
            Processor.Memory = null;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.TestInitializeFSPScheduler();
            core.PrepareExecutionStart(0x8B00UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
            StoreMicroOp store = CreateStore(address, 0x1122_3344_5566_7788UL);
            store.MarkSpeculative();
            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            int outstandingMemoryBefore = scheduler.GetOutstandingMemoryCount(0);

            core.TestRunExecuteStageWithDecodedInstruction(
                CreateStoreInstruction(),
                store,
                isMemoryOp: true,
                writesRegister: false,
                reg1Id: 2,
                reg2Id: 0,
                reg3Id: 1,
                pc: 0x8B00UL);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                    store,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
            byte[] sentinelAfter = new byte[8];

            Assert.True(mainMemory.TryReadPhysicalRange(sentinelAddress, sentinelAfter));
            Assert.True(store.IsSpeculative);
            Assert.True(store.Faulted);
            Assert.True(store.IsSpeculativeFaultSuppressed);
            Assert.False(store.HasInvalidTransferSize);
            Assert.False(store.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Equal(ExecutionOutcomeKind.StructuralBlocked, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.SpeculativeFaultSuppressed, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);

            Assert.True(executeStage.Valid);
            Assert.False(executeStage.ResultReady);
            Assert.NotEqual(-1, executeStage.MshrScoreboardSlot);
            Assert.Equal(outstandingMemoryBefore + 1, scheduler.GetOutstandingMemoryCount(0));
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(sentinelBefore, sentinelAfter);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void SpeculativeSuppressionProjection_RejectsNonFaultedAndCompletedObservations()
    {
        StoreMicroOp notFaulted = CreateStore(0x400UL, 0x1234UL);
        notFaulted.MarkSpeculative();
        StoreMicroOp faulted = CreateStore(0x400UL, 0x1234UL);
        faulted.MarkSpeculative();
        faulted.MarkFaulted();

        Assert.False(notFaulted.IsSpeculativeFaultSuppressed);
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                notFaulted,
                legacySuccess: false));
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                faulted,
                legacySuccess: true));
    }

    [Fact]
    public void InvalidSizeAndNonSpeculativeDenial_DoNotBorrowSuppressionDisposition()
    {
        StoreMicroOp invalid = CreateStore(0x400UL, 0x1234UL);
        invalid.Size = 3;
        invalid.MarkSpeculative();
        invalid.MarkFaulted();
        StoreMicroOp nonSpeculative = CreateStore(0x400UL, 0x1234UL);
        nonSpeculative.MarkFaulted();

        Assert.False(invalid.IsSpeculativeFaultSuppressed);
        Assert.False(nonSpeculative.IsSpeculativeFaultSuppressed);
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                invalid,
                legacySuccess: false));
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                nonSpeculative,
                legacySuccess: false));
    }

    private static VLIW_Instruction CreateStoreInstruction() =>
        new()
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd: 1, rs1: 2, rs2: 0),
        };

    private static StoreMicroOp CreateStore(ulong address, ulong value)
    {
        var store = new StoreMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            Address = address,
            Value = value,
            Size = 8,
            SrcRegID = 2,
            BaseRegID = 1,
        };
        store.InitializeMetadata();
        return store;
    }
}
