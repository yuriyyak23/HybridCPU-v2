using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072hScalarLoadFallbackDenialTests
{
    [Fact]
    public void RealSingleLaneScalarLoadFallbackDenial_FailClosesAsNoEffectBackendUnavailable()
    {
        const ulong address = 0x400UL;
        const ushort destinationRegister = 9;
        const ulong originalDestinationValue = 0xA5A5UL;
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.TestInitializeFSPScheduler();
            core.PrepareExecutionStart(0x8800UL, activeVtId: 0);
            core.WriteCommittedArch(0, destinationRegister, originalDestinationValue);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            VLIW_Instruction instruction = CreateLoadInstruction(destinationRegister);
            LoadMicroOp load = CreateLoad(address, destinationRegister);

            UnsupportedExecutionSurfaceException exception =
                Assert.Throws<UnsupportedExecutionSurfaceException>(
                    () => core.TestRunExecuteStageWithDecodedInstruction(
                        instruction,
                        load,
                        isMemoryOp: true,
                        writesRegister: true,
                        reg1Id: instruction.Reg1ID,
                        reg2Id: instruction.Reg2ID,
                        reg3Id: instruction.Reg3ID,
                        pc: 0x8800UL));

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                    load,
                    core,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
            MicroOpScheduler scheduler =
                Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());

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
            Assert.Equal("ScalarLoadFallbackBackend", exception.SurfaceName);
            Assert.Equal(0, exception.SlotIndex);
            Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.LD, exception.OpCode);
            Assert.Equal(0x8800UL, exception.BundlePc);
            Assert.IsType<InvalidOperationException>(exception.InnerException);

            Assert.False(executeStage.Valid);
            Assert.Equal(-1, executeStage.MshrScoreboardSlot);
            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(0));
            Assert.False(scheduler.IsBankPendingForVT(load.MemoryBankId, 0));
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(
                originalDestinationValue,
                core.ReadArch(0, destinationRegister));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void BackendUnavailableProjection_WithoutExactFallbackDenialEvidence_FailsClosed()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x1000UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            LoadMicroOp load = CreateLoad(address: 0x80UL, destinationRegister: 9);

            ExecutionOutcomeContractViolationException exception =
                Assert.Throws<ExecutionOutcomeContractViolationException>(
                    () => Processor.CPU_Core.ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                        load,
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
    public void SpeculativeFaultSuppression_IsNotFallbackBackendDenialEvidence()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            LoadMicroOp load = CreateLoad(address: 0x400UL, destinationRegister: 9);
            load.MarkSpeculative();
            load.MarkFaulted();

            Assert.False(load.HasNonSpeculativeFallbackBackendDenial(core));
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                    load,
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
    public void BackendUnavailableProjection_RejectsCompletedLegacyObservation()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;

        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x100UL);
            Processor.Memory = null;
            var core = new Processor.CPU_Core(0);
            LoadMicroOp load = CreateLoad(address: 0x400UL, destinationRegister: 9);

            ExecutionOutcomeContractViolationException exception =
                Assert.Throws<ExecutionOutcomeContractViolationException>(
                    () => Processor.CPU_Core.ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                        load,
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

    private static VLIW_Instruction CreateLoadInstruction(ushort destinationRegister) =>
        new()
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                (byte)destinationRegister,
                rs1: 1,
                rs2: 0),
        };

    private static LoadMicroOp CreateLoad(ulong address, ushort destinationRegister)
    {
        var load = new LoadMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            Address = address,
            Size = 8,
            DestRegID = destinationRegister,
            BaseRegID = 1,
            WritesRegister = true,
        };
        load.InitializeMetadata();
        return load;
    }
}
