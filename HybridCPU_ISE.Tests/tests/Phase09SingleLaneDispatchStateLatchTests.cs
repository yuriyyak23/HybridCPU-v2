using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneDispatchStateLatchTests
{
    private sealed class TaggedScalarMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public TaggedScalarMicroOp(
            ushort destRegId,
            ulong resultValue,
            int ownerThreadId,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag)
        {
            _resultValue = resultValue;
            OpCode = (uint)InstructionsEnum.ADDI;
            DestRegID = destRegId;
            WritesRegister = true;
            WriteRegisters = new[] { (int)destRegId };
            OwnerThreadId = ownerThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            IsFspInjected = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
            Placement = Placement with { DomainTag = domainTag };
            RefreshAdmissionMetadata();
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic single-lane state-latch carrier";
    }

    [Fact]
    public void SingleLaneExecute_WhenDispatchStateIsLatched_ThenExecuteStageCarriesLiveMetadata()
    {
        const ushort destinationRegister = 9;
        const int ownerThreadId = 1;
        const int virtualThreadId = 2;
        const int ownerContextId = 5;
        const ulong domainTag = 0xAUL;
        const ulong pc = 0x8A00UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.ADDI,
                rd: (byte)destinationRegister,
                rs1: 4,
                rs2: 7,
                virtualThreadId: (byte)virtualThreadId);
        var microOp = new TaggedScalarMicroOp(
            destinationRegister,
            resultValue: 0x55AAUL,
            ownerThreadId,
            virtualThreadId,
            ownerContextId,
            domainTag);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: pc,
            admissionExecutionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared);

        ExecuteStage executeStage = core.GetExecuteStage();

        Assert.True(executeStage.Valid);
        Assert.Equal(pc, executeStage.PC);
        Assert.Equal(instruction.OpCode, executeStage.OpCode);
        Assert.True(executeStage.WritesRegister);
        Assert.Equal(destinationRegister, executeStage.DestRegID);
        Assert.Same(microOp, executeStage.MicroOp);
        Assert.Equal(ownerThreadId, executeStage.OwnerThreadId);
        Assert.Equal(virtualThreadId, executeStage.VirtualThreadId);
        Assert.Equal(ownerContextId, executeStage.OwnerContextId);
        Assert.True(executeStage.WasFspInjected);
        Assert.Equal(ownerThreadId, executeStage.OriginalThreadId);
        Assert.Equal(domainTag, executeStage.DomainTag);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ClusterPrepared, executeStage.AdmissionExecutionMode);
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
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
            VirtualThreadId = virtualThreadId
        };
    }
}

