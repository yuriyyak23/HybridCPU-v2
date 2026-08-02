using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072fLoadSegmentRetryContourTests
{
    [Fact]
    public void RealSingleLaneLoadSegmentFalse_ProjectsToNoEffectRetryableAndPreservesLegacyWait()
    {
        const ulong address = 0x640UL;
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
            core.PrepareExecutionStart(0x8600UL, activeVtId: 0);
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
            int queuedBefore = memorySubsystem.CycleController.OutstandingVectorSegmentLoads;

            var instruction = new VLIW_Instruction
            {
                OpCode = (ushort)Processor.CPU_Core.InstructionsEnum.VLOAD,
                DestSrc1Pointer = address,
                StreamLength = 4,
                DataTypeValue = DataTypeEnum.UINT32,
                Stride = 4,
            };
            var load = new LoadSegmentMicroOp { Instruction = instruction };
            load.InitializeMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                load,
                isVectorOp: true,
                isMemoryOp: true,
                pc: 0x8600UL);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneLoadSegmentRetryOutcome(
                    load,
                    legacySuccess: false);
            Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.True(
                memorySubsystem.CycleController.OutstandingVectorSegmentLoads > queuedBefore);
            Assert.Equal(0, memorySubsystem.CurrentQueuedRequests);
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
    public void LoadSegmentRetryProjection_WithoutOwnedMemoryCompletion_FailsClosed()
    {
        var load = new LoadSegmentMicroOp();

        ExecutionOutcomeContractViolationException exception = Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneLoadSegmentRetryOutcome(
                load,
                legacySuccess: false));

        Assert.Contains("owned pending memory completion", exception.Message, StringComparison.Ordinal);
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
