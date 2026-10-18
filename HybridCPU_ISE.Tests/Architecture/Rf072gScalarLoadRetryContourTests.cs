using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072gScalarLoadRetryContourTests
{
    [Fact]
    public void RealSingleLaneScalarLoadFalse_WithOwnedPendingRead_ProjectsToNoEffectRetryable()
    {
        const ulong address = 0x720UL;
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var mainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);

        try
        {
            Processor.MainMemory = mainMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor processor = default;
            var memorySubsystem = new MemorySubsystem(ref processor);
            Processor.Memory = memorySubsystem;
            InitializeIommu();

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0x8700UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
            int queuedBefore = memorySubsystem.CycleController.OutstandingSingleLaneScalarLoads;

            var instruction = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
                DataTypeValue = DataTypeEnum.UINT64,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 0),
            };
            var load = new LoadMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
                Address = address,
                Size = 8,
                DestRegID = 9,
                BaseRegID = 1,
                WritesRegister = true,
            };
            load.InitializeMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                load,
                isMemoryOp: true,
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x8700UL);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarLoadRetryOutcome(
                    load,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.True(
                memorySubsystem.CycleController.OutstandingSingleLaneScalarLoads > queuedBefore);
            Assert.True(executeStage.Valid);
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
            Assert.Equal(
                0,
                executeStage.GetLane(executeStage.ActiveLaneIndex).GeneratedRetireRecordCount);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void ScalarLoadRetryProjection_WithoutOwnedPendingRead_FailsClosed()
    {
        var load = new LoadMicroOp
        {
            Address = 0x720UL,
            Size = 8,
        };

        ExecutionOutcomeContractViolationException exception =
            Assert.Throws<ExecutionOutcomeContractViolationException>(
                () => Processor.CPU_Core.ProjectSingleLaneScalarLoadRetryOutcome(
                    load,
                    legacySuccess: false));

        Assert.Contains("exact owned pending read completion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeculativeFaultSuppressionEvidence_IsNotScalarLoadRetryEvidence()
    {
        var load = new LoadMicroOp
        {
            Address = ulong.MaxValue,
            Size = 8,
        };
        load.MarkSpeculative();
        load.MarkFaulted();

        Assert.True(load.Faulted);
        Assert.False(load.OwnsPendingMemoryCompletion);
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarLoadRetryOutcome(
                load,
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
