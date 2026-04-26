// Phase 05: Pipeline FSM вЂ” PipelineState / PipelineTransitionTrigger / PipelineFsmGuard
// Covers: all legal transitions, all illegal transition rejections, VT isolation,
//         SafetyMask admission guard for VMLAUNCH/VMRESUME, TraceSink FSM logging

using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase05
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Minimal ICanonicalCpuState stub for Phase 05 tests.
    /// Supports PipelineState read/write and guarded FSM transitions.
    /// </summary>
    internal sealed class FsmCpuState : ICanonicalCpuState
    {
        private readonly Dictionary<ushort, ulong> _regs = new();
        private ulong _pc;
        private PipelineState _pipelineState = PipelineState.Task;

        // в”Ђв”Ђ Pipeline FSM в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public PipelineState GetCurrentPipelineState()                   => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state)         => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger t) => _pipelineState = PipelineFsmGuard.Transition(_pipelineState, t);

        // в”Ђв”Ђ Register access в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public ulong ReadIntRegister(ushort id)  => _regs.TryGetValue(id, out var v) ? v : 0UL;
        public void WriteIntRegister(ushort id, ulong value) { if (id != 0) _regs[id] = value; }
        public void SetReg(ushort id, ulong value) => _regs[id] = value;

        public long ReadRegister(byte vtId, int regId) =>
            unchecked((long)ReadIntRegister((ushort)regId));

        public void WriteRegister(byte vtId, int regId, ulong value) =>
            WriteIntRegister((ushort)regId, value);

        // в”Ђв”Ђ Core status в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public ulong  GetInstructionPointer()            => _pc;
        public void   SetInstructionPointer(ulong ip)    => _pc = ip;
        public ushort GetCoreID()                        => 0;

        public ulong ReadPc(byte vtId) => GetInstructionPointer();
        public void WritePc(byte vtId, ulong pc) => SetInstructionPointer(pc);

        // в”Ђв”Ђ Unused surface (required by interface) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public ulong  GetVL()                            => 0;
        public void   SetVL(ulong vl)                    { }
        public ulong  GetVLMAX()                         => 0;
        public byte   GetSEW()                           => 0;
        public void   SetSEW(byte s)                     { }
        public byte   GetLMUL()                          => 0;
        public void   SetLMUL(byte l)                    { }
        public bool   GetTailAgnostic()                  => false;
        public void   SetTailAgnostic(bool a)            { }
        public bool   GetMaskAgnostic()                  => false;
        public void   SetMaskAgnostic(bool a)            { }
        public uint   GetExceptionMask()                 => 0;
        public void   SetExceptionMask(uint m)           { }
        public uint   GetExceptionPriority()             => 0;
        public void   SetExceptionPriority(uint p)       { }
        public byte   GetRoundingMode()                  => 0;
        public void   SetRoundingMode(byte m)            { }
        public ulong  GetOverflowCount()                 => 0;
        public ulong  GetUnderflowCount()                => 0;
        public ulong  GetDivByZeroCount()                => 0;
        public ulong  GetInvalidOpCount()                => 0;
        public ulong  GetInexactCount()                  => 0;
        public void   ClearExceptionCounters()           { }
        public bool   GetVectorDirty()                   => false;
        public void   SetVectorDirty(bool d)             { }
        public bool   GetVectorEnabled()                 => false;
        public void   SetVectorEnabled(bool e)           { }
        public ushort GetPredicateMask(ushort id)        => 0;
        public void   SetPredicateMask(ushort id, ushort m) { }
        public ulong  GetCycleCount()                    => 0;
        public ulong  GetInstructionsRetired()           => 0;
        public double GetIPC()                           => 0;
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // PipelineState enum
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineStateEnumTests
    {
        [Fact]
        public void PipelineState_HasTask_Value0()
            => Assert.Equal(0, (int)PipelineState.Task);

        [Fact]
        public void PipelineState_HasVmEntry_Value1()
            => Assert.Equal(1, (int)PipelineState.VmEntry);

        [Fact]
        public void PipelineState_HasGuestExecution_Value2()
            => Assert.Equal(2, (int)PipelineState.GuestExecution);

        [Fact]
        public void PipelineState_HasVmExit_Value3()
            => Assert.Equal(3, (int)PipelineState.VmExit);

        [Fact]
        public void PipelineState_HasHalted_Value4()
            => Assert.Equal(4, (int)PipelineState.Halted);

        [Fact]
        public void PipelineState_HasReset_Value5()
            => Assert.Equal(5, (int)PipelineState.Reset);

        [Fact]
        public void PipelineState_HasTrapPending_Value9()
            => Assert.Equal(9, (int)PipelineState.TrapPending);

        [Fact]
        public void PipelineState_HasWaitForEvent_Value10()
            => Assert.Equal(10, (int)PipelineState.WaitForEvent);

        [Fact]
        public void PipelineState_ExactlyElevenMembers()
            => Assert.Equal(11, Enum.GetValues<PipelineState>().Length);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // PipelineFsmGuard вЂ” all legal transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineFsmGuardLegalTransitionsTests
    {
        [Fact]
        public void Reset_Init_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.Reset, PipelineTransitionTrigger.Init));

        [Fact]
        public void Task_VmLaunch_Transitions_To_VmEntry()
            => Assert.Equal(PipelineState.VmEntry,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.VmLaunch));

        [Fact]
        public void Task_VmResume_Transitions_To_VmEntry()
            => Assert.Equal(PipelineState.VmEntry,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.VmResume));

        [Fact]
        public void Task_HaltAll_Transitions_To_Halted()
            => Assert.Equal(PipelineState.Halted,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.HaltAll));

        [Fact]
        public void VmEntry_EntryOk_Transitions_To_GuestExecution()
            => Assert.Equal(PipelineState.GuestExecution,
                PipelineFsmGuard.Transition(PipelineState.VmEntry, PipelineTransitionTrigger.EntryOk));

        [Fact]
        public void VmEntry_EntryFail_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.VmEntry, PipelineTransitionTrigger.EntryFail));

        [Fact]
        public void GuestExecution_VmExitCond_Transitions_To_VmExit()
            => Assert.Equal(PipelineState.VmExit,
                PipelineFsmGuard.Transition(PipelineState.GuestExecution, PipelineTransitionTrigger.VmExitCond));

        [Fact]
        public void GuestExecution_VmxOff_Transitions_To_VmExit()
            => Assert.Equal(PipelineState.VmExit,
                PipelineFsmGuard.Transition(PipelineState.GuestExecution, PipelineTransitionTrigger.VmxOff));

        [Fact]
        public void VmExit_ExitComplete_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.VmExit, PipelineTransitionTrigger.ExitComplete));

        [Fact]
        public void Halted_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.Halted, PipelineTransitionTrigger.Interrupt));
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // PipelineFsmGuard вЂ” IsLegalTransition
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineFsmGuardIsLegalTests
    {
        [Theory]
        [InlineData(PipelineState.Reset,          PipelineTransitionTrigger.Init)]
        [InlineData(PipelineState.Task,           PipelineTransitionTrigger.VmLaunch)]
        [InlineData(PipelineState.Task,           PipelineTransitionTrigger.VmResume)]
        [InlineData(PipelineState.Task,           PipelineTransitionTrigger.HaltAll)]
        [InlineData(PipelineState.VmEntry,        PipelineTransitionTrigger.EntryOk)]
        [InlineData(PipelineState.VmEntry,        PipelineTransitionTrigger.EntryFail)]
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.VmExitCond)]
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.VmxOff)]
        [InlineData(PipelineState.VmExit,         PipelineTransitionTrigger.ExitComplete)]
        [InlineData(PipelineState.Halted,         PipelineTransitionTrigger.Interrupt)]
        public void IsLegal_ReturnsTrue_ForAllLegalTransitions(
            PipelineState from, PipelineTransitionTrigger trigger)
            => Assert.True(PipelineFsmGuard.IsLegalTransition(from, trigger));

        [Theory]
        [InlineData(PipelineState.Task,           PipelineTransitionTrigger.EntryOk)]
        [InlineData(PipelineState.Task,           PipelineTransitionTrigger.VmExitCond)]
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.VmLaunch)]
        [InlineData(PipelineState.VmExit,         PipelineTransitionTrigger.VmLaunch)]
        [InlineData(PipelineState.Halted,         PipelineTransitionTrigger.VmLaunch)]
        public void IsLegal_ReturnsFalse_ForIllegalTransitions(
            PipelineState from, PipelineTransitionTrigger trigger)
            => Assert.False(PipelineFsmGuard.IsLegalTransition(from, trigger));
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // PipelineFsmGuard вЂ” illegal transition exception
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineFsmGuardIllegalTransitionTests
    {
        [Theory]
        // Cannot start VMLAUNCH/VMRESUME from guest mode
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.VmLaunch)]
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.VmResume)]
        // Cannot use VMLAUNCH from VmExit / Halted
        [InlineData(PipelineState.VmExit,  PipelineTransitionTrigger.VmLaunch)]
        [InlineData(PipelineState.Halted,  PipelineTransitionTrigger.VmLaunch)]
        // Cannot call Init from Task
        [InlineData(PipelineState.Task,    PipelineTransitionTrigger.Init)]
        // Cannot call EntryOk from Task
        [InlineData(PipelineState.Task,    PipelineTransitionTrigger.EntryOk)]
        // Cannot exit from Task
        [InlineData(PipelineState.Task,    PipelineTransitionTrigger.VmExitCond)]
        [InlineData(PipelineState.Task,    PipelineTransitionTrigger.ExitComplete)]
        // Cannot call VmxOff from Task
        [InlineData(PipelineState.Task,    PipelineTransitionTrigger.VmxOff)]
        // Cannot halt from guest
        [InlineData(PipelineState.GuestExecution, PipelineTransitionTrigger.HaltAll)]
        public void Transition_ThrowsIllegalFsmTransitionException_ForIllegalPairs(
            PipelineState from, PipelineTransitionTrigger trigger)
        {
            var ex = Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(from, trigger));
            Assert.Equal(from, ex.FromState);
            Assert.Equal(trigger, ex.Trigger);
        }

        [Fact]
        public void IllegalFsmTransitionException_MessageContainsStateAndTrigger()
        {
            var ex = Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.Halted, PipelineTransitionTrigger.VmLaunch));
            Assert.Contains("Halted", ex.Message);
            Assert.Contains("VmLaunch", ex.Message);
            Assert.Contains("Deterministic execution contract violated", ex.Message);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ICanonicalCpuState pipeline state integration
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class CpuStatePipelineStateTests
    {
        [Fact]
        public void DefaultState_IsTask()
        {
            var s = new FsmCpuState();
            Assert.Equal(PipelineState.Task, s.GetCurrentPipelineState());
        }

        [Fact]
        public void SetCurrentPipelineState_UpdatesState()
        {
            var s = new FsmCpuState();
            s.SetCurrentPipelineState(PipelineState.Halted);
            Assert.Equal(PipelineState.Halted, s.GetCurrentPipelineState());
        }

        [Fact]
        public void TransitionPipelineState_ExecutesGuardedTransition()
        {
            var s = new FsmCpuState();
            s.TransitionPipelineState(PipelineTransitionTrigger.VmLaunch);
            Assert.Equal(PipelineState.VmEntry, s.GetCurrentPipelineState());
        }

        [Fact]
        public void TransitionPipelineState_ThrowsOnIllegalTransition()
        {
            var s = new FsmCpuState();
            // Default state is Task; Init is only legal from Reset
            Assert.Throws<IllegalFsmTransitionException>(
                () => s.TransitionPipelineState(PipelineTransitionTrigger.Init));
        }

        [Fact]
        public void FullVmxRoundTrip_Task_VmEntry_GuestExecution_VmExit_Task()
        {
            var s = new FsmCpuState();
            Assert.Equal(PipelineState.Task,           s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.VmLaunch);
            Assert.Equal(PipelineState.VmEntry,        s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.EntryOk);
            Assert.Equal(PipelineState.GuestExecution, s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.VmExitCond);
            Assert.Equal(PipelineState.VmExit,         s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.ExitComplete);
            Assert.Equal(PipelineState.Task,           s.GetCurrentPipelineState());
        }

        [Fact]
        public void EntryFailPath_VmEntry_Task()
        {
            var s = new FsmCpuState();
            s.TransitionPipelineState(PipelineTransitionTrigger.VmLaunch);
            Assert.Equal(PipelineState.VmEntry, s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.EntryFail);
            Assert.Equal(PipelineState.Task, s.GetCurrentPipelineState());
        }

        [Fact]
        public void HaltAndWake_Task_Halted_Task()
        {
            var s = new FsmCpuState();
            s.TransitionPipelineState(PipelineTransitionTrigger.HaltAll);
            Assert.Equal(PipelineState.Halted, s.GetCurrentPipelineState());
            s.TransitionPipelineState(PipelineTransitionTrigger.Interrupt);
            Assert.Equal(PipelineState.Task, s.GetCurrentPipelineState());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // VT isolation: each state is independent
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VtIsolationTests
    {
        [Fact]
        public void TwoVTs_StateChangesAreIndependent()
        {
            var vt0 = new FsmCpuState();
            var vt1 = new FsmCpuState();

            vt0.TransitionPipelineState(PipelineTransitionTrigger.VmLaunch);
            // vt1 should remain in Task
            Assert.Equal(PipelineState.VmEntry, vt0.GetCurrentPipelineState());
            Assert.Equal(PipelineState.Task,    vt1.GetCurrentPipelineState());
        }

        [Fact]
        public void VT1GuestExecution_DoesNotAffectVT0Task()
        {
            var vt0 = new FsmCpuState();
            var vt1 = new FsmCpuState();

            vt1.TransitionPipelineState(PipelineTransitionTrigger.VmLaunch);
            vt1.TransitionPipelineState(PipelineTransitionTrigger.EntryOk);
            Assert.Equal(PipelineState.GuestExecution, vt1.GetCurrentPipelineState());
            Assert.Equal(PipelineState.Task,           vt0.GetCurrentPipelineState());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Direct VMX surface contract вЂ” ExecutionDispatcherV4 compatibility guard
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VmxAdmissionGuardTests
    {
        private static InstructionIR MakeVmxIr(InstructionsEnum opcode)
        {
            return new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.Vmx,
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rs1 = 0,
                Rs2 = 0,
                Rd  = 0,
                Imm = 0,
            };
        }

        [Theory]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        public void Vmlaunch_Vmresume_WithoutWiredVmxPlane_Throws(InstructionsEnum opcode)
        {
            var disp  = new ExecutionDispatcherV4();
            var state = new FsmCpuState();
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(MakeVmxIr(opcode)));
            Assert.Throws<InvalidOperationException>(
                () => disp.Execute(MakeVmxIr(opcode), state));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        public void Vmlaunch_Vmresume_DoesNotReachExecutionFallback_WhenPipelineStateIsNotTask(InstructionsEnum opcode)
        {
            var disp  = new ExecutionDispatcherV4();
            var state = new FsmCpuState();
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(MakeVmxIr(opcode)));

            // Move pipeline to VmEntry (non-Task state)
            state.SetCurrentPipelineState(PipelineState.VmEntry);

            Assert.Throws<InvalidOperationException>(
                () => disp.Execute(MakeVmxIr(opcode), state));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        public void Vmlaunch_Vmresume_DoesNotReachExecutionFallback_WhenPipelineStateIsGuestExecution(InstructionsEnum opcode)
        {
            var disp  = new ExecutionDispatcherV4();
            var state = new FsmCpuState();
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(MakeVmxIr(opcode)));
            state.SetCurrentPipelineState(PipelineState.GuestExecution);

            Assert.Throws<InvalidOperationException>(
                () => disp.Execute(MakeVmxIr(opcode), state));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMXOFF)]
        [InlineData(InstructionsEnum.VMREAD)]
        [InlineData(InstructionsEnum.VMWRITE)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        [InlineData(InstructionsEnum.VMPTRLD)]
        public void Other_VmxOpcodes_WithoutWiredVmxPlane_Throw(InstructionsEnum opcode)
        {
            var disp  = new ExecutionDispatcherV4();
            var state = new FsmCpuState();
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(MakeVmxIr(opcode)));
            state.SetCurrentPipelineState(PipelineState.GuestExecution);

            Assert.Throws<InvalidOperationException>(
                () => disp.Execute(MakeVmxIr(opcode), state));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // TraceSink FSM transition logging
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class TraceSinkFsmTransitionTests
    {
        [Fact]
        public void RecordFsmTransition_WhenEnabled_AppearsInGetFsmTransitions()
        {
            var sink = new TraceSink();
            sink.SetEnabled(true);

            sink.RecordFsmTransition(
                cycleNumber: 42,
                vtId: 0,
                fromState: PipelineState.Task,
                trigger: PipelineTransitionTrigger.VmLaunch,
                toState: PipelineState.VmEntry);

            var transitions = sink.GetFsmTransitions();
            Assert.Single(transitions);
            var t = transitions[0];
            Assert.Equal(42,                               t.CycleNumber);
            Assert.Equal(0,                                t.VtId);
            Assert.Equal(PipelineState.Task,               t.FromState);
            Assert.Equal(PipelineTransitionTrigger.VmLaunch, t.Trigger);
            Assert.Equal(PipelineState.VmEntry,            t.ToState);
        }

        [Fact]
        public void RecordFsmTransition_WhenDisabled_IsNotRecorded()
        {
            var sink = new TraceSink();
            // sink is disabled by default

            sink.RecordFsmTransition(
                cycleNumber: 1,
                vtId: 0,
                fromState: PipelineState.Task,
                trigger: PipelineTransitionTrigger.HaltAll,
                toState: PipelineState.Halted);

            Assert.Empty(sink.GetFsmTransitions());
        }

        [Fact]
        public void RecordFsmTransition_MultipleTransitions_AllCaptured()
        {
            var sink = new TraceSink();
            sink.SetEnabled(true);

            // Simulate a full VMX round-trip
            sink.RecordFsmTransition(1, 0, PipelineState.Task,           PipelineTransitionTrigger.VmLaunch,    PipelineState.VmEntry);
            sink.RecordFsmTransition(2, 0, PipelineState.VmEntry,        PipelineTransitionTrigger.EntryOk,     PipelineState.GuestExecution);
            sink.RecordFsmTransition(3, 0, PipelineState.GuestExecution, PipelineTransitionTrigger.VmExitCond,  PipelineState.VmExit);
            sink.RecordFsmTransition(4, 0, PipelineState.VmExit,         PipelineTransitionTrigger.ExitComplete,PipelineState.Task);

            var transitions = sink.GetFsmTransitions();
            Assert.Equal(4, transitions.Count);
            Assert.Equal(PipelineState.Task,           transitions[3].ToState);
        }

        [Fact]
        public void FullStateTraceEvent_HasPipelineStateField()
        {
            var evt = new FullStateTraceEvent
            {
                CurrentPipelineState = PipelineState.GuestExecution
            };
            Assert.Equal(PipelineState.GuestExecution, evt.CurrentPipelineState);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D19/D20): New PipelineState members вЂ” value assertions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2PipelineStateEnumTests
    {
        [Fact]
        public void PipelineState_HasWaitForClusterSync_Value6()
            => Assert.Equal(6, (int)PipelineState.WaitForClusterSync);

        [Fact]
        public void PipelineState_HasPtwStall_Value7()
            => Assert.Equal(7, (int)PipelineState.PtwStall);

        [Fact]
        public void PipelineState_HasClockGatedDonor_Value8()
            => Assert.Equal(8, (int)PipelineState.ClockGatedDonor);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D19/D20): ClusterSync FSM transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2ClusterSyncTransitionTests
    {
        [Fact]
        public void Task_EnterClusterSync_Transitions_To_WaitForClusterSync()
            => Assert.Equal(PipelineState.WaitForClusterSync,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.EnterClusterSync));

        [Fact]
        public void WaitForClusterSync_ExitClusterSync_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.WaitForClusterSync, PipelineTransitionTrigger.ExitClusterSync));

        [Fact]
        public void WaitForClusterSync_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.WaitForClusterSync, PipelineTransitionTrigger.Interrupt));

        [Fact]
        public void Task_ExitClusterSync_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.ExitClusterSync));

        [Fact]
        public void WaitForClusterSync_EnterClusterSync_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.WaitForClusterSync, PipelineTransitionTrigger.EnterClusterSync));

        [Fact]
        public void ClusterSyncEnterEvent_Advance_Task_To_WaitForClusterSync()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.ClusterSyncEnterEvent
            {
                VtId        = 0,
                BundleSerial = 1,
                AffinityMask = 0xFF,
            };
            Assert.Equal(PipelineState.WaitForClusterSync,
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void ClusterSyncExitEvent_Advance_WaitForClusterSync_To_Task()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.ClusterSyncExitEvent
            {
                VtId        = 0,
                BundleSerial = 2,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.WaitForClusterSync, evt));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D19/D20): PTW stall FSM transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2PtwStallTransitionTests
    {
        [Fact]
        public void Task_PtwStart_Transitions_To_PtwStall()
            => Assert.Equal(PipelineState.PtwStall,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.PtwStart));

        [Fact]
        public void PtwStall_PtwComplete_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.PtwStall, PipelineTransitionTrigger.PtwComplete));

        [Fact]
        public void PtwStall_PtwFault_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.PtwStall, PipelineTransitionTrigger.PtwFault));

        [Fact]
        public void PtwStall_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.PtwStall, PipelineTransitionTrigger.Interrupt));

        [Fact]
        public void Task_PtwComplete_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.PtwComplete));

        [Fact]
        public void Task_PtwFault_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.PtwFault));

        [Fact]
        public void PtwWalkStartEvent_Advance_Task_To_PtwStall()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.PtwWalkStartEvent
            {
                VtId         = 0,
                BundleSerial = 3,
                FaultAddress = 0xDEAD_0000,
            };
            Assert.Equal(PipelineState.PtwStall,
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void PtwWalkCompleteEvent_Advance_PtwStall_To_Task()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.PtwWalkCompleteEvent
            {
                VtId            = 0,
                BundleSerial    = 4,
                PhysicalAddress = 0x1234_5000,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.PtwStall, evt));
        }

        [Fact]
        public void PtwWalkFaultEvent_Advance_PtwStall_To_Task()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.PtwWalkFaultEvent
            {
                VtId         = 0,
                BundleSerial = 5,
                FaultAddress = 0xDEAD_0000,
                IsWrite      = false,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.PtwStall, evt));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D19/D20): Clock-gating FSM transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2ClockGatingTransitionTests
    {
        [Fact]
        public void Task_EnterClockGate_Transitions_To_ClockGatedDonor()
            => Assert.Equal(PipelineState.ClockGatedDonor,
                PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.EnterClockGate));

        [Fact]
        public void ClockGatedDonor_ExitClockGate_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.ClockGatedDonor, PipelineTransitionTrigger.ExitClockGate));

        [Fact]
        public void ClockGatedDonor_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.ClockGatedDonor, PipelineTransitionTrigger.Interrupt));

        [Fact]
        public void Task_ExitClockGate_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.ExitClockGate));

        [Fact]
        public void ClockGatedDonor_EnterClockGate_IsIllegal()
            => Assert.Throws<IllegalFsmTransitionException>(
                () => PipelineFsmGuard.Transition(PipelineState.ClockGatedDonor, PipelineTransitionTrigger.EnterClockGate));

        [Fact]
        public void ClockGatedDonorEnterEvent_Advance_Task_To_ClockGatedDonor()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.ClockGatedDonorEnterEvent
            {
                VtId        = 0,
                BundleSerial = 6,
            };
            Assert.Equal(PipelineState.ClockGatedDonor,
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void ClockGatedDonorExitEvent_Advance_ClockGatedDonor_To_Task()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.ClockGatedDonorExitEvent
            {
                VtId        = 0,
                BundleSerial = 7,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.ClockGatedDonor, evt));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D19/D20): Non-FSM-changing events still consumed without error
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2NonFsmEventTests
    {
        [Fact]
        public void EcallEvent_InTask_ReturnsTask_UnchangedState()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.EcallEvent
            {
                VtId        = 0,
                BundleSerial = 1,
                EcallCode    = 42,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void FenceEvent_InTask_ReturnsTask_UnchangedState()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.FenceEvent
            {
                VtId              = 0,
                BundleSerial      = 1,
                IsInstructionFence = false,
            };
            Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void PodBarrierEvent_InWaitForClusterSync_ReturnsWaitForClusterSync()
        {
            var evt = new YAKSys_Hybrid_CPU.Core.Pipeline.PodBarrierEvent
            {
                VtId        = 0,
                BundleSerial = 1,
            };
            // PodBarrierEvent does not have an FSM mapping; state unchanged
            Assert.Equal(PipelineState.WaitForClusterSync,
                PipelineFsmGuard.Advance(PipelineState.WaitForClusterSync, evt));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Phase 2 (D23/D24): IAbstractBundle interface structure
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class Phase2AbstractBundleInterfaceTests
    {
        [Fact]
        public void IAbstractBundle_InterfaceExists_InPipelineNamespace()
        {
            var type = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle);
            Assert.NotNull(type);
        }

        [Fact]
        public void IAbstractBundleSlot_InterfaceExists_InPipelineNamespace()
        {
            var type = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundleSlot);
            Assert.NotNull(type);
        }

        [Fact]
        public void IAbstractBundle_HasBundleAddress_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle)
                .GetProperty("BundleAddress");
            Assert.NotNull(prop);
            Assert.Equal(typeof(ulong), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundle_HasSlotCount_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle)
                .GetProperty("SlotCount");
            Assert.NotNull(prop);
            Assert.Equal(typeof(int), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundle_HasGetSlot_Method()
        {
            var method = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle)
                .GetMethod("GetSlot");
            Assert.NotNull(method);
            Assert.Equal(typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundleSlot), method!.ReturnType);
        }

        [Fact]
        public void IAbstractBundle_HasBundleSerial_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle)
                .GetProperty("BundleSerial");
            Assert.NotNull(prop);
            Assert.Equal(typeof(ulong), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundle_HasIsEmpty_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundle)
                .GetProperty("IsEmpty");
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundleSlot_HasSlotIndex_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundleSlot)
                .GetProperty("SlotIndex");
            Assert.NotNull(prop);
            Assert.Equal(typeof(int), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundleSlot_HasIsOccupied_Property()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundleSlot)
                .GetProperty("IsOccupied");
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop!.PropertyType);
        }

        [Fact]
        public void IAbstractBundleSlot_HasInstruction_Property_OfTypeInstructionIR()
        {
            var prop = typeof(YAKSys_Hybrid_CPU.Core.Pipeline.IAbstractBundleSlot)
                .GetProperty("Instruction");
            Assert.NotNull(prop);
            // The Instruction property is of type InstructionIR? (nullable reference type).
            // At runtime the underlying type is InstructionIR (nullable annotations are erased).
            Assert.Equal(
                typeof(YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps.InstructionIR),
                prop!.PropertyType);
        }
    }
}

