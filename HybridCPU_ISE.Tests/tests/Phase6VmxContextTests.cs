// Phase 6 (analysis) вЂ” VMX as FSM: Full VMCS Host/Guest Context + Epoch Tracking
// Covers:
//   - VmcsManager.SaveGuestState: persists PC + SP to VMCS guest state area
//   - VmcsManager.RestoreGuestState: loads PC + SP from VMCS guest state area
//   - VmcsManager.SaveHostState: persists PC + SP to VMCS host state area
//   - VmcsManager.RestoreHostState: loads PC + SP from VMCS host state area (expanded)
//   - Full context-switch cycle: SaveHostState в†’ RestoreGuestState в†’ вЂ¦ в†’ SaveGuestState в†’ RestoreHostState
//   - VmxEpochTracker: epoch counter incremented on VM-Entry/VM-Exit/VMXON/VMXOFF
//   - VmxEpochTracker: non-epoch events do NOT bump the counter
//   - VmxEpochTracker.IsCurrentEpoch: stale detection
//   - VmxEpochTracker.Reset: counter returns to 0
//   - CompositeVmxEventSink: routes to both primary and secondary sinks
//   - VmxExecutionUnit VMLAUNCH: uses RestoreGuestState (loads both PC and SP from VMCS)
//   - VmxExecutionUnit VMRESUME: uses RestoreGuestState
//   - VmxExecutionUnit VM-Exit: SaveGuestState persists SP in addition to PC

