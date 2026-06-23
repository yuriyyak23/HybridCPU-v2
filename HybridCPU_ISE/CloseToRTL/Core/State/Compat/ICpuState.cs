using System;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Compat
{
    /// <summary>
    /// Legacy compatibility surface retained only for compat/test callers that still
    /// exercise the old non-VT shims. Production execution paths should depend on
    /// <see cref="ICanonicalCpuState"/> instead.
    /// </summary>
    public interface ICpuState : ICanonicalCpuState
    {
        [Obsolete("Use ReadRegister(byte vtId, int regId) for VT-scoped access.")]
        ulong ReadIntRegister(ushort regID);

        [Obsolete("Use WriteRegister(byte vtId, int regId, ulong value) for VT-scoped access.")]
        void WriteIntRegister(ushort regID, ulong value);

        [Obsolete("Use ReadPc(byte vtId) for VT-scoped access.")]
        ulong GetInstructionPointer();

        [Obsolete("Use WritePc(byte vtId, ulong pc) for VT-scoped access.")]
        void SetInstructionPointer(ulong ip);
    }
}
