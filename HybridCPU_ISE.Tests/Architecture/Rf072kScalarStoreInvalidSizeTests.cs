using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072kScalarStoreInvalidSizeTests
{
    [Fact]
    public void RealSingleLaneInvalidSizeStore_FailClosesAsNoEffectFatalInvariantViolation()
    {
        const ulong address = 0x400UL;
        const ulong sentinelAddress = 0x40UL;
        byte[] sentinelBefore = { 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A };
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(1, 0x1000UL);
            Processor.MainMemory = mainMemory;
            Assert.True(mainMemory.TryWritePhysicalRange(sentinelAddress, sentinelBefore));
            Processor.Memory = null;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.TestInitializeFSPScheduler();
            core.PrepareExecutionStart(0x8A00UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
            StoreMicroOp store = CreateStore(address, value: 0x1122_3344_5566_7788UL, size: 3);

            ExecutionOutcomeContractViolationException exception =
                Assert.Throws<ExecutionOutcomeContractViolationException>(
                    () => core.TestRunExecuteStageWithDecodedInstruction(
                        CreateStoreInstruction(),
                        store,
                        isMemoryOp: true,
                        writesRegister: false,
                        reg1Id: 2,
                        reg2Id: 0,
                        reg3Id: 1,
                        pc: 0x8A00UL));

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreInvalidSizeOutcome(
                    store,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            byte[] sentinelAfter = new byte[8];

            Assert.True(mainMemory.TryReadPhysicalRange(sentinelAddress, sentinelAfter));
            Assert.True(store.HasInvalidTransferSize);
            Assert.False(store.OwnsPendingWriteCompletion);
            Assert.False(store.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ExistingExecutionFault, outcome.Diagnostic!.Code);
            Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, outcome.Diagnostic.LegacyFaultCategory);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);

            Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(exception));
            Assert.Contains("ScalarStoreInvalidSize", exception.Message, StringComparison.Ordinal);
            Assert.Contains("transfer size 3", exception.Message, StringComparison.Ordinal);
            Assert.False(executeStage.Valid);
            Assert.Equal(-1, executeStage.MshrScoreboardSlot);
            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(0));
            Assert.False(scheduler.IsBankPendingForVT(store.MemoryBankId, 0));
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
    public void InvalidSizeProjection_RejectsValidSizeAndCompletedObservation()
    {
        StoreMicroOp valid = CreateStore(address: 0x400UL, value: 0x1234UL, size: 8);
        StoreMicroOp invalid = CreateStore(address: 0x400UL, value: 0x1234UL, size: 3);

        Assert.False(valid.HasInvalidTransferSize);
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreInvalidSizeOutcome(
                valid,
                legacySuccess: false));
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreInvalidSizeOutcome(
                invalid,
                legacySuccess: true));
    }

    [Fact]
    public void SpeculativeInvalidSize_IsMalformedCarrierNotBackendDenialOrFaultSuppression()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            StoreMicroOp store = CreateStore(address: 0x400UL, value: 0x1234UL, size: 3);
            store.MarkSpeculative();
            store.MarkFaulted();

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreInvalidSizeOutcome(
                    store,
                    legacySuccess: false);

            Assert.True(store.HasInvalidTransferSize);
            Assert.False(store.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
            Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, outcome.Diagnostic!.LegacyFaultCategory);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static VLIW_Instruction CreateStoreInstruction() =>
        new()
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd: 1, rs1: 2, rs2: 0),
        };

    private static StoreMicroOp CreateStore(ulong address, ulong value, byte size)
    {
        var store = new StoreMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            Address = address,
            Value = value,
            Size = size,
            SrcRegID = 2,
            BaseRegID = 1,
        };
        store.InitializeMetadata();
        return store;
    }
}
