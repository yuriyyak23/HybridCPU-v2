using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072zVectorCompletedTokenSingleLaneIntegrationTests
{
    [Fact]
    public void LoadSegmentCompletedFailedRead_UsesSingleLaneTypedPageFaultDelivery()
    {
        WithRejectedMemorySubsystem((core, memorySubsystem) =>
        {
            VLIW_Instruction instruction = CreateInstruction(Processor.CPU_Core.InstructionsEnum.VLOAD);
            var microOp = new LoadSegmentMicroOp { Instruction = instruction };
            microOp.InitializeMetadata();
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction, microOp, isVectorOp: true, isMemoryOp: true, pc: 0x8A00UL);
            Assert.True(core.GetExecuteStage().Valid);
            Assert.False(core.GetExecuteStage().ResultReady);

            Advance(memorySubsystem);
            PageFaultException fault = Assert.Throws<PageFaultException>(() =>
                core.TestRunExecuteStageWithDecodedInstruction(
                    instruction, microOp, isVectorOp: true, isMemoryOp: true, pc: 0x8A00UL));

            Assert.Equal(0x2000UL, fault.FaultAddress);
            Assert.False(fault.IsWrite);
            Assert.False(core.GetExecuteStage().Valid);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        });
    }

    [Fact]
    public void StoreSegmentCompletedFailedWrite_UsesSingleLaneTypedPageFaultDelivery()
    {
        WithRejectedMemorySubsystem((core, memorySubsystem) =>
        {
            VLIW_Instruction instruction = CreateInstruction(Processor.CPU_Core.InstructionsEnum.VSTORE);
            var microOp = new StoreSegmentMicroOp { Instruction = instruction };
            microOp.SetStoreBuffer(
                BitConverter.GetBytes(0x1122_3344U)
                    .Concat(BitConverter.GetBytes(0x5566_7788U))
                    .ToArray());
            microOp.InitializeMetadata();
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction, microOp, isVectorOp: true, isMemoryOp: true, pc: 0x8A10UL);
            Assert.True(core.GetExecuteStage().Valid);
            Assert.False(core.GetExecuteStage().ResultReady);

            Advance(memorySubsystem);
            PageFaultException fault = Assert.Throws<PageFaultException>(() =>
                core.TestRunExecuteStageWithDecodedInstruction(
                    instruction, microOp, isVectorOp: true, isMemoryOp: true, pc: 0x8A10UL));

            Assert.Equal(0x2000UL, fault.FaultAddress);
            Assert.True(fault.IsWrite);
            Assert.False(core.GetExecuteStage().Valid);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        });
    }

    private static void WithRejectedMemorySubsystem(Action<Processor.CPU_Core, MemorySubsystem> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(0, 0, 0, 0x1000UL, IOMMUAccessPermissions.ReadWrite);
            Processor processor = default;
            var memorySubsystem = new MemorySubsystem(ref processor);
            Processor.Memory = memorySubsystem;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0x8A00UL, activeVtId: 0);
            body(core, memorySubsystem);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static VLIW_Instruction CreateInstruction(Processor.CPU_Core.InstructionsEnum opcode) =>
        new()
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.UINT32,
            DestSrc1Pointer = 0x2000UL,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 0
        };

    private static void Advance(MemorySubsystem memorySubsystem)
    {
        for (int cycle = 0; cycle < 64; cycle++)
        {
            memorySubsystem.AdvanceCycles(1);
        }
    }
}
