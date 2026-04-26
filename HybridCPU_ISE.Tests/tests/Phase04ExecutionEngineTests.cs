// Phase 04: Execution Engine Alignment
// Covers: MULH/MULHU/MULHSU/DIVU/REM/REMU, explicit unsupported direct Atomic/stream/VMX execute-surface rejection,
// ISA-v4 dispatch, InvalidOpcodeException

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase04
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Simple dictionary-backed ICanonicalCpuState stub for unit tests.</summary>
    internal sealed class FakeCpuState : ICanonicalCpuState
    {
        private readonly Dictionary<ushort, ulong> _regs = new();
        private ulong _pc;

        public ulong ReadIntRegister(ushort id)  => _regs.TryGetValue(id, out var v) ? v : 0UL;
        public void WriteIntRegister(ushort id, ulong value)
        {
            if (id != 0) _regs[id] = value;
        }

        public long ReadRegister(byte vtId, int regId) =>
            unchecked((long)ReadIntRegister((ushort)regId));

        public void WriteRegister(byte vtId, int regId, ulong value) =>
            WriteIntRegister((ushort)regId, value);

        public void SetReg(ushort id, ulong value) => _regs[id] = value;

        public ulong GetInstructionPointer()            => _pc;
        public void  SetInstructionPointer(ulong ip)    => _pc = ip;

        public ulong ReadPc(byte vtId) => GetInstructionPointer();
        public void WritePc(byte vtId, ulong pc) => SetInstructionPointer(pc);
        public ushort GetCoreID()                       => 0;

        // в”Ђв”Ђ Unused state surface (required by interface) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public ulong  GetVL()                           => 0;
        public void   SetVL(ulong vl)                   { }
        public ulong  GetVLMAX()                        => 0;
        public byte   GetSEW()                          => 0;
        public void   SetSEW(byte s)                    { }
        public byte   GetLMUL()                         => 0;
        public void   SetLMUL(byte l)                   { }
        public bool   GetTailAgnostic()                 => false;
        public void   SetTailAgnostic(bool a)           { }
        public bool   GetMaskAgnostic()                 => false;
        public void   SetMaskAgnostic(bool a)           { }
        public uint   GetExceptionMask()                => 0;
        public void   SetExceptionMask(uint m)          { }
        public uint   GetExceptionPriority()            => 0;
        public void   SetExceptionPriority(uint p)      { }
        public byte   GetRoundingMode()                 => 0;
        public void   SetRoundingMode(byte m)           { }
        public ulong  GetOverflowCount()                => 0;
        public ulong  GetUnderflowCount()               => 0;
        public ulong  GetDivByZeroCount()               => 0;
        public ulong  GetInvalidOpCount()               => 0;
        public ulong  GetInexactCount()                 => 0;
        public void   ClearExceptionCounters()          { }
        public bool   GetVectorDirty()                  => false;
        public void   SetVectorDirty(bool d)            { }
        public bool   GetVectorEnabled()                => false;
        public void   SetVectorEnabled(bool e)          { }
        public ushort GetPredicateMask(ushort id)       => 0;
        public void   SetPredicateMask(ushort id, ushort m) { }
        public ulong  GetCycleCount()                   => 0;
        public ulong  GetInstructionsRetired()          => 0;
        public double GetIPC()                          => 0;

        // в”Ђв”Ђ Pipeline FSM (Phase 05) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private PipelineState _pipelineState = PipelineState.Task;
        public PipelineState GetCurrentPipelineState()                   => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state)         => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger t) => _pipelineState = PipelineFsmGuard.Transition(_pipelineState, t);
    }

    /// <summary>In-memory atomic bus that simulates a single 64-bit word at a fixed address.</summary>
    internal sealed class FakeAtomicBus : IAtomicMemoryBus
    {
        private readonly Dictionary<ulong, ulong> _mem = new();

        public void Set(ulong addr, ulong value) => _mem[addr] = value;
        public ulong Get(ulong addr)             => _mem.TryGetValue(addr, out var v) ? v : 0UL;

        public ulong AtomicRMW64(ulong address, Func<ulong, ulong> modify)
        {
            ulong old = Get(address);
            _mem[address] = modify(old);
            return old;
        }
    }

    internal sealed class FakeEventQueue : IPipelineEventQueue
    {
        public List<PipelineEvent> Events { get; } = new();

        public void Enqueue(PipelineEvent evt) => Events.Add(evt);
    }

    internal static class IrBuilder
    {
        public static InstructionIR Make(
            InstructionsEnum opcode,
            byte rd = 0, byte rs1 = 0, byte rs2 = 0, long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode   = opcode,
                Class             = InstructionClassifier.GetClass(opcode),
                SerializationClass= InstructionClassifier.GetSerializationClass(opcode),
                Rd   = rd, Rs1 = rs1, Rs2 = rs2,
                Imm  = imm,
            };
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 1. Enum completeness вЂ” new ISA v4 opcodes present in the enum
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_EnumCompletenessTests
    {
        [Theory]
        [InlineData(InstructionsEnum.MULH,   220)]
        [InlineData(InstructionsEnum.MULHU,  221)]
        [InlineData(InstructionsEnum.MULHSU, 222)]
        [InlineData(InstructionsEnum.DIVU,   223)]
        [InlineData(InstructionsEnum.REM,    224)]
        [InlineData(InstructionsEnum.REMU,   225)]
        public void MExtension_Opcodes_HaveExpectedSlots(InstructionsEnum opcode, int expected)
        {
            Assert.Equal(expected, (int)opcode);
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_D,  230)]
        [InlineData(InstructionsEnum.AMOSWAP_D, 231)]
        [InlineData(InstructionsEnum.AMOOR_D,   232)]
        [InlineData(InstructionsEnum.AMOAND_D,  233)]
        [InlineData(InstructionsEnum.AMOXOR_D,  234)]
        [InlineData(InstructionsEnum.AMOMIN_D,  235)]
        [InlineData(InstructionsEnum.AMOMAX_D,  236)]
        [InlineData(InstructionsEnum.AMOMINU_D, 237)]
        [InlineData(InstructionsEnum.AMOMAXU_D, 238)]
        public void AmoDoubleword_Opcodes_HaveExpectedSlots(InstructionsEnum opcode, int expected)
        {
            Assert.Equal(expected, (int)opcode);
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD,       240)]
        [InlineData(InstructionsEnum.WFE,         241)]
        [InlineData(InstructionsEnum.SEV,         242)]
        [InlineData(InstructionsEnum.POD_BARRIER, 243)]
        [InlineData(InstructionsEnum.VT_BARRIER,  244)]
        public void SmtVt_Opcodes_HaveExpectedSlots(InstructionsEnum opcode, int expected)
        {
            Assert.Equal(expected, (int)opcode);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON,    250)]
        [InlineData(InstructionsEnum.VMXOFF,   251)]
        [InlineData(InstructionsEnum.VMLAUNCH, 252)]
        [InlineData(InstructionsEnum.VMRESUME, 253)]
        [InlineData(InstructionsEnum.VMREAD,   254)]
        [InlineData(InstructionsEnum.VMWRITE,  255)]
        [InlineData(InstructionsEnum.VMCLEAR,  256)]
        [InlineData(InstructionsEnum.VMPTRLD,  257)]
        public void Vmx_Opcodes_HaveExpectedSlots(InstructionsEnum opcode, int expected)
        {
            Assert.Equal(expected, (int)opcode);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 2. InstructionClassifier вЂ” new opcodes get correct InstructionClass
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_ClassifierTests
    {
        [Theory]
        [InlineData(InstructionsEnum.MULH)]
        [InlineData(InstructionsEnum.MULHU)]
        [InlineData(InstructionsEnum.MULHSU)]
        [InlineData(InstructionsEnum.DIVU)]
        [InlineData(InstructionsEnum.REM)]
        [InlineData(InstructionsEnum.REMU)]
        public void MExtension_ClassifiesAs_ScalarAlu(InstructionsEnum op)
        {
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(op));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_D)]
        [InlineData(InstructionsEnum.AMOSWAP_D)]
        [InlineData(InstructionsEnum.AMOOR_D)]
        [InlineData(InstructionsEnum.AMOAND_D)]
        [InlineData(InstructionsEnum.AMOXOR_D)]
        [InlineData(InstructionsEnum.AMOMIN_D)]
        [InlineData(InstructionsEnum.AMOMAX_D)]
        [InlineData(InstructionsEnum.AMOMINU_D)]
        [InlineData(InstructionsEnum.AMOMAXU_D)]
        public void AmoDoubleword_ClassifiesAs_Atomic(InstructionsEnum op)
        {
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(op));
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.WFE)]
        [InlineData(InstructionsEnum.SEV)]
        [InlineData(InstructionsEnum.POD_BARRIER)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void SmtVt_ClassifiesAs_SmtVt(InstructionsEnum op)
        {
            Assert.Equal(InstructionClass.SmtVt, InstructionClassifier.GetClass(op));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMXOFF)]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        [InlineData(InstructionsEnum.VMREAD)]
        [InlineData(InstructionsEnum.VMWRITE)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        [InlineData(InstructionsEnum.VMPTRLD)]
        public void Vmx_ClassifiesAs_Vmx(InstructionsEnum op)
        {
            Assert.Equal(InstructionClass.Vmx, InstructionClassifier.GetClass(op));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_D)]
        [InlineData(InstructionsEnum.AMOSWAP_D)]
        public void AmoDoubleword_SerializationClass_IsAtomicSerial(InstructionsEnum op)
        {
            Assert.Equal(SerializationClass.AtomicSerial, InstructionClassifier.GetSerializationClass(op));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        public void Vmx_SerializationClass_IsVmxSerial(InstructionsEnum op)
        {
            Assert.Equal(SerializationClass.VmxSerial, InstructionClassifier.GetSerializationClass(op));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 3. OpcodeInfo вЂ” new opcodes are registered
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_OpcodeInfoTests
    {
        [Theory]
        [InlineData(InstructionsEnum.MULH,   "MULH")]
        [InlineData(InstructionsEnum.MULHU,  "MULHU")]
        [InlineData(InstructionsEnum.MULHSU, "MULHSU")]
        [InlineData(InstructionsEnum.DIVU,   "DIVU")]
        [InlineData(InstructionsEnum.REM,    "REM")]
        [InlineData(InstructionsEnum.REMU,   "REMU")]
        public void MExtension_OpcodeInfo_Registered(InstructionsEnum op, string mnemonic)
        {
            var info = OpcodeRegistry.GetInfo((uint)op);
            Assert.NotNull(info);
            Assert.Equal(mnemonic, info!.Value.Mnemonic);
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_D,  "AMOADD.D")]
        [InlineData(InstructionsEnum.AMOSWAP_D, "AMOSWAP.D")]
        [InlineData(InstructionsEnum.AMOMAXU_D, "AMOMAXU.D")]
        public void AmoDoubleword_OpcodeInfo_Registered(InstructionsEnum op, string mnemonic)
        {
            var info = OpcodeRegistry.GetInfo((uint)op);
            Assert.NotNull(info);
            Assert.Equal(mnemonic, info!.Value.Mnemonic);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON,   "VMXON")]
        [InlineData(InstructionsEnum.VMPTRLD, "VMPTRLD")]
        public void Vmx_OpcodeInfo_Registered(InstructionsEnum op, string mnemonic)
        {
            var info = OpcodeRegistry.GetInfo((uint)op);
            Assert.NotNull(info);
            Assert.Equal(mnemonic, info!.Value.Mnemonic);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 4. ExecutionDispatcherV4 вЂ” ScalarAlu: M-extension ops
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_AluMExtensionTests
    {
        private static readonly ExecutionDispatcherV4 _disp = new();

        private static ulong Run(InstructionsEnum op, ulong rs1, ulong rs2, byte rd = 1)
        {
            var state = new FakeCpuState();
            state.SetReg(1, rs1);
            state.SetReg(2, rs2);
            var ir = IrBuilder.Make(op, rd: rd, rs1: 1, rs2: 2);
            _disp.Execute(ir, state);
            return state.ReadIntRegister(rd);
        }

        // в”Ђв”Ђ MULH (signed Г— signed, upper 64 bits) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void MULH_PositivePositive_UpperHalf()
        {
            // 0x7FFF_FFFF_FFFF_FFFF * 0x7FFF_FFFF_FFFF_FFFF
            // = 0x3FFF_FFFF_FFFF_FFFF_0000_0000_0000_0001
            // upper 64 = 0x3FFF_FFFF_FFFF_FFFF
            ulong a = 0x7FFF_FFFF_FFFF_FFFF;
            ulong expected = (ulong)(long)(((Int128)(long)a * (Int128)(long)a) >> 64);
            Assert.Equal(expected, Run(InstructionsEnum.MULH, a, a));
        }

        [Fact]
        public void MULH_NegativeNegative_ReturnsPositiveUpperHalf()
        {
            ulong minusOne = unchecked((ulong)-1L);
            // (-1) * (-1) = 1; upper 64 = 0
            Assert.Equal(0UL, Run(InstructionsEnum.MULH, minusOne, minusOne));
        }

        [Fact]
        public void MULH_NegativePositive_ReturnsNegativeUpperHalf()
        {
            ulong minusOne = unchecked((ulong)-1L);
            ulong two      = 2;
            // (-1) * 2 = -2; upper 64 = -1 (sign-extended)
            Assert.Equal(unchecked((ulong)-1L), Run(InstructionsEnum.MULH, minusOne, two));
        }

        // в”Ђв”Ђ MULHU (unsigned Г— unsigned, upper 64 bits) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void MULHU_MaxValues_ReturnsNonZeroUpperHalf()
        {
            ulong max = ulong.MaxValue;
            // (2^64-1)^2 = 2^128 - 2^65 + 1; upper 64 = 2^64 - 2 = 0xFFFF...FFFE
            ulong expected = (ulong)(((UInt128)max * (UInt128)max) >> 64);
            Assert.Equal(expected, Run(InstructionsEnum.MULHU, max, max));
        }

        [Fact]
        public void MULHU_SmallValues_UpperHalfIsZero()
        {
            Assert.Equal(0UL, Run(InstructionsEnum.MULHU, 5, 6));
        }

        // в”Ђв”Ђ MULHSU (signed Г— unsigned, upper 64 bits) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void MULHSU_NegativeSigned_UnsignedOperand()
        {
            ulong minusOne = unchecked((ulong)-1L);
            ulong large    = ulong.MaxValue;
            // (-1) * (2^64 - 1) = -(2^64 - 1) в†’ upper 64 = -1 (all ones)
            // zero-extend rs2 via (Int128)(ulong) to preserve unsigned semantics
            ulong expected = (ulong)(long)(((Int128)(long)minusOne * (Int128)(ulong)large) >> 64);
            Assert.Equal(expected, Run(InstructionsEnum.MULHSU, minusOne, large));
        }

        // в”Ђв”Ђ DIVU (unsigned divide, div-by-zero в†’ 2^64-1) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void DIVU_Normal_Division()
        {
            Assert.Equal(3UL, Run(InstructionsEnum.DIVU, 10, 3));
        }

        [Fact]
        public void DIVU_DivByZero_Returns2Pow64Minus1()
        {
            Assert.Equal(ulong.MaxValue, Run(InstructionsEnum.DIVU, 42, 0));
        }

        [Fact]
        public void DIVU_LargeUnsignedDividend()
        {
            // 0xFFFF_FFFF_FFFF_FFFE / 2 = 0x7FFF_FFFF_FFFF_FFFF
            Assert.Equal(0x7FFF_FFFF_FFFF_FFFFul, Run(InstructionsEnum.DIVU, 0xFFFF_FFFF_FFFF_FFFEul, 2));
        }

        // в”Ђв”Ђ REM (signed remainder, div-by-zero в†’ dividend) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void REM_PositiveDividend()
        {
            Assert.Equal(1UL, Run(InstructionsEnum.REM, 10, 3));
        }

        [Fact]
        public void REM_NegativeDividend()
        {
            // -7 % 3 = -1 (C# semantics = ISA spec semantics: sign follows dividend)
            ulong expected = unchecked((ulong)-1L);
            Assert.Equal(expected, Run(InstructionsEnum.REM, unchecked((ulong)-7L), 3));
        }

        [Fact]
        public void REM_DivByZero_ReturnsDividend()
        {
            Assert.Equal(42UL, Run(InstructionsEnum.REM, 42, 0));
        }

        // в”Ђв”Ђ REMU (unsigned remainder, div-by-zero в†’ dividend) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void REMU_Normal()
        {
            Assert.Equal(1UL, Run(InstructionsEnum.REMU, 10, 3));
        }

        [Fact]
        public void REMU_DivByZero_ReturnsDividend()
        {
            Assert.Equal(99UL, Run(InstructionsEnum.REMU, 99, 0));
        }

        // в”Ђв”Ђ Rd == 0: writes are discarded в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void MULH_Rd0_WritesDiscarded()
        {
            var result = Run(InstructionsEnum.MULH, 5, 6, rd: 0);
            Assert.Equal(0UL, result); // x0 always reads 0
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 5. ExecutionDispatcherV4 вЂ” Atomic direct surface contract
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_AtomicSurfaceContractTests
    {
        [Theory]
        [InlineData(InstructionsEnum.LR_W)]
        [InlineData(InstructionsEnum.SC_D)]
        [InlineData(InstructionsEnum.AMOADD_W)]
        [InlineData(InstructionsEnum.AMOADD_D)]
        public void Atomic_Opcodes_AreNotRoutableThroughExecutionDispatcherV4(InstructionsEnum op)
        {
            Assert.False(new ExecutionDispatcherV4(new FakeAtomicBus()).CanRouteToConfiguredExecutionSurface(IrBuilder.Make(op)));
        }

        [Fact]
        public void Atomic_Execute_Rejection_LeavesBusAndRegistersUntouched()
        {
            const ulong addr = 0x1000;
            var bus = new FakeAtomicBus();
            var dispatcher = new ExecutionDispatcherV4(bus);
            bus.Set(addr, 10UL);

            var state = new FakeCpuState();
            state.SetReg(1, addr);
            state.SetReg(2, 5UL);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(IrBuilder.Make(InstructionsEnum.AMOADD_D, rd: 3, rs1: 1, rs2: 2), state));

            Assert.Contains("Atomic opcode", ex.Message, StringComparison.Ordinal);
            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Equal(10UL, bus.Get(addr));
            Assert.Equal(0UL, state.ReadIntRegister(3));
        }
    }

    public sealed class Phase04_StreamExecuteSurfaceContractTests
    {
        private static (InvalidOperationException Exception, FakeEventQueue Queue, FakeCpuState State) InvokePrivateExecutionUnit(
            string methodName,
            InstructionIR ir)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            state.SetInstructionPointer(0x4000);

            MethodInfo method = typeof(ExecutionDispatcherV4).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Private method {methodName} was not found.");

            TargetInvocationException ex = Assert.Throws<TargetInvocationException>(
                () => method.Invoke(dispatcher, new object[] { ir, state, 9UL, (byte)1 }));

            return (Assert.IsType<InvalidOperationException>(ex.InnerException), queue, state);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_START)]
        [InlineData(InstructionsEnum.STREAM_WAIT)]
        public void StreamControl_Opcodes_AreNotRoutableThroughExecutionDispatcherV4(InstructionsEnum op)
        {
            Assert.False(new ExecutionDispatcherV4().CanRouteToConfiguredExecutionSurface(IrBuilder.Make(op)));
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP, "stub success/trace")]
        [InlineData(InstructionsEnum.STREAM_START, "stub success/trace")]
        [InlineData(InstructionsEnum.STREAM_WAIT, "CaptureRetireWindowPublications")]
        public void StreamControl_Execute_Rejection_IsExplicitAndSideEffectFree(
            InstructionsEnum opcode,
            string expectedMessageToken)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            state.SetInstructionPointer(0x4000);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(IrBuilder.Make(opcode), state, bundleSerial: 9, vtId: 1));

            Assert.Contains(expectedMessageToken, ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Equal(0x4000UL, state.GetInstructionPointer());
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_START)]
        [InlineData(InstructionsEnum.STREAM_WAIT)]
        public void StreamControl_InternalSystemUnit_RejectsHiddenStubSuccess(
            InstructionsEnum opcode)
        {
            var (ex, queue, state) = InvokePrivateExecutionUnit("ExecuteSystem", IrBuilder.Make(opcode));

            Assert.Contains("inner-unit stub success", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Equal(0x4000UL, state.GetInstructionPointer());
        }

        [Fact]
        public void StreamWait_InternalSmtVtUnit_RejectsHiddenStubSuccess()
        {
            var (ex, queue, state) = InvokePrivateExecutionUnit("ExecuteSmtVt", IrBuilder.Make(InstructionsEnum.STREAM_WAIT));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Contains("inner-unit stub success", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Equal(0x4000UL, state.GetInstructionPointer());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 6. ExecutionDispatcherV4 вЂ” System / SmtVt / direct VMX surface contract
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_SystemSmtVtVmxTests
    {
        private static readonly FakeCpuState _state = new();

        private static (ExecutionResult Result, FakeEventQueue Queue) ExecWithQueue(InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            return (dispatcher.Execute(IrBuilder.Make(op), _state), queue);
        }

        private static ExecutionResult Exec(InstructionsEnum op)
            => new ExecutionDispatcherV4().Execute(IrBuilder.Make(op), _state);

        [Theory]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.WFE)]
        [InlineData(InstructionsEnum.SEV)]
        [InlineData(InstructionsEnum.POD_BARRIER)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void SmtVt_Opcodes_RequireDirectCompatRetireForEagerExecute(InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(op);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL)]
        [InlineData(InstructionsEnum.EBREAK)]
        [InlineData(InstructionsEnum.MRET)]
        [InlineData(InstructionsEnum.SRET)]
        [InlineData(InstructionsEnum.WFI)]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void SystemEvent_Opcodes_RequireDirectCompatRetireForEagerExecute(
            InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(op);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.Interrupt)]
        [InlineData(InstructionsEnum.InterruptReturn)]
        public void RetainedInterruptSystemOpcodes_RejectEagerExecuteContour(
            InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(op);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("Retained system opcode", ex.Message, StringComparison.Ordinal);
            Assert.Contains("typed mainline retire/boundary carrier", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVEXCPMASK)]
        [InlineData(InstructionsEnum.VSETVEXCPPRI)]
        public void VectorExceptionControlCsr_Opcodes_RequireCanonicalMainlineRetireForEagerExecute(
            InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            state.SetReg(1, 0x12UL);
            var ir = IrBuilder.Make(op, rs1: 1);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("canonical mainline CSR/materializer surface", ex.Message, StringComparison.Ordinal);
            Assert.Contains("lane-7 pipeline path", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVL)]
        [InlineData(InstructionsEnum.VSETVLI)]
        [InlineData(InstructionsEnum.VSETIVLI)]
        public void VectorConfigSystemOpcodes_RequireCanonicalMainlineRetireForEagerExecute(
            InstructionsEnum op)
        {
            var queue = new FakeEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            var state = new FakeCpuState();
            state.SetReg(5, 19UL);
            state.SetReg(6, 0x43UL);
            var ir = IrBuilder.Make(op, rd: 4, rs1: 5, rs2: 6, imm: 13);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("Vector-config opcode", ex.Message, StringComparison.Ordinal);
            Assert.Contains("authoritative system-singleton carrier", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMXOFF)]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        [InlineData(InstructionsEnum.VMREAD)]
        [InlineData(InstructionsEnum.VMWRITE)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        [InlineData(InstructionsEnum.VMPTRLD)]
        public void Vmx_Opcodes_RequireDirectCompatRetireForEagerExecute(InstructionsEnum op)
        {
            var csr = new CsrFile();
            var dispatcher = new ExecutionDispatcherV4(
                csrFile: csr,
                vmxUnit: new VmxExecutionUnit(csr, new VmcsManager()));
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(op);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state, bundleSerial: 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 7. ExecutionDispatcherV4 вЂ” class-level dispatch routing
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_DispatchRoutingTests
    {
        private static readonly ExecutionDispatcherV4 _disp = new();

        [Fact]
        public void Dispatch_ScalarAlu_Addition_Executes()
        {
            var state = new FakeCpuState();
            state.SetReg(1, 10); state.SetReg(2, 5);
            var ir = IrBuilder.Make(InstructionsEnum.Addition, rd: 3, rs1: 1, rs2: 2);
            _disp.Execute(ir, state);
            Assert.Equal(15UL, state.ReadIntRegister(3));
        }

        [Fact]
        public void Dispatch_Atomic_AMO_D_IsRejectedThroughExplicitSurfaceContract()
        {
            var disp2 = new ExecutionDispatcherV4(new FakeAtomicBus());
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(InstructionsEnum.AMOADD_D, rd: 4, rs1: 1, rs2: 2);

            Assert.False(disp2.CanRouteToConfiguredExecutionSurface(ir));
            Assert.Throws<InvalidOperationException>(() => disp2.Execute(ir, state));
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_START)]
        [InlineData(InstructionsEnum.STREAM_WAIT)]
        public void Dispatch_StreamControl_EagerExecute_IsRejectedThroughExplicitSurfaceContract(InstructionsEnum opcode)
        {
            var state = new FakeCpuState();
            var ir = IrBuilder.Make(opcode);

            Assert.False(_disp.CanRouteToConfiguredExecutionSurface(ir));
            Assert.Throws<InvalidOperationException>(() => _disp.Execute(ir, state));
        }

        [Theory]
        [InlineData(InstructionsEnum.Load)]
        [InlineData(InstructionsEnum.Store)]
        [InlineData(InstructionsEnum.VGATHER)]
        [InlineData(InstructionsEnum.VSCATTER)]
        [InlineData(InstructionsEnum.MTILE_LOAD)]
        [InlineData(InstructionsEnum.MTILE_STORE)]
        public void Dispatch_NonScalarMemoryEagerExecute_IsRejectedThroughExplicitSurfaceContract(
            InstructionsEnum opcode)
        {
            var state = new FakeCpuState();
            state.SetReg(1, 0x300);
            var ir = IrBuilder.Make(opcode, rd: 2, rs1: 1, imm: 16);

            Assert.False(_disp.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => _disp.Execute(ir, state));

            Assert.Contains("pipeline execution remains the supported path", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Dispatch_ControlFlow_JAL_Redirects()
        {
            var state = new FakeCpuState();
            state.SetInstructionPointer(0x100);
            var ir = IrBuilder.Make(InstructionsEnum.JAL, rd: 1, imm: 0x20);
            var result = _disp.Execute(ir, state);
            Assert.True(result.PcRedirected);
            Assert.Equal(0x120UL, result.NewPc);
            Assert.Equal(0x104UL, state.ReadIntRegister(1)); // ra = PC + 4
        }

        [Fact]
        public void Dispatch_UnknownClass_ThrowsUnreachableException()
        {
            // Craft an IR with an out-of-range InstructionClass value
            var ir = new InstructionIR
            {
                CanonicalOpcode    = InstructionsEnum.Nope,
                Class              = (InstructionClass)0xFF,  // invalid
                SerializationClass = SerializationClass.Free,
                Rd = 0, Rs1 = 0, Rs2 = 0, Imm = 0,
            };
            Assert.Throws<System.Diagnostics.UnreachableException>(
                () => _disp.Execute(ir, new FakeCpuState()));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // 8. ExecutionResult value semantics
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase04_ExecutionResultTests
    {
        [Fact]
        public void Ok_Default_NoPcRedirect_NoTrap()
        {
            var r = ExecutionResult.Ok(42);
            Assert.Equal(42UL, r.Value);
            Assert.False(r.PcRedirected);
            Assert.False(r.TrapRaised);
        }

        [Fact]
        public void Redirect_SetsPcRedirected()
        {
            var r = ExecutionResult.Redirect(0xDEAD, 0xBEEF);
            Assert.True(r.PcRedirected);
            Assert.Equal(0xDEADUL, r.NewPc);
            Assert.Equal(0xBEEFUL, r.Value);
            Assert.False(r.TrapRaised);
        }

        [Fact]
        public void Trap_SetsTrapRaised()
        {
            var r = ExecutionResult.Trap();
            Assert.True(r.TrapRaised);
            Assert.False(r.PcRedirected);
        }
    }
}

