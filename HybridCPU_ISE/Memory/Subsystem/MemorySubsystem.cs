using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Memory
{
    public partial class MemorySubsystem : IBurstBackend, IHardwareOccupancyInput
    {
        private const int HardwareQueueSaturationThreshold = 4;
        private const byte LiveWidenedMemoryIssueWidth = 2;

        #region Configuration Properties

        private int _numBanks = 8;

        /// <summary>
        /// Number of memory banks (default: 8).
        /// Resizes the live bank-state arrays so configured bank geometry
        /// cannot drift from the executing memory subsystem.
        /// </summary>
        public int NumBanks
        {
            get => _numBanks;
            set
            {
                int sanitized = Math.Max(1, value);
                if (_numBanks == sanitized)
                {
                    return;
                }

                _numBanks = sanitized;
                ReconfigureBankTopology();
            }
        }

        /// <summary>
        /// Width of each memory bank in bytes (default: 64)
        /// </summary>
        public int BankWidthBytes { get; set; } = 64;

        /// <summary>
        /// Threshold for DMA routing in bytes (default: 1024)
        /// Transfers larger than this use DMA Controller
        /// </summary>
        public int DmaThresholdBytes { get; set; } = 1024;

        /// <summary>
        /// AXI4 boundary alignment requirement (default: 4096 = 4KB)
        /// </summary>
        public int AxiBoundary { get; set; } = 4096;

        /// <summary>
        /// Maximum burst length in beats (default: 256 for AXI4)
        /// </summary>
        public int MaxBurstLength { get; set; } = 256;

        private int _numMemoryPorts = 2;

        /// <summary>
        /// Number of memory ports (default: 2).
        /// Resizes the sampled port state so directional occupancy truth
        /// follows the configured runtime surface.
        /// </summary>
        public int NumMemoryPorts
        {
            get => _numMemoryPorts;
            set
            {
                int sanitized = Math.Max(1, value);
                if (_numMemoryPorts == sanitized)
                {
                    return;
                }

                _numMemoryPorts = sanitized;
                ReconfigurePortStates();
            }
        }

        /// <summary>
        /// Additional turnaround delay in cycles when a memory port flips
        /// between read traffic and write traffic.
        /// This feeds the sampled directional occupancy truth used by the
        /// live widened lane4..5 LSU subset.
        /// </summary>
        public int ReadWriteTurnaroundPenalty { get; set; } = 1;

        /// <summary>
        /// Memory bandwidth in GB/s per bank (default: 25.6)
        /// Used for adaptive stall calculation
        /// </summary>
        public double BankBandwidthGBps { get; set; } = 25.6;

        /// <summary>
        /// Bank arbitration policy (default: RoundRobin)
        /// </summary>
        public BankArbitrationPolicy ArbitrationPolicy { get; set; } = BankArbitrationPolicy.RoundRobin;

        /// <summary>
        /// Port switching penalty in cycles (Phase 2: Requirement 2.2)
        /// Default: 3 cycles
        /// </summary>
        public int PortSwitchingPenalty { get; set; } = 3;

        /// <summary>
        /// Row buffer hit latency in cycles (Phase 2: Requirement 2.2)
        /// Default: 10 cycles
        /// </summary>
        public int RowBufferHitLatency { get; set; } = 10;

        /// <summary>
        /// Row buffer miss latency in cycles (Phase 2: Requirement 2.2)
        /// Default: 30 cycles
        /// </summary>
        public int RowBufferMissLatency { get; set; } = 30;

        /// <summary>
        /// Row buffer size in bytes (Phase 2: Requirement 2.2)
        /// Default: 1024 bytes (1KB)
        /// </summary>
        public int RowBufferSize { get; set; } = 1024;

        /// <summary>
        /// Stream Register File for hardware prefetching
        /// </summary>
        public StreamRegisterFile StreamRegisters { get; private set; }

        /// <summary>
        /// L3 Global Distributed Cache — shared SRAM buffer for HBM4 (req.md §2).
        /// Acts as intermediate cache between L2 (per-Pod) and DRAM/HBM4.
        /// </summary>
        public GlobalDistributedCache L3Cache { get; private set; }

        #endregion

        #region Performance Statistics

        /// <summary>
        /// Total number of burst operations
        /// </summary>
        public long TotalBursts { get; private set; }

        /// <summary>
        /// Total bytes transferred through this subsystem
        /// </summary>
        public long TotalBytesTransferred { get; private set; }

        /// <summary>
        /// Number of bank conflicts detected
        /// </summary>
        public long BankConflicts { get; private set; }

        /// <summary>
        /// Number of stall cycles due to conflicts
        /// </summary>
        public long StallCycles { get; private set; }

        /// <summary>
        /// Number of transfers routed through DMA
        /// </summary>
        public long DmaTransfers { get; private set; }

        /// <summary>
        /// Average burst length in bytes
        /// </summary>
        public double AverageBurstLength
        {
            get
            {
                if (TotalBursts == 0) return 0.0;
                return (double)TotalBytesTransferred / TotalBursts;
            }
        }

        /// <summary>
        /// Total wait time in cycles across all requests
        /// </summary>
        public long TotalWaitCycles { get; private set; }

        /// <summary>
        /// Average wait time per request in cycles
        /// </summary>
        public double AverageWaitCycles
        {
            get
            {
                if (TotalBursts == 0) return 0.0;
                return (double)TotalWaitCycles / TotalBursts;
            }
        }

        /// <summary>
        /// Maximum queue depth observed across all banks
        /// </summary>
        public int MaxQueueDepth { get; private set; }

        /// <summary>
        /// Current total requests queued across all banks
        /// </summary>
        public int CurrentQueuedRequests
        {
            get
            {
                int total = 0;
                if (bankQueues != null)
                {
                    foreach (var queue in bankQueues)
                    {
                        total += queue.Count;
                    }
                }
                return total;
            }
        }

        /// <summary>
        /// Get a snapshot of current per-bank queue depths for diagnostics.
        /// </summary>
        internal int[] GetBankQueueDepthsSnapshot()
        {
            var depths = new int[NumBanks];
            for (int i = 0; i < NumBanks; i++)
            {
                depths[i] = bankQueues[i].Count;
            }

            return depths;
        }

        /// <summary>
        /// Current memory subsystem cycle for diagnostics.
        /// </summary>
        internal long CurrentCycle => currentCycle;

        /// <summary>
        /// Indicates if the memory channel is overloaded (Memory Wall FSP).
        /// Used by MicroOpScheduler to suppress LSU operations and steal ALU-only slots.
        /// </summary>
        public bool IsChannelOverloaded
        {
            get
            {
                return CurrentQueuedRequests > (NumBanks * HardwareQueueSaturationThreshold);
            }
        }

        /// <summary>
        /// Returns the deterministic sampled hardware occupancy snapshot for the current cycle.
        /// The structural mask is kept as a compatibility projection, while the explicit
        /// memory budget closes the widened lane4..5 contract for already-live LSU loads/stores.
        /// The snapshot now also carries deterministic read-credit / write-credit projections
        /// derived from the live per-port read/write turnaround model.
        /// </summary>
        public HardwareOccupancySnapshot128 GetHardwareOccupancySnapshot128()
        {
            return _cachedHardwareOccupancySnapshot;
        }

        /// <summary>
        /// Generates a 128-bit safety mask representing the currently overloaded banks or channels.
        /// Compatibility projection from the deterministic occupancy snapshot.
        /// </summary>
        public Core.SafetyMask128 GetHardwareOccupancyMask128()
        {
            return GetHardwareOccupancySnapshot128().OverloadedResources;
        }

        public void AdvanceSamplingEpoch()
        {
            _hardwareSamplingEpoch++;
            _cachedHardwareOccupancySnapshot = BuildHardwareOccupancySnapshot();
        }

        private HardwareOccupancySnapshot128 BuildHardwareOccupancySnapshot()
        {
            Core.SafetyMask128 mask = Core.SafetyMask128.Zero;
            ushort memoryBankBudgetAtLeastOneMask = 0;
            ushort memoryBankBudgetAtLeastTwoMask = 0;
            ushort readBankBudgetAtLeastOneMask = 0;
            ushort readBankBudgetAtLeastTwoMask = 0;
            ushort writeBankBudgetAtLeastOneMask = 0;
            ushort writeBankBudgetAtLeastTwoMask = 0;
            int queuedReadRequests = 0;
            int queuedWriteRequests = 0;
            int readyReadPorts = CountDirectionallyReadyPorts(isRead: true);
            int readyWritePorts = CountDirectionallyReadyPorts(isRead: false);

            int trackedBanks = Math.Min(NumBanks, 16);
            for (int i = 0; i < NumBanks; i++)
            {
                int bankQueuedRequests = bankQueues[i].Count;
                int bankQueuedReadRequests = 0;
                foreach (BankRequest request in bankQueues[i])
                {
                    if (request.IsRead)
                    {
                        bankQueuedReadRequests++;
                    }
                }

                int bankQueuedWriteRequests = bankQueuedRequests - bankQueuedReadRequests;
                queuedReadRequests += bankQueuedReadRequests;
                queuedWriteRequests += bankQueuedWriteRequests;

                if (i >= trackedBanks)
                {
                    continue;
                }

                int remainingBankBudget = HardwareQueueSaturationThreshold - bankQueuedRequests;
                if (remainingBankBudget <= 0)
                {
                    mask |= Core.ResourceMaskBuilder.ForMemoryBank128(i);
                    continue;
                }

                memoryBankBudgetAtLeastOneMask |= (ushort)(1 << i);
                if (remainingBankBudget >= 2)
                {
                    memoryBankBudgetAtLeastTwoMask |= (ushort)(1 << i);
                }

                int remainingReadBankBudget = ProjectDirectionalBudget(
                    remainingBankBudget,
                    bankQueuedReadRequests,
                    HardwareQueueSaturationThreshold,
                    readyReadPorts);
                if (remainingReadBankBudget >= 1)
                {
                    readBankBudgetAtLeastOneMask |= (ushort)(1 << i);
                }
                if (remainingReadBankBudget >= 2)
                {
                    readBankBudgetAtLeastTwoMask |= (ushort)(1 << i);
                }

                int remainingWriteBankBudget = ProjectDirectionalBudget(
                    remainingBankBudget,
                    bankQueuedWriteRequests,
                    HardwareQueueSaturationThreshold,
                    readyWritePorts);
                if (remainingWriteBankBudget >= 1)
                {
                    writeBankBudgetAtLeastOneMask |= (ushort)(1 << i);
                }
                if (remainingWriteBankBudget >= 2)
                {
                    writeBankBudgetAtLeastTwoMask |= (ushort)(1 << i);
                }
            }

            int totalQueueCapacity = NumBanks * HardwareQueueSaturationThreshold;
            int remainingGlobalBudget = totalQueueCapacity - (queuedReadRequests + queuedWriteRequests);
            byte memoryIssueBudget = SaturateMemoryIssueBudget(remainingGlobalBudget);
            int remainingReadBudget = ProjectDirectionalBudget(
                remainingGlobalBudget,
                queuedReadRequests,
                totalQueueCapacity,
                readyReadPorts);
            int remainingWriteBudget = ProjectDirectionalBudget(
                remainingGlobalBudget,
                queuedWriteRequests,
                totalQueueCapacity,
                readyWritePorts);
            if (remainingGlobalBudget <= 0)
            {
                mask |= Core.ResourceMaskBuilder.ForLoad128();
                mask |= Core.ResourceMaskBuilder.ForStore128();
            }

            return new HardwareOccupancySnapshot128(
                mask,
                currentCycle,
                _hardwareSamplingEpoch,
                memoryIssueBudget,
                memoryBankBudgetAtLeastOneMask,
                memoryBankBudgetAtLeastTwoMask,
                SaturateMemoryIssueBudget(remainingReadBudget),
                readBankBudgetAtLeastOneMask,
                readBankBudgetAtLeastTwoMask,
                SaturateMemoryIssueBudget(remainingWriteBudget),
                writeBankBudgetAtLeastOneMask,
                writeBankBudgetAtLeastTwoMask);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static byte SaturateMemoryIssueBudget(int remainingGlobalBudget)
        {
            if (remainingGlobalBudget <= 0)
            {
                return 0;
            }

            if (remainingGlobalBudget == 1)
            {
                return 1;
            }

            return LiveWidenedMemoryIssueWidth;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int ProjectDirectionalBudget(
            int remainingSharedBudget,
            int queuedDirectionalRequests,
            int directionalCapacity,
            int directionallyReadyPorts)
        {
            if (remainingSharedBudget <= 0)
            {
                return 0;
            }

            if (directionallyReadyPorts <= 0)
            {
                return 0;
            }

            int remainingDirectionalHeadroom = directionalCapacity - queuedDirectionalRequests;
            if (remainingDirectionalHeadroom <= 0)
            {
                return 0;
            }

            return Math.Min(
                Math.Min(remainingSharedBudget, remainingDirectionalHeadroom),
                directionallyReadyPorts);
        }

        // Phase 3: Burst timing and efficiency metrics
        /// <summary>
        /// Number of unaligned memory accesses (Phase 3)
        /// </summary>
        public long UnalignedAccessCount { get; private set; }

        /// <summary>
        /// Total alignment penalty cycles (Phase 3)
        /// </summary>
        public long TotalAlignmentPenalty { get; private set; }

        /// <summary>
        /// Burst efficiency (data cycles / total cycles) (Phase 3)
        /// </summary>
        public double BurstEfficiency { get; private set; }

        /// <summary>
        /// Total burst timing cycles (Phase 3)
        /// </summary>
        public long TotalBurstTimingCycles { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Event arguments for burst operations
        /// </summary>
        public class BurstEventArgs : EventArgs
        {
            public ulong Address { get; set; }
            public int Length { get; set; }
            public bool IsRead { get; set; }
            public int BankId { get; set; }
            public long Timestamp { get; set; }
        }

        /// <summary>
        /// Fired when a burst operation starts
        /// </summary>
        public event EventHandler<BurstEventArgs>? BurstStarted;

        /// <summary>
        /// Fired when a burst operation completes
        /// </summary>
        public event EventHandler<BurstEventArgs>? BurstCompleted;

        #endregion

        #region Asynchronous Request Tracking

        /// <summary>
        /// Token for tracking asynchronous memory requests
        /// </summary>
        public class MemoryRequestToken
        {
            /// <summary>
            /// Unique identifier for this request
            /// </summary>
            public ulong RequestID { get; internal set; }

            /// <summary>
            /// Whether the request has completed
            /// </summary>
            public bool IsComplete { get; internal set; }

            /// <summary>
            /// Whether the completed request successfully materialized its data/effect.
            /// Only meaningful once <see cref="IsComplete"/> is true.
            /// </summary>
            public bool Succeeded { get; internal set; }

            /// <summary>
            /// Buffer containing read data (for reads) or write data (for writes)
            /// </summary>
            private byte[] buffer;

            /// <summary>
            /// Device ID that initiated this request
            /// </summary>
            public ulong DeviceID { get; internal set; }

            /// <summary>
            /// Memory address for this request
            /// </summary>
            public ulong Address { get; internal set; }

            /// <summary>
            /// Size of the request in bytes
            /// </summary>
            public int Size { get; internal set; }

            /// <summary>
            /// Whether this is a read request (false = write)
            /// </summary>
            public bool IsRead { get; internal set; }

            /// <summary>
            /// Whether the request consumed subsystem timing/resources but must not
            /// physically update memory until the core retires the corresponding store.
            /// </summary>
            public bool DefersPhysicalWriteUntilRetire { get; internal set; }

            /// <summary>
            /// Cycle when this request was enqueued
            /// </summary>
            public long EnqueueCycle { get; internal set; }

            /// <summary>
            /// Cycle when this request completed (0 if not complete)
            /// </summary>
            public long CompleteCycle { get; internal set; }

            /// <summary>
            /// Truthful failure reason for completed-but-unsuccessful requests.
            /// </summary>
            public string? FailureReason { get; internal set; }

            /// <summary>
            /// Initialize a new memory request token
            /// </summary>
            public MemoryRequestToken(
                ulong requestID,
                ulong deviceID,
                ulong address,
                int size,
                byte[] dataBuffer,
                bool isRead,
                long enqueueCycle,
                bool defersPhysicalWriteUntilRetire = false)
            {
                RequestID = requestID;
                DeviceID = deviceID;
                Address = address;
                Size = size;
                buffer = dataBuffer;
                IsRead = isRead;
                DefersPhysicalWriteUntilRetire = defersPhysicalWriteUntilRetire;
                IsComplete = false;
                Succeeded = false;
                EnqueueCycle = enqueueCycle;
                CompleteCycle = 0;
                FailureReason = null;
            }

            /// <summary>
            /// Get the buffer containing the data
            /// </summary>
            public byte[] GetBuffer()
            {
                return buffer;
            }

            /// <summary>
            /// Fail-closed guard for runtime surfaces that were previously treating
            /// completed async requests as implicit success.
            /// </summary>
            public void ThrowIfFailed(string executionSurface)
            {
                if (!IsComplete || Succeeded)
                {
                    return;
                }

                string requestKind = IsRead ? "read" : "write";
                string failureReason = string.IsNullOrWhiteSpace(FailureReason)
                    ? $"The underlying async {requestKind} request did not materialize successfully."
                    : FailureReason;

                throw new InvalidOperationException(
                    $"{executionSurface} observed completed async MemorySubsystem request {RequestID} for {requestKind} at 0x{Address:X} " +
                    $"covering {Size} byte(s), but the request did not materialize successfully. {failureReason} " +
                    "The authoritative runtime surface must fail closed instead of decoding buffered zeros, reusing stale data, or retiring a memory-visible success after unsuccessful async completion.");
            }
        }

        #endregion

        #region Internal State

        /// <summary>
        /// Port state tracking for port switching penalty simulation (Phase 2: Requirement 2.2)
        /// </summary>
        private struct PortState
        {
            public int PortId;                  // Port identifier
            public int CurrentBankId;           // Which bank is this port currently accessing (-1 = none)
            public long LastSwitchCycle;        // Cycle when port last switched banks
            public bool Busy;                   // Is port currently busy?
            public bool HasLastDirection;       // Has this port already observed traffic?
            public bool LastWasRead;            // Direction of the most recent completed request
            public long DirectionReadyCycle;    // Earliest cycle when opposite direction is admitted again
        }

        private readonly DMAController? dma;
        private readonly Processor processor;

        // Track bank occupancy for conflict detection
        private bool[] bankOccupied;
        private long[] bankLastAccessCycle;
        private long currentCycle;

        // Phase 2: Port management for switching penalties
        private PortState[] _portStates;

        // Per-bank request queues for dynamic scheduling
        private Queue<BankRequest>[] bankQueues;
        private int roundRobinIndex; // For round-robin arbitration

        // Track pending DMA operations for async completion
        private readonly System.Collections.Generic.Dictionary<byte, PendingDmaOperation> pendingDmaOps;

        // Track asynchronous memory requests
        private readonly System.Collections.Generic.Dictionary<ulong, MemoryRequestToken> pendingRequests;
        private ulong nextRequestID;
        private HardwareOccupancySnapshot128 _cachedHardwareOccupancySnapshot;
        private ulong _hardwareSamplingEpoch;

        /// <summary>
        /// Transaction Reorder Buffer: allows out-of-order burst completion
        /// while maintaining in-order retirement (Plan 08).
        /// </summary>
        public TransactionReorderBuffer TRB;

        /// <summary>
        /// Represents a memory request in the bank queue
        /// </summary>
        private struct BankRequest
        {
            public ulong RequestID;
            public ulong DeviceID;
            public ulong Address;
            public int Length;
            public bool IsRead;
            public bool DefersPhysicalWriteUntilRetire;
            public int Priority;
            public long EnqueueCycle;
            public byte[] Buffer; // For deferred execution
        }

        private struct PendingDmaOperation
        {
            public int BankId;
            public ulong Address;
            public int Length;
            public bool IsRead;
        }

        // ── Domain Inflight Burst Tracking (Q1 Review §4) ──────────

        /// <summary>
        /// Per-domain inflight burst counter.
        /// Prevents one security domain from saturating shared memory bandwidth.
        /// Key: domain tag hash slot (0–15). Value: current inflight count.
        /// HLS: 16 × 8-bit saturating counters = 128 bits.
        /// </summary>
        private readonly int[] _domainInflightBursts = new int[16];

        /// <summary>
        /// Maximum inflight bursts per domain before local stall.
        /// Default: 8 (allows 8 concurrent bursts per domain).
        /// Zero disables domain burst limiting.
        /// </summary>
        public int MaxInflightBurstsPerDomain { get; set; } = 8;

        /// <summary>Statistics: burst requests rejected due to domain inflight limit.</summary>
        public long DomainBurstStalls { get; private set; }

        #endregion

        private void ReconfigureBankTopology()
        {
            bool[] resizedBankOccupied = new bool[_numBanks];
            long[] resizedBankLastAccessCycle = new long[_numBanks];
            Queue<BankRequest>[] resizedBankQueues = new Queue<BankRequest>[_numBanks];
            Queue<BankRequest>[]? existingBankQueues = bankQueues;

            int copiedBanks = existingBankQueues == null ? 0 : Math.Min(existingBankQueues.Length, _numBanks);
            for (int i = 0; i < _numBanks; i++)
            {
                if (i < copiedBanks)
                {
                    resizedBankQueues[i] = new Queue<BankRequest>(existingBankQueues![i]);
                }
                else
                {
                    resizedBankQueues[i] = new Queue<BankRequest>();
                }
            }

            if (bankOccupied != null)
            {
                Array.Copy(bankOccupied, resizedBankOccupied, Math.Min(bankOccupied.Length, _numBanks));
            }

            if (bankLastAccessCycle != null)
            {
                Array.Copy(
                    bankLastAccessCycle,
                    resizedBankLastAccessCycle,
                    Math.Min(bankLastAccessCycle.Length, _numBanks));
            }

            bankOccupied = resizedBankOccupied;
            bankLastAccessCycle = resizedBankLastAccessCycle;
            bankQueues = resizedBankQueues;
            roundRobinIndex = _numBanks == 0 ? 0 : roundRobinIndex % _numBanks;

            RefreshHardwareSamplingAfterTopologyChange();
        }

        private void ReconfigurePortStates()
        {
            PortState[] resizedPortStates = new PortState[_numMemoryPorts];
            int copiedPorts = _portStates == null ? 0 : Math.Min(_portStates.Length, _numMemoryPorts);

            if (_portStates != null && copiedPorts > 0)
            {
                Array.Copy(_portStates, resizedPortStates, copiedPorts);
            }

            for (int i = 0; i < resizedPortStates.Length; i++)
            {
                if (i >= copiedPorts)
                {
                    resizedPortStates[i] = new PortState
                    {
                        PortId = i,
                        CurrentBankId = -1,
                        LastSwitchCycle = 0,
                        Busy = false,
                        HasLastDirection = false,
                        LastWasRead = true,
                        DirectionReadyCycle = 0
                    };
                }
                else
                {
                    resizedPortStates[i].PortId = i;
                }
            }

            _portStates = resizedPortStates;

            RefreshHardwareSamplingAfterTopologyChange();
        }

        private void RefreshHardwareSamplingAfterTopologyChange()
        {
            if (bankQueues == null || _portStates == null)
            {
                return;
            }

            AdvanceSamplingEpoch();
        }

        #region Constructor

        /// <summary>
        /// Initialize memory subsystem
        /// </summary>
        /// <param name="proc">Reference to processor for memory access</param>
        /// <param name="dmaController">Optional DMA controller for offloading</param>
        public MemorySubsystem(ref Processor proc, DMAController? dmaController = null)
        {
            processor = proc;
            dma = dmaController;

            StreamRegisters = new StreamRegisterFile();
            L3Cache = new GlobalDistributedCache();

            bankOccupied = new bool[NumBanks];
            bankLastAccessCycle = new long[NumBanks];
            bankQueues = new Queue<BankRequest>[NumBanks];
            for (int i = 0; i < NumBanks; i++)
            {
                bankQueues[i] = new Queue<BankRequest>();
            }
            roundRobinIndex = 0;
            currentCycle = 0;
            pendingDmaOps = new System.Collections.Generic.Dictionary<byte, PendingDmaOperation>();
            pendingRequests = new System.Collections.Generic.Dictionary<ulong, MemoryRequestToken>();
            nextRequestID = 1;

            // Plan 08: Initialize Transaction Reorder Buffer
            TRB = new TransactionReorderBuffer(0);

            // Phase 2: Initialize port states
            _portStates = new PortState[NumMemoryPorts];
            for (int i = 0; i < NumMemoryPorts; i++)
            {
                _portStates[i] = new PortState
                {
                    PortId = i,
                    CurrentBankId = -1,
                    LastSwitchCycle = 0,
                    Busy = false,
                    HasLastDirection = false,
                    LastWasRead = true,
                    DirectionReadyCycle = 0
                };
            }

            // Subscribe to DMA completion events
            if (dma != null)
            {
                dma.TransferCompleted += OnDmaTransferCompleted;
            }

            AdvanceSamplingEpoch();

            // IOMMU is already initialized in Processor constructor
            // We just need to reference the static class
        }

        #endregion

        #region IBurstBackend Implementation

        /// <summary>
        /// Read data from memory with burst optimization and conflict detection
        /// </summary>
        public bool Read(ulong deviceID, ulong address, Span<byte> buffer)
        {
            if (buffer.Length == 0) return true;

            int length = buffer.Length;
            int bankId = ComputeBankId(address);

            // Fire BurstStarted event
            OnBurstStarted(address, length, true, bankId);

            // Check for bank conflict with adaptive stall calculation
            if (bankOccupied[bankId])
            {
                BankConflicts++;

                // Calculate adaptive stall based on queue depth and transfer size
                int queueDepth = bankQueues[bankId].Count;
                int stallCycles = CalculateAdaptiveStall(queueDepth, length);
                StallCycles += stallCycles;
                currentCycle += stallCycles;

                // Update max queue depth metric
                if (queueDepth > MaxQueueDepth)
                {
                    MaxQueueDepth = queueDepth;
                }
            }

            // Mark bank as occupied
            bankOccupied[bankId] = true;
            bankLastAccessCycle[bankId] = currentCycle;

            // Route through DMA if above threshold and DMA is available
            if (length >= DmaThresholdBytes && dma != null)
            {
                DmaTransfers++;
                bool result = ReadViaDMA(deviceID, address, buffer);

                // Release bank
                bankOccupied[bankId] = false;

                // Update statistics
                if (result)
                {
                    TotalBursts++;
                    TotalBytesTransferred += length;
                }

                // Fire BurstCompleted event
                OnBurstCompleted(address, length, true, bankId);

                return result;
            }

            // Use IOMMU for direct access
            bool success = IOMMU.ReadBurst(deviceID, address, buffer);

            // Release bank
            bankOccupied[bankId] = false;

            // Update statistics
            if (success)
            {
                TotalBursts++;
                TotalBytesTransferred += length;
            }

            // Simulate memory access latency (4 cycles base + length dependent)
            int accessCycles = 4 + (length / BankWidthBytes);
            currentCycle += accessCycles;

            // Fire BurstCompleted event
            OnBurstCompleted(address, length, true, bankId);

            return success;
        }

        /// <summary>
        /// Write data to memory with burst optimization and conflict detection
        /// </summary>
        public bool Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0) return true;

            int length = buffer.Length;
            int bankId = ComputeBankId(address);

            // Fire BurstStarted event
            OnBurstStarted(address, length, false, bankId);

            // Check for bank conflict with adaptive stall calculation
            if (bankOccupied[bankId])
            {
                BankConflicts++;

                // Calculate adaptive stall based on queue depth and transfer size
                int queueDepth = bankQueues[bankId].Count;
                int stallCycles = CalculateAdaptiveStall(queueDepth, length);
                StallCycles += stallCycles;
                currentCycle += stallCycles;

                // Update max queue depth metric
                if (queueDepth > MaxQueueDepth)
                {
                    MaxQueueDepth = queueDepth;
                }
            }

            // Mark bank as occupied
            bankOccupied[bankId] = true;
            bankLastAccessCycle[bankId] = currentCycle;

            // Route through DMA if above threshold and DMA is available
            if (length >= DmaThresholdBytes && dma != null)
            {
                DmaTransfers++;
                bool result = WriteViaDMA(deviceID, address, buffer);

                // Release bank
                bankOccupied[bankId] = false;

                // Update statistics
                if (result)
                {
                    TotalBursts++;
                    TotalBytesTransferred += length;
                }

                // Fire BurstCompleted event
                OnBurstCompleted(address, length, false, bankId);

                return result;
            }

            // Use IOMMU for direct access
            bool success = IOMMU.WriteBurst(deviceID, address, buffer);

            // Release bank
            bankOccupied[bankId] = false;

            // Update statistics
            if (success)
            {
                TotalBursts++;
                TotalBytesTransferred += length;
            }

            // Simulate memory access latency (4 cycles base + length dependent)
            int accessCycles = 4 + (length / BankWidthBytes);
            currentCycle += accessCycles;

            // Fire BurstCompleted event
            OnBurstCompleted(address, length, false, bankId);

            return success;
        }

        #endregion
    }
}
