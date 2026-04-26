using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneDispatchResourceLatchTests
{
    private sealed class ConstantWriteBackMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public ConstantWriteBackMicroOp(ushort destRegId, ulong resultValue, ResourceBitset resourceMask)
        {
            _resultValue = resultValue;
            OpCode = 0;
            DestRegID = destRegId;
            WritesRegister = true;
            ResourceMask = resourceMask;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic single-lane resource-latch carrier";
    }

    [Fact]
    public void SingleLaneExecute_WhenMicroOpCarriesNonZeroResourceMask_ThenLatchesMaskAndTokenIntoExecuteStage()
    {
        const ushort destinationRegister = 9;
        ResourceBitset resourceMask =
            ResourceMaskBuilder.ForRegisterRead(2) |
            ResourceMaskBuilder.ForRegisterWrite(destinationRegister);

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: (byte)destinationRegister, rs1: 2);
        var microOp = new ConstantWriteBackMicroOp(destinationRegister, 0x1234UL, resourceMask);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8600UL);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

        Assert.True(executeStage.Valid);
        Assert.Equal(resourceMask, executeStage.ResourceMask);
        Assert.NotEqual(0UL, executeStage.ResourceToken);
    }

    [Fact]
    public void SingleLaneExecute_WhenMicroOpCarriesZeroResourceMask_ThenKeepsZeroToken()
    {
        const ushort destinationRegister = 10;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: (byte)destinationRegister, rs1: 3);
        var microOp = new ConstantWriteBackMicroOp(destinationRegister, 0x5678UL, ResourceBitset.Zero);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8700UL);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

        Assert.True(executeStage.Valid);
        Assert.Equal(ResourceBitset.Zero, executeStage.ResourceMask);
        Assert.Equal(0UL, executeStage.ResourceToken);
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

