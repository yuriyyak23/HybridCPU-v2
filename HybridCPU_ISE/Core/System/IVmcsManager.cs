// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — VMCS Management Interface
// Phase 09: VMX Subsystem
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// VMCS (VM Control Structure) management interface.
    /// <para>
    /// A VMCS stores the guest and host state for a single VM execution context.
    /// The VMX instruction plane (<c>VMREAD</c>, <c>VMWRITE</c>, <c>VMCLEAR</c>,
    /// <c>VMPTRLD</c>, <c>VMLAUNCH</c>, <c>VMRESUME</c>) operates on the
    /// currently-active VMCS through this interface.
    /// </para>
    /// </summary>
    public interface IVmcsManager
    {
        /// <summary>True when a VMCS has been loaded via <c>VMPTRLD</c>.</summary>
        bool HasActiveVmcs { get; }

        /// <summary>True when the active VMCS has been launched via <c>VMLAUNCH</c>.</summary>
        bool HasLaunchedVmcs { get; }

        /// <summary>
        /// Explicit VMCS pointer-load lifecycle service for <c>VMPTRLD</c>.
        /// Production VMX code should prefer this typed result surface.
        /// </summary>
        VmcsPointerResult LoadPointer(ulong vmcsPhysicalAddress);

        /// <summary>
        /// Explicit VMCS pointer-clear lifecycle service for <c>VMCLEAR</c>.
        /// Production VMX code should prefer this typed result surface.
        /// </summary>
        VmcsPointerResult ClearPointer(ulong vmcsPhysicalAddress);

        /// <summary>
        /// Reads a field from the active VMCS and returns a typed result payload.
        /// </summary>
        VmcsFieldReadResult ReadFieldValue(VmcsField field);

        /// <summary>
        /// Writes a field in the active VMCS and returns a typed result payload.
        /// </summary>
        VmcsFieldWriteResult WriteFieldValue(VmcsField field, long value);

        /// <summary>
        /// Resolves VM-entry guest context from the active VMCS. When
        /// <paramref name="markLaunchedOnSuccess"/> is <see langword="true"/>,
        /// the active VMCS launch state is committed as part of the same lifecycle step.
        /// </summary>
        VmEntryTransitionResult BeginVmEntry(bool markLaunchedOnSuccess);

        /// <summary>
        /// Saves guest state into the active VMCS and returns the host context that should
        /// be restored for the VM-exit completion path.
        /// </summary>
        VmExitTransitionResult CompleteVmExit(ICanonicalCpuState state, byte vtId, VmExitReason reason);

        /// <summary>
        /// Load a VMCS, making it the current active VMCS.
        /// Called by the <c>VMPTRLD</c> instruction.
        /// </summary>
        /// <param name="vmcsPhysicalAddress">Physical address of the VMCS region.</param>
        void Load(ulong vmcsPhysicalAddress) => LoadPointer(vmcsPhysicalAddress);

        /// <summary>
        /// Clear and invalidate a VMCS pointer, resetting its launch state.
        /// Called by the <c>VMCLEAR</c> instruction.
        /// </summary>
        /// <param name="vmcsPhysicalAddress">Physical address of the VMCS to clear.</param>
        void Clear(ulong vmcsPhysicalAddress) => ClearPointer(vmcsPhysicalAddress);

        /// <summary>
        /// Read a field from the currently-active VMCS.
        /// Called by the <c>VMREAD</c> instruction.
        /// </summary>
        long ReadField(VmcsField field) => ReadFieldValue(field).Value;

        /// <summary>
        /// Write a value to a field of the currently-active VMCS.
        /// Called by the <c>VMWRITE</c> instruction.
        /// </summary>
        void WriteField(VmcsField field, long value) => WriteFieldValue(field, value);

        /// <summary>
        /// Save the current guest CPU state (PC and SP) into the active VMCS guest
        /// state area. Called during VM-Exit before host state restoration.
        /// </summary>
        void SaveGuestState(ICanonicalCpuState state);

        /// <summary>
        /// Save the current guest CPU state (PC and SP) into the active VMCS guest
        /// state area using VT-scoped reads for <paramref name="vtId"/>.
        /// New privileged/VMX call sites should prefer this overload.
        /// </summary>
        void SaveGuestState(ICanonicalCpuState state, byte vtId) => SaveGuestState(state);

        /// <summary>
        /// Restore guest CPU state (PC and SP) from the active VMCS guest state area
        /// into the CPU state. Called during VM-Entry (VMLAUNCH / VMRESUME) to load
        /// the guest execution context.
        /// </summary>
        void RestoreGuestState(ICanonicalCpuState state);

        /// <summary>
        /// Restore guest CPU state (PC and SP) for virtual thread
        /// <paramref name="vtId"/> from the active VMCS guest state area.
        /// Write-side mutation still targets the supplied scoped adapter.
        /// </summary>
        void RestoreGuestState(ICanonicalCpuState state, byte vtId) => RestoreGuestState(state);

        /// <summary>
        /// Save the current host CPU state (PC and SP) into the active VMCS host
        /// state area. Called before VM-Entry to record the host resume address.
        /// </summary>
        void SaveHostState(ICanonicalCpuState state);

        /// <summary>
        /// Save the current host CPU state (PC and SP) into the active VMCS host
        /// state area using VT-scoped reads for <paramref name="vtId"/>.
        /// New privileged/VMX call sites should prefer this overload.
        /// </summary>
        void SaveHostState(ICanonicalCpuState state, byte vtId) => SaveHostState(state);

        /// <summary>
        /// Restore host CPU state (PC and SP) from the active VMCS host state area.
        /// Called during VM-Exit after guest state has been saved.
        /// </summary>
        void RestoreHostState(ICanonicalCpuState state);

        /// <summary>
        /// Restore host CPU state (PC and SP) for virtual thread
        /// <paramref name="vtId"/> from the active VMCS host state area.
        /// Write-side mutation still targets the supplied scoped adapter.
        /// </summary>
        void RestoreHostState(ICanonicalCpuState state, byte vtId) => RestoreHostState(state);

        /// <summary>
        /// Mark the active VMCS as launched (set by <c>VMLAUNCH</c>).
        /// A launched VMCS can be re-entered via <c>VMRESUME</c>.
        /// </summary>
        void MarkLaunched();
    }
}

