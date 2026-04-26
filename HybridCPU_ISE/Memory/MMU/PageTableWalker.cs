using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Hardware Page Table Walker (PTW) — autonomous FSM that walks
    /// 2-level page tables on TLB miss, eliminating costly software trap handlers.
    ///
    /// <para><b>Microarchitectural contract:</b></para>
    /// <list type="bullet">
    ///   <item>On TLB miss: stalls requesting thread, starts walk.</item>
    ///   <item>Walk uses dedicated DMA port (no contention with data DMA).</item>
    ///   <item>On success: fills TLB, unstalls thread, replays access.</item>
    ///   <item>On fault: triggers page fault exception on thread.</item>
    /// </list>
    ///
    /// <para><b>HLS characteristics:</b></para>
    /// <list type="bullet">
    ///   <item>7-state FSM → 3-bit state register → 1 LUT chain per transition.</item>
    ///   <item>Dedicated AXI4-Lite master port (address + 64-bit data), not shared with data DMA.</item>
    ///   <item>Walk latency: 2 × <see cref="MEMORY_READ_LATENCY"/> = 8 cycles worst-case.</item>
    ///   <item>Pipelined throughput: 1 walk / 8 cycles.</item>
    ///   <item>Queue depth: <see cref="MAX_PENDING_WALKS"/> entries (expandable for MSHR-like logic).</item>
    ///   <item>Zero heap allocation — <see cref="InlineArray"/>-backed fixed storage.</item>
    ///   <item>Combinatorial depth: 2 levels (state decode + next-state mux).</item>
    ///   <item>Estimated area: ~120 LUT + 1 BRAM (for pending queue, if expanded).</item>
    /// </list>
    /// </summary>
    public struct PageTableWalker
    {
        // ── FSM states ──────────────────────────────────────────────

        /// <summary>
        /// PTW FSM states (3-bit encoding in hardware).
        /// </summary>
        public enum PtwState : byte
        {
            /// <summary>Idle — no walk in progress.</summary>
            Idle = 0,

            /// <summary>Reading Page Directory Entry (level 1) — issuing DMA read.</summary>
            ReadPDE = 1,

            /// <summary>Waiting for PDE memory response (MEMORY_READ_LATENCY cycles).</summary>
            WaitPDE = 2,

            /// <summary>Reading Page Table Entry (level 2) — issuing DMA read.</summary>
            ReadPTE = 3,

            /// <summary>Waiting for PTE memory response (MEMORY_READ_LATENCY cycles).</summary>
            WaitPTE = 4,

            /// <summary>Walk complete — result ready for TLB fill.</summary>
            Complete = 5,

            /// <summary>Fault — page not present or permission denied.</summary>
            Fault = 6
        }

        // ── Walk request descriptor ─────────────────────────────────

        /// <summary>
        /// Walk request queued by IOMMU on TLB miss.
        /// HLS: 168 bits (VA 64 + ThreadId 32 + DomainId 32 + IsWrite 1 + Valid 1 + pad).
        /// </summary>
        public struct WalkRequest
        {
            /// <summary>Virtual address that caused TLB miss.</summary>
            public ulong VirtualAddress;

            /// <summary>Hardware thread ID (0–15) that issued the access.</summary>
            public int ThreadId;

            /// <summary>Memory domain / ASID for page table root lookup.</summary>
            public int DomainId;

            /// <summary>True if the original access was a write (for permission check).</summary>
            public bool IsWrite;

            /// <summary>Valid bit — entry is active in the queue.</summary>
            public bool Valid;
        }

        // ── HLS-compatible pending queue (InlineArray, fixed 4 entries) ─

        /// <summary>Max pending walk requests (MSHR-like, expandable).</summary>
        private const int MAX_PENDING_WALKS = 4;

        [InlineArray(MAX_PENDING_WALKS)]
        private struct PendingWalkArray
        {
            private WalkRequest _element0;
        }

        // ── Timing constants ────────────────────────────────────────

        /// <summary>
        /// Cycles to read one level of page table via dedicated DMA port.
        /// Matches AXI4-Lite single-beat latency (header + data + response).
        /// </summary>
        private const int MEMORY_READ_LATENCY = 4;

        // ── Instance state ──────────────────────────────────────────

        private PtwState _state;
        private WalkRequest _currentRequest;

        // Intermediate walk state
        private ulong _pdeAddress;
        private ulong _pdeValue;
        private ulong _pteAddress;
        private int _waitCycles;

        // Pending request queue
        private PendingWalkArray _pendingQueue;
        private int _pendingHead;
        private int _pendingCount;

        // ── Statistics ──────────────────────────────────────────────

        /// <summary>Number of walks completed successfully (TLB filled).</summary>
        public ulong WalksCompleted { get; private set; }

        /// <summary>Number of walks that ended in page fault.</summary>
        public ulong WalksFaulted { get; private set; }

        /// <summary>Total cycles spent in non-Idle state (walk overhead).</summary>
        public ulong TotalWalkCycles { get; private set; }

        // ── Public properties ───────────────────────────────────────

        /// <summary>True if PTW is busy with a walk.</summary>
        public readonly bool IsBusy => _state != PtwState.Idle;

        /// <summary>Current FSM state (for diagnostics / TraceSink).</summary>
        public readonly PtwState CurrentState => _state;

        /// <summary>Thread ID currently stalled by PTW, or -1 if idle.</summary>
        public readonly int StalledThreadId => IsBusy ? _currentRequest.ThreadId : -1;

        /// <summary>Number of pending requests waiting in queue.</summary>
        public readonly int PendingCount => _pendingCount;

        // ── Walk result (returned by AdvanceCycle) ──────────────────

        /// <summary>
        /// Result of a single PTW FSM cycle advancement.
        /// </summary>
        public readonly struct WalkResult
        {
            /// <summary>True if walk finished (success or fault) this cycle.</summary>
            public readonly bool Done;

            /// <summary>True if walk ended in page fault (only valid when Done=true).</summary>
            public readonly bool Faulted;

            /// <summary>Thread ID that requested this walk (-1 if no event).</summary>
            public readonly int ThreadId;

            /// <summary>Translated physical address (only valid when Done=true and Faulted=false).</summary>
            public readonly ulong PhysicalAddress;

            /// <summary>Permission bits from PTE (bit 1=Read, bit 2=Write).</summary>
            public readonly byte Permissions;

            /// <summary>Virtual address that was walked (for TLB insertion by caller).</summary>
            public readonly ulong VirtualAddress;

            /// <summary>Domain ID for TLB insertion.</summary>
            public readonly int DomainId;

            internal WalkResult(bool done, bool faulted, int threadId,
                                ulong physAddr, byte perms,
                                ulong virtualAddress, int domainId)
            {
                Done = done;
                Faulted = faulted;
                ThreadId = threadId;
                PhysicalAddress = physAddr;
                Permissions = perms;
                VirtualAddress = virtualAddress;
                DomainId = domainId;
            }

            /// <summary>No-event result (idle cycle).</summary>
            public static readonly WalkResult NoEvent = new(false, false, -1, 0, 0, 0, 0);
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Submit walk request (called by IOMMU on TLB miss).
        /// If PTW is idle, starts immediately. Otherwise queues the request.
        /// </summary>
        /// <param name="virtualAddress">Virtual address that missed TLB.</param>
        /// <param name="threadId">Requesting hardware thread (0–15).</param>
        /// <param name="domainId">Memory domain / ASID for page table root.</param>
        /// <param name="isWrite">True if the original access was a write.</param>
        /// <returns>True if accepted (started or queued), false if queue full.</returns>
        public bool SubmitWalk(ulong virtualAddress, int threadId, int domainId, bool isWrite)
        {
            if (_state == PtwState.Idle)
            {
                _currentRequest = new WalkRequest
                {
                    VirtualAddress = virtualAddress,
                    ThreadId = threadId,
                    DomainId = domainId,
                    IsWrite = isWrite,
                    Valid = true
                };
                BeginWalk();
                return true;
            }

            // Enqueue if space available
            if (_pendingCount < MAX_PENDING_WALKS)
            {
                int tail = (_pendingHead + _pendingCount) % MAX_PENDING_WALKS;
                _pendingQueue[tail] = new WalkRequest
                {
                    VirtualAddress = virtualAddress,
                    ThreadId = threadId,
                    DomainId = domainId,
                    IsWrite = isWrite,
                    Valid = true
                };
                _pendingCount++;
                return true;
            }

            return false; // Queue full — caller must retry next cycle
        }

        /// <summary>
        /// Advance PTW FSM by one clock cycle.
        /// Called from MemorySubsystem.AdvanceCycles() or IOMMU tick.
        ///
        /// <para><b>HLS:</b> single always_ff block, 3-bit state register,
        /// combinatorial next-state logic depth ≤ 2.</para>
        /// </summary>
        /// <param name="pageDirectory">
        /// Reference to IOMMU page directory for synchronous page table reads.
        /// In hardware this would be the dedicated PTW DMA port response.
        /// </param>
        /// <returns>Walk result for this cycle.</returns>
        public WalkResult AdvanceCycle(ulong[][]? pageDirectory)
        {
            if (_state != PtwState.Idle)
                TotalWalkCycles++;

            switch (_state)
            {
                case PtwState.Idle:
                    return TryDequeue();

                case PtwState.ReadPDE:
                    // Issue DMA read for PDE (dedicated PTW port)
                    _state = PtwState.WaitPDE;
                    _waitCycles = 0;
                    return WalkResult.NoEvent;

                case PtwState.WaitPDE:
                    _waitCycles++;
                    if (_waitCycles >= MEMORY_READ_LATENCY)
                    {
                        // Read PDE from page directory
                        uint dirIndex = (uint)((_currentRequest.VirtualAddress >> 22) & 0x3FF);

                        if (pageDirectory == null || pageDirectory[dirIndex] == null)
                        {
                            // PDE not present — fault
                            _state = PtwState.Fault;
                            return WalkResult.NoEvent;
                        }

                        // PDE is the page table base for this directory entry.
                        // In our 2-level model the page directory array entry at dirIndex
                        // IS the page table array — we move to PTE read.
                        _state = PtwState.ReadPTE;
                        _waitCycles = 0;
                    }
                    return WalkResult.NoEvent;

                case PtwState.ReadPTE:
                    // Issue DMA read for PTE (second level)
                    _state = PtwState.WaitPTE;
                    _waitCycles = 0;
                    return WalkResult.NoEvent;

                case PtwState.WaitPTE:
                    _waitCycles++;
                    if (_waitCycles >= MEMORY_READ_LATENCY)
                    {
                        // Read PTE from page table
                        uint dirIdx = (uint)((_currentRequest.VirtualAddress >> 22) & 0x3FF);
                        uint tblIdx = (uint)((_currentRequest.VirtualAddress >> 12) & 0x3FF);

                        if (pageDirectory == null ||
                            pageDirectory[dirIdx] == null)
                        {
                            _state = PtwState.Fault;
                            return WalkResult.NoEvent;
                        }

                        ulong pte = pageDirectory[dirIdx][tblIdx];

                        // Check present bit
                        if ((pte & 0x1UL) == 0)
                        {
                            _state = PtwState.Fault;
                            return WalkResult.NoEvent;
                        }

                        // Keep PTW permission truth aligned with the synchronous IOMMU walk.
                        if (_currentRequest.IsWrite && (pte & 0x4UL) == 0)
                        {
                            _state = PtwState.Fault;
                            return WalkResult.NoEvent;
                        }

                        if (!_currentRequest.IsWrite && (pte & 0x2UL) == 0)
                        {
                            _state = PtwState.Fault;
                            return WalkResult.NoEvent;
                        }

                        // Successful — extract physical address and permissions
                        ulong ppn = pte & 0xFFFFFFFFFFFFF000UL;
                        ulong offset = _currentRequest.VirtualAddress & 0xFFF;
                        ulong physAddr = ppn | offset;
                        byte perms = (byte)((pte >> 1) & 0x06);

                        WalksCompleted++;
                        _state = PtwState.Idle;

                        var result = new WalkResult(
                            done: true,
                            faulted: false,
                            threadId: _currentRequest.ThreadId,
                            physAddr: physAddr,
                            perms: perms,
                            virtualAddress: _currentRequest.VirtualAddress,
                            domainId: _currentRequest.DomainId);

                        // Immediately try to start next pending walk
                        TryDequeue();

                        return result;
                    }
                    return WalkResult.NoEvent;

                case PtwState.Complete:
                    // Should not normally reach here (Complete is handled inline in WaitPTE),
                    // but included for FSM completeness.
                    _state = PtwState.Idle;
                    return WalkResult.NoEvent;

                case PtwState.Fault:
                {
                    WalksFaulted++;
                    int faultedThread = _currentRequest.ThreadId;
                    _state = PtwState.Idle;

                    var result = new WalkResult(
                        done: true,
                        faulted: true,
                        threadId: faultedThread,
                        physAddr: 0,
                        perms: 0,
                        virtualAddress: _currentRequest.VirtualAddress,
                        domainId: _currentRequest.DomainId);

                    // Try to start next pending walk
                    TryDequeue();

                    return result;
                }
            }

            return WalkResult.NoEvent;
        }

        /// <summary>
        /// Reset PTW state (on IOMMU init or global TLB flush).
        /// </summary>
        public void Reset()
        {
            _state = PtwState.Idle;
            _currentRequest = default;
            _pdeAddress = 0;
            _pdeValue = 0;
            _pteAddress = 0;
            _waitCycles = 0;
            _pendingHead = 0;
            _pendingCount = 0;
            _pendingQueue = default;
            WalksCompleted = 0;
            WalksFaulted = 0;
            TotalWalkCycles = 0;
        }

        // ── Internal helpers ────────────────────────────────────────

        /// <summary>
        /// Begin a walk for the current request.
        /// Calculates PDE address from domain-indexed page table root.
        /// </summary>
        private void BeginWalk()
        {
            _state = PtwState.ReadPDE;
            _waitCycles = 0;
        }

        /// <summary>
        /// Try to dequeue next pending request and start walk.
        /// Returns NoEvent if nothing pending.
        /// HLS: single-cycle dequeue with head pointer increment.
        /// </summary>
        private WalkResult TryDequeue()
        {
            if (_pendingCount > 0 && _state == PtwState.Idle)
            {
                _currentRequest = _pendingQueue[_pendingHead];
                _pendingQueue[_pendingHead].Valid = false;
                _pendingHead = (_pendingHead + 1) % MAX_PENDING_WALKS;
                _pendingCount--;
                BeginWalk();
            }

            return WalkResult.NoEvent;
        }
    }
}
