// Phase 09: VMX Subsystem вЂ” Instruction Plane + FSM Transitions
// Covers:
//   - VmExitReason: VMEXIT reason codes
//   - VmxEventKind: VMX diagnostic event kinds
//   - VmcsField: VMCS field identifiers
//   - IVmcsManager / VmcsManager: VMCS lifecycle management
//   - IVmxEventSink / NullVmxEventSink: diagnostic event recording
//   - VmxExecutionUnit: all 8 VMX instruction implementations
//   - ExecutionResult.VmxFault: VMX fault signalling
//   - ExecutionDispatcherV4: VMX dispatch delegation + explicit direct surface contract
//   - Pipeline FSM transitions: VmEntry / GuestExecution / VmExit cycle
//   - Privilege enforcement: VMX instructions require M-mode
//   - VMCS read/write round-trip
//   - VMXON/VMXOFF CSR enable/disable cycle
//   - VM-Entry/VM-Exit state save/restore

using System;
using System.Collections.Generic;
using Xunit;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Simple dictionary-backed ICanonicalCpuState stub for Phase 09 VMX tests.</summary>
    internal sealed class Vmx09FakeCpuState : ICanonicalCpuState
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

    /// <summary>Helper to create VMX InstructionIR records.</summary>
    internal static class VmxIrHelper
    {
        public static InstructionIR MakeVmx(
            InstructionsEnum opcode,
            byte rd = 0, byte rs1 = 0, byte rs2 = 0, long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = imm,
            };
        }
    }

    /// <summary>Recording VMX event sink for test assertions.</summary>
    internal sealed class RecordingVmxEventSink : IVmxEventSink
    {
        public List<(VmxEventKind Kind, ushort VtId, VmExitReason Reason)> Events { get; } = new();

        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
        {
            Events.Add((kind, vtId, exitReason));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 1. VmExitReason вЂ” VMEXIT Reason Codes
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmExitReasonTests
    {
        [Fact]
        public void None_IsZero() => Assert.Equal(0u, (uint)VmExitReason.None);

        [Fact]
        public void ExternalInterrupt_Is1() => Assert.Equal(1u, (uint)VmExitReason.ExternalInterrupt);

        [Fact]
        public void VmxOff_Is26() => Assert.Equal(26u, (uint)VmExitReason.VmxOff);

        [Fact]
        public void InvalidGuestState_Is33() => Assert.Equal(33u, (uint)VmExitReason.InvalidGuestState);
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 2. VmxEventKind вЂ” VMX Diagnostic Event Kinds
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxEventKindTests
    {
        [Theory]
        [InlineData((byte)VmxEventKind.VmxOn)]
        [InlineData((byte)VmxEventKind.VmxOff)]
        [InlineData((byte)VmxEventKind.VmEntry)]
        [InlineData((byte)VmxEventKind.VmResume)]
        [InlineData((byte)VmxEventKind.VmExit)]
        [InlineData((byte)VmxEventKind.VmClear)]
        [InlineData((byte)VmxEventKind.VmPtrLd)]
        [InlineData((byte)VmxEventKind.VmRead)]
        [InlineData((byte)VmxEventKind.VmWrite)]
        public void AllEventKinds_AreDefined(byte kind) =>
            Assert.True(Enum.IsDefined((VmxEventKind)kind));
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 3. VmcsField вЂ” VMCS Field Identifiers
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsFieldTests
    {
        [Fact]
        public void GuestPc_Is0() => Assert.Equal(0, (ushort)VmcsField.GuestPc);

        [Fact]
        public void HostPc_Is32() => Assert.Equal(32, (ushort)VmcsField.HostPc);

        [Fact]
        public void ExitReason_Is96() => Assert.Equal(96, (ushort)VmcsField.ExitReason);
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 4. VmcsManager вЂ” VMCS Lifecycle Management
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsManagerTests
    {
        [Fact]
        public void NewManager_HasNoActiveVmcs()
        {
            var mgr = new VmcsManager();
            Assert.False(mgr.HasActiveVmcs);
            Assert.False(mgr.HasLaunchedVmcs);
        }

        [Fact]
        public void Load_ActivatesVmcs()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            Assert.True(mgr.HasActiveVmcs);
            Assert.False(mgr.HasLaunchedVmcs);
        }

        [Fact]
        public void MarkLaunched_SetsLaunchedFlag()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.MarkLaunched();
            Assert.True(mgr.HasLaunchedVmcs);
        }

        [Fact]
        public void Clear_ResetsLaunchState()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.MarkLaunched();
            mgr.Clear(0x1000);
            Assert.False(mgr.HasActiveVmcs);
            Assert.False(mgr.HasLaunchedVmcs);
        }

        [Fact]
        public void WriteField_ReadField_RoundTrip()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestPc, 0x4000);
            Assert.Equal(0x4000, mgr.ReadField(VmcsField.GuestPc));
        }

        [Fact]
        public void ReadField_Unset_ReturnsZero()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            Assert.Equal(0L, mgr.ReadField(VmcsField.GuestFlags));
        }

        [Fact]
        public void ReadField_WithoutActiveVmcs_Throws()
        {
            var mgr = new VmcsManager();
            Assert.Throws<InvalidOperationException>(() => mgr.ReadField(VmcsField.GuestPc));
        }

        [Fact]
        public void WriteField_WithoutActiveVmcs_Throws()
        {
            var mgr = new VmcsManager();
            Assert.Throws<InvalidOperationException>(() => mgr.WriteField(VmcsField.GuestPc, 42));
        }

        [Fact]
        public void Clear_DifferentAddress_DoesNotAffectActive()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestPc, 0x4000);
            mgr.MarkLaunched();

            // Clearing a different VMCS address should not affect the active one
            mgr.Clear(0x2000);
            Assert.True(mgr.HasActiveVmcs);
            Assert.True(mgr.HasLaunchedVmcs);
            Assert.Equal(0x4000, mgr.ReadField(VmcsField.GuestPc));
        }

        [Fact]
        public void SaveGuestState_PersistsPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new Vmx09FakeCpuState();
            state.SetInstructionPointer(0xDEADBEEF);

            mgr.SaveGuestState(state);
            Assert.Equal((long)0xDEADBEEF, mgr.ReadField(VmcsField.GuestPc));
        }

        [Fact]
        public void RestoreHostState_RestoresPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.HostPc, 0xCAFE0000);

            var state = new Vmx09FakeCpuState();
            mgr.RestoreHostState(state);
            Assert.Equal(0xCAFE0000UL, state.GetInstructionPointer());
        }

        [Fact]
        public void MultipleVmcs_IndependentFieldStorage()
        {
            var mgr = new VmcsManager();

            // Write to VMCS at 0x1000
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestPc, 100);

            // Switch to VMCS at 0x2000
            mgr.Load(0x2000);
            mgr.WriteField(VmcsField.GuestPc, 200);

            // Verify VMCS 0x2000 has its own value
            Assert.Equal(200, mgr.ReadField(VmcsField.GuestPc));

            // Switch back to VMCS 0x1000 вЂ” its value should be preserved
            mgr.Load(0x1000);
            Assert.Equal(100, mgr.ReadField(VmcsField.GuestPc));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 5. NullVmxEventSink вЂ” Default No-Op Trace
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class NullVmxEventSinkTests
    {
        [Fact]
        public void Instance_IsSingleton()
        {
            Assert.Same(NullVmxEventSink.Instance, NullVmxEventSink.Instance);
        }

        [Fact]
        public void RecordVmxEvent_DoesNotThrow()
        {
            NullVmxEventSink.Instance.RecordVmxEvent(VmxEventKind.VmxOn, 0);
            NullVmxEventSink.Instance.RecordVmxEvent(VmxEventKind.VmExit, 1, VmExitReason.VmxOff);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 6. ExecutionResult вЂ” VmxFault Extension
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class ExecutionResultVmxFaultTests
    {
        [Fact]
        public void VmxFault_SetsVmxFaultedFlag()
        {
            var result = ExecutionResult.VmxFault();
            Assert.True(result.VmxFaulted);
            Assert.False(result.TrapRaised);
            Assert.False(result.PcRedirected);
        }

        [Fact]
        public void Ok_DoesNotSetVmxFaulted()
        {
            var result = ExecutionResult.Ok();
            Assert.False(result.VmxFaulted);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 7. VmxExecutionUnit вЂ” VMXON / VMXOFF Cycle
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxOnOffCycleTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly RecordingVmxEventSink _sink = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmxOnOffCycleTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs, _sink);
        }

        [Fact]
        public void VmxOn_EnablesVmx_CsrSetTo1()
        {
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);
            Assert.False(result.VmxFaulted);
            Assert.Equal(1UL, _csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
        }

        [Fact]
        public void VmxOn_WhenAlreadyEnabled_ReturnsVmxFault()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmxOff_DisablesVmx_CsrSetTo0()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);
            Assert.False(result.VmxFaulted);
            Assert.Equal(0UL, _csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
        }

        [Fact]
        public void VmxOff_WhenNotEnabled_ReturnsVmxFault()
        {
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmxOn_RecordsTraceEvent()
        {
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);
            Assert.Single(_sink.Events);
            Assert.Equal(VmxEventKind.VmxOn, _sink.Events[0].Kind);
        }

        [Fact]
        public void VmxOff_RecordsTraceEvent()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);
            Assert.Single(_sink.Events);
            Assert.Equal(VmxEventKind.VmxOff, _sink.Events[0].Kind);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 8. VmxExecutionUnit вЂ” Privilege Enforcement
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxPrivilegeTests
    {
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmxPrivilegeTests()
        {
            _vmx = new VmxExecutionUnit(new CsrFile(), new VmcsManager());
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
        public void VmxInstruction_UserMode_ThrowsPrivilegeViolation(InstructionsEnum op)
        {
            Assert.Throws<VmxPrivilegeViolationException>(
                () => _vmx.Execute(VmxIrHelper.MakeVmx(op), _state, PrivilegeLevel.User));
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
        public void VmxInstruction_SupervisorMode_ThrowsPrivilegeViolation(InstructionsEnum op)
        {
            Assert.Throws<VmxPrivilegeViolationException>(
                () => _vmx.Execute(VmxIrHelper.MakeVmx(op), _state, PrivilegeLevel.Supervisor));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 9. VmxExecutionUnit вЂ” VMLAUNCH / VMRESUME / FSM Transitions
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmLaunchResumeTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly RecordingVmxEventSink _sink = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmLaunchResumeTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs, _sink);
        }

        private void EnableVmx()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
        }

        private void LoadVmcs(ulong addr = 0x1000)
        {
            _vmcs.Load(addr);
        }

        [Fact]
        public void VmLaunch_WithActiveVmcs_TransitionsToGuestExecution()
        {
            EnableVmx();
            LoadVmcs();
            _vmcs.WriteField(VmcsField.GuestPc, 0x2000);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(PipelineState.GuestExecution, _state.GetCurrentPipelineState());
        }

        [Fact]
        public void VmLaunch_LoadsGuestPcFromVmcs()
        {
            EnableVmx();
            LoadVmcs();
            _vmcs.WriteField(VmcsField.GuestPc, 0x8000);
            _state.SetInstructionPointer(0x1000);

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);

            Assert.Equal(0x8000UL, _state.GetInstructionPointer());
        }

        [Fact]
        public void VmLaunch_MarksVmcsAsLaunched()
        {
            EnableVmx();
            LoadVmcs();

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);

            Assert.True(_vmcs.HasLaunchedVmcs);
        }

        [Fact]
        public void VmLaunch_WithoutVmxEnabled_ReturnsVmxFault()
        {
            LoadVmcs();
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmLaunch_WithoutActiveVmcs_ReturnsVmxFault()
        {
            EnableVmx();
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmLaunch_WhenAlreadyLaunched_ReturnsVmxFault()
        {
            EnableVmx();
            LoadVmcs();
            _vmcs.MarkLaunched();

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmLaunch_NotInTaskState_ReturnsVmxFault()
        {
            EnableVmx();
            LoadVmcs();
            _state.SetCurrentPipelineState(PipelineState.Halted);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmResume_WithLaunchedVmcs_TransitionsToGuestExecution()
        {
            EnableVmx();
            LoadVmcs();
            _vmcs.MarkLaunched();

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMRESUME), _state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(PipelineState.GuestExecution, _state.GetCurrentPipelineState());
        }

        [Fact]
        public void VmResume_WithoutLaunchedVmcs_ReturnsVmxFault()
        {
            EnableVmx();
            LoadVmcs();  // loaded but not launched

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMRESUME), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmResume_WithoutVmxEnabled_ReturnsVmxFault()
        {
            LoadVmcs();
            _vmcs.MarkLaunched();

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMRESUME), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmLaunch_RecordsVmEntryTraceEvent()
        {
            EnableVmx();
            LoadVmcs();

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);

            Assert.Contains(_sink.Events, e => e.Kind == VmxEventKind.VmEntry);
        }

        [Fact]
        public void VmResume_RecordsVmResumeEvent()
        {
            EnableVmx();
            LoadVmcs();
            _vmcs.MarkLaunched();

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMRESUME), _state, PrivilegeLevel.Machine);

            Assert.Contains(_sink.Events, e => e.Kind == VmxEventKind.VmResume);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 10. VmxExecutionUnit вЂ” VMREAD / VMWRITE
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmReadWriteTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmReadWriteTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs);
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
        }

        [Fact]
        public void VmWrite_VmRead_RoundTrip()
        {
            // rs1 = register containing field ID, rs2 = register containing value
            _state.SetReg(1, (ulong)VmcsField.GuestPc);
            _state.SetReg(2, 0x12345678);

            // VMWRITE: VMCS[rs1] = rs2
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);

            // VMREAD: rd = VMCS[rs1]  (rd=3)
            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMREAD, rd: 3, rs1: 1), _state, PrivilegeLevel.Machine);

            Assert.Equal(0x12345678UL, _state.ReadIntRegister(3));
            Assert.Equal(0x12345678UL, result.Value);
        }

        [Fact]
        public void VmRead_AllVmcsFieldTypes()
        {
            // Write to multiple VMCS field types and read them back
            VmcsField[] fields = { VmcsField.GuestPc, VmcsField.HostPc, VmcsField.PinBasedControls, VmcsField.ExitReason };

            for (int i = 0; i < fields.Length; i++)
            {
                _state.SetReg(1, (ulong)fields[i]);
                _state.SetReg(2, (ulong)(i + 100));
                _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);
            }

            for (int i = 0; i < fields.Length; i++)
            {
                _state.SetReg(1, (ulong)fields[i]);
                _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMREAD, rd: 3, rs1: 1), _state, PrivilegeLevel.Machine);
                Assert.Equal((ulong)(i + 100), _state.ReadIntRegister(3));
            }
        }

        [Fact]
        public void VmRead_WithoutActiveVmcs_ReturnsVmxFault()
        {
            _vmcs.Clear(0x1000);
            _state.SetReg(1, (ulong)VmcsField.GuestPc);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMREAD, rd: 3, rs1: 1), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }

        [Fact]
        public void VmWrite_WithoutVmxEnabled_ReturnsVmxFault()
        {
            _csr.Write(CsrAddresses.VmxEnable, 0, PrivilegeLevel.Machine);
            _state.SetReg(1, (ulong)VmcsField.GuestPc);
            _state.SetReg(2, 42);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 11. VmxExecutionUnit вЂ” VMCLEAR / VMPTRLD
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmClearPtrLdTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly RecordingVmxEventSink _sink = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmClearPtrLdTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs, _sink);
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
        }

        [Fact]
        public void VmPtrLd_LoadsVmcs()
        {
            _state.SetReg(1, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), _state, PrivilegeLevel.Machine);

            Assert.True(_vmcs.HasActiveVmcs);
        }

        [Fact]
        public void VmClear_ClearsVmcs()
        {
            _vmcs.Load(0x2000);
            _vmcs.MarkLaunched();

            _state.SetReg(1, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMCLEAR, rs1: 1), _state, PrivilegeLevel.Machine);

            Assert.False(_vmcs.HasActiveVmcs);
            Assert.False(_vmcs.HasLaunchedVmcs);
        }

        [Fact]
        public void VmPtrLd_RecordsTraceEvent()
        {
            _state.SetReg(1, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), _state, PrivilegeLevel.Machine);

            Assert.Single(_sink.Events);
            Assert.Equal(VmxEventKind.VmPtrLd, _sink.Events[0].Kind);
        }

        [Fact]
        public void VmClear_RecordsTraceEvent()
        {
            _state.SetReg(1, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMCLEAR, rs1: 1), _state, PrivilegeLevel.Machine);

            Assert.Single(_sink.Events);
            Assert.Equal(VmxEventKind.VmClear, _sink.Events[0].Kind);
        }

        [Fact]
        public void VmPtrLd_WithoutVmxEnabled_ReturnsVmxFault()
        {
            _csr.Write(CsrAddresses.VmxEnable, 0, PrivilegeLevel.Machine);
            _state.SetReg(1, 0x2000);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), _state, PrivilegeLevel.Machine);
            Assert.True(result.VmxFaulted);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 12. VmxExecutionUnit вЂ” VMXOFF from GuestExecution (VM-Exit path)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxOffFromGuestTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly RecordingVmxEventSink _sink = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmxOffFromGuestTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs, _sink);
        }

        [Fact]
        public void VmxOff_InGuestExecution_TriggersVmExit_TransitionsToTask()
        {
            // Set up: VMX enabled, in GuestExecution state, VMCS loaded
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
            _vmcs.WriteField(VmcsField.HostPc, 0x5000);
            _state.SetCurrentPipelineState(PipelineState.GuestExecution);

            var result = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(PipelineState.Task, _state.GetCurrentPipelineState());
            Assert.Equal(0UL, _csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
        }

        [Fact]
        public void VmxOff_InGuestExecution_RecordsVmExitReason()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
            _vmcs.WriteField(VmcsField.HostPc, 0x5000);
            _state.SetCurrentPipelineState(PipelineState.GuestExecution);

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);

            Assert.Equal((ulong)VmExitReason.VmxOff,
                _csr.DirectRead(CsrAddresses.VmxExitReason));
        }

        [Fact]
        public void VmxOff_InGuestExecution_IncrementsVmExitCnt()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
            _vmcs.WriteField(VmcsField.HostPc, 0x5000);
            _state.SetCurrentPipelineState(PipelineState.GuestExecution);

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);

            Assert.Equal(1UL, _csr.DirectRead(CsrAddresses.VmExitCnt));
        }

        [Fact]
        public void VmxOff_InGuestExecution_RestoresHostPc()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
            _vmcs.WriteField(VmcsField.HostPc, 0x5000);
            _state.SetCurrentPipelineState(PipelineState.GuestExecution);
            _state.SetInstructionPointer(0x9000); // guest PC

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);

            Assert.Equal(0x5000UL, _state.GetInstructionPointer());
        }

        [Fact]
        public void VmxOff_InGuestExecution_RecordsVmExitTraceEvent()
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _vmcs.Load(0x1000);
            _vmcs.WriteField(VmcsField.HostPc, 0x5000);
            _state.SetCurrentPipelineState(PipelineState.GuestExecution);

            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);

            Assert.Contains(_sink.Events, e => e.Kind == VmxEventKind.VmExit && e.Reason == VmExitReason.VmxOff);
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 13. VmxExecutionUnit вЂ” Full VM Entry/Exit Cycle
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmEntryExitCycleTests
    {
        private readonly CsrFile _csr = new();
        private readonly VmcsManager _vmcs = new();
        private readonly RecordingVmxEventSink _sink = new();
        private readonly VmxExecutionUnit _vmx;
        private readonly Vmx09FakeCpuState _state = new();

        public VmEntryExitCycleTests()
        {
            _vmx = new VmxExecutionUnit(_csr, _vmcs, _sink);
        }

        [Fact]
        public void FullCycle_VmxOn_PtrLd_Launch_VmxOff()
        {
            // 1. VMXON
            var r1 = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);
            Assert.False(r1.VmxFaulted);

            // 2. VMPTRLD (load VMCS at address 0x1000)
            _state.SetReg(1, 0x1000);
            var r2 = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), _state, PrivilegeLevel.Machine);
            Assert.False(r2.VmxFaulted);

            // 3. VMWRITE guest PC
            _state.SetReg(1, (ulong)VmcsField.GuestPc);
            _state.SetReg(2, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);

            // 4. VMWRITE host PC (for VM-Exit restore)
            _state.SetReg(1, (ulong)VmcsField.HostPc);
            _state.SetReg(2, 0x3000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);

            // 5. VMLAUNCH
            _state.SetInstructionPointer(0x1000);  // host PC before launch
            var r3 = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.False(r3.VmxFaulted);
            Assert.Equal(PipelineState.GuestExecution, _state.GetCurrentPipelineState());
            Assert.Equal(0x2000UL, _state.GetInstructionPointer()); // guest PC loaded

            // 6. VMXOFF (triggers VM-Exit from GuestExecution)
            var r4 = _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);
            Assert.False(r4.VmxFaulted);
            Assert.Equal(PipelineState.Task, _state.GetCurrentPipelineState());
            Assert.Equal(0x3000UL, _state.GetInstructionPointer()); // host PC restored
        }

        [Fact]
        public void FullCycle_Launch_VmxOff_Resume()
        {
            // Enable VMX and set up VMCS
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);
            _state.SetReg(1, 0x1000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), _state, PrivilegeLevel.Machine);

            // Set VMCS fields
            _state.SetReg(1, (ulong)VmcsField.GuestPc);
            _state.SetReg(2, 0x2000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);
            _state.SetReg(1, (ulong)VmcsField.HostPc);
            _state.SetReg(2, 0x3000);
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), _state, PrivilegeLevel.Machine);

            // VMLAUNCH
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), _state, PrivilegeLevel.Machine);
            Assert.Equal(PipelineState.GuestExecution, _state.GetCurrentPipelineState());

            // VMXOFF в†’ VM-Exit в†’ Task
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), _state, PrivilegeLevel.Machine);
            Assert.Equal(PipelineState.Task, _state.GetCurrentPipelineState());

            // Re-enable VMX
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), _state, PrivilegeLevel.Machine);

            // VMRESUME вЂ” VMCS is still launched
            _vmx.Execute(VmxIrHelper.MakeVmx(InstructionsEnum.VMRESUME), _state, PrivilegeLevel.Machine);
            Assert.Equal(PipelineState.GuestExecution, _state.GetCurrentPipelineState());
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 14. ExecutionDispatcherV4 вЂ” VMX Dispatch Delegation
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class DispatcherVmxDelegationTests
    {
        [Fact]
        public void Dispatcher_WithVmxUnit_RejectsEagerSurface_AndResolvesDirectCompatVmxOn()
        {
            var csr = new CsrFile();
            var vmcs = new VmcsManager();
            var vmxUnit = new VmxExecutionUnit(csr, vmcs);
            var disp = new ExecutionDispatcherV4(vmxUnit: vmxUnit);

            var state = new Vmx09FakeCpuState();
            var instr = VmxIrHelper.MakeVmx(InstructionsEnum.VMXON);
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(instr));

            VmxRetireOutcome outcome = ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                instr,
                state);

            Assert.False(outcome.Faulted);
            Assert.Equal(1UL, csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
        }

        [Fact]
        public void Dispatcher_WithoutVmxUnit_FailsSurfaceContract_AndThrowsWhenBypassed()
        {
            var disp = new ExecutionDispatcherV4();
            var state = new Vmx09FakeCpuState();
            var instr = VmxIrHelper.MakeVmx(InstructionsEnum.VMXON);

            Assert.False(disp.CanRouteToConfiguredExecutionSurface(instr));
            Assert.Throws<InvalidOperationException>(
                () => RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                    disp,
                    instr,
                    state));

            Assert.Throws<InvalidOperationException>(
                () => disp.Execute(instr, state));
        }

        [Fact]
        public void Dispatcher_WithVmxUnit_DirectCompatLaunchCycle_ReachesGuestExecution()
        {
            var csr = new CsrFile();
            var vmcs = new VmcsManager();
            var vmxUnit = new VmxExecutionUnit(csr, vmcs);
            var disp = new ExecutionDispatcherV4(vmxUnit: vmxUnit);
            var state = new Vmx09FakeCpuState();
            Assert.False(disp.CanRouteToConfiguredExecutionSurface(VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH)));

            // VMXON
            ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                VmxIrHelper.MakeVmx(InstructionsEnum.VMXON),
                state);

            // VMPTRLD
            state.SetReg(1, 0x1000);
            ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1),
                state);

            // Set guest/host PCs via VMWRITE
            state.SetReg(1, (ulong)VmcsField.GuestPc);
            state.SetReg(2, 0x2000);
            ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2),
                state);
            state.SetReg(1, (ulong)VmcsField.HostPc);
            state.SetReg(2, 0x3000);
            ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                VmxIrHelper.MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2),
                state);

            // VMLAUNCH
            VmxRetireOutcome outcome = ResolveAndApplyDirectCompatVmxTransaction(
                disp,
                vmxUnit,
                VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH),
                state);

            Assert.False(outcome.Faulted);
            Assert.Equal(PipelineState.GuestExecution, state.GetCurrentPipelineState());
        }

        private static VmxRetireOutcome ResolveAndApplyDirectCompatVmxTransaction(
            ExecutionDispatcherV4 dispatcher,
            VmxExecutionUnit vmxUnit,
            InstructionIR instruction,
            Vmx09FakeCpuState state,
            byte vtId = 0)
        {
            RetireWindowCaptureSnapshot transaction =
                RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                    dispatcher,
                    instruction,
                    state,
                    bundleSerial: 0,
                    vtId: vtId);

        Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            VmxRetireOutcome outcome = vmxUnit.RetireEffect(transaction.VmxEffect, state, vtId);

            if (outcome.HasRegisterWriteback)
            {
                state.WriteIntRegister(outcome.RegisterDestination, outcome.RegisterWritebackValue);
            }

            if (outcome.RedirectTargetPc.HasValue)
            {
                state.SetInstructionPointer(outcome.RedirectTargetPc.Value);
            }

            if (outcome.RestoredStackPointer.HasValue)
            {
                state.WriteIntRegister(2, outcome.RestoredStackPointer.Value);
            }

            state.SetCurrentPipelineState(
                VmxExecutionUnit.ResolveFinalPipelineState(
                    state.GetCurrentPipelineState(),
                    transaction.VmxEffect,
                    outcome));

            return outcome;
        }
    }
}

