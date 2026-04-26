// Phase 07: Memory Subsystem вЂ” Typed Loads/Stores and Full 64-bit Atomic Plane
// Covers:
//   - IAtomicMemoryUnit interface: complete atomic memory contract
//   - MemoryUnit: typed load/store execution with sign/zero extension
//   - Alignment checking: misaligned access traps for halfword/word/doubleword
//   - ExecutionDispatcherV4: memory unit integration
//   - IMemoryBus: byte-level read/write interface
//   - MemoryAlignmentException: misalignment trap semantics

using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase07
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Simple dictionary-backed ICanonicalCpuState stub for Phase 07 memory tests.</summary>
    internal sealed class Mem07FakeCpuState : ICanonicalCpuState
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
        public ushort GetCoreID()                       => 0;

        public ulong ReadPc(byte vtId) => GetInstructionPointer();
        public void WritePc(byte vtId, ulong pc) => SetInstructionPointer(pc);

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

    /// <summary>Byte-addressable memory backed by a flat array (64 KB).</summary>
    internal sealed class FakeMemoryBus : IMemoryBus
    {
        private readonly byte[] _mem = new byte[65536]; // 64 KB

        public byte[] Read(ulong address, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(_mem, (int)address, result, 0, length);
            return result;
        }

        public void Write(ulong address, byte[] data)
        {
            Array.Copy(data, 0, _mem, (int)address, data.Length);
        }

        /// <summary>Write a byte at the given address.</summary>
        public void StoreByte(ulong addr, byte value) => _mem[(int)addr] = value;

        /// <summary>Write a little-endian 16-bit value.</summary>
        public void StoreHalf(ulong addr, ushort value)
        {
            _mem[(int)addr]     = (byte)(value & 0xFF);
            _mem[(int)addr + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>Write a little-endian 32-bit value.</summary>
        public void StoreWord(ulong addr, uint value)
        {
            _mem[(int)addr]     = (byte)(value & 0xFF);
            _mem[(int)addr + 1] = (byte)((value >> 8) & 0xFF);
            _mem[(int)addr + 2] = (byte)((value >> 16) & 0xFF);
            _mem[(int)addr + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>Write a little-endian 64-bit value.</summary>
        public void StoreDword(ulong addr, ulong value)
        {
            for (int i = 0; i < 8; i++)
                _mem[(int)addr + i] = (byte)((value >> (i * 8)) & 0xFF);
        }

        /// <summary>Read a little-endian 64-bit value.</summary>
        public ulong LoadDword(ulong addr) => BitConverter.ToUInt64(_mem, (int)addr);

        /// <summary>Read a little-endian 32-bit value.</summary>
        public uint LoadWord(ulong addr) =>
            (uint)(_mem[(int)addr] | (_mem[(int)addr + 1] << 8) |
                   (_mem[(int)addr + 2] << 16) | (_mem[(int)addr + 3] << 24));

        /// <summary>Read a little-endian 16-bit value.</summary>
        public ushort LoadHalf(ulong addr) =>
            (ushort)(_mem[(int)addr] | (_mem[(int)addr + 1] << 8));
    }

    internal static class Ir07
    {
        public static InstructionIR Make(
            InstructionsEnum opcode,
            byte rd = 0, byte rs1 = 0, byte rs2 = 0, long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = rd, Rs1 = rs1, Rs2 = rs2,
                Imm = imm,
            };
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 1. IAtomicMemoryUnit interface presence
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_IAtomicMemoryUnitInterfaceTests
    {
        [Fact]
        public void IAtomicMemoryUnit_InterfaceExists()
        {
            var type = typeof(IAtomicMemoryUnit);
            Assert.True(type.IsInterface);
        }

        [Theory]
        [InlineData("LoadReserved32")]
        [InlineData("StoreConditional32")]
        [InlineData("LoadReserved64")]
        [InlineData("StoreConditional64")]
        [InlineData("AtomicSwap32")]
        [InlineData("AtomicAdd32")]
        [InlineData("AtomicXor32")]
        [InlineData("AtomicAnd32")]
        [InlineData("AtomicOr32")]
        [InlineData("AtomicMinSigned32")]
        [InlineData("AtomicMaxSigned32")]
        [InlineData("AtomicMinUnsigned32")]
        [InlineData("AtomicMaxUnsigned32")]
        [InlineData("AtomicSwap64")]
        [InlineData("AtomicAdd64")]
        [InlineData("AtomicXor64")]
        [InlineData("AtomicAnd64")]
        [InlineData("AtomicOr64")]
        [InlineData("AtomicMin64Signed")]
        [InlineData("AtomicMax64Signed")]
        [InlineData("AtomicMin64Unsigned")]
        [InlineData("AtomicMax64Unsigned")]
        public void IAtomicMemoryUnit_ContainsRequiredMethod(string methodName)
        {
            var method = typeof(IAtomicMemoryUnit).GetMethod(methodName);
            Assert.NotNull(method);
        }

        [Fact]
        public void IAtomicMemoryUnit_HasExactly22Methods()
        {
            // 4 LR/SC + 9 AMO_W + 9 AMO_D = 22
            var methods = typeof(IAtomicMemoryUnit).GetMethods();
            Assert.Equal(22, methods.Length);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 2. MemoryUnit вЂ” signed load tests (LB, LH, LW with negative values)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_SignedLoadTests
    {
        private readonly FakeMemoryBus _bus = new();
        private readonly MemoryUnit _unit;
        private readonly Mem07FakeCpuState _state = new();

        public Phase07_SignedLoadTests()
        {
            _unit = new MemoryUnit(_bus);
            _state.SetReg(1, 0x100); // rs1 = base address 0x100
        }

        [Fact]
        public void LB_NegativeByte_SignExtendsTo64Bits()
        {
            _bus.StoreByte(0x100, 0x80); // -128 as signed byte
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            ulong result = _state.ReadIntRegister(2);
            // 0x80 sign-extended to 64 bits = 0xFFFFFFFFFFFFFF80
            Assert.Equal(unchecked((ulong)(long)(sbyte)0x80), result);
        }

        [Fact]
        public void LB_PositiveByte_SignExtendsTo64Bits()
        {
            _bus.StoreByte(0x100, 0x7F); // +127 as signed byte
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            Assert.Equal(0x7FUL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LH_NegativeHalfword_SignExtendsTo64Bits()
        {
            _bus.StoreHalf(0x100, 0x8000); // -32768 as signed halfword
            _unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), _state);
            ulong result = _state.ReadIntRegister(2);
            Assert.Equal(unchecked((ulong)(long)(short)0x8000), result);
        }

        [Fact]
        public void LH_PositiveHalfword_SignExtendsTo64Bits()
        {
            _bus.StoreHalf(0x100, 0x7FFF); // +32767
            _unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), _state);
            Assert.Equal(0x7FFFUL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LW_NegativeWord_SignExtendsTo64Bits()
        {
            _bus.StoreWord(0x100, 0x80000000); // -2147483648 as signed word
            _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state);
            ulong result = _state.ReadIntRegister(2);
            Assert.Equal(unchecked((ulong)(long)(int)0x80000000), result);
        }

        [Fact]
        public void LW_PositiveWord_SignExtendsTo64Bits()
        {
            _bus.StoreWord(0x100, 0x7FFFFFFF);
            _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state);
            Assert.Equal(0x7FFFFFFFUL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LB_WithImmediate_OffsetsFromBase()
        {
            _bus.StoreByte(0x104, 0xFE); // -2 as signed byte
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1, imm: 4), _state);
            ulong result = _state.ReadIntRegister(2);
            Assert.Equal(unchecked((ulong)(long)(sbyte)0xFE), result);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 3. MemoryUnit вЂ” unsigned load tests (LBU, LHU, LWU вЂ” zero extension)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_UnsignedLoadTests
    {
        private readonly FakeMemoryBus _bus = new();
        private readonly MemoryUnit _unit;
        private readonly Mem07FakeCpuState _state = new();

        public Phase07_UnsignedLoadTests()
        {
            _unit = new MemoryUnit(_bus);
            _state.SetReg(1, 0x200); // rs1 = base address 0x200
        }

        [Fact]
        public void LBU_HighBitByte_ZeroExtendsTo64Bits()
        {
            _bus.StoreByte(0x200, 0xFF);
            _unit.Execute(Ir07.Make(InstructionsEnum.LBU, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFUL, _state.ReadIntRegister(2)); // no sign extension
        }

        [Fact]
        public void LHU_HighBitHalfword_ZeroExtendsTo64Bits()
        {
            _bus.StoreHalf(0x200, 0xFFFF);
            _unit.Execute(Ir07.Make(InstructionsEnum.LHU, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFUL, _state.ReadIntRegister(2)); // no sign extension
        }

        [Fact]
        public void LWU_HighBitWord_ZeroExtendsTo64Bits()
        {
            _bus.StoreWord(0x200, 0xFFFFFFFF);
            _unit.Execute(Ir07.Make(InstructionsEnum.LWU, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFFFFFUL, _state.ReadIntRegister(2)); // no sign extension
        }

        [Fact]
        public void LBU_ValueBelowMidpoint_IdenticalToLB()
        {
            _bus.StoreByte(0x200, 0x42);
            _unit.Execute(Ir07.Make(InstructionsEnum.LBU, rd: 2, rs1: 1), _state);
            Assert.Equal(0x42UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LHU_Value8000_NotSignExtended()
        {
            _bus.StoreHalf(0x200, 0x8000);
            _unit.Execute(Ir07.Make(InstructionsEnum.LHU, rd: 2, rs1: 1), _state);
            Assert.Equal(0x8000UL, _state.ReadIntRegister(2)); // must NOT be sign-extended
        }

        [Fact]
        public void LWU_Value80000000_NotSignExtended()
        {
            _bus.StoreWord(0x200, 0x80000000);
            _unit.Execute(Ir07.Make(InstructionsEnum.LWU, rd: 2, rs1: 1), _state);
            Assert.Equal(0x80000000UL, _state.ReadIntRegister(2)); // must NOT be sign-extended
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 4. MemoryUnit вЂ” doubleword load test
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_DoublewordLoadTests
    {
        [Fact]
        public void LD_Full64Bit_NoExtension()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x300);
            bus.StoreDword(0x300, 0xDEADBEEFCAFEBABE);
            unit.Execute(Ir07.Make(InstructionsEnum.LD, rd: 2, rs1: 1), state);
            Assert.Equal(0xDEADBEEFCAFEBABEUL, state.ReadIntRegister(2));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 5. MemoryUnit вЂ” typed store tests (SB, SH, SW, SD)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_TypedStoreTests
    {
        private readonly FakeMemoryBus _bus = new();
        private readonly MemoryUnit _unit;
        private readonly Mem07FakeCpuState _state = new();

        public Phase07_TypedStoreTests()
        {
            _unit = new MemoryUnit(_bus);
            _state.SetReg(1, 0x400); // rs1 = base address 0x400
            _state.SetReg(2, 0xFEDCBA9876543210); // rs2 = value to store
        }

        [Fact]
        public void SB_StoresOnlyLow8Bits()
        {
            _unit.Execute(Ir07.Make(InstructionsEnum.SB, rs1: 1, rs2: 2), _state);
            byte[] raw = _bus.Read(0x400, 1);
            Assert.Equal(0x10, raw[0]);
        }

        [Fact]
        public void SH_StoresOnlyLow16Bits()
        {
            _unit.Execute(Ir07.Make(InstructionsEnum.SH, rs1: 1, rs2: 2), _state);
            ushort stored = _bus.LoadHalf(0x400);
            Assert.Equal((ushort)0x3210, stored);
        }

        [Fact]
        public void SW_StoresOnlyLow32Bits()
        {
            _unit.Execute(Ir07.Make(InstructionsEnum.SW, rs1: 1, rs2: 2), _state);
            uint stored = _bus.LoadWord(0x400);
            Assert.Equal(0x76543210U, stored);
        }

        [Fact]
        public void SD_StoresFull64Bits()
        {
            _unit.Execute(Ir07.Make(InstructionsEnum.SD, rs1: 1, rs2: 2), _state);
            ulong stored = _bus.LoadDword(0x400);
            Assert.Equal(0xFEDCBA9876543210UL, stored);
        }

        [Fact]
        public void SB_WithImmediate_OffsetsFromBase()
        {
            _unit.Execute(Ir07.Make(InstructionsEnum.SB, rs1: 1, rs2: 2, imm: 8), _state);
            byte[] raw = _bus.Read(0x408, 1);
            Assert.Equal(0x10, raw[0]);
        }

        [Fact]
        public void SH_DoesNotAffectAdjacentBytes()
        {
            _bus.StoreByte(0x402, 0xAA);
            _unit.Execute(Ir07.Make(InstructionsEnum.SH, rs1: 1, rs2: 2), _state);
            byte[] raw = _bus.Read(0x400, 3);
            Assert.Equal(0x10, raw[0]); // low byte of 0x3210
            Assert.Equal(0x32, raw[1]); // high byte of 0x3210
            Assert.Equal(0xAA, raw[2]); // adjacent byte unchanged
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 6. MemoryUnit вЂ” load/store round-trip tests
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_LoadStoreRoundTripTests
    {
        [Fact]
        public void SB_LBU_RoundTrip()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x500);
            state.SetReg(2, 0xAB);
            unit.Execute(Ir07.Make(InstructionsEnum.SB, rs1: 1, rs2: 2), state);
            unit.Execute(Ir07.Make(InstructionsEnum.LBU, rd: 3, rs1: 1), state);
            Assert.Equal(0xABUL, state.ReadIntRegister(3));
        }

        [Fact]
        public void SH_LHU_RoundTrip()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x600);
            state.SetReg(2, 0xBEEF);
            unit.Execute(Ir07.Make(InstructionsEnum.SH, rs1: 1, rs2: 2), state);
            unit.Execute(Ir07.Make(InstructionsEnum.LHU, rd: 3, rs1: 1), state);
            Assert.Equal(0xBEEFUL, state.ReadIntRegister(3));
        }

        [Fact]
        public void SW_LW_SignedRoundTrip()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x700);
            state.SetReg(2, 0xDEADBEEF); // low 32 bits = 0xDEADBEEF
            unit.Execute(Ir07.Make(InstructionsEnum.SW, rs1: 1, rs2: 2), state);
            unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 3, rs1: 1), state);
            // LW sign-extends: 0xDEADBEEF в†’ 0xFFFFFFFFDEADBEEF
            Assert.Equal(unchecked((ulong)(int)0xDEADBEEF), state.ReadIntRegister(3));
        }

        [Fact]
        public void SD_LD_RoundTrip()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x800);
            state.SetReg(2, 0x123456789ABCDEF0);
            unit.Execute(Ir07.Make(InstructionsEnum.SD, rs1: 1, rs2: 2), state);
            unit.Execute(Ir07.Make(InstructionsEnum.LD, rd: 3, rs1: 1), state);
            Assert.Equal(0x123456789ABCDEF0UL, state.ReadIntRegister(3));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 7. MemoryUnit вЂ” alignment trap tests
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_AlignmentTrapTests
    {
        private readonly FakeMemoryBus _bus = new();
        private readonly MemoryUnit _unit;
        private readonly Mem07FakeCpuState _state = new();

        public Phase07_AlignmentTrapTests()
        {
            _unit = new MemoryUnit(_bus, trapOnMisalign: true);
        }

        [Fact]
        public void LB_Unaligned_NoTrap()
        {
            _state.SetReg(1, 0x101); // odd address вЂ” OK for byte
            _bus.StoreByte(0x101, 0x42);
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            Assert.Equal(0x42UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void SB_Unaligned_NoTrap()
        {
            _state.SetReg(1, 0x103); // odd address вЂ” OK for byte
            _state.SetReg(2, 0x55);
            _unit.Execute(Ir07.Make(InstructionsEnum.SB, rs1: 1, rs2: 2), _state);
            // No exception = pass
        }

        [Fact]
        public void LH_MisalignedOddAddress_Traps()
        {
            _state.SetReg(1, 0x101); // not 2-byte aligned
            var ex = Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), _state));
            Assert.Equal(0x101UL, ex.Address);
            Assert.Equal(2, ex.RequiredAlignment);
            Assert.Equal("LH", ex.InstructionMnemonic);
        }

        [Fact]
        public void LHU_MisalignedOddAddress_Traps()
        {
            _state.SetReg(1, 0x103);
            Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.LHU, rd: 2, rs1: 1), _state));
        }

        [Fact]
        public void LW_Misaligned_Traps()
        {
            _state.SetReg(1, 0x102); // not 4-byte aligned
            var ex = Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state));
            Assert.Equal(4, ex.RequiredAlignment);
        }

        [Fact]
        public void LWU_Misaligned_Traps()
        {
            _state.SetReg(1, 0x101);
            Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.LWU, rd: 2, rs1: 1), _state));
        }

        [Fact]
        public void LD_Misaligned_Traps()
        {
            _state.SetReg(1, 0x104); // 4-byte aligned but not 8-byte aligned
            var ex = Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.LD, rd: 2, rs1: 1), _state));
            Assert.Equal(8, ex.RequiredAlignment);
        }

        [Fact]
        public void SH_Misaligned_Traps()
        {
            _state.SetReg(1, 0x101);
            _state.SetReg(2, 0x1234);
            Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.SH, rs1: 1, rs2: 2), _state));
        }

        [Fact]
        public void SW_Misaligned_Traps()
        {
            _state.SetReg(1, 0x102);
            _state.SetReg(2, 0x12345678);
            Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.SW, rs1: 1, rs2: 2), _state));
        }

        [Fact]
        public void SD_Misaligned_Traps()
        {
            _state.SetReg(1, 0x104); // 0x104 is not 8-byte aligned
            _state.SetReg(2, 0xDEADBEEF);
            Assert.Throws<MemoryAlignmentException>(() =>
                _unit.Execute(Ir07.Make(InstructionsEnum.SD, rs1: 1, rs2: 2), _state));
        }

        [Fact]
        public void LH_AlignedAddress_NoTrap()
        {
            _state.SetReg(1, 0x100);
            _bus.StoreHalf(0x100, 0x1234);
            _unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), _state);
            // No exception = pass
        }

        [Fact]
        public void LW_4ByteAligned_NoTrap()
        {
            _state.SetReg(1, 0x100);
            _bus.StoreWord(0x100, 0x12345678);
            _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state);
            // No exception = pass
        }

        [Fact]
        public void LD_8ByteAligned_NoTrap()
        {
            _state.SetReg(1, 0x100);
            _bus.StoreDword(0x100, 0x123456789ABCDEF0);
            _unit.Execute(Ir07.Make(InstructionsEnum.LD, rd: 2, rs1: 1), _state);
            // No exception = pass
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 8. MemoryUnit вЂ” alignment trap disabled mode
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_AlignmentDisabledTests
    {
        [Fact]
        public void LH_Misaligned_NoTrap_WhenDisabled()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus, trapOnMisalign: false);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x101); // misaligned
            bus.StoreHalf(0x101, 0xABCD);
            // Should not throw
            unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), state);
            Assert.Equal(unchecked((ulong)(long)(short)0xABCD), state.ReadIntRegister(2));
        }

        [Fact]
        public void SW_Misaligned_NoTrap_WhenDisabled()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus, trapOnMisalign: false);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x103); // misaligned
            state.SetReg(2, 0xDEADDEAD);
            unit.Execute(Ir07.Make(InstructionsEnum.SW, rs1: 1, rs2: 2), state);
            Assert.Equal(0xDEADDEADU, bus.LoadWord(0x103));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 9. MemoryUnit вЂ” x0 write protection (writes to rd=0 are discarded)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_X0WriteProtectionTests
    {
        [Fact]
        public void LB_Rd0_DoesNotWriteRegister()
        {
            var bus = new FakeMemoryBus();
            var unit = new MemoryUnit(bus);
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x100);
            bus.StoreByte(0x100, 0xFF);
            unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 0, rs1: 1), state);
            Assert.Equal(0UL, state.ReadIntRegister(0)); // x0 always 0
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 10. ExecutionDispatcherV4 вЂ” memory unit integration
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_DispatcherMemoryIntegrationTests
    {
        [Fact]
        public void Dispatcher_WithMemoryUnit_ResolvesTypedLoadWithoutRegisterMutation()
        {
            var bus = new FakeMemoryBus();
            var memUnit = new MemoryUnit(bus);
            var dispatcher = new ExecutionDispatcherV4(memoryUnit: memUnit);
            var state = new Mem07FakeCpuState();

            state.SetReg(1, 0x100);
            state.SetReg(2, 0xABCDUL);
            bus.StoreWord(0x100, 0xDEADBEEF);

            var result = dispatcher.Execute(
                Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), state);
            Assert.Equal(0x100UL, result.Value);
            Assert.Equal(0xABCDUL, state.ReadIntRegister(2));
        }

        [Fact]
        public void Dispatcher_WithoutMemoryUnit_ReturnsEffectiveAddress()
        {
            var dispatcher = new ExecutionDispatcherV4(); // no memory unit
            var state = new Mem07FakeCpuState();
            state.SetReg(1, 0x1000);

            var result = dispatcher.Execute(
                Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1, imm: 8), state);

            // EA-only mode: returns effective address, does not write to rd
            Assert.Equal(0x1008UL, result.Value);
        }

        [Fact]
        public void Dispatcher_WithMemoryUnit_ResolvesTypedStoreWithoutBusMutation()
        {
            var bus = new FakeMemoryBus();
            var memUnit = new MemoryUnit(bus);
            var dispatcher = new ExecutionDispatcherV4(memoryUnit: memUnit);
            var state = new Mem07FakeCpuState();

            state.SetReg(1, 0x200);
            state.SetReg(2, 0xCAFEBABE);

            var result = dispatcher.Execute(Ir07.Make(InstructionsEnum.SW, rs1: 1, rs2: 2), state);

            Assert.Equal(0x200UL, result.Value);
            Assert.Equal(0U, bus.LoadWord(0x200));
        }

        [Theory]
        [InlineData(InstructionsEnum.VGATHER)]
        [InlineData(InstructionsEnum.VSCATTER)]
        [InlineData(InstructionsEnum.MTILE_LOAD)]
        [InlineData(InstructionsEnum.MTILE_STORE)]
        public void Dispatcher_NonScalarMemoryOps_RejectEagerExecuteSurface(
            InstructionsEnum opcode)
        {
            var bus = new FakeMemoryBus();
            var memUnit = new MemoryUnit(bus);
            var dispatcher = new ExecutionDispatcherV4(memoryUnit: memUnit);
            var state = new Mem07FakeCpuState();

            state.SetReg(1, 0x300);
            state.SetReg(2, 0x55);
            InstructionIR ir = Ir07.Make(opcode, rd: 2, rs1: 1, rs2: 2, imm: 16);

            Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(ir));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(ir, state));

            Assert.Contains("EA-only success/trace", ex.Message, StringComparison.Ordinal);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 11. Classification audit вЂ” memory ops have correct class and serialization
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_ClassificationAuditTests
    {
        [Theory]
        [InlineData(InstructionsEnum.LB)]
        [InlineData(InstructionsEnum.LBU)]
        [InlineData(InstructionsEnum.LH)]
        [InlineData(InstructionsEnum.LHU)]
        [InlineData(InstructionsEnum.LW)]
        [InlineData(InstructionsEnum.LWU)]
        [InlineData(InstructionsEnum.LD)]
        [InlineData(InstructionsEnum.SB)]
        [InlineData(InstructionsEnum.SH)]
        [InlineData(InstructionsEnum.SW)]
        [InlineData(InstructionsEnum.SD)]
        public void AllTypedMemoryOps_ClassifiedAsMemory(InstructionsEnum opcode)
        {
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(opcode));
        }

        [Theory]
        [InlineData(InstructionsEnum.SB)]
        [InlineData(InstructionsEnum.SH)]
        [InlineData(InstructionsEnum.SW)]
        [InlineData(InstructionsEnum.SD)]
        public void AllStores_AreMemoryOrdered(InstructionsEnum opcode)
        {
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(opcode));
        }

        [Fact]
        public void FENCE_IsMemoryOrdered()
        {
            Assert.Equal(SerializationClass.MemoryOrdered,
                InstructionClassifier.GetSerializationClass(InstructionsEnum.FENCE));
        }

        [Fact]
        public void FENCE_I_IsFullSerial()
        {
            Assert.Equal(SerializationClass.FullSerial,
                InstructionClassifier.GetSerializationClass(InstructionsEnum.FENCE_I));
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
        public void AllAmoD_AreAtomicSerial(InstructionsEnum opcode)
        {
            Assert.Equal(SerializationClass.AtomicSerial,
                InstructionClassifier.GetSerializationClass(opcode));
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
        public void AllAmoD_ClassifiedAsAtomic(InstructionsEnum opcode)
        {
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(opcode));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 12. MemoryAlignmentException вЂ” properties
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_MemoryAlignmentExceptionTests
    {
        [Fact]
        public void Exception_CarriesAddressAndAlignment()
        {
            var ex = new MemoryAlignmentException(0x1003, 4, "LW");
            Assert.Equal(0x1003UL, ex.Address);
            Assert.Equal(4, ex.RequiredAlignment);
            Assert.Equal("LW", ex.InstructionMnemonic);
            Assert.Contains("LW", ex.Message);
            Assert.Contains("1003", ex.Message);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 13. AMO*_D opcode enum completeness (verify all 9 AMO_D are present)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_AmoD_EnumCompletenessTests
    {
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
        public void AmoD_Opcodes_HaveExpectedSlots(InstructionsEnum opcode, int expected)
        {
            Assert.Equal(expected, (int)opcode);
        }

        [Fact]
        public void AmoD_All9DoublewordOps_Present()
        {
            // Verify all 9 AMO_D opcodes exist in the enum
            InstructionsEnum[] amoDOps =
            {
                InstructionsEnum.AMOADD_D,  InstructionsEnum.AMOSWAP_D,
                InstructionsEnum.AMOOR_D,   InstructionsEnum.AMOAND_D,
                InstructionsEnum.AMOXOR_D,  InstructionsEnum.AMOMIN_D,
                InstructionsEnum.AMOMAX_D,  InstructionsEnum.AMOMINU_D,
                InstructionsEnum.AMOMAXU_D,
            };
            Assert.Equal(9, amoDOps.Length);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 14. IMemoryBus interface presence
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_IMemoryBusInterfaceTests
    {
        [Fact]
        public void IMemoryBus_InterfaceExists()
        {
            Assert.True(typeof(IMemoryBus).IsInterface);
        }

        [Fact]
        public void IMemoryBus_HasReadAndWriteMethods()
        {
            Assert.NotNull(typeof(IMemoryBus).GetMethod("Read"));
            Assert.NotNull(typeof(IMemoryBus).GetMethod("Write"));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 15. MemoryUnit вЂ” constructor validation
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_MemoryUnitConstructorTests
    {
        [Fact]
        public void MemoryUnit_NullBus_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryUnit(null!));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 16. Sign extension boundary value tests
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class Phase07_SignExtensionBoundaryTests
    {
        private readonly FakeMemoryBus _bus = new();
        private readonly MemoryUnit _unit;
        private readonly Mem07FakeCpuState _state = new();

        public Phase07_SignExtensionBoundaryTests()
        {
            _unit = new MemoryUnit(_bus);
            _state.SetReg(1, 0x100);
        }

        [Fact]
        public void LB_Zero_ExtendedCorrectly()
        {
            _bus.StoreByte(0x100, 0x00);
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            Assert.Equal(0UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LB_MaxPositive_ExtendedCorrectly()
        {
            _bus.StoreByte(0x100, 0x7F); // +127
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            Assert.Equal(0x7FUL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LB_MinNegative_ExtendedCorrectly()
        {
            _bus.StoreByte(0x100, 0x80); // -128
            _unit.Execute(Ir07.Make(InstructionsEnum.LB, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFFFFFFFFFFF80UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LH_MinNegative_ExtendedCorrectly()
        {
            _bus.StoreHalf(0x100, 0x8000); // -32768
            _unit.Execute(Ir07.Make(InstructionsEnum.LH, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFFFFFFFFF8000UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LW_MinNegative_ExtendedCorrectly()
        {
            _bus.StoreWord(0x100, 0x80000000); // -2147483648
            _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFFFFF80000000UL, _state.ReadIntRegister(2));
        }

        [Fact]
        public void LW_AllOnes_SignExtended()
        {
            _bus.StoreWord(0x100, 0xFFFFFFFF); // -1
            _unit.Execute(Ir07.Make(InstructionsEnum.LW, rd: 2, rs1: 1), _state);
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, _state.ReadIntRegister(2)); // -1 sign-extended
        }

        [Fact]
        public void LWU_AllOnes_ZeroExtended()
        {
            _bus.StoreWord(0x100, 0xFFFFFFFF);
            _unit.Execute(Ir07.Make(InstructionsEnum.LWU, rd: 2, rs1: 1), _state);
            Assert.Equal(0x00000000FFFFFFFFUL, _state.ReadIntRegister(2)); // zero-extended
        }
    }
}

