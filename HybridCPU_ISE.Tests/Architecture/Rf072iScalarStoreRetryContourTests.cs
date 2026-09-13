using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072iScalarStoreRetryContourTests
{
    [Fact]
    public void RealSingleLaneScalarStoreFalse_WithExactPendingWrite_ProjectsToNoEffectRetryable()
    {
        const ulong address = 0x760UL;
        const ulong value = 0x1122_3344_5566_7788UL;
        byte[] originalBytes = { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 };
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var mainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);

        try
        {
            Processor.MainMemory = mainMemory;
            Assert.True(mainMemory.TryWritePhysicalRange(address, originalBytes));
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor processor = default;
            var memorySubsystem = new MemorySubsystem(ref processor);
            Processor.Memory = memorySubsystem;
            InitializeIommu();

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0x8800UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
            int queuedBefore = memorySubsystem.CurrentQueuedRequests;

            var instruction = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
                DataTypeValue = DataTypeEnum.UINT64,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 0),
            };
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

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                store,
                isMemoryOp: true,
                writesRegister: false,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x8800UL);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreRetryOutcome(
                    store,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
            byte[] bytesBeforeService = new byte[8];

            Assert.True(mainMemory.TryReadPhysicalRange(address, bytesBeforeService));
            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.True(store.OwnsPendingWriteCompletion);
            Assert.Equal(queuedBefore, memorySubsystem.CurrentQueuedRequests);
            Assert.Equal(1, memorySubsystem.CycleController.OutstandingSingleLaneScalarStores);
            Assert.Equal(originalBytes, bytesBeforeService);
            Assert.True(executeStage.Valid);
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
            Assert.Equal(
                0,
                executeStage.GetLane(executeStage.ActiveLaneIndex).GeneratedRetireRecordCount);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);

            memorySubsystem.AdvanceCycles(1);
            Assert.True(store.OwnsPendingWriteCompletion);
            Assert.False(store.Execute(ref core));
            memorySubsystem.AdvanceCycles(1);
            Assert.True(store.Execute(ref core));
            Assert.False(store.OwnsPendingWriteCompletion);
            byte[] bytesAfterService = new byte[8];
            Assert.True(mainMemory.TryReadPhysicalRange(address, bytesAfterService));
            Assert.Equal(originalBytes, bytesAfterService);
            Assert.Equal(0, memorySubsystem.CycleController.OutstandingSingleLaneScalarStores);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void ScalarStoreRetryProjection_WithoutOwnedPendingWrite_FailsClosed()
    {
        var store = new StoreMicroOp
        {
            Address = 0x760UL,
            Value = 0x1234UL,
            Size = 8,
        };

        ExecutionOutcomeContractViolationException exception =
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarStoreRetryOutcome(
                    store,
                    legacySuccess: false));

        Assert.Contains("exact owned pending write completion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MutatedStoreIdentity_CannotBorrowPendingWriteRetryDisposition()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor processor = default;
            Processor.Memory = new MemorySubsystem(ref processor);
            InitializeIommu();

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            var store = new StoreMicroOp
            {
                Address = 0x780UL,
                Value = 0x1122_3344_5566_7788UL,
                Size = 8,
            };

            Assert.False(store.Execute(ref core));
            Assert.True(store.OwnsPendingWriteCompletion);
            store.Value ^= 1;

            Assert.False(store.OwnsPendingWriteCompletion);
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarStoreRetryOutcome(
                    store,
                    legacySuccess: false));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void InvalidSizeAndSpeculativeFaultEvidence_AreNotPendingWriteEvidence()
    {
        var invalidSize = new StoreMicroOp
        {
            Address = 0x760UL,
            Value = 0x1234UL,
            Size = 3,
        };
        var speculativeFault = new StoreMicroOp
        {
            Address = ulong.MaxValue,
            Value = 0x1234UL,
            Size = 8,
        };
        speculativeFault.MarkSpeculative();
        speculativeFault.MarkFaulted();

        Assert.False(invalidSize.OwnsPendingWriteCompletion);
        Assert.False(speculativeFault.OwnsPendingWriteCompletion);
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreRetryOutcome(
                invalidSize,
                legacySuccess: false));
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarStoreRetryOutcome(
                speculativeFault,
                legacySuccess: false));
    }

    private static void InitializeIommu()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }
}
