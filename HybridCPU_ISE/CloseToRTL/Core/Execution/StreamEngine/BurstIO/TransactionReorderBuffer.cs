using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Transaction Reorder Buffer for AXI4 burst requests.
    ///
    /// Allows out-of-order completion of memory transactions while
    /// maintaining in-order retirement (commit) semantics:
    /// <list type="bullet">
    ///   <item>Requests are allocated in program order.</item>
    ///   <item>Responses may arrive in any order (based on bank availability).</item>
    ///   <item>TRB retires (commits) entries strictly in allocation order.</item>
    /// </list>
    ///
    /// <para><b>Key insight:</b> the VLIW core is In-Order, but the memory
    /// subsystem can be Out-of-Order without affecting architectural state.
    /// The TRB is the boundary between In-Order (core) and OoO (memory).</para>
    ///
    /// <para><b>HLS:</b> fixed-size entry array with valid/complete bits.
    /// 16 entries × ~256 bits = 4096 bits → 2 BRAM18 or LUTRAM.
    /// <c>FindNextIssuable</c> maps to a 16-input priority encoder
    /// with bank_free masking (2 LUT levels).</para>
    /// </summary>
    public struct TransactionReorderBuffer
    {
        /// <summary>Maximum outstanding transactions (HLS: maps to BRAM).</summary>
        public const int TRB_DEPTH = 16;

        /// <summary>
        /// TRB entry: tracks one outstanding burst transaction.
        /// HLS: fixed-size struct, no heap allocation.
        /// </summary>
        public struct TrbEntry
        {
            /// <summary>Unique transaction ID (monotonically increasing).</summary>
            public ulong TransactionId;

            /// <summary>Source thread ID that initiated this transaction.</summary>
            public int ThreadId;

            /// <summary>Target address for the burst.</summary>
            public ulong Address;

            /// <summary>Size in bytes.</summary>
            public int Size;

            /// <summary>True if read, false if write.</summary>
            public bool IsRead;

            /// <summary>Which memory bank this targets (for conflict detection).</summary>
            public int TargetBank;

            /// <summary>Cycle when request was issued.</summary>
            public ulong IssueCycle;

            /// <summary>Cycle when response was received.</summary>
            public ulong CompleteCycle;

            /// <summary>Entry is occupied.</summary>
            public bool Valid;

            /// <summary>Transaction has completed (data available).</summary>
            public bool Complete;

            /// <summary>Scratch buffer slot where result data is stored.</summary>
            public int CompletionSlot;
        }

        private readonly TrbEntry[] _entries;
        private ulong _nextTransactionId;
        private int _head;  // Oldest entry (in-order commit pointer)
        private int _tail;  // Newest entry (allocation pointer)
        private int _count;

        /// <summary>Total transactions allocated through this TRB.</summary>
        public ulong TotalTransactions { get; private set; }

        /// <summary>Completions that arrived out-of-order (entry != head).</summary>
        public ulong ReorderedCompletions { get; private set; }

        /// <summary>Times <see cref="FindNextIssuable"/> found all entries bank-blocked.</summary>
        public ulong BankConflictStalls { get; private set; }

        /// <summary>
        /// Create a new TRB with fixed-depth entry storage.
        /// </summary>
        public TransactionReorderBuffer(int _ = 0)
        {
            _entries = new TrbEntry[TRB_DEPTH];
            _nextTransactionId = 0;
            _head = 0;
            _tail = 0;
            _count = 0;
            TotalTransactions = 0;
            ReorderedCompletions = 0;
            BankConflictStalls = 0;
        }

        /// <summary>
        /// Allocate a TRB entry for a new burst transaction.
        /// Returns entry index, or -1 if TRB is full (backpressure).
        /// </summary>
        /// <param name="threadId">Thread that initiated the request.</param>
        /// <param name="address">Target burst address.</param>
        /// <param name="size">Burst size in bytes.</param>
        /// <param name="isRead">True for read, false for write.</param>
        /// <param name="targetBank">Destination memory bank.</param>
        /// <param name="currentCycle">Current simulation cycle.</param>
        /// <returns>Entry index (0–15), or -1 if full.</returns>
        public int Allocate(int threadId, ulong address, int size, bool isRead,
                            int targetBank, ulong currentCycle)
        {
            if (_count >= TRB_DEPTH)
                return -1;

            int idx = _tail;
            _entries[idx] = new TrbEntry
            {
                TransactionId = _nextTransactionId++,
                ThreadId = threadId,
                Address = address,
                Size = size,
                IsRead = isRead,
                TargetBank = targetBank,
                IssueCycle = currentCycle,
                Valid = true,
                Complete = false,
                CompletionSlot = -1
            };

            _tail = (_tail + 1) % TRB_DEPTH;
            _count++;
            TotalTransactions++;

            return idx;
        }

        /// <summary>
        /// Mark a transaction as complete (called when memory response arrives).
        /// Out-of-order: any valid entry can complete regardless of position.
        /// </summary>
        /// <param name="entryIndex">TRB slot to mark complete.</param>
        /// <param name="currentCycle">Cycle when completion arrived.</param>
        /// <param name="completionSlot">Scratch buffer slot holding result data.</param>
        public void Complete(int entryIndex, ulong currentCycle, int completionSlot = 0)
        {
            if ((uint)entryIndex >= TRB_DEPTH) return;

            ref var entry = ref _entries[entryIndex];
            if (!entry.Valid) return;

            entry.Complete = true;
            entry.CompleteCycle = currentCycle;
            entry.CompletionSlot = completionSlot;

            if (entryIndex != _head)
                ReorderedCompletions++;
        }

        /// <summary>
        /// Try to retire the oldest completed transaction (in-order commit).
        /// Only the head entry can retire, preserving program order.
        /// </summary>
        /// <param name="retired">Retired entry data (valid only when returning true).</param>
        /// <returns>True if head entry was complete and retired.</returns>
        public bool TryRetire(out TrbEntry retired)
        {
            retired = default;
            if (_count == 0) return false;

            ref var head = ref _entries[_head];
            if (!head.Valid || !head.Complete) return false;

            retired = head;
            head.Valid = false;
            head.Complete = false;

            _head = (_head + 1) % TRB_DEPTH;
            _count--;
            return true;
        }

        /// <summary>
        /// Find next issuable transaction whose target bank is not busy.
        /// Scans from head towards tail; returns first entry that is
        /// valid, not yet complete, and whose bank is free.
        /// This is the "reorder" logic: skip bank-blocked entries.
        ///
        /// HLS: priority encoder over (valid &amp; !complete &amp; bank_free) bits → 2 LUT levels.
        /// </summary>
        /// <param name="bankBusy">Per-bank busy flags (indexed by bank ID).</param>
        /// <returns>Entry index of next issuable transaction, or -1 if all blocked.</returns>
        public int FindNextIssuable(ReadOnlySpan<bool> bankBusy)
        {
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head + i) % TRB_DEPTH;
                ref var entry = ref _entries[idx];

                if (!entry.Valid || entry.Complete) continue;

                if ((uint)entry.TargetBank < (uint)bankBusy.Length && bankBusy[entry.TargetBank])
                    continue;

                return idx;
            }

            if (_count > 0)
                BankConflictStalls++;

            return -1;
        }

        /// <summary>True if TRB has space for new transactions.</summary>
        public readonly bool HasSpace => _count < TRB_DEPTH;

        /// <summary>Number of currently allocated (not yet retired) entries.</summary>
        public readonly int OutstandingCount => _count;

        /// <summary>
        /// Read a TRB entry by index (for diagnostics).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TrbEntry GetEntry(int index)
        {
            if ((uint)index >= TRB_DEPTH) return default;
            return _entries[index];
        }
    }
}
