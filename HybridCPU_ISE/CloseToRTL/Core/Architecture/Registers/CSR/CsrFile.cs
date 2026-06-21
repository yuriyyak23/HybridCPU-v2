// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — CSR Register File
// Phase 08: CSR Layer
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    // ─────────────────────────────────────────────────────────────────────────
    // CsrPrivilegeFaultException
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thrown when a CSR access is denied due to insufficient privilege.
    /// </summary>
    public sealed class CsrPrivilegeFaultException : Exception
    {
        public ushort CsrAddress { get; }
        public PrivilegeLevel RequiredLevel { get; }
        public PrivilegeLevel ActualLevel { get; }
        public bool IsWrite { get; }

        public CsrPrivilegeFaultException(
            ushort csrAddress, PrivilegeLevel required, PrivilegeLevel actual, bool isWrite)
            : base($"CSR privilege fault: {(isWrite ? "write" : "read")} to 0x{csrAddress:X3} " +
                   $"requires {required}, current level is {actual}")
        {
            CsrAddress = csrAddress;
            RequiredLevel = required;
            ActualLevel = actual;
            IsWrite = isWrite;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CsrUnknownAddressException
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thrown when an unregistered CSR address is accessed.
    /// </summary>
    public sealed class CsrUnknownAddressException : Exception
    {
        public ushort CsrAddress { get; }

        public CsrUnknownAddressException(ushort csrAddress)
            : base($"Unknown CSR address: 0x{csrAddress:X3}")
        {
            CsrAddress = csrAddress;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CsrFile
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ISA v4 CSR register file — the canonical typed CSR storage and access control layer.
    /// <para>
    /// Supports all <see cref="CsrAddresses"/> groups: privilege/system, VT/SMT,
    /// capability/configuration, VMX, and performance counters.
    /// </para>
    /// <para>
    /// Access control is enforced per-register via <see cref="CsrAccessPolicy"/>.
    /// Hardware-updated registers (e.g., VtId) can only be written through
    /// <see cref="HardwareWrite"/> — software writes via CSRRW/CSRRS/CSRRC raise
    /// a privilege fault.
    /// </para>
    /// </summary>
    public sealed class CsrFile
    {
        private readonly Dictionary<ushort, ulong> _values = new();
        private readonly Dictionary<ushort, CsrAccessPolicy> _policies = new();

        /// <summary>
        /// Create a CSR file and register all canonical v4 CSR addresses with
        /// their default values and access policies.
        /// </summary>
        public CsrFile()
        {
            RegisterDefaults();
        }

        // ── Public read/write (software-facing, privilege-checked) ────────

        /// <summary>
        /// Read a CSR value with privilege checking.
        /// </summary>
        /// <param name="address">12-bit CSR address.</param>
        /// <param name="privilege">Current privilege level of the hart.</param>
        /// <returns>64-bit CSR value.</returns>
        public ulong Read(ushort address, PrivilegeLevel privilege)
        {
            if (!_policies.TryGetValue(address, out var policy))
                throw new CsrUnknownAddressException(address);

            CheckReadAccess(address, policy, privilege);

            return _values.TryGetValue(address, out var v) ? v : 0UL;
        }

        /// <summary>
        /// Write a CSR value with privilege checking.
        /// Hardware-updated registers reject software writes.
        /// </summary>
        /// <param name="address">12-bit CSR address.</param>
        /// <param name="value">64-bit value to write.</param>
        /// <param name="privilege">Current privilege level of the hart.</param>
        public void Write(ushort address, ulong value, PrivilegeLevel privilege)
        {
            if (!_policies.TryGetValue(address, out var policy))
                throw new CsrUnknownAddressException(address);

            CheckWriteAccess(address, policy, privilege);

            _values[address] = value;
        }

        // ── Hardware-only write (bypasses privilege checks) ───────────────

        /// <summary>
        /// Write a CSR value from hardware/firmware context.
        /// This bypasses privilege checks and is used for:
        /// <list type="bullet">
        ///   <item>VT dispatch: writing VtId/VtMask on context switch</item>
        ///   <item>Hardware counter updates: Cycle, InstrRet, etc.</item>
        ///   <item>Firmware reset: initialising read-only CSRs</item>
        /// </list>
        /// </summary>
        public void HardwareWrite(ushort address, ulong value)
        {
            if (!_policies.ContainsKey(address))
                throw new CsrUnknownAddressException(address);

            _values[address] = value;
        }

        // ── Direct read (no privilege check — for hardware/internal use) ──

        /// <summary>
        /// Read a CSR value without privilege checking.
        /// Used by hardware subsystems that need direct access (e.g., pipeline
        /// reading VtId for thread dispatch).
        /// </summary>
        public ulong DirectRead(ushort address)
        {
            return _values.TryGetValue(address, out var v) ? v : 0UL;
        }

        // ── Query ─────────────────────────────────────────────────────────

        /// <summary>Returns true if <paramref name="address"/> is a registered CSR.</summary>
        public bool IsRegistered(ushort address) => _policies.ContainsKey(address);


        // ── Access control helpers ────────────────────────────────────────

        private static void CheckReadAccess(ushort address, CsrAccessPolicy policy, PrivilegeLevel privilege)
        {
            // All policies allow read from Machine mode.
            if (privilege == PrivilegeLevel.Machine)
                return;

            switch (policy)
            {
                case CsrAccessPolicy.ReadOnly:
                case CsrAccessPolicy.HardwareUpdated:
                case CsrAccessPolicy.SupervisorReadWrite:
                    // Readable from any mode (S and U can read).
                    return;

                case CsrAccessPolicy.MachineReadWrite:
                    // M-mode only — U-mode and S-mode reads are denied.
                    if (privilege < PrivilegeLevel.Machine)
                        throw new CsrPrivilegeFaultException(address, PrivilegeLevel.Machine, privilege, isWrite: false);
                    return;
            }
        }

        private static void CheckWriteAccess(ushort address, CsrAccessPolicy policy, PrivilegeLevel privilege)
        {
            switch (policy)
            {
                case CsrAccessPolicy.ReadOnly:
                    throw new CsrPrivilegeFaultException(address, PrivilegeLevel.Machine, privilege, isWrite: true);

                case CsrAccessPolicy.HardwareUpdated:
                    // Software writes are forbidden — must use HardwareWrite.
                    throw new CsrPrivilegeFaultException(address, PrivilegeLevel.Machine, privilege, isWrite: true);

                case CsrAccessPolicy.MachineReadWrite:
                    if (privilege < PrivilegeLevel.Machine)
                        throw new CsrPrivilegeFaultException(address, PrivilegeLevel.Machine, privilege, isWrite: true);
                    return;

                case CsrAccessPolicy.SupervisorReadWrite:
                    if (privilege < PrivilegeLevel.Supervisor)
                        throw new CsrPrivilegeFaultException(address, PrivilegeLevel.Supervisor, privilege, isWrite: true);
                    return;
            }
        }

        // ── Default registration ──────────────────────────────────────────

        private void RegisterDefaults()
        {
            // ── Machine-mode privilege/system (M-mode R/W) ────────────────
            Register(CsrAddresses.Mstatus, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Misa, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.Mie, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mtvec, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mscratch, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mepc, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mcause, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mtval, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.Mip, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.MpowerState, CsrAccessPolicy.MachineReadWrite, (ulong)Processor.CPU_Core.CorePowerState.C0_Active);
            Register(CsrAddresses.MperfLevel, CsrAccessPolicy.MachineReadWrite, (ulong)Processor.CPU_Core.CorePowerState.P0_MaxPerformance);

            // ── Supervisor-mode (S-mode R/W) ──────────────────────────────
            Register(CsrAddresses.Sstatus, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Sie, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Stvec, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Sscratch, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Sepc, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Scause, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Stval, CsrAccessPolicy.SupervisorReadWrite);
            Register(CsrAddresses.Sip, CsrAccessPolicy.SupervisorReadWrite);

            // ── VT / SMT (hardware-updated, read-only for software) ───────
            Register(CsrAddresses.VtId, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.VtMask, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.VtStatus, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.VtCause, CsrAccessPolicy.HardwareUpdated);

            // ── Capability / Configuration (read-only discovery) ──────────
            Register(CsrAddresses.IsaCaps, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.LaneCaps, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.BundleCaps, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.SafetyCaps, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.MemCaps, CsrAccessPolicy.ReadOnly);

            // ── VMX (M-mode R/W — VmxEnable also updated by VMXON/VMXOFF) ─
            Register(CsrAddresses.VmxEnable, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.VmxCaps, CsrAccessPolicy.ReadOnly);
            Register(CsrAddresses.VmxControl, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.VmxExitReason, CsrAccessPolicy.MachineReadWrite);
            Register(CsrAddresses.VmxExitQual, CsrAccessPolicy.MachineReadWrite);

            // ── Performance counters (read-only from software, updated by HW)
            Register(CsrAddresses.Cycle, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.BundleRet, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.InstrRet, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.VmExitCnt, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.BarrierCnt, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.StealCnt, CsrAccessPolicy.HardwareUpdated);
            Register(CsrAddresses.ReplayCnt, CsrAccessPolicy.HardwareUpdated);
        }

        private void Register(ushort address, CsrAccessPolicy policy, ulong defaultValue = 0UL)
        {
            _policies[address] = policy;
            _values[address] = defaultValue;
        }
    }
}
