using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// REF-04: CI invariants for reference raw execute fallback budget.
/// Verifies that:
///   - Standard MicroOp execution does not trigger the raw fallback path.
///   - The reference-raw fallback counter is observable only through an explicit test/reference seam.
///   - Opcodes outside the allow-list are rejected with an exception.
///   - Production-like memory/control-flow fallback attempts fail closed with a typed surface fault.
///   - Non-fault MicroOp execution failures fail closed instead of re-entering raw fallback.
/// </summary>
public sealed class Phase09LegacyRawFallbackBudgetTests
{
    private sealed class SucceedingScalarMicroOp : MicroOp
    {
        public SucceedingScalarMicroOp(uint opCode, ushort destReg)
        {
            OpCode = opCode;
            DestRegID = destReg;
            WritesRegister = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Synthetic succeeding scalar MicroOp";
    }

    private sealed class FailingScalarMicroOp : MicroOp
    {
        public FailingScalarMicroOp(uint opCode, ushort destReg)
        {
            OpCode = opCode;
            DestRegID = destReg;
            WritesRegister = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new InvalidOperationException("synthetic MicroOp execution failure");
        }

        public override string GetDescription() => "Synthetic failing scalar MicroOp";
    }

    [Fact]
    public void TestReferenceRawFallbackCount_IsZero_WhenMicroOpExecutesSuccessfully()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        core.TestRunExecuteStageWithDecodedInstruction(
            CreateScalarInstruction(InstructionsEnum.Addition, rd: 1),
            new SucceedingScalarMicroOp((uint)InstructionsEnum.Addition, destReg: 1),
            writesRegister: true,
            reg1Id: 1,
            pc: 0x1000UL);

        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneMicroOpFailure_FailCloses_WithoutEnteringReferenceRawFallback()
    {
        const ushort destReg = 7;
        const ushort src1 = 2;
        const ushort src2 = 3;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.WriteCommittedArch(0, src1, 10UL);
        core.WriteCommittedArch(0, src2, 5UL);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.Addition,
                rd: (byte)destReg,
                rs1: (byte)src1,
                rs2: (byte)src2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                new FailingScalarMicroOp((uint)InstructionsEnum.Addition, destReg),
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x2000UL));

        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(ex));
        Assert.NotNull(ex.InnerException);
        Assert.Contains("no longer falls back to reference raw execution", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void SingleLaneExecute_RejectsMissingAuthoritativeMicroOp_WithoutReferenceRawFallback()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                CreateScalarInstruction(InstructionsEnum.Addition, rd: 4),
                microOp: null,
                writesRegister: true,
                reg1Id: 4,
                pc: 0x3000UL));

        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(ex));
        Assert.Contains("test-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
    }

    [Fact]
    public void ReferenceRawFallback_Throws_ForOpcodeOutsideAllowList()
    {
        const uint unknownOpcode = 999;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        var instruction = new VLIW_Instruction
        {
            OpCode = unknownOpcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0,
            Immediate = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        Assert.Throws<InvalidOperationException>(
            () => core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
                instruction,
                writesRegister: true,
                reg1Id: 1,
                pc: 0x4000UL));

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ForwardingPath forwardEx = core.TestGetExecuteForwardingPath();
        Assert.False(executeStage.Valid);
        Assert.False(forwardEx.Valid);
    }

    [Fact]
    public void ReferenceRawFallback_Rejects_ControlFlowSurface_WithTypedUnsupportedExecutionSurfaceFault()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.BEQ,
                rs1: 1,
                rs2: 2,
                immediate: 0x10);

        UnsupportedExecutionSurfaceException ex = Assert.Throws<UnsupportedExecutionSurfaceException>(
            () => core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
                instruction,
                isBranchOp: true,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x4800UL));

        Assert.Equal(ExecutionFaultCategory.UnsupportedExecutionSurface, ExecutionFaultContract.GetCategory(ex));
        Assert.Contains("ControlFlow", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1UL, core.TestGetReferenceRawFallbackCount());
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
    }

    [Fact]
    public void ReferenceRawFallback_Rejects_MemorySurface_WithTypedUnsupportedExecutionSurfaceFault()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.Load,
                rd: 3,
                rs1: 1,
                immediate: 0x20);

        UnsupportedExecutionSurfaceException ex = Assert.Throws<UnsupportedExecutionSurfaceException>(
            () => core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
                instruction,
                isMemoryOp: true,
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                pc: 0x4900UL));

        Assert.Equal(ExecutionFaultCategory.UnsupportedExecutionSurface, ExecutionFaultContract.GetCategory(ex));
        Assert.Contains("Memory", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1UL, core.TestGetReferenceRawFallbackCount());
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
    }

    [Fact]
    public void ReferenceRawFallback_Succeeds_ForAllowListedAluOpcode()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.WriteCommittedArch(0, 2, 7UL);
        core.WriteCommittedArch(0, 3, 3UL);

        VLIW_Instruction instruction =
            CreateScalarInstruction(InstructionsEnum.Subtraction, rd: 5, rs1: 2, rs2: 3);

        core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
            instruction,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x5000UL);

        ExecuteStage exStage = core.GetExecuteStage();
        Assert.True(exStage.Valid);
        Assert.True(exStage.ResultReady);
        Assert.Equal(4UL, exStage.ResultValue);
        Assert.Equal(1UL, core.TestGetReferenceRawFallbackCount());
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

