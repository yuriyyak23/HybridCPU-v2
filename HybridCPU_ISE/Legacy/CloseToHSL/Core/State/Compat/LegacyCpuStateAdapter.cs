using System;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Compat
{
    /// <summary>
    /// Explicit compat shim that binds the legacy non-VT state surface to a selected VT.
    /// This keeps obsolete single-thread semantics outside the production live adapter.
    /// </summary>
    public sealed class LegacyCpuStateAdapter : ICpuState
    {
        private readonly ICanonicalCpuState _canonicalState;
        private readonly byte _selectedVtId;

        public LegacyCpuStateAdapter(ICanonicalCpuState canonicalState, byte selectedVtId)
        {
            _canonicalState = canonicalState ?? throw new ArgumentNullException(nameof(canonicalState));
            _selectedVtId = selectedVtId;
        }

        public byte SelectedVtId => _selectedVtId;

        [Obsolete("Use ReadRegister(byte vtId, int regId) for VT-scoped access.")]
        public ulong ReadIntRegister(ushort regID) =>
            unchecked((ulong)_canonicalState.ReadRegister(_selectedVtId, regID));

        [Obsolete("Use WriteRegister(byte vtId, int regId, ulong value) for VT-scoped access.")]
        public void WriteIntRegister(ushort regID, ulong value) =>
            _canonicalState.WriteRegister(_selectedVtId, regID, value);

        [Obsolete("Use ReadPc(byte vtId) for VT-scoped access.")]
        public ulong GetInstructionPointer() =>
            _canonicalState.ReadPc(_selectedVtId);

        [Obsolete("Use WritePc(byte vtId, ulong pc) for VT-scoped access.")]
        public void SetInstructionPointer(ulong ip) =>
            _canonicalState.WritePc(_selectedVtId, ip);

        public ulong GetVL() => _canonicalState.GetVL();
        public void SetVL(ulong vl) => _canonicalState.SetVL(vl);
        public ulong GetVLMAX() => _canonicalState.GetVLMAX();
        public byte GetSEW() => _canonicalState.GetSEW();
        public void SetSEW(byte sew) => _canonicalState.SetSEW(sew);
        public byte GetLMUL() => _canonicalState.GetLMUL();
        public void SetLMUL(byte lmul) => _canonicalState.SetLMUL(lmul);
        public bool GetTailAgnostic() => _canonicalState.GetTailAgnostic();
        public void SetTailAgnostic(bool agnostic) => _canonicalState.SetTailAgnostic(agnostic);
        public bool GetMaskAgnostic() => _canonicalState.GetMaskAgnostic();
        public void SetMaskAgnostic(bool agnostic) => _canonicalState.SetMaskAgnostic(agnostic);
        public uint GetExceptionMask() => _canonicalState.GetExceptionMask();
        public void SetExceptionMask(uint mask) => _canonicalState.SetExceptionMask(mask);
        public uint GetExceptionPriority() => _canonicalState.GetExceptionPriority();
        public void SetExceptionPriority(uint priority) => _canonicalState.SetExceptionPriority(priority);
        public byte GetRoundingMode() => _canonicalState.GetRoundingMode();
        public void SetRoundingMode(byte mode) => _canonicalState.SetRoundingMode(mode);
        public ulong GetOverflowCount() => _canonicalState.GetOverflowCount();
        public ulong GetUnderflowCount() => _canonicalState.GetUnderflowCount();
        public ulong GetDivByZeroCount() => _canonicalState.GetDivByZeroCount();
        public ulong GetInvalidOpCount() => _canonicalState.GetInvalidOpCount();
        public ulong GetInexactCount() => _canonicalState.GetInexactCount();
        public void ClearExceptionCounters() => _canonicalState.ClearExceptionCounters();
        public bool GetVectorDirty() => _canonicalState.GetVectorDirty();
        public void SetVectorDirty(bool dirty) => _canonicalState.SetVectorDirty(dirty);
        public bool GetVectorEnabled() => _canonicalState.GetVectorEnabled();
        public void SetVectorEnabled(bool enabled) => _canonicalState.SetVectorEnabled(enabled);
        public long ReadRegister(byte vtId, int regId) => _canonicalState.ReadRegister(vtId, regId);
        public void WriteRegister(byte vtId, int regId, ulong value) => _canonicalState.WriteRegister(vtId, regId, value);
        public ushort GetPredicateMask(ushort maskID) => _canonicalState.GetPredicateMask(maskID);
        public void SetPredicateMask(ushort maskID, ushort mask) => _canonicalState.SetPredicateMask(maskID, mask);
        public ulong ReadPc(byte vtId) => _canonicalState.ReadPc(vtId);
        public void WritePc(byte vtId, ulong pc) => _canonicalState.WritePc(vtId, pc);
        public ushort GetCoreID() => _canonicalState.GetCoreID();
        public ulong GetCycleCount() => _canonicalState.GetCycleCount();
        public ulong GetInstructionsRetired() => _canonicalState.GetInstructionsRetired();
        public double GetIPC() => _canonicalState.GetIPC();
        public PipelineState GetCurrentPipelineState() => _canonicalState.GetCurrentPipelineState();
        public void SetCurrentPipelineState(PipelineState state) => _canonicalState.SetCurrentPipelineState(state);
        public void TransitionPipelineState(PipelineTransitionTrigger trigger) => _canonicalState.TransitionPipelineState(trigger);
    }
}
