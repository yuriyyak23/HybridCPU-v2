using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VtScopedStateOwnershipTests
{
    [Fact]
    public void PipelineFsmEventHandler_Ecall_UsesVtScopedPcWrite()
    {
        const byte vtId = 2;
        const ulong currentPc = 0x1000;
        const ulong trapVector = 0x4000;

        var csr = new CsrFile();
        csr.Write(CsrAddresses.Mtvec, trapVector, PrivilegeLevel.Machine);

        var state = new TrackingVtScopedCpuState();
        state.SetPc(vtId, currentPc);

        var handler = new PipelineFsmEventHandler(csr);
        _ = handler.Handle(
            new EcallEvent
            {
                VtId = vtId,
                BundleSerial = 7,
                EcallCode = 0,
            },
            PipelineState.Task,
            state,
            PrivilegeLevel.User);

        Assert.Equal(0, state.LegacySetInstructionPointerCalls);
        Assert.Equal(0, state.LegacyWriteIntRegisterCalls);

        var pcWrite = Assert.Single(state.PcWrites);
        Assert.Equal(vtId, pcWrite.VtId);
        Assert.Equal(trapVector, pcWrite.Pc);
        Assert.Equal(currentPc, csr.Read(CsrAddresses.Mepc, PrivilegeLevel.Machine));
    }

    [Fact]
    public void VmcsManager_RestoreGuestState_UsesVtScopedPcAndRegisterWrites()
    {
        const byte vtId = 3;
        const ulong guestPc = 0x8000;
        const ulong guestSp = 0x9000;

        var manager = new VmcsManager();
        manager.Load(0x1000);
        manager.WriteField(VmcsField.GuestPc, unchecked((long)guestPc));
        manager.WriteField(VmcsField.GuestSp, unchecked((long)guestSp));

        var state = new TrackingVtScopedCpuState();
        manager.RestoreGuestState(state, vtId);

        Assert.Equal(0, state.LegacySetInstructionPointerCalls);
        Assert.Equal(0, state.LegacyWriteIntRegisterCalls);

        var pcWrite = Assert.Single(state.PcWrites);
        Assert.Equal(vtId, pcWrite.VtId);
        Assert.Equal(guestPc, pcWrite.Pc);

        var registerWrite = Assert.Single(state.RegisterWrites);
        Assert.Equal(vtId, registerWrite.VtId);
        Assert.Equal(2, registerWrite.RegId);
        Assert.Equal(guestSp, registerWrite.Value);
    }

    [Fact]
    public void VmcsManager_RestoreHostState_UsesVtScopedPcAndRegisterWrites()
    {
        const byte vtId = 1;
        const ulong hostPc = 0xA000;
        const ulong hostSp = 0xB000;

        var manager = new VmcsManager();
        manager.Load(0x2000);
        manager.WriteField(VmcsField.HostPc, unchecked((long)hostPc));
        manager.WriteField(VmcsField.HostSp, unchecked((long)hostSp));

        var state = new TrackingVtScopedCpuState();
        manager.RestoreHostState(state, vtId);

        Assert.Equal(0, state.LegacySetInstructionPointerCalls);
        Assert.Equal(0, state.LegacyWriteIntRegisterCalls);

        var pcWrite = Assert.Single(state.PcWrites);
        Assert.Equal(vtId, pcWrite.VtId);
        Assert.Equal(hostPc, pcWrite.Pc);

        var registerWrite = Assert.Single(state.RegisterWrites);
        Assert.Equal(vtId, registerWrite.VtId);
        Assert.Equal(2, registerWrite.RegId);
        Assert.Equal(hostSp, registerWrite.Value);
    }

    [Fact]
    public void LiveCpuStateAdapter_ApplyTo_DoesNotAutoCommitActiveFrontendPcWithoutExplicitPcWrite()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x2000, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteCommittedPc(0, 0x1000);
        core.WriteActiveLivePc(0x2200);

        var state = core.CreateLiveCpuStateAdapter(0);
        state.ApplyTo(ref core);

        Assert.Equal(0x1000UL, core.ReadCommittedPc(0));
        Assert.Equal(0x2200UL, core.ReadActiveLivePc());
    }

    [Fact]
    public void LiveCpuStateAdapter_ApplyTo_PublishesPendingPcThroughCoreOwnedPath()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteCommittedPc(0, 0x1000);

        var state = core.CreateLiveCpuStateAdapter(0);
        state.WritePc(0, 0x2400);
        state.ApplyTo(ref core);

        Assert.Equal(0x2400UL, core.ReadCommittedPc(0));
        Assert.Equal(0x2400UL, core.ReadActiveLivePc());
    }

    [Fact]
    public void LiveCpuStateAdapter_ApplyTo_ForInactiveVt_PreservesActiveFrontendPc()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteCommittedPc(2, 0x2200);
        ulong activeLivePcBefore = core.ReadActiveLivePc();

        var state = core.CreateLiveCpuStateAdapter(2);
        state.WritePc(2, 0x6600);
        state.ApplyTo(ref core);

        Assert.Equal(0x6600UL, core.ReadCommittedPc(2));
        Assert.Equal(activeLivePcBefore, core.ReadActiveLivePc());
        Assert.Equal(0x1000UL, core.ReadCommittedPc(0));
    }

    private sealed class TrackingVtScopedCpuState : ICanonicalCpuState
    {
        private readonly Dictionary<byte, ulong> _pcs = new();
        private readonly Dictionary<(byte VtId, int RegId), ulong> _registers = new();
        private PipelineState _pipelineState = PipelineState.Task;
        private ulong _legacyPc;

        public int LegacySetInstructionPointerCalls { get; private set; }
        public int LegacyWriteIntRegisterCalls { get; private set; }
        public List<(byte VtId, ulong Pc)> PcWrites { get; } = new();
        public List<(byte VtId, int RegId, ulong Value)> RegisterWrites { get; } = new();

        public void SetPc(byte vtId, ulong pc) => _pcs[vtId] = pc;

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

        public ulong ReadIntRegister(ushort regID) =>
            _registers.TryGetValue((0, regID), out ulong value) ? value : 0;

        public void WriteIntRegister(ushort regID, ulong value)
        {
            LegacyWriteIntRegisterCalls++;
            if (regID == 0)
                return;

            _registers[(0, regID)] = value;
        }

        public long ReadRegister(byte vtId, int regId) =>
            unchecked((long)(_registers.TryGetValue((vtId, regId), out ulong value) ? value : 0));

        public void WriteRegister(byte vtId, int regId, ulong value)
        {
            RegisterWrites.Add((vtId, regId, value));
            if (regId == 0)
                return;

            _registers[(vtId, regId)] = value;
        }

        public ushort GetPredicateMask(ushort maskID) => 0;
        public void SetPredicateMask(ushort maskID, ushort mask) { }
        public ulong GetInstructionPointer() => _legacyPc;

        public void SetInstructionPointer(ulong ip)
        {
            LegacySetInstructionPointerCalls++;
            _legacyPc = ip;
        }

        public ulong ReadPc(byte vtId) => _pcs.TryGetValue(vtId, out ulong pc) ? pc : _legacyPc;

        public void WritePc(byte vtId, ulong pc)
        {
            PcWrites.Add((vtId, pc));
            _pcs[vtId] = pc;
        }

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

