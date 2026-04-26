// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — VMCS Manager Implementation
// Phase 09: VMX Subsystem
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// In-memory VMCS (VM Control Structure) manager.
    /// <para>
    /// Manages VMCS lifecycle:
    /// <list type="bullet">
    ///   <item><c>VMPTRLD</c> — loads a VMCS, making it active</item>
    ///   <item><c>VMCLEAR</c> — clears a VMCS, resetting its launch state</item>
    ///   <item><c>VMLAUNCH</c> — marks the active VMCS as launched</item>
    ///   <item><c>VMREAD/VMWRITE</c> — read/write individual VMCS fields</item>
    ///   <item>VM-Entry — restores guest state from VMCS</item>
    ///   <item>VM-Exit — saves guest state to VMCS, restores host state</item>
    /// </list>
    /// </para>
    /// <para>
    /// Each VMCS is identified by a physical address and maintains its own
    /// field storage and launch state. Multiple VMCS regions can exist,
    /// but only one is active at a time.
    /// </para>
    /// </summary>
    public sealed class VmcsManager : IVmcsManager
    {
        // ── Per-VMCS storage ──────────────────────────────────────────────

        private sealed class VmcsRegion
        {
            public Dictionary<VmcsField, long> Fields { get; } = new();
            public bool IsLaunched { get; set; }
        }

        private const int StackPointerArchRegId = 2;

        private readonly Dictionary<ulong, VmcsRegion> _regions = new();

        private ulong _activeAddress;
        private VmcsRegion? _activeRegion;

        // ── IVmcsManager ──────────────────────────────────────────────────

        /// <inheritdoc />
        public bool HasActiveVmcs => _activeRegion is not null;

        /// <inheritdoc />
        public bool HasLaunchedVmcs => _activeRegion is { IsLaunched: true };

        /// <inheritdoc />
        public VmcsPointerResult LoadPointer(ulong vmcsPhysicalAddress)
        {
            if (!_regions.TryGetValue(vmcsPhysicalAddress, out var region))
            {
                region = new VmcsRegion();
                _regions[vmcsPhysicalAddress] = region;
            }

            _activeAddress = vmcsPhysicalAddress;
            _activeRegion = region;
            return new VmcsPointerResult(vmcsPhysicalAddress, HasActiveVmcs, HasLaunchedVmcs);
        }

        /// <inheritdoc />
        public VmcsPointerResult ClearPointer(ulong vmcsPhysicalAddress)
        {
            if (_regions.TryGetValue(vmcsPhysicalAddress, out var region))
            {
                region.IsLaunched = false;
                region.Fields.Clear();
            }

            // If clearing the currently-active VMCS, deactivate it
            if (_activeAddress == vmcsPhysicalAddress)
            {
                _activeRegion = null;
                _activeAddress = 0;
            }

            return new VmcsPointerResult(vmcsPhysicalAddress, HasActiveVmcs, HasLaunchedVmcs);
        }

        /// <inheritdoc />
        public VmcsFieldReadResult ReadFieldValue(VmcsField field)
        {
            EnsureActiveVmcs();
            long value = _activeRegion!.Fields.TryGetValue(field, out var storedValue)
                ? storedValue
                : 0L;
            return new VmcsFieldReadResult(field, value, HasActiveVmcs);
        }

        /// <inheritdoc />
        public VmcsFieldWriteResult WriteFieldValue(VmcsField field, long value)
        {
            EnsureActiveVmcs();
            _activeRegion!.Fields[field] = value;
            return new VmcsFieldWriteResult(field, value, HasActiveVmcs);
        }

        /// <inheritdoc />
        public void Load(ulong vmcsPhysicalAddress) => LoadPointer(vmcsPhysicalAddress);

        /// <inheritdoc />
        public void Clear(ulong vmcsPhysicalAddress) => ClearPointer(vmcsPhysicalAddress);

        /// <inheritdoc />
        public long ReadField(VmcsField field) => ReadFieldValue(field).Value;

        /// <inheritdoc />
        public void WriteField(VmcsField field, long value) => WriteFieldValue(field, value);

        /// <inheritdoc />
        public void SaveGuestState(ICanonicalCpuState state) => SaveGuestState(state, 0);

        /// <inheritdoc />
        public void SaveGuestState(ICanonicalCpuState state, byte vtId)
        {
            EnsureActiveVmcs();
            _activeRegion!.Fields[VmcsField.GuestPc] = unchecked((long)state.ReadPc(vtId));
            _activeRegion!.Fields[VmcsField.GuestSp] = unchecked((long)state.ReadRegister(vtId, StackPointerArchRegId));
        }

        /// <inheritdoc />
        public void RestoreGuestState(ICanonicalCpuState state) => RestoreGuestState(state, 0);

        /// <inheritdoc />
        public void RestoreGuestState(ICanonicalCpuState state, byte vtId)
        {
            VmEntryTransitionResult entry = BeginVmEntry(markLaunchedOnSuccess: false);
            if (entry.GuestPc.HasValue)
                state.WritePc(vtId, entry.GuestPc.Value);
            if (entry.GuestSp.HasValue)
                state.WriteRegister(vtId, StackPointerArchRegId, entry.GuestSp.Value);
        }

        /// <inheritdoc />
        public void SaveHostState(ICanonicalCpuState state) => SaveHostState(state, 0);

        /// <inheritdoc />
        public void SaveHostState(ICanonicalCpuState state, byte vtId)
        {
            EnsureActiveVmcs();
            _activeRegion!.Fields[VmcsField.HostPc] = unchecked((long)state.ReadPc(vtId));
            _activeRegion!.Fields[VmcsField.HostSp] = unchecked((long)state.ReadRegister(vtId, StackPointerArchRegId));
        }

        /// <inheritdoc />
        public void RestoreHostState(ICanonicalCpuState state) => RestoreHostState(state, 0);

        /// <inheritdoc />
        public void RestoreHostState(ICanonicalCpuState state, byte vtId)
        {
            EnsureActiveVmcs();
            if (TryGetOptionalContextValue(VmcsField.HostPc, out ulong hostPc))
                state.WritePc(vtId, hostPc);
            if (TryGetOptionalContextValue(VmcsField.HostSp, out ulong hostSp))
                state.WriteRegister(vtId, StackPointerArchRegId, hostSp);
        }

        /// <inheritdoc />
        public VmEntryTransitionResult BeginVmEntry(bool markLaunchedOnSuccess)
        {
            EnsureActiveVmcs();

            ulong? guestPc = TryGetOptionalContextValue(VmcsField.GuestPc, out ulong restoredGuestPc)
                ? restoredGuestPc
                : null;
            ulong? guestSp = TryGetOptionalContextValue(VmcsField.GuestSp, out ulong restoredGuestSp)
                ? restoredGuestSp
                : null;

            if (markLaunchedOnSuccess)
                _activeRegion!.IsLaunched = true;

            return new VmEntryTransitionResult(
                Success: true,
                FailureReason: VmExitReason.None,
                GuestPc: guestPc,
                GuestSp: guestSp,
                HasActiveVmcs: HasActiveVmcs,
                HasLaunchedVmcs: HasLaunchedVmcs);
        }

        /// <inheritdoc />
        public VmExitTransitionResult CompleteVmExit(ICanonicalCpuState state, byte vtId, VmExitReason reason)
        {
            EnsureActiveVmcs();

            ulong savedGuestPc = state.ReadPc(vtId);
            ulong savedGuestSp = unchecked((ulong)state.ReadRegister(vtId, StackPointerArchRegId));
            _activeRegion!.Fields[VmcsField.GuestPc] = unchecked((long)savedGuestPc);
            _activeRegion!.Fields[VmcsField.GuestSp] = unchecked((long)savedGuestSp);

            ulong? hostPc = TryGetOptionalContextValue(VmcsField.HostPc, out ulong restoredHostPc)
                ? restoredHostPc
                : null;
            ulong? hostSp = TryGetOptionalContextValue(VmcsField.HostSp, out ulong restoredHostSp)
                ? restoredHostSp
                : null;

            return new VmExitTransitionResult(
                Success: true,
                ExitReason: reason,
                SavedGuestPc: savedGuestPc,
                SavedGuestSp: savedGuestSp,
                HostPc: hostPc,
                HostSp: hostSp,
                HasActiveVmcs: HasActiveVmcs,
                HasLaunchedVmcs: HasLaunchedVmcs);
        }

        /// <inheritdoc />
        public void MarkLaunched()
        {
            EnsureActiveVmcs();
            _activeRegion!.IsLaunched = true;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void EnsureActiveVmcs()
        {
            if (_activeRegion is null)
                throw new InvalidOperationException("No active VMCS loaded (VMPTRLD not executed).");
        }

        private bool TryGetOptionalContextValue(VmcsField field, out ulong value)
        {
            EnsureActiveVmcs();
            if (_activeRegion!.Fields.TryGetValue(field, out var storedValue))
            {
                value = unchecked((ulong)storedValue);
                return true;
            }

            value = 0;
            return false;
        }
    }
}

