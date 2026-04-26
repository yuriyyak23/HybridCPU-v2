using System;
using System.IO;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Multi-bank memory area implementation for HybridCPU.
    /// Provides parallel memory access through multiple independent memory banks.
    ///
    /// Key features:
    /// - Multiple independent memory banks for parallel access
    /// - Bank interleaving based on address
    /// - Simulates parallel bandwidth improvement
    /// - Compatible with existing IOMMU infrastructure
    /// - Phase 2: Bank conflict detection with cycle-accurate timing
    /// - Phase 2: Port management with switching penalties
    /// - Phase 2: Row buffer simulation (hit/miss latencies)
    ///
    /// Architecture:
    /// - Each bank is an independent MemoryStream
    /// - Address mapping: bank = (address / bankSize) % bankCount
    /// - Offset within bank: address % bankSize
    ///
    /// Design goals (ref2.md):
    /// - Enable concurrent burst transactions to different banks
    /// - Reduce memory access contention
    /// - Support streaming operations with high bandwidth
    /// </summary>
    public class MultiBankMemoryArea : Processor.MainMemoryArea
    {
        /// <summary>
        /// Bank state tracking for cycle-accurate timing simulation (Phase 2)
        /// </summary>
        private struct BankState
        {
            public bool Busy;                    // Is bank currently busy?
            public long BusyUntilCycle;         // Cycle when bank becomes available
            public int QueueDepth;              // Number of pending requests
            public ulong LastAccessAddress;     // Last accessed address for row buffer
            public int PortId;                  // Which port is using this bank (-1 = none)
        }

        private readonly MemoryStream[] _banks;
        private readonly int _bankCount;
        private readonly ulong _bankSize; // Size of each bank in bytes
        private readonly ulong _totalSize; // Total memory size
        private readonly BankState[] _bankStates;  // Phase 2: Per-bank state tracking
        private readonly object[] _bankLocks;      // Phase 2: Per-bank locks for thread safety

        /// <summary>
        /// Initialize multi-bank memory area.
        /// </summary>
        /// <param name="bankCount">Number of memory banks (typically 2, 4, or 8)</param>
        /// <param name="bankSize">Size of each bank in bytes (e.g., 64MB = 0x4000000UL)</param>
        public MultiBankMemoryArea(int bankCount, ulong bankSize)
        {
            if (bankCount <= 0 || bankCount > 16)
                throw new ArgumentOutOfRangeException(nameof(bankCount), "Bank count must be between 1 and 16");

            if (bankSize == 0 || bankSize > (ulong)int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(bankSize), "Bank size must be > 0 and <= int.MaxValue");

            _bankCount = bankCount;
            _bankSize = bankSize;
            _totalSize = (ulong)_bankCount * _bankSize;
            _banks = new MemoryStream[_bankCount];
            _bankStates = new BankState[_bankCount];
            _bankLocks = new object[_bankCount];
            _bankDomainCapabilities = new ulong[_bankCount];

            // Initialize all banks and their states
            for (int i = 0; i < _bankCount; i++)
            {
                _banks[i] = new MemoryStream(new byte[bankSize]);
                _bankLocks[i] = new object();
                _bankStates[i] = new BankState
                {
                    Busy = false,
                    BusyUntilCycle = 0,
                    QueueDepth = 0,
                    LastAccessAddress = 0,
                    PortId = -1  // No port assigned
                };
            }
        }

        /// <summary>
        /// Get the bank and offset for a given physical address.
        /// Bank selection uses interleaving for better distribution.
        /// </summary>
        /// <param name="physicalAddress">Physical address to map</param>
        /// <returns>Bank index and offset within that bank</returns>
        private (MemoryStream bank, ulong offset) GetBank(ulong physicalAddress)
        {
            // Bank interleaving: address is mapped to banks cyclically
            // This ensures sequential addresses are distributed across banks
            int bankIndex = (int)((physicalAddress / _bankSize) % (ulong)_bankCount);
            ulong offset = physicalAddress % _bankSize;
            return (_banks[bankIndex], offset);
        }

        /// <summary>
        /// Override Length to return total memory size across all banks.
        /// </summary>
        public override long Length
        {
            get
            {
                return (long)_totalSize;
            }
        }

        /// <summary>
        /// Override Capacity to return total capacity across all banks.
        /// </summary>
        public override int Capacity
        {
            get
            {
                // Return aggregate capacity
                long total = 0;
                for (int i = 0; i < _bankCount; i++)
                {
                    total += _banks[i].Capacity;
                }
                return (int)Math.Min(total, int.MaxValue);
            }
            set
            {
                // Distribute capacity across banks
                int perBank = value / _bankCount;
                for (int i = 0; i < _bankCount; i++)
                {
                    _banks[i].Capacity = perBank;
                }
            }
        }

        /// <summary>
        /// Silent Squash counter: incremented when a domain-checked access is rejected (req.md §4).
        /// The operation is silently dropped (no interrupt, no pipeline stall).
        /// </summary>
        public long SilentSquashCount { get; private set; }

        [ThreadStatic]
        private static bool _lastAccessSilentlySquashed;

        /// <summary>
        /// Override Read to access appropriate memory bank.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");

            // Check if this is physical context (direct bank access)
            if (_isPhysicalAccessContext)
            {
                ulong physAddr = (ulong)Position;

                // Silent Squash: check domain access before physical read (req.md §4)
                if (_currentDomainTag != 0)
                {
                    int bankId = (int)((physAddr / _bankSize) % (ulong)_bankCount);
                    if (!CheckBankDomainAccess(bankId, _currentDomainTag))
                    {
                        SilentSquashCount++;
                        _lastAccessSilentlySquashed = true;
                        return 0; // Silent squash — return zero bytes read
                    }
                }

                var (bank, bankOffset) = GetBank(physAddr);

                lock (bank)
                {
                    bank.Position = (long)bankOffset;
                    return bank.Read(buffer, offset, count);
                }
            }
            else
            {
                // Virtual context - use base IOMMU-aware read
                return base.Read(buffer, offset, count);
            }
        }

        /// <summary>
        /// Override Write to access appropriate memory bank.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");

            // Check if this is physical context (direct bank access)
            if (_isPhysicalAccessContext)
            {
                ulong physAddr = (ulong)Position;

                // Silent Squash: check domain access before physical write (req.md §4)
                if (_currentDomainTag != 0)
                {
                    int bankId = (int)((physAddr / _bankSize) % (ulong)_bankCount);
                    if (!CheckBankDomainAccess(bankId, _currentDomainTag))
                    {
                        SilentSquashCount++;
                        _lastAccessSilentlySquashed = true;
                        return; // Silent squash — discard write silently
                    }
                }

                var (bank, bankOffset) = GetBank(physAddr);

                lock (bank)
                {
                    bank.Position = (long)bankOffset;
                    bank.Write(buffer, offset, count);
                }
            }
            else
            {
                // Virtual context - use base IOMMU-aware write
                base.Write(buffer, offset, count);
            }
        }

        /// <summary>
        /// Current domain tag for the active memory access context (req.md §4).
        /// Set before physical read/write by the pipeline/memory subsystem.
        /// 0 = trusted kernel / unrestricted access.
        /// </summary>
        [ThreadStatic]
        private static ulong _currentDomainTag;

        /// <summary>
        /// Set the domain tag for subsequent physical memory accesses.
        /// Called by the pipeline before executing a stolen MicroOp's memory access.
        /// </summary>
        /// <param name="domainTag">Domain tag from MicroOp.DomainTag (0 = unrestricted)</param>
        public static void SetAccessDomainTag(ulong domainTag)
        {
            _currentDomainTag = domainTag;
        }

        public static void ResetLastAccessSilentSquashFlag()
        {
            _lastAccessSilentlySquashed = false;
        }

        public static bool ConsumeLastAccessSilentSquashFlag()
        {
            bool result = _lastAccessSilentlySquashed;
            _lastAccessSilentlySquashed = false;
            return result;
        }

        /// <summary>
        /// Override SetLength to adjust total memory size.
        /// </summary>
        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Length must be non-negative");

            // Distribute new length across banks
            long perBank = value / _bankCount;

            for (int i = 0; i < _bankCount; i++)
            {
                lock (_banks[i])
                {
                    _banks[i].SetLength(perBank);
                }
            }
        }

        /// <summary>
        /// Override Flush to flush all banks.
        /// </summary>
        public override void Flush()
        {
            for (int i = 0; i < _bankCount; i++)
            {
                lock (_banks[i])
                {
                    _banks[i].Flush();
                }
            }
        }

        /// <summary>
        /// Get statistics about bank usage (for debugging/profiling).
        /// </summary>
        public (int bankCount, ulong bankSize, ulong totalSize) GetBankInfo()
        {
            return (_bankCount, _bankSize, _totalSize);
        }

        /// <summary>
        /// Get the current length of a specific bank.
        /// </summary>
        public long GetBankLength(int bankIndex)
        {
            if (bankIndex < 0 || bankIndex >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankIndex));

            lock (_banks[bankIndex])
            {
                return _banks[bankIndex].Length;
            }
        }

        #region Phase 2: Microarchitecture Fidelity

        /// <summary>
        /// Per-bank domain capability mask (req.md §2: Capability Tags).
        /// Each bank stores a bitmask of domain tags allowed to access it.
        /// A zero mask means unrestricted access (backward-compatible default).
        /// HLS: maps to a fixed-size register array (one 64-bit register per bank).
        /// </summary>
        private readonly ulong[] _bankDomainCapabilities;

        /// <summary>
        /// Assign a domain capability mask to a specific bank.
        /// Only threads whose DomainTag overlaps with the bank capability mask are allowed access.
        /// Called by OS/kernel during memory domain setup.
        /// </summary>
        /// <param name="bankId">Target bank index</param>
        /// <param name="domainMask">Bitmask of allowed domain tags (0 = unrestricted)</param>
        public void SetBankDomainCapability(int bankId, ulong domainMask)
        {
            if (bankId < 0 || bankId >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankId));

            _bankDomainCapabilities[bankId] = domainMask;
        }

        /// <summary>
        /// Check if a domain tag is allowed to access a specific bank (req.md §2).
        /// Performs a single-cycle AND gate check: (bankCapability &amp; domainTag) != 0.
        /// Returns true if access is allowed, false for silent squash.
        /// HLS: synthesizes as a single AND + compare-to-zero gate.
        /// </summary>
        /// <param name="bankId">Bank being accessed</param>
        /// <param name="domainTag">Requester's domain tag (from MicroOp.DomainTag)</param>
        /// <returns>True if access is permitted</returns>
        public bool CheckBankDomainAccess(int bankId, ulong domainTag)
        {
            if ((uint)bankId >= (uint)_bankCount)
                return false;

            ulong capability = _bankDomainCapabilities[bankId];

            // Zero capability = unrestricted (backward compat / kernel mode)
            if (capability == 0)
                return true;

            // Zero domain tag = trusted kernel — always allowed
            if (domainTag == 0)
                return true;

            // Hardware AND gate: domain must match bank capability
            return (capability & domainTag) != 0;
        }

        /// <summary>
        /// Check if a memory access to the given address will cause a bank conflict.
        /// Returns the bank ID, conflict status, and estimated stall cycles.
        /// (Phase 2: Requirement 2.1)
        /// </summary>
        public (int BankId, bool HasConflict, int StallCycles) CheckBankConflict(
            ulong address, int size, long currentCycle)
        {
            int bankId = (int)((address / _bankSize) % (ulong)_bankCount);

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                // Check if bank is busy
                if (state.Busy && currentCycle < state.BusyUntilCycle)
                {
                    int stallCycles = (int)(state.BusyUntilCycle - currentCycle);
                    // Additional stalls based on queue depth
                    stallCycles += state.QueueDepth * 2;
                    return (bankId, true, stallCycles);
                }

                return (bankId, false, 0);
            }
        }

        /// <summary>
        /// Calculate memory access latency based on address and access pattern.
        /// Includes row buffer simulation (hit = 10 cycles, miss = 30 cycles).
        /// (Phase 2: Requirement 2.1)
        /// </summary>
        public int CalculateAccessLatency(ulong address, int size, bool isSequential)
        {
            int bankId = (int)((address / _bankSize) % (ulong)_bankCount);
            const ulong RowBufferSize = 1024; // 1KB row buffer per bank

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                // Calculate row address (address / row buffer size)
                ulong rowAddress = address / RowBufferSize;
                ulong lastRowAddress = state.LastAccessAddress / RowBufferSize;

                // Row buffer hit if accessing same row
                if (rowAddress == lastRowAddress && state.LastAccessAddress != 0)
                {
                    return 10; // Row buffer hit latency
                }

                // Row buffer miss - need to load new row
                return 30; // Row buffer miss latency
            }
        }

        /// <summary>
        /// Allocate a memory port for accessing a specific bank.
        /// Returns the port ID that was allocated.
        /// (Phase 2: Requirement 2.1)
        /// </summary>
        public int AllocatePort(int bankId, long currentCycle)
        {
            if (bankId < 0 || bankId >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankId));

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                // Simple port allocation - use bank ID modulo number of ports
                // In real hardware this would be more sophisticated
                int portId = bankId % 2; // Assuming 2 ports (configurable in MemorySubsystem)

                state.PortId = portId;
                return portId;
            }
        }

        /// <summary>
        /// Release a memory port that was accessing a specific bank.
        /// (Phase 2: Requirement 2.1)
        /// </summary>
        public void ReleasePort(int portId, int bankId, long currentCycle)
        {
            if (bankId < 0 || bankId >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankId));

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                if (state.PortId == portId)
                {
                    state.PortId = -1; // Release port
                }
            }
        }

        /// <summary>
        /// Mark a bank as busy until the specified cycle.
        /// Updates last access address for row buffer simulation.
        /// (Phase 2: Internal utility)
        /// </summary>
        public void MarkBankBusy(int bankId, ulong address, long completionCycle)
        {
            if (bankId < 0 || bankId >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankId));

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                state.Busy = true;
                state.BusyUntilCycle = completionCycle;
                state.LastAccessAddress = address;
                state.QueueDepth++;
            }
        }

        /// <summary>
        /// Mark a bank as available (no longer busy).
        /// (Phase 2: Internal utility)
        /// </summary>
        public void MarkBankAvailable(int bankId, long currentCycle)
        {
            if (bankId < 0 || bankId >= _bankCount)
                throw new ArgumentOutOfRangeException(nameof(bankId));

            lock (_bankLocks[bankId])
            {
                ref var state = ref _bankStates[bankId];

                if (currentCycle >= state.BusyUntilCycle)
                {
                    state.Busy = false;
                    state.QueueDepth = Math.Max(0, state.QueueDepth - 1);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Bank arbitrator for managing per-cycle bank access conflicts.
    /// Tracks which banks are reserved in the current cycle to detect conflicts.
    /// HLS-compatible: fixed-size bool array, no dynamic allocation.
    /// (Phase 2: Memory Arbitration & Bank Conflict Detection)
    /// </summary>
    public class BankArbitrator
    {
        private readonly bool[] _busyBanks;
        private readonly int _bankCount;
        private readonly ulong _bankSize;
        private int _busyCount;

        /// <summary>
        /// Initialize bank arbitrator with bank configuration
        /// </summary>
        public BankArbitrator(int bankCount, ulong bankSize)
        {
            _bankCount = bankCount;
            _bankSize = bankSize;
            _busyBanks = new bool[bankCount];
        }

        /// <summary>
        /// Calculate which bank an address belongs to
        /// </summary>
        private int CalculateBank(ulong address)
        {
            return (int)((address / _bankSize) % (ulong)_bankCount);
        }

        /// <summary>
        /// Try to reserve a bank for the current cycle.
        /// Returns false if the bank is already busy (conflict detected).
        /// HLS: single array read + conditional write, O(1).
        /// </summary>
        public bool TryReserveBank(ulong address, out int bankId)
        {
            bankId = CalculateBank(address);
            if (_busyBanks[bankId])
                return false;

            _busyBanks[bankId] = true;
            _busyCount++;
            return true;
        }

        /// <summary>
        /// Reset arbitrator state at the end of each cycle.
        /// Must be called once per cycle to clear busy bank tracking.
        /// HLS: fixed-iteration array clear.
        /// </summary>
        public void ResetCycle()
        {
            for (int i = 0; i < _bankCount; i++)
                _busyBanks[i] = false;
            _busyCount = 0;
        }

        /// <summary>
        /// Get count of currently busy banks
        /// </summary>
        public int GetBusyBankCount()
        {
            return _busyCount;
        }
    }
}
