using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Compat;

namespace HybridCPU_ISE.Tests;

public sealed class PhaseAuditLegacyCpuStateBoundaryTests
{
    [Fact]
    public void LiveCpuStateAdapter_NoLongerImplementsCompatNonVtInterface()
    {
        Assert.DoesNotContain(
            typeof(ICpuState),
            typeof(Processor.CPU_Core.LiveCpuStateAdapter).GetInterfaces());
    }

    [Fact]
    public void LegacyCpuStateAdapter_WhenSelectedVtIsExplicit_RoutesLegacyRegisterAndPcCallsToThatVt()
    {
        var canonical = new MultiVtCanonicalCpuState();
        canonical.WriteRegister(0, 3, 0x10);
        canonical.WriteRegister(2, 3, 0x20);
        canonical.WritePc(0, 0x1000);
        canonical.WritePc(2, 0x2200);

        var legacy = new LegacyCpuStateAdapter(canonical, selectedVtId: 2);

        Assert.Equal(2, legacy.SelectedVtId);
        Assert.Equal(0x20UL, legacy.ReadIntRegister(3));
        Assert.Equal(0x2200UL, legacy.GetInstructionPointer());

        legacy.WriteIntRegister(3, 0x55);
        legacy.SetInstructionPointer(0x3300);

        Assert.Equal(0x55UL, legacy.ReadIntRegister(3));
        Assert.Equal(0x3300UL, legacy.GetInstructionPointer());
        Assert.Equal(0x10UL, unchecked((ulong)canonical.ReadRegister(0, 3)));
        Assert.Equal(0x1000UL, canonical.ReadPc(0));
    }

    [Fact]
    public void CreateLegacyCpuStateAdapter_WhenRequestedFromCore_BindsCompatAdapterToExplicitVt()
    {
        _ = new Processor(ProcessorMode.Emulation);
        ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];
        core.SeedCommittedArchForSetup(2, 7, 0xABCD);
        core.SeedCommittedPcForSetup(2, 0x4400);

        LegacyCpuStateAdapter legacy = core.CreateLegacyCpuStateAdapter(2);

        Assert.Equal(2, legacy.SelectedVtId);
        Assert.Equal(0xABCDUL, legacy.ReadIntRegister(7));
        Assert.Equal(0x4400UL, legacy.GetInstructionPointer());
    }

    private sealed class MultiVtCanonicalCpuState : ICanonicalCpuState
    {
        private readonly ulong[,] _registers = new ulong[4, 32];
        private readonly ulong[] _pcs = new ulong[4];
        private PipelineState _pipelineState;

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
        public ulong ReadPc(byte vtId) => _pcs[vtId];
        public void WritePc(byte vtId, ulong pc) => _pcs[vtId] = pc;
        public ushort GetCoreID() => 0;
        public ulong GetCycleCount() => 0;
        public ulong GetInstructionsRetired() => 0;
        public double GetIPC() => 0.0;
        public PipelineState GetCurrentPipelineState() => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state) => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger trigger) =>
            _pipelineState = PipelineFsmGuard.Transition(_pipelineState, trigger);
    }
}