using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase6
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Minimal ICanonicalCpuState stub for Phase 6 VMCS context tests.</summary>
    internal sealed class P6FakeCpuState : ICanonicalCpuState
    {
        private readonly Dictionary<ushort, ulong> _regs = new();
        private readonly Dictionary<byte, ulong> _vtPcs = new();
        private readonly Dictionary<(byte VtId, ushort RegId), ulong> _vtRegs = new();
        private ulong _pc;

        public byte? LastReadPcVtId { get; private set; }
        public (byte VtId, int RegId)? LastReadRegisterRequest { get; private set; }

        public ulong ReadIntRegister(ushort id)  => _regs.TryGetValue(id, out var v) ? v : 0UL;
        public void WriteIntRegister(ushort id, ulong value)
        {
            if (id != 0) _regs[id] = value;
        }
        public void SetReg(ushort id, ulong value)
        {
            _regs[id] = value;
            _vtRegs[(0, id)] = value;
        }
        public void SetVtPc(byte vtId, ulong value) => _vtPcs[vtId] = value;
        public void SetVtReg(byte vtId, ushort id, ulong value) => _vtRegs[(vtId, id)] = value;

        public ulong GetInstructionPointer()            => _pc;
        public void  SetInstructionPointer(ulong ip)
        {
            _pc = ip;
            _vtPcs[0] = ip;
        }
        public ulong ReadPc(byte vtId)
        {
            LastReadPcVtId = vtId;
            return _vtPcs.TryGetValue(vtId, out var value) ? value : _pc;
        }

        public void WritePc(byte vtId, ulong pc)
        {
            _vtPcs[vtId] = pc;
            _pc = pc;
        }

        public long ReadRegister(byte vtId, int regId)
        {
            LastReadRegisterRequest = (vtId, regId);
            return _vtRegs.TryGetValue((vtId, (ushort)regId), out var value)
                ? unchecked((long)value)
                : unchecked((long)ReadIntRegister((ushort)regId));
        }

        public void WriteRegister(byte vtId, int regId, ulong value)
        {
            if (regId == 0)
                return;

            _vtRegs[(vtId, (ushort)regId)] = value;
            _regs[(ushort)regId] = value;
        }
        public ushort GetCoreID()                       => 0;

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

        private PipelineState _pipelineState = PipelineState.Task;
        public PipelineState GetCurrentPipelineState()                   => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state)         => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger t) => _pipelineState = PipelineFsmGuard.Transition(_pipelineState, t);
    }

    /// <summary>VMX event sink that records all events for test assertions.</summary>
    internal sealed class P6RecordingSink : IVmxEventSink
    {
        public List<(VmxEventKind Kind, ushort VtId, VmExitReason Reason)> Events { get; } = new();
        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
            => Events.Add((kind, vtId, exitReason));
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 1. VmcsManager вЂ” SaveGuestState persists PC and SP
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsSaveGuestStatePhase6Tests
    {
        [Fact]
        public void SaveGuestState_PersistsGuestPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetInstructionPointer(0xABCD_0000);

            mgr.SaveGuestState(state);

            Assert.Equal((long)0xABCD_0000, mgr.ReadField(VmcsField.GuestPc));
        }

        [Fact]
        public void SaveGuestState_PersistsGuestSp()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetReg(2, 0xDEAD_BEE0); // x2 = sp

            mgr.SaveGuestState(state);

            Assert.Equal((long)0xDEAD_BEE0, mgr.ReadField(VmcsField.GuestSp));
        }

        [Fact]
        public void SaveGuestState_PcAndSpSavedTogether()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetInstructionPointer(0x1234_5678);
            state.SetReg(2, 0xBEEF_CAFE);

            mgr.SaveGuestState(state);

            Assert.Equal((long)0x1234_5678, mgr.ReadField(VmcsField.GuestPc));
            Assert.Equal((long)0xBEEF_CAFE, mgr.ReadField(VmcsField.GuestSp));
        }

        [Fact]
        public void SaveGuestState_VtScopedOverload_UsesVtScopedPcAndSpReads()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetInstructionPointer(0x1111);
            state.SetReg(2, 0x2222);
            state.SetVtPc(3, 0x3333);
            state.SetVtReg(3, 2, 0x4444);

            mgr.SaveGuestState(state, 3);

            Assert.Equal((byte)3, state.LastReadPcVtId);
            Assert.Equal((byte)3, state.LastReadRegisterRequest?.VtId);
            Assert.Equal(2, state.LastReadRegisterRequest?.RegId);
            Assert.Equal((long)0x3333, mgr.ReadField(VmcsField.GuestPc));
            Assert.Equal((long)0x4444, mgr.ReadField(VmcsField.GuestSp));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 2. VmcsManager вЂ” RestoreGuestState loads PC and SP
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsRestoreGuestStatePhase6Tests
    {
        [Fact]
        public void RestoreGuestState_LoadsGuestPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestPc, 0x5000);

            var state = new P6FakeCpuState();
            mgr.RestoreGuestState(state);

            Assert.Equal(0x5000UL, state.GetInstructionPointer());
        }

        [Fact]
        public void RestoreGuestState_LoadsGuestSp()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestSp, 0x7FFF_FFF0);

            var state = new P6FakeCpuState();
            mgr.RestoreGuestState(state);

            Assert.Equal(0x7FFF_FFF0UL, state.ReadIntRegister(2));
        }

        [Fact]
        public void RestoreGuestState_LoadsPcAndSpTogether()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.GuestPc, 0x2000);
            mgr.WriteField(VmcsField.GuestSp, 0x9000);

            var state = new P6FakeCpuState();
            mgr.RestoreGuestState(state);

            Assert.Equal(0x2000UL, state.GetInstructionPointer());
            Assert.Equal(0x9000UL, state.ReadIntRegister(2));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 3. VmcsManager вЂ” SaveHostState persists host PC and SP
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsSaveHostStatePhase6Tests
    {
        [Fact]
        public void SaveHostState_PersistsHostPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetInstructionPointer(0xC000_0000);

            mgr.SaveHostState(state);

            Assert.Equal((long)0xC000_0000, mgr.ReadField(VmcsField.HostPc));
        }

        [Fact]
        public void SaveHostState_PersistsHostSp()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            var state = new P6FakeCpuState();
            state.SetReg(2, 0xFFFF_F000);

            mgr.SaveHostState(state);

            Assert.Equal((long)0xFFFF_F000, mgr.ReadField(VmcsField.HostSp));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 4. VmcsManager вЂ” RestoreHostState loads host SP in addition to PC
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsRestoreHostStatePhase6Tests
    {
        [Fact]
        public void RestoreHostState_LoadsHostPc()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.HostPc, 0xCAFE_0000);

            var state = new P6FakeCpuState();
            mgr.RestoreHostState(state);

            Assert.Equal(0xCAFE_0000UL, state.GetInstructionPointer());
        }

        [Fact]
        public void RestoreHostState_LoadsHostSp()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);
            mgr.WriteField(VmcsField.HostSp, 0xBBBB_0000);

            var state = new P6FakeCpuState();
            mgr.RestoreHostState(state);

            Assert.Equal(0xBBBB_0000UL, state.ReadIntRegister(2));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 5. VmcsManager вЂ” Full context-switch cycle
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmcsContextSwitchCyclePhase6Tests
    {
        [Fact]
        public void GuestContextSurvivesExitAndReentry()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000);

            // Pre-configure host state via SaveHostState
            var hostState = new P6FakeCpuState();
            hostState.SetInstructionPointer(0x3000);  // host PC
            hostState.SetReg(2, 0xF000);              // host SP
            mgr.SaveHostState(hostState);

            // Pre-configure guest context via VMCS fields
            mgr.WriteField(VmcsField.GuestPc, 0x5000);
            mgr.WriteField(VmcsField.GuestSp, 0x8000);

            // VM-Entry: load guest context
            var cpu = new P6FakeCpuState();
            mgr.RestoreGuestState(cpu);
            Assert.Equal(0x5000UL, cpu.GetInstructionPointer()); // guest PC
            Assert.Equal(0x8000UL, cpu.ReadIntRegister(2));      // guest SP

            // Guest advances PC and SP
            cpu.SetInstructionPointer(0x5100);
            cpu.SetReg(2, 0x7FF0);

            // VM-Exit: save guest state, restore host state
            mgr.SaveGuestState(cpu);
            mgr.RestoreHostState(cpu);

            Assert.Equal(0x3000UL, cpu.GetInstructionPointer()); // host PC restored
            Assert.Equal(0xF000UL, cpu.ReadIntRegister(2));      // host SP restored
            Assert.Equal((long)0x5100, mgr.ReadField(VmcsField.GuestPc)); // guest PC saved
            Assert.Equal((long)0x7FF0, mgr.ReadField(VmcsField.GuestSp)); // guest SP saved
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 6. VmxEpochTracker вЂ” epoch boundary events
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxEpochTrackerTests
    {
        [Fact]
        public void InitialEpochIsZero()
        {
            var tracker = new VmxEpochTracker();
            Assert.Equal(0UL, tracker.CurrentEpoch);
        }

        [Theory]
        [InlineData((byte)VmxEventKind.VmEntry)]
        [InlineData((byte)VmxEventKind.VmResume)]
        [InlineData((byte)VmxEventKind.VmExit)]
        [InlineData((byte)VmxEventKind.VmxOn)]
        [InlineData((byte)VmxEventKind.VmxOff)]
        public void EpochBoundaryEvents_IncrementEpoch(byte kind)
        {
            var tracker = new VmxEpochTracker();
            tracker.RecordVmxEvent((VmxEventKind)kind, 0);
            Assert.Equal(1UL, tracker.CurrentEpoch);
        }

        [Theory]
        [InlineData((byte)VmxEventKind.VmRead)]
        [InlineData((byte)VmxEventKind.VmWrite)]
        [InlineData((byte)VmxEventKind.VmClear)]
        [InlineData((byte)VmxEventKind.VmPtrLd)]
        public void NonEpochEvents_DoNotIncrementEpoch(byte kind)
        {
            var tracker = new VmxEpochTracker();
            tracker.RecordVmxEvent((VmxEventKind)kind, 0);
            Assert.Equal(0UL, tracker.CurrentEpoch);
        }

        [Fact]
        public void MultipleTransitions_EpochMonotonicallyIncreases()
        {
            var tracker = new VmxEpochTracker();
            tracker.RecordVmxEvent(VmxEventKind.VmxOn, 0);
            tracker.RecordVmxEvent(VmxEventKind.VmEntry, 0);
            tracker.RecordVmxEvent(VmxEventKind.VmExit, 0);
            tracker.RecordVmxEvent(VmxEventKind.VmxOff, 0);
            Assert.Equal(4UL, tracker.CurrentEpoch);
        }

        [Fact]
        public void IsCurrentEpoch_ReturnsTrueForMatchingEpoch()
        {
            var tracker = new VmxEpochTracker();
            ulong captured = tracker.CurrentEpoch; // 0
            Assert.True(tracker.IsCurrentEpoch(captured));
        }

        [Fact]
        public void IsCurrentEpoch_ReturnsFalseAfterTransition()
        {
            var tracker = new VmxEpochTracker();
            ulong captured = tracker.CurrentEpoch; // 0
            tracker.RecordVmxEvent(VmxEventKind.VmEntry, 0);
            Assert.False(tracker.IsCurrentEpoch(captured)); // epoch is now 1
        }

        [Fact]
        public void IsEpochBoundary_ReturnsCorrectly()
        {
            Assert.True(VmxEpochTracker.IsEpochBoundary(VmxEventKind.VmEntry));
            Assert.True(VmxEpochTracker.IsEpochBoundary(VmxEventKind.VmExit));
            Assert.False(VmxEpochTracker.IsEpochBoundary(VmxEventKind.VmRead));
            Assert.False(VmxEpochTracker.IsEpochBoundary(VmxEventKind.VmWrite));
        }

        [Fact]
        public void LastTransitionKind_ReflectsMostRecentBoundaryEvent()
        {
            var tracker = new VmxEpochTracker();
            Assert.Null(tracker.LastTransitionKind); // no transition yet
            tracker.RecordVmxEvent(VmxEventKind.VmxOn, 0);
            tracker.RecordVmxEvent(VmxEventKind.VmEntry, 0);
            Assert.Equal(VmxEventKind.VmEntry, tracker.LastTransitionKind);
        }

        [Fact]
        public void Reset_ResetsEpochToZero()
        {
            var tracker = new VmxEpochTracker();
            tracker.RecordVmxEvent(VmxEventKind.VmxOn, 0);
            tracker.RecordVmxEvent(VmxEventKind.VmEntry, 0);
            tracker.Reset();
            Assert.Equal(0UL, tracker.CurrentEpoch);
            Assert.Null(tracker.LastTransitionKind); // cleared by Reset
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 7. CompositeVmxEventSink вЂ” routes to both sinks
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class CompositeVmxEventSinkTests
    {
        [Fact]
        public void RecordVmxEvent_RoutesToBothSinks()
        {
            var primary   = new P6RecordingSink();
            var secondary = new P6RecordingSink();
            var composite = new CompositeVmxEventSink(primary, secondary);

            composite.RecordVmxEvent(VmxEventKind.VmEntry, 1);

            Assert.Single(primary.Events);
            Assert.Single(secondary.Events);
            Assert.Equal(VmxEventKind.VmEntry, primary.Events[0].Kind);
            Assert.Equal(VmxEventKind.VmEntry, secondary.Events[0].Kind);
        }

        [Fact]
        public void CompositeWithEpochTracker_EpochIsTracked()
        {
            var tracker    = new VmxEpochTracker();
            var diagnostic = new P6RecordingSink();
            var composite  = new CompositeVmxEventSink(tracker, diagnostic);

            composite.RecordVmxEvent(VmxEventKind.VmxOn,  0);
            composite.RecordVmxEvent(VmxEventKind.VmEntry, 0);
            composite.RecordVmxEvent(VmxEventKind.VmExit,  0);

            Assert.Equal(3UL, tracker.CurrentEpoch);
            Assert.Equal(3, diagnostic.Events.Count);
        }

        [Fact]
        public void Constructor_ThrowsOnNullPrimary()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CompositeVmxEventSink(null!, new P6RecordingSink()));
        }

        [Fact]
        public void Constructor_ThrowsOnNullSecondary()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CompositeVmxEventSink(new P6RecordingSink(), null!));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 8. VmxExecutionUnit вЂ” VMLAUNCH loads guest SP in addition to PC (Phase 6)
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class VmxExecutionUnitPhase6Tests
    {
        private static InstructionIR MakeVmx(InstructionsEnum opcode, byte rd = 0, byte rs1 = 0, byte rs2 = 0)
            => new()
            {
                CanonicalOpcode = opcode,
                Class  = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = rd, Rs1 = rs1, Rs2 = rs2, Imm = 0,
            };

        private static (VmxExecutionUnit vmx, CsrFile csr, VmcsManager vmcs, P6FakeCpuState state)
            SetupVmxWithVmcs(ulong guestPc, ulong guestSp, ulong hostPc)
        {
            var csr   = new CsrFile();
            var vmcs  = new VmcsManager();
            var state = new P6FakeCpuState();
            var vmx   = new VmxExecutionUnit(csr, vmcs);

            // VMXON
            vmx.Execute(MakeVmx(InstructionsEnum.VMXON), state, PrivilegeLevel.Machine);

            // VMPTRLD вЂ” load VMCS at 0x1000
            state.SetReg(1, 0x1000);
            vmx.Execute(MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), state, PrivilegeLevel.Machine);

            // VMWRITE GuestPc
            state.SetReg(1, (ulong)VmcsField.GuestPc);
            state.SetReg(2, guestPc);
            vmx.Execute(MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), state, PrivilegeLevel.Machine);

            // VMWRITE GuestSp
            state.SetReg(1, (ulong)VmcsField.GuestSp);
            state.SetReg(2, guestSp);
            vmx.Execute(MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), state, PrivilegeLevel.Machine);

            // VMWRITE HostPc
            state.SetReg(1, (ulong)VmcsField.HostPc);
            state.SetReg(2, hostPc);
            vmx.Execute(MakeVmx(InstructionsEnum.VMWRITE, rs1: 1, rs2: 2), state, PrivilegeLevel.Machine);

            return (vmx, csr, vmcs, state);
        }

        [Fact]
        public void Vmlaunch_LoadsGuestSpFromVmcs()
        {
            var (vmx, _, _, state) = SetupVmxWithVmcs(guestPc: 0x2000, guestSp: 0xA000, hostPc: 0x3000);

            var result = vmx.Execute(MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(0x2000UL, state.GetInstructionPointer()); // guest PC loaded
            Assert.Equal(0xA000UL, state.ReadIntRegister(2));       // guest SP loaded (Phase 6)
        }

        [Fact]
        public void VmxOff_GuestSpSavedToVmcs()
        {
            var (vmx, _, vmcs, state) = SetupVmxWithVmcs(guestPc: 0x2000, guestSp: 0xA000, hostPc: 0x3000);

            // VMLAUNCH
            vmx.Execute(MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            // Advance guest SP while in guest execution
            state.SetReg(2, 0x9FF0);

            // VMXOFF triggers VM-Exit which calls SaveGuestState
            vmx.Execute(MakeVmx(InstructionsEnum.VMXOFF), state, PrivilegeLevel.Machine);

            // Guest SP should have been saved to VMCS
            Assert.Equal((long)0x9FF0, vmcs.ReadField(VmcsField.GuestSp));
        }

        [Fact]
        public void Vmresume_LoadsGuestSpFromVmcs()
        {
            var (vmx, _, vmcs, state) = SetupVmxWithVmcs(guestPc: 0x2000, guestSp: 0xA000, hostPc: 0x3000);

            // VMLAUNCH
            vmx.Execute(MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            // VMXOFF в†’ VM-Exit (saves guest state, restores host state, disables VMX)
            vmx.Execute(MakeVmx(InstructionsEnum.VMXOFF), state, PrivilegeLevel.Machine);

            // Update saved guest SP in VMCS to simulate guest having advanced the stack
            vmcs.WriteField(VmcsField.GuestSp, 0xB000);

            // VMXON again, then VMRESUME
            vmx.Execute(MakeVmx(InstructionsEnum.VMXON),    state, PrivilegeLevel.Machine);
            var result = vmx.Execute(MakeVmx(InstructionsEnum.VMRESUME), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(0xB000UL, state.ReadIntRegister(2)); // updated guest SP loaded
        }
    }
}

