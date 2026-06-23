using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    /// <summary>
    /// Handles first-class non-VMX system and privileged <see cref="PipelineEvent"/> instances.
    /// Production VMX no longer flows through this event plane or its constructor
    /// dependencies; it retires through typed VMX retire outcomes instead.
    /// </summary>
    public sealed class PipelineFsmEventHandler
    {
        private const ulong CauseMachineEcall = 11;
        private const ulong CauseSupervisorEcall = 9;
        private const ulong CauseUserEcall = 8;
        private const ulong CauseBreakpoint = 3;
        private const ulong CauseIllegalInstruction = 2;

        private readonly CsrFile _csr;

        public PipelineFsmEventHandler(
            CsrFile csr,
            int vtCount = 4)
        {
            _csr = csr ?? throw new ArgumentNullException(nameof(csr));

            if (vtCount < 1)
                throw new ArgumentOutOfRangeException(nameof(vtCount), "At least one virtual thread is required.");
        }

        public PipelineState Handle(
            PipelineEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState,
            PrivilegeLevel privilege)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (cpuState is null) throw new ArgumentNullException(nameof(cpuState));

            return evt switch
            {
                EcallEvent e => HandleEcall(e, current, cpuState, privilege),
                EbreakEvent e => HandleEbreak(e, current, cpuState),
                TrapEntryEvent e => HandleTrapEntry(e, current, cpuState),
                MretEvent e => HandleMret(e, current, cpuState),
                SretEvent e => HandleSret(e, current, cpuState),
                FenceEvent e => HandleFence(e, current),
                WfiEvent e => HandleWfi(e, current),
                YieldEvent e => HandleYield(e, current),
                WfeEvent e => HandleWfe(e, current),
                SevEvent e => HandleSev(e, current),
                PodBarrierEvent e => HandlePodBarrier(e, current),
                VtBarrierEvent e => HandleVtBarrier(e, current),
                _ => current,
            };
        }

        private PipelineState HandleEcall(
            EcallEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState,
            PrivilegeLevel privilege)
        {
            _csr.Write(CsrAddresses.Mepc, cpuState.ReadPc(evt.VtId), PrivilegeLevel.Machine);

            ulong cause = privilege switch
            {
                PrivilegeLevel.Machine => CauseMachineEcall,
                PrivilegeLevel.Supervisor => CauseSupervisorEcall,
                _ => CauseUserEcall,
            };
            _csr.Write(CsrAddresses.Mcause, cause, PrivilegeLevel.Machine);

            ulong mtvec = _csr.Read(CsrAddresses.Mtvec, PrivilegeLevel.Machine);
            cpuState.WritePc(evt.VtId, mtvec);
            return current;
        }

        private PipelineState HandleEbreak(
            EbreakEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState)
        {
            _csr.Write(CsrAddresses.Mepc, cpuState.ReadPc(evt.VtId), PrivilegeLevel.Machine);
            _csr.Write(CsrAddresses.Mcause, CauseBreakpoint, PrivilegeLevel.Machine);

            ulong mtvec = _csr.Read(CsrAddresses.Mtvec, PrivilegeLevel.Machine);
            cpuState.WritePc(evt.VtId, mtvec);
            return current;
        }

        private PipelineState HandleTrapEntry(
            TrapEntryEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState)
        {
            _csr.Write(CsrAddresses.Mepc, cpuState.ReadPc(evt.VtId), PrivilegeLevel.Machine);
            _csr.Write(
                CsrAddresses.Mcause,
                evt.CauseCode == 0 ? CauseIllegalInstruction : evt.CauseCode,
                PrivilegeLevel.Machine);
            _csr.Write(CsrAddresses.Mtval, evt.FaultAddress, PrivilegeLevel.Machine);

            ulong mtvec = _csr.Read(CsrAddresses.Mtvec, PrivilegeLevel.Machine);
            cpuState.WritePc(evt.VtId, mtvec);
            return current;
        }

        private PipelineState HandleMret(
            MretEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState)
        {
            ulong mepc = _csr.Read(CsrAddresses.Mepc, PrivilegeLevel.Machine);
            cpuState.WritePc(evt.VtId, mepc);
            return current;
        }

        private PipelineState HandleSret(
            SretEvent evt,
            PipelineState current,
            ICanonicalCpuState cpuState)
        {
            ulong sepc = _csr.Read(CsrAddresses.Sepc, PrivilegeLevel.Supervisor);
            cpuState.WritePc(evt.VtId, sepc);
            return current;
        }

        private static PipelineState HandleFence(FenceEvent evt, PipelineState current)
        {
            _ = evt;
            return current;
        }

        private static PipelineState HandleWfi(WfiEvent evt, PipelineState current)
        {
            return PipelineFsmGuard.Advance(current, evt);
        }

        private static PipelineState HandleYield(YieldEvent evt, PipelineState current)
        {
            _ = evt;
            return current;
        }

        private static PipelineState HandleWfe(WfeEvent evt, PipelineState current)
        {
            return PipelineFsmGuard.Advance(current, evt);
        }

        private static PipelineState HandleSev(SevEvent evt, PipelineState current)
        {
            return current == PipelineState.WaitForEvent
                ? PipelineFsmGuard.Advance(current, evt)
                : current;
        }

        private static PipelineState HandlePodBarrier(PodBarrierEvent evt, PipelineState current)
        {
            _ = evt;
            return current;
        }

        private static PipelineState HandleVtBarrier(VtBarrierEvent evt, PipelineState current)
        {
            _ = evt;
            return current;
        }
    }
}

