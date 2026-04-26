// Phase 08: CSR Layer — CSR Plane Cleanup and VT Identity
// Split into focused test files for surgical maintenance.

using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase08
{
    /// <summary>Simple dictionary-backed ICanonicalCpuState stub for Phase 08 CSR tests.</summary>
    internal sealed class Csr08FakeCpuState : ICanonicalCpuState
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

    /// <summary>Helper to create CSR InstructionIR records.</summary>
    internal static class CsrIrHelper
    {
        public static InstructionIR MakeCsr(
            InstructionsEnum opcode,
            byte rd = 0, byte rs1 = 0, long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClass.Csr,
                SerializationClass = SerializationClass.CsrOrdered,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = 0,
                Imm = imm,
            };
        }
    }
}


