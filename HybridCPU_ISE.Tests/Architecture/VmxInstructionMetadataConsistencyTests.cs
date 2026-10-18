using System.Collections.Generic;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class VmxInstructionMetadataConsistencyTests
{
    public static IEnumerable<object[]> Vmx8Specs() =>
        VmxSpecTable.Vmx8Opcodes.Select(spec => new object[] { spec });

    [Theory]
    [MemberData(nameof(Vmx8Specs))]
    public void OpcodeRegistry_Classifier_AndInternalOpBuilder_MatchVmxSpec(VmxOpcodeSpec spec)
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(spec.Opcode));

        Assert.Equal(spec.Mnemonic, info.Mnemonic);
        Assert.Equal(spec.Category, info.Category);
        Assert.Equal(spec.OperandCount, info.OperandCount);
        Assert.Equal(spec.Flags, info.Flags);
        Assert.Equal(spec.ExecutionLatency, info.ExecutionLatency);
        Assert.Equal(spec.MemoryBandwidth, info.MemoryBandwidth);
        Assert.Equal(spec.InstructionClass, info.InstructionClass);
        Assert.Equal(spec.SerializationClass, info.SerializationClass);
        Assert.Equal(spec.InstructionClass, InstructionClassifier.GetClass(spec.Opcode));
        Assert.Equal(spec.SerializationClass, InstructionClassifier.GetSerializationClass(spec.Opcode));
        Assert.Equal(spec.InternalOpKindName, InternalOpBuilder.MapToKind(spec.Opcode).ToString());
    }

    [Theory]
    [MemberData(nameof(Vmx8Specs))]
    public void InstructionRegistry_MaterializesVmxMicroOp_RegisterRoles_AndLane7Placement(VmxOpcodeSpec spec)
    {
        DecoderContext context = BuildDecoderContext(spec);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(InstructionRegistry.CreateMicroOp(spec.Opcode, context));

        Assert.Equal(spec.Opcode, (ushort)microOp.OpCode);
        Assert.Equal(InstructionClass.Vmx, microOp.InstructionClass);
        Assert.Equal(SerializationClass.VmxSerial, microOp.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(spec.PinnedLaneId, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.False(microOp.IsMemoryOp);

        Assert.Equal(ExpectedReadRegisters(spec).ToArray(), microOp.ReadRegisters);
        Assert.Equal(ExpectedWriteRegisters(spec).ToArray(), microOp.WriteRegisters);
        Assert.Equal(spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.WritesRd), microOp.WritesRegister);
    }

    [Theory]
    [MemberData(nameof(Vmx8Specs))]
    public void CurrentFailClosedRouting_MapsEveryFrozenOpcodeToSpecOperationKind(VmxOpcodeSpec spec)
    {
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        InstructionIR instruction = BuildInstructionIr(spec);
        var microOp = new VmxMicroOp { OpCode = spec.Opcode, Instruction = instruction };

        Assert.True(microOp.Execute(ref core));
        VmxRetireEffect effect = microOp.CreateRetireEffect();

        Assert.Equal(spec.OperationKindName, effect.Operation.ToString());
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
    }

    [Fact]
    public void VmxOn_EncodedOperand_IsReservedIgnoredByVmx8Runtime()
    {
        VmxOpcodeSpec spec = VmxSpecTable.GetVmx8Opcode(IsaOpcodeValues.VMXON);
        DecoderContext context = BuildDecoderContext(spec);
        context.Reg2ID = 13;

        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(InstructionRegistry.CreateMicroOp(spec.Opcode, context));

        Assert.Equal(VmxOperandForm.ReservedIgnoredRegister, spec.OperandForm);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.False(microOp.WritesRegister);
    }

    [Fact]
    public void Vmread_CurrentRoutingFailsClosedWithoutVmcsFieldAuthority()
    {
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        VmxOpcodeSpec spec = VmxSpecTable.GetVmx8Opcode(IsaOpcodeValues.VMREAD);
        var microOp = new VmxMicroOp
        {
            OpCode = spec.Opcode,
            Instruction = BuildInstructionIr(spec, rd: 7, rs1: 2),
        };

        Assert.True(microOp.Execute(ref core));
        VmxRetireEffect effect = microOp.CreateRetireEffect();

        Assert.Equal(VmxOperationKind.VmRead, effect.Operation);
        Assert.True(effect.IsFaulted);
        Assert.False(effect.HasRegisterDestination);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
    }

    private static DecoderContext BuildDecoderContext(VmxOpcodeSpec spec)
    {
        return new DecoderContext
        {
            OpCode = spec.Opcode,
            Reg1ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.WritesRd) ? (ushort)7 : (ushort)0,
            Reg2ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs1) ||
                     spec.OperandForm == VmxOperandForm.ReservedIgnoredRegister
                ? (ushort)2
                : (ushort)0,
            Reg3ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs2) ? (ushort)3 : (ushort)0,
        };
    }

    private static InstructionIR BuildInstructionIr(VmxOpcodeSpec spec, byte? rd = null, byte? rs1 = null, byte? rs2 = null)
    {
        byte defaultRd = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.WritesRd) ? (byte)7 : (byte)0;
        byte defaultRs1 = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs1) ? (byte)2 : (byte)0;
        byte defaultRs2 = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs2) ? (byte)3 : (byte)0;

        return new InstructionIR
        {
            CanonicalOpcode = new IsaOpcode(spec.Opcode),
            Class = InstructionClass.Vmx,
            SerializationClass = SerializationClass.VmxSerial,
            Rd = rd ?? defaultRd,
            Rs1 = rs1 ?? defaultRs1,
            Rs2 = rs2 ?? defaultRs2,
            Imm = 0,
        };
    }

    private static IEnumerable<int> ExpectedReadRegisters(VmxOpcodeSpec spec)
    {
        if (spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs1))
        {
            yield return 2;
        }

        if (spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs2))
        {
            yield return 3;
        }
    }

    private static IEnumerable<int> ExpectedWriteRegisters(VmxOpcodeSpec spec)
    {
        if (spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.WritesRd))
        {
            yield return 7;
        }
    }

    private sealed class VmxMetadataCpuState : ICanonicalCpuState
    {
        private readonly ulong[,] _registers = new ulong[4, 32];
        private readonly ulong[] _pc = new ulong[4];
        private PipelineState _pipelineState = PipelineState.Task;

        public ulong GetVL() => 0;
        public void SetVL(ulong vl) { }
        public ulong GetVLMAX() => 0;
        public byte GetSEW() => 0;
        public void SetSEW(byte sew) { }
        public byte GetLMUL() => 0;
        public void SetLMUL(byte lmul) { }
        public bool GetTailAgnostic() => false;
        public void SetTailAgnostic(bool agnostic) { }
        public bool GetMaskAgnostic() => false;
        public void SetMaskAgnostic(bool agnostic) { }
        public uint GetExceptionMask() => 0;
        public void SetExceptionMask(uint mask) { }
        public uint GetExceptionPriority() => 0;
        public void SetExceptionPriority(uint priority) { }
        public byte GetRoundingMode() => 0;
        public void SetRoundingMode(byte mode) { }
        public ulong GetOverflowCount() => 0;
        public ulong GetUnderflowCount() => 0;
        public ulong GetDivByZeroCount() => 0;
        public ulong GetInvalidOpCount() => 0;
        public ulong GetInexactCount() => 0;
        public void ClearExceptionCounters() { }
        public bool GetVectorDirty() => false;
        public void SetVectorDirty(bool dirty) { }
        public bool GetVectorEnabled() => false;
        public void SetVectorEnabled(bool enabled) { }
        public long ReadRegister(byte vtId, int regId) => unchecked((long)_registers[vtId, regId]);
        public void WriteRegister(byte vtId, int regId, ulong value) => _registers[vtId, regId] = value;
        public ushort GetPredicateMask(ushort maskID) => 0;
        public void SetPredicateMask(ushort maskID, ushort mask) { }
        public ulong ReadPc(byte vtId) => _pc[vtId];
        public void WritePc(byte vtId, ulong pc) => _pc[vtId] = pc;
        public ushort GetCoreID() => 0;
        public ulong GetCycleCount() => 0;
        public ulong GetInstructionsRetired() => 0;
        public double GetIPC() => 0;
        public PipelineState GetCurrentPipelineState() => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state) => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger trigger) =>
            _pipelineState = PipelineFsmGuard.Transition(_pipelineState, trigger);
    }
}
