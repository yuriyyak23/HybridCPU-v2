using System;

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    /// <summary>
    /// Per-VT architectural context that groups committed GPR/PC state with
    /// privilege/context metadata during the phase-04 state unification.
    /// </summary>
    public sealed class ArchContextState
    {
        private ulong _committedPc;

        public ArchContextState(int committedRegisterCount, int vtId)
        {
            if (committedRegisterCount < 1)
                throw new ArgumentOutOfRangeException(nameof(committedRegisterCount));

            if (vtId < 0)
                throw new ArgumentOutOfRangeException(nameof(vtId));

            VtId = vtId;
            CommittedRegs = new ulong[committedRegisterCount];
            CurrentPrivilege = PrivilegeLevel.Machine;
        }

        public int VtId { get; }

        public ulong[] CommittedRegs { get; }

        public ulong CommittedPc
        {
            get => _committedPc;
            set => _committedPc = value;
        }

        public PrivilegeLevel CurrentPrivilege { get; set; }

        public bool IsVmxNonRoot { get; set; }
    }
}
