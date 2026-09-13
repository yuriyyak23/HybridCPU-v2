using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072jScalarStoreFallbackDenialTests
{
    [Fact]
    public void RealSingleLaneScalarStoreFallbackDenial_FailClosesAsNoEffectBackendUnavailable()
    {
        const ulong address = 0x400UL;
        const ulong value = 0x1122_3344_5566_7788UL;
        const ulong sentinelAddress = 0x40UL;
        byte[] sentinelBefore = { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 };
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
            core.PrepareExecutionStart(0x8900UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            VLIW_Instruction instruction = CreateStoreInstruction();
            StoreMicroOp store = CreateStore(address, value);

            UnsupportedExecutionSurfaceException exception =
                Assert.Throws<UnsupportedExecutionSurfaceException>(
                    () => core.TestRunExecuteStageWithDecodedInstruction(
                        instruction,
                        store,
                        isMemoryOp: true,
                        writesRegister: false,
                        reg1Id: instruction.Reg1ID,
                        reg2Id: instruction.Reg2ID,
                        reg3Id: instruction.Reg3ID,
                        pc: 0x8900UL));

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                    store,
                    core,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
            MicroOpScheduler scheduler =
                Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            byte[] sentinelAfter = new byte[8];

            Assert.True(mainMemory.TryReadPhysicalRange(sentinelAddress, sentinelAfter));
            Assert.Equal(ExecutionOutcomeKind.BackendUnavailable, outcome.Kind);
            Assert.Equal(
                ExecutionDiagnosticCode.RuntimeBackendUnavailable,
                outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.Contains("neither a bound asynchronous", outcome.Diagnostic.Reason, StringComparison.Ordinal);

            Assert.Equal(
                ExecutionFaultCategory.UnsupportedExecutionSurface,
                ExecutionFaultContract.GetCategory(exception));
            Assert.Equal("ScalarStoreFallbackBackend", exception.SurfaceName);
            Assert.Equal(0, exception.SlotIndex);
            Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.SD, exception.OpCode);
            Assert.Equal(0x8900UL, exception.BundlePc);
            Assert.IsType<InvalidOperationException>(exception.InnerException);

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
    public void BackendUnavailableProjection_WithoutExactStoreFallbackDenialEvidence_FailsClosed()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x1000UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            StoreMicroOp store = CreateStore(address: 0x80UL, value: 0x1234UL);

            ExecutionOutcomeContractViolationException exception =
                Assert.Throws<ExecutionOutcomeContractViolationException>(
                    () => Processor.CPU_Core.ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                        store,
                        core,
                        legacySuccess: false));

            Assert.Contains("no-subsystem/no-range evidence", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void SpeculativeStoreFaultSuppression_IsNotFallbackBackendDenialEvidence()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            StoreMicroOp store = CreateStore(address: 0x400UL, value: 0x1234UL);
            store.MarkSpeculative();
            store.MarkFaulted();

            Assert.False(store.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                    store,
                    core,
                    legacySuccess: false));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void InvalidStoreSize_IsNotFallbackBackendDenialEvidence()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            StoreMicroOp store = CreateStore(address: 0x400UL, value: 0x1234UL);
            store.Size = 3;

            Assert.False(store.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                    store,
                    core,
                    legacySuccess: false));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void StoreBackendUnavailableProjection_RejectsCompletedLegacyObservation()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            StoreMicroOp store = CreateStore(address: 0x400UL, value: 0x1234UL);

            ExecutionOutcomeContractViolationException exception =
                Assert.Throws<ExecutionOutcomeContractViolationException>(
                    () => Processor.CPU_Core.ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                        store,
                        core,
                        legacySuccess: true));

            Assert.Contains("cannot be projected as BackendUnavailable", exception.Message, StringComparison.Ordinal);
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
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                rd: 1,
                rs1: 2,
                rs2: 0),
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
