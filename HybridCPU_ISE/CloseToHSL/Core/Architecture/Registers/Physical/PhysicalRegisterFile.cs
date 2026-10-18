// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Physical Register File
// Blueprint Refactoring Step 1: PRF + Rename
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    /// <summary>
    /// Flat physical register file shared across all virtual threads.
    /// <para>
    /// Holds <see cref="TotalPhysRegs"/> × 64-bit physical registers.
    /// Physical register 0 is reserved as the zero register (always reads 0).
    /// Allocation/deallocation is managed by <see cref="FreeList"/>.
    /// </para>
    /// </summary>
    public sealed class PhysicalRegisterFile
    {
        /// <summary>Total number of physical registers (shared PRF, 128 entries).</summary>
        public const int TotalPhysRegs = 128;

        private readonly ulong[] _physRegs = new ulong[TotalPhysRegs];

        /// <summary>Read the value of physical register <paramref name="physId"/>.</summary>
        /// <param name="physId">Physical register index [0, <see cref="TotalPhysRegs"/>).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Read(int physId)
        {
            ValidateId(physId);
            return physId == 0 ? 0UL : _physRegs[physId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Read(PhysRegId physId) => Read(physId.Value);

        /// <summary>Write <paramref name="value"/> to physical register <paramref name="physId"/>.</summary>
        /// <param name="physId">Physical register index [0, <see cref="TotalPhysRegs"/>).</param>
        /// <param name="value">64-bit value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int physId, ulong value)
        {
            ValidateId(physId);
            if (physId == 0) return; // p0 is hardwired zero
            _physRegs[physId] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(PhysRegId physId, ulong value) => Write(physId.Value, value);

        private static void ValidateId(int physId)
        {
            if ((uint)physId >= TotalPhysRegs)
                throw new ArgumentOutOfRangeException(
                    nameof(physId),
                    physId,
                    $"Physical register index must be in [0, {TotalPhysRegs - 1}].");
        }
    }

    /// <summary>
    /// Per-VT architectural→physical register rename map.
    /// <para>
    /// Maintains one mapping table per virtual thread:
    /// <c>RenameMap[vtId][archRegId]</c> → physical register index.
    /// At initialization each architectural register <c>i</c> maps to physical register <c>i</c>
    /// (identity map), reserving the first <see cref="ArchRegs"/> physical registers.
    /// </para>
    /// </summary>
    public sealed class RenameMap
    {
        /// <summary>Number of architectural integer registers (x0–x31).</summary>
        public const int ArchRegs = 32;

        private readonly int _smtWays;
        private readonly int[,] _map;

        /// <summary>
        /// Initialise rename maps for <paramref name="smtWays"/> virtual threads.
        /// Each VT starts with the identity mapping: arch[i] → phys[i].
        /// </summary>
        /// <param name="smtWays">Number of virtual threads (typically 4).</param>
        public RenameMap(int smtWays)
        {
            if (smtWays < 1)
                throw new ArgumentOutOfRangeException(nameof(smtWays));
            _smtWays = smtWays;
            _map = new int[smtWays, ArchRegs];
            Reset();
        }

        /// <summary>
        /// Return the physical register index that architectural register
        /// <paramref name="archReg"/> maps to for virtual thread <paramref name="vtId"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Lookup(int vtId, int archReg)
        {
            ValidateVt(vtId);
            ValidateArch(archReg);
            return _map[vtId, archReg];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PhysRegId Lookup(VtId vtId, ArchRegId archReg) => PhysRegId.Create(Lookup(vtId.Value, archReg.Value));

        /// <summary>
        /// Update the rename mapping: arch register <paramref name="archReg"/> for VT
        /// <paramref name="vtId"/> now maps to physical register <paramref name="physReg"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remap(int vtId, int archReg, int physReg)
        {
            ValidateVt(vtId);
            ValidateArch(archReg);
            _map[vtId, archReg] = physReg;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remap(VtId vtId, ArchRegId archReg, PhysRegId physReg) => Remap(vtId.Value, archReg.Value, physReg.Value);

        /// <summary>Reset all VT rename maps to the identity mapping.</summary>
        public void Reset()
        {
            for (int vt = 0; vt < _smtWays; vt++)
            for (int r = 0; r < ArchRegs; r++)
                _map[vt, r] = r; // identity: arch r → phys r
        }

        private void ValidateVt(int vtId)
        {
            if ((uint)vtId >= (uint)_smtWays)
                throw new ArgumentOutOfRangeException(
                    nameof(vtId), vtId, $"VT ID must be in [0, {_smtWays - 1}].");
        }

        private static void ValidateArch(int archReg)
        {
            if ((uint)archReg >= ArchRegs)
                throw new ArgumentOutOfRangeException(
                    nameof(archReg), archReg,
                    $"Architectural register index must be in [0, {ArchRegs - 1}].");
        }
    }

    /// <summary>
    /// Commit map — a snapshot of the rename map at the commit boundary.
    /// Used to restore the precise architectural state on a branch mispredict or
    /// precise exception: the commit map represents the last architecturally retired
    /// register assignments.
    /// </summary>
    public sealed class CommitMap
    {
        private readonly int _smtWays;
        private readonly int[,] _map;

        /// <summary>Initialise a commit map for <paramref name="smtWays"/> virtual threads.</summary>
        public CommitMap(int smtWays)
        {
            if (smtWays < 1)
                throw new ArgumentOutOfRangeException(nameof(smtWays));
            _smtWays = smtWays;
            _map = new int[smtWays, RenameMap.ArchRegs];
            Reset();
        }

        /// <summary>Retrieve the committed physical register for arch register <paramref name="archReg"/> of VT <paramref name="vtId"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Lookup(int vtId, int archReg) => _map[vtId, archReg];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PhysRegId Lookup(VtId vtId, ArchRegId archReg) => PhysRegId.Create(_map[vtId.Value, archReg.Value]);

        /// <summary>Record that arch register <paramref name="archReg"/> of VT <paramref name="vtId"/> has been committed to physical <paramref name="physReg"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit(int vtId, int archReg, int physReg)
        {
            _map[vtId, archReg] = physReg;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit(VtId vtId, ArchRegId archReg, PhysRegId physReg) => Commit(vtId.Value, archReg.Value, physReg.Value);

        /// <summary>Restore the <see cref="RenameMap"/> from the commit snapshot (rollback).</summary>
        public void RestoreInto(RenameMap renameMap)
        {
            for (int vt = 0; vt < _smtWays; vt++)
            for (int r = 0; r < RenameMap.ArchRegs; r++)
                renameMap.Remap(vt, r, _map[vt, r]);
        }

        /// <summary>Reset all entries to the identity mapping.</summary>
        public void Reset()
        {
            for (int vt = 0; vt < _smtWays; vt++)
            for (int r = 0; r < RenameMap.ArchRegs; r++)
                _map[vt, r] = r;
        }
    }

    /// <summary>
    /// Free-list of available physical register indices.
    /// <para>
    /// At start-up the free list is seeded with all physical registers above the
    /// identity-mapped region (<see cref="RenameMap.ArchRegs"/> … <see cref="PhysicalRegisterFile.TotalPhysRegs"/>−1).
    /// <see cref="Allocate"/> pops a free register; <see cref="Release"/> pushes one back.
    /// Physical register 0 is never returned — it is the hardwired-zero register.
    /// </para>
    /// </summary>
    public sealed class FreeList
    {
        private readonly int[] _entries;
        private int _head;

        /// <summary>
        /// Number of registers currently available for allocation.
        /// </summary>
        public int Available => _head;

        /// <summary>Initialise the free list, seeding it with the speculative physical registers.</summary>
        public FreeList()
        {
            // Physical registers [ArchRegs .. TotalPhysRegs-1] are available for renaming.
            // Registers [0 .. ArchRegs-1] are pre-committed to the identity map.
            int freeCount = PhysicalRegisterFile.TotalPhysRegs - RenameMap.ArchRegs;
            _entries = new int[freeCount];
            for (int i = 0; i < freeCount; i++)
                _entries[i] = RenameMap.ArchRegs + i;
            _head = freeCount;
        }

        /// <summary>
        /// Allocate a free physical register.
        /// </summary>
        /// <returns>Physical register index, or -1 if the free list is exhausted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Allocate()
        {
            if (_head == 0) return -1;
            return _entries[--_head];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAllocate(out PhysRegId physReg)
        {
            int allocated = Allocate();
            if (allocated < 0)
            {
                physReg = default;
                return false;
            }

            physReg = PhysRegId.Create(allocated);
            return true;
        }

        /// <summary>
        /// Return physical register <paramref name="physReg"/> to the free list.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(int physReg)
        {
            if (physReg <= 0 || physReg >= PhysicalRegisterFile.TotalPhysRegs)
                return; // p0 is never released; out-of-range is silently ignored
            if (_head < _entries.Length)
                _entries[_head++] = physReg;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(PhysRegId physReg) => Release(physReg.Value);
    }
}
