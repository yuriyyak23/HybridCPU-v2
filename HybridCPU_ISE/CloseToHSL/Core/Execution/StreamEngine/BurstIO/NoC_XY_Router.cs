using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// NoC port direction for XY dimension-order routing.
    /// </summary>
    public enum NoCPort : byte
    {
        Local = 0,
        East = 1,
        West = 2,
        North = 3,
        South = 4
    }

    /// <summary>
    /// NoC flit — minimal transfer unit in the 2D-Mesh network (tech.md §2).
    /// Carries burst data between Pods with routing metadata and domain isolation tags.
    /// HLS-compatible: fixed-size struct, no heap allocation.
    /// </summary>
    public struct NoCFlit
    {
        /// <summary>Destination Pod X coordinate</summary>
        public int DestX;

        /// <summary>Destination Pod Y coordinate</summary>
        public int DestY;

        /// <summary>Source Pod X coordinate</summary>
        public int SrcX;

        /// <summary>Source Pod Y coordinate</summary>
        public int SrcY;

        /// <summary>Device ID for IOMMU translation at destination</summary>
        public ulong DeviceId;

        /// <summary>Target memory address at destination Pod</summary>
        public ulong Address;

        /// <summary>Payload data (burst segment)</summary>
        public byte[] Payload;

        /// <summary>True = write, False = read request</summary>
        public bool IsWrite;

        /// <summary>Domain tag for Singularity-style isolation (tech.md §3)</summary>
        public ulong DomainTag;

        /// <summary>QoS priority level (from CSR NOC_ROUTE_CFG, 0xB03)</summary>
        public byte QosPriority;

        /// <summary>Hop counter for diagnostics / TTL-like protection</summary>
        public byte HopCount;

        /// <summary>
        /// Virtual channel ID (0 = high-priority/DMA completion, 1 = bulk data).
        /// Enables HoL-blocking avoidance: if VC0 is stalled, VC1 continues.
        /// </summary>
        public byte VirtualChannel;
    }

    /// <summary>
    /// NoC XY Router — inter-Pod burst backend via 2D-Mesh Network-on-Chip (tech.md §2).
    ///
    /// Key properties:
    /// - Implements IBurstBackend for transparent integration with existing burst pipeline
    /// - XY dimension-order routing: mathematically deadlock-free (route X first, then Y)
    /// - Only burst transfers between Pods (no inter-pod FSP — FSP is pod-local only)
    /// - Domain tag propagation through flit metadata for IOMMU/security compatibility
    /// - Credit-based flow control to prevent congestion
    ///
    /// HLS constraints:
    /// - Fixed-size buffers per port (no dynamic allocation)
    /// - Deterministic routing decision (single-cycle mux in hardware)
    /// - Bounded hop count for timing closure
    /// </summary>
    public class NoC_XY_Router : IBurstBackend
    {
        /// <summary>Maximum flits per output port buffer (flow control)</summary>
        private const int PORT_BUFFER_DEPTH = 8;

        /// <summary>Maximum hops before TTL expiry (8×8 grid worst case = 14 hops)</summary>
        private const int MAX_HOPS = 16;

        /// <summary>Local Pod X coordinate in 2D-Mesh</summary>
        public int LocalX { get; }

        /// <summary>Local Pod Y coordinate in 2D-Mesh</summary>
        public int LocalY { get; }

        /// <summary>Number of virtual channels per physical port.</summary>
        private const int VC_COUNT = 2;

        /// <summary>Buffer depth per VC per port (HLS: LUTRAM FIFO).</summary>
        private const int VC_BUFFER_DEPTH = 4;

        /// <summary>Maximum tracked security domains for VC isolation (Q1 Review §4).</summary>
        private const int MAX_DOMAINS = 16;

        /// <summary>Legacy credit counters per port (used by default VC0).</summary>
        private readonly int[] _credits = new int[5];

        /// <summary>
        /// Per-VC credit counters: [port, vc].
        /// HLS: 5×2 = 10 registers (trivial).
        /// </summary>
        private readonly int[,] _vcCredits = new int[5, VC_COUNT];

        // ── Domain-Isolated Virtual Channels (Q1 Review §4) ─────────

        /// <summary>
        /// Per-domain inflight flit counter for queue bounding.
        /// Prevents one domain from saturating shared NoC resources.
        /// HLS: 16 × 8-bit saturating counters = 128 bits.
        /// </summary>
        private readonly int[] _domainInflightCount = new int[MAX_DOMAINS];

        /// <summary>
        /// Maximum inflight flits per domain before local stall.
        /// Default: VC_BUFFER_DEPTH × 5 ports = 20, allowing full utilization
        /// when only one domain is active. Zero disables domain isolation.
        /// </summary>
        public int MaxInflightPerDomain { get; set; } = VC_BUFFER_DEPTH * 5;

        /// <summary>
        /// Enable domain-to-VC isolation: when true, flit VC is selected
        /// based on DomainTag hash rather than flit.VirtualChannel field.
        /// HLS: single AND-gate on the VC mux select line.
        /// </summary>
        public bool DomainVcIsolationEnabled { get; set; }

        /// <summary>Statistics: flits rejected due to domain inflight limit.</summary>
        public long DomainStallCount { get; private set; }

        /// <summary>Local backend for executing burst operations when flit reaches destination</summary>
        private readonly IBurstBackend? _localBackend;

        /// <summary>Neighbor routers connected via directional links</summary>
        private readonly NoC_XY_Router?[] _neighbors = new NoC_XY_Router?[5];

        /// <summary>Statistics: total flits routed through this node</summary>
        public long FlitsRouted { get; private set; }

        /// <summary>Statistics: flits delivered locally (destination reached)</summary>
        public long FlitsDelivered { get; private set; }

        /// <summary>Statistics: flits dropped due to TTL expiry or full buffer</summary>
        public long FlitsDropped { get; private set; }

        public NoC_XY_Router(int localX, int localY, IBurstBackend? localBackend = null)
        {
            LocalX = localX;
            LocalY = localY;
            _localBackend = localBackend;

            // Initialize credits (all ports start with full buffer capacity)
            for (int i = 0; i < _credits.Length; i++)
                _credits[i] = PORT_BUFFER_DEPTH;

            // Initialize per-VC credits
            for (int i = 0; i < 5; i++)
                for (int vc = 0; vc < VC_COUNT; vc++)
                    _vcCredits[i, vc] = VC_BUFFER_DEPTH;
        }

        /// <summary>
        /// Connect a neighbor router on a specific port direction.
        /// Called during NoC topology construction.
        /// </summary>
        public void ConnectNeighbor(NoCPort direction, NoC_XY_Router neighbor)
        {
            _neighbors[(int)direction] = neighbor;
        }

        /// <summary>
        /// XY dimension-order routing: route flit towards destination.
        /// Algorithm: route X dimension first, then Y dimension.
        /// This ordering mathematically excludes routing deadlocks (tech.md §2).
        ///
        /// HLS: synthesizes as a 2-level priority mux (compare X, then compare Y).
        /// Single-cycle routing decision in hardware.
        /// </summary>
        public void RoutePacket(NoCFlit flit)
        {
            // TTL protection against routing loops (should never happen with XY, but defense-in-depth)
            if (flit.HopCount >= MAX_HOPS)
            {
                FlitsDropped++;
                return;
            }

            flit.HopCount++;
            FlitsRouted++;

            // XY routing: X dimension first, then Y
            if (flit.DestX != LocalX)
            {
                // Route along X axis
                NoCPort port = flit.DestX > LocalX ? NoCPort.East : NoCPort.West;
                SendToPort(port, flit);
            }
            else if (flit.DestY != LocalY)
            {
                // Route along Y axis
                NoCPort port = flit.DestY > LocalY ? NoCPort.North : NoCPort.South;
                SendToPort(port, flit);
            }
            else
            {
                // Flit has reached destination Pod — deliver locally
                AcceptLocalDMA(flit);
            }
        }

        /// <summary>
        /// Forward flit to neighbor via output port with per-VC credit-based flow control.
        /// HoL avoidance: if primary VC is blocked, low-priority flits try alternate VC.
        ///
        /// Q1 Review §4 enhancement: when DomainVcIsolationEnabled is true, the VC is
        /// selected by hashing DomainTag, and per-domain inflight counters enforce queue
        /// bounding to prevent cross-domain timing interference.
        ///
        /// HLS: 2-input mux per port, single-cycle decision.
        /// </summary>
        private void SendToPort(NoCPort port, NoCFlit flit)
        {
            int portIdx = (int)port;
            var neighbor = _neighbors[portIdx];

            if (neighbor == null)
            {
                FlitsDropped++;
                return;
            }

            // Q1 Review §4: Domain inflight queue bounding
            if (DomainVcIsolationEnabled && MaxInflightPerDomain > 0 && flit.DomainTag != 0)
            {
                int domainSlot = MapDomainToSlot(flit.DomainTag);
                if (_domainInflightCount[domainSlot] >= MaxInflightPerDomain)
                {
                    // Domain has saturated its allocation — stall locally
                    DomainStallCount++;
                    FlitsDropped++;
                    return;
                }
                _domainInflightCount[domainSlot]++;
            }

            int vc = flit.VirtualChannel;

            // Q1 Review §4: Domain-isolated VC selection
            if (DomainVcIsolationEnabled && flit.DomainTag != 0)
            {
                vc = MapDomainToVC(flit.DomainTag);
                flit.VirtualChannel = (byte)vc;
            }

            // Per-VC credit check (does not block other VC)
            if (_vcCredits[portIdx, vc] <= 0)
            {
                // HoL avoidance: try alternate VC for non-priority traffic
                int altVc = 1 - vc;
                if (_vcCredits[portIdx, altVc] > 0 && flit.QosPriority == 0)
                {
                    vc = altVc;
                    flit.VirtualChannel = (byte)altVc;
                }
                else
                {
                    FlitsDropped++;
                    return;
                }
            }

            _vcCredits[portIdx, vc]--;
            _credits[portIdx] = _vcCredits[portIdx, 0] + _vcCredits[portIdx, 1]; // Sync legacy counter
            neighbor.RoutePacket(flit);
        }

        /// <summary>
        /// Map a DomainTag to a Virtual Channel index (0 or 1).
        /// Uses simple hash to distribute domains across VCs.
        /// HLS: XOR-fold of DomainTag → single-bit result.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int MapDomainToVC(ulong domainTag)
        {
            // XOR-fold 64 bits to 1 bit for VC selection
            return (int)((domainTag ^ (domainTag >> 32)) & 1);
        }

        /// <summary>
        /// Map a DomainTag to an inflight counter slot (0–15).
        /// HLS: low-4-bit XOR hash of DomainTag.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int MapDomainToSlot(ulong domainTag)
        {
            return (int)((domainTag ^ (domainTag >> 4)) & 0xF);
        }

        /// <summary>
        /// Release a domain inflight slot (called when flit is consumed at destination).
        /// </summary>
        public void ReleaseDomainInflight(ulong domainTag)
        {
            if (!DomainVcIsolationEnabled || domainTag == 0) return;

            int domainSlot = MapDomainToSlot(domainTag);
            if (_domainInflightCount[domainSlot] > 0)
                _domainInflightCount[domainSlot]--;
        }

        /// <summary>
        /// Return credit to a port on VC0 (backward-compatible overload).
        /// </summary>
        public void ReturnCredit(NoCPort port)
        {
            ReturnCredit(port, 0);
        }

        /// <summary>
        /// Return credit to a specific virtual channel on a port.
        /// Called by downstream router after consuming flit.
        /// </summary>
        /// <param name="port">Port direction.</param>
        /// <param name="virtualChannel">Virtual channel ID (0 or 1).</param>
        public void ReturnCredit(NoCPort port, int virtualChannel)
        {
            int portIdx = (int)port;
            if (_vcCredits[portIdx, virtualChannel] < VC_BUFFER_DEPTH)
                _vcCredits[portIdx, virtualChannel]++;

            _credits[portIdx] = _vcCredits[portIdx, 0] + _vcCredits[portIdx, 1];
        }

        /// <summary>
        /// Accept flit at destination: execute burst via local backend.
        /// Domain tag from flit metadata is propagated for IOMMU security checking.
        /// </summary>
        private void AcceptLocalDMA(NoCFlit flit)
        {
            FlitsDelivered++;

            if (_localBackend == null) return;

            if (flit.IsWrite && flit.Payload != null)
            {
                _localBackend.Write(flit.DeviceId, flit.Address, flit.Payload);
            }
            else if (!flit.IsWrite)
            {
                // For read requests, allocate response buffer
                // In real hardware this would be a registered buffer
                byte[] buffer = new byte[flit.Payload?.Length ?? 64];
                _localBackend.Read(flit.DeviceId, flit.Address, buffer);
            }
        }

        // ===== IBurstBackend implementation: transparently routes burst ops through NoC =====

        /// <inheritdoc/>
        public bool Read(ulong deviceID, ulong address, Span<byte> buffer)
        {
            // Local read: delegate to local backend if available
            if (_localBackend != null)
                return _localBackend.Read(deviceID, address, buffer);

            return false;
        }

        /// <inheritdoc/>
        public bool Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)
        {
            // Local write: delegate to local backend if available
            if (_localBackend != null)
                return _localBackend.Write(deviceID, address, buffer);

            return false;
        }

        /// <summary>
        /// Initiate a remote burst write via NoC routing to a target Pod.
        /// Creates a NoCFlit with routing metadata and injects it into the network.
        /// </summary>
        /// <param name="destPodX">Destination Pod X coordinate</param>
        /// <param name="destPodY">Destination Pod Y coordinate</param>
        /// <param name="deviceId">Device ID at destination</param>
        /// <param name="address">Target address at destination</param>
        /// <param name="data">Payload data</param>
        /// <param name="domainTag">Domain tag for isolation (tech.md §3)</param>
        public void SendRemoteBurst(int destPodX, int destPodY, ulong deviceId,
                                    ulong address, byte[] data, ulong domainTag = 0)
        {
            var flit = new NoCFlit
            {
                DestX = destPodX,
                DestY = destPodY,
                SrcX = LocalX,
                SrcY = LocalY,
                DeviceId = deviceId,
                Address = address,
                Payload = data,
                IsWrite = true,
                DomainTag = domainTag,
                QosPriority = 0,
                HopCount = 0,
                VirtualChannel = 1 // Default: bulk data VC
            };

            RoutePacket(flit);
        }

        /// <inheritdoc/>
        public void RegisterAcceleratorDevice(ulong deviceId, AcceleratorDMACapabilities capabilities)
        {
            IBurstBackend? localBackend = _localBackend;
            if (localBackend == null)
            {
                AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported();
            }

            localBackend!.RegisterAcceleratorDevice(deviceId, capabilities);
        }

        /// <inheritdoc/>
        public DMATransferToken InitiateAcceleratorDMA(ulong deviceId, ulong srcAddr, ulong dstAddr, int size)
        {
            IBurstBackend? localBackend = _localBackend;
            if (localBackend != null)
                return localBackend.InitiateAcceleratorDMA(deviceId, srcAddr, dstAddr, size);

            return AcceleratorRuntimeFailClosed.ThrowTransferNotSupported();
        }
    }
}
