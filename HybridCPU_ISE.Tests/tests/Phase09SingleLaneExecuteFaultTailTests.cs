using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class SingleLaneExecuteFaultTailTests
{
    private abstract class ThrowingSingleLaneMicroOp : MicroOp
    {
        protected ThrowingSingleLaneMicroOp(uint opCode)
        {
            OpCode = opCode;
            WritesRegister = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }
    }

    private sealed class ThrowingPageFaultMicroOp : ThrowingSingleLaneMicroOp
    {
        public ThrowingPageFaultMicroOp()
            : base((uint)InstructionsEnum.ADDI)
        {
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new PageFaultException(0xCAFEUL, isWrite: false);
        }

        public override string GetDescription() => "Synthetic single-lane page-fault carrier";
    }

    private sealed class ThrowingAlignmentFaultMicroOp : ThrowingSingleLaneMicroOp
    {
        public ThrowingAlignmentFaultMicroOp()
            : base((uint)InstructionsEnum.ADDI)
        {
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new MemoryAlignmentException(0x1003UL, 4, "SYNTH");
        }

        public override string GetDescription() => "Synthetic single-lane alignment-fault carrier";
    }

    private sealed class ThrowingNonFaultMicroOp : ThrowingSingleLaneMicroOp
    {
        public ThrowingNonFaultMicroOp(ushort destinationRegister)
            : base((uint)InstructionsEnum.ADD)
        {
            DestRegID = destinationRegister;
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new InvalidOperationException("synthetic single-lane execute failure");
        }

        public override string GetDescription() => "Synthetic single-lane non-fault failure carrier";
    }

    private sealed class ThrowingExistingExecutionFaultMicroOp : ThrowingSingleLaneMicroOp
    {
        public ThrowingExistingExecutionFaultMicroOp()
            : base((uint)InstructionsEnum.ADD)
        {
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw ExecutionFaultContract.CreateWrappedException(
                ExecutionFaultCategory.InvalidInternalOp,
                "synthetic pre-categorized single-lane fault",
                new InvalidOperationException("synthetic categorized inner failure"));
        }

        public override string GetDescription() => "Synthetic pre-categorized single-lane failure carrier";
    }

    private sealed class RetryWithoutMutationMicroOp : ThrowingSingleLaneMicroOp
    {
        public RetryWithoutMutationMicroOp()
            : base((uint)InstructionsEnum.ADD)
        {
        }

        public override bool Execute(ref Processor.CPU_Core core) => false;

        public override string GetDescription() => "Synthetic single-lane legacy retry carrier";
    }

    [Fact]
    public void SingleLaneMicroOp_WhenExecuteThrowsPageFault_ThenPropagatesStageAwareFault()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5),
                new ThrowingPageFaultMicroOp(),
                writesRegister: true,
                reg1Id: 5,
                pc: 0x8300UL));

        Assert.Equal(0xCAFEUL, ex.FaultAddress);
        Assert.False(ex.IsWrite);
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneMicroOp_WhenExecuteThrowsAlignmentFault_ThenRethrowsTranslatedPageFault()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 6),
                new ThrowingAlignmentFaultMicroOp(),
                writesRegister: true,
                reg1Id: 6,
                pc: 0x8400UL));

        Assert.Equal(0x1003UL, ex.FaultAddress);
        Assert.True(ex.IsWrite);
        Assert.IsType<MemoryAlignmentException>(ex.InnerException);
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneMicroOp_WhenExecuteThrowsNonFaultException_ThenFailClosesWithoutReferenceRawFallback()
    {
        const ushort sourceRegister1 = 2;
        const ushort sourceRegister2 = 3;
        const ushort destinationRegister = 7;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.WriteCommittedArch(0, sourceRegister1, 9UL);
        core.WriteCommittedArch(0, sourceRegister2, 4UL);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.ADD,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister1,
                rs2: (byte)sourceRegister2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                new ThrowingNonFaultMicroOp(destinationRegister),
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x8500UL));

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ForwardingPath forwardEx = core.TestGetExecuteForwardingPath();

        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(ex));
        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("no longer falls back to reference raw execution", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(executeStage.Valid);
        Assert.False(forwardEx.Valid);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneMicroOp_WhenExecuteThrowsExistingExecutionFault_ThenTypedFatalAdapterPreservesCategoryAndCleanup()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                CreateScalarInstruction(InstructionsEnum.ADD, rd: 8),
                new ThrowingExistingExecutionFaultMicroOp(),
                writesRegister: true,
                reg1Id: 8,
                pc: 0x8510UL));

        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(ex));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneMicroOp_WhenExecuteReturnsFalse_ThenLegacyNotReadyContourRemainsUnclassifiedAndUnchanged()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

        core.TestRunExecuteStageWithDecodedInstruction(
            CreateScalarInstruction(InstructionsEnum.ADD, rd: 9),
            new RetryWithoutMutationMicroOp(),
            writesRegister: true,
            reg1Id: 9,
            pc: 0x8520UL);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Assert.True(executeStage.Valid);
        Assert.False(executeStage.ResultReady);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
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

