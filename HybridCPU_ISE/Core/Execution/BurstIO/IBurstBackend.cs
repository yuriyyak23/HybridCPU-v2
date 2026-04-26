using System;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// DMA capabilities for custom accelerator (Phase 4)
    /// </summary>
    public struct AcceleratorDMACapabilities
    {
        public int MaxBurstLength;      // Maximum burst size
        public bool Supports2D;         // Can do 2D transfers
        public bool SupportsScatter;    // Can do scatter-gather
        public int BandwidthMBps;       // Peak bandwidth
    }

    /// <summary>
    /// Token representing a DMA transfer operation (Phase 4)
    /// </summary>
    public class DMATransferToken
    {
        public ulong DeviceId { get; set; }
        public ulong SourceAddress { get; set; }
        public ulong DestinationAddress { get; set; }
        public int Size { get; set; }
        public bool IsComplete { get; set; }
        public int CyclesRemaining { get; set; }
    }

    /// <summary>
    /// Backend abstraction for burst memory operations.
    ///
    /// Purpose:
    /// - Decouple burst planning (BurstPlanner) from memory access mechanism
    /// - Enable multiple backend implementations (IOMMU, simulation, testing)
    /// - Simplify testing: mock backends for unit tests
    ///
    /// Implementations:
    /// - IOMMUBurstBackend: Uses IOMMU for virtual address translation
    /// - SimulatedBackend: In-memory simulation for testing (future)
    ///
    /// Design philosophy:
    /// - Minimal interface: only Read/Write, no configuration
    /// - Span-based: zero-copy semantics
    /// - Boolean return: success/failure (simple error handling)
    /// </summary>
    public interface IBurstBackend
    {
        /// <summary>
        /// Read burst of bytes from memory.
        /// </summary>
        /// <param name="deviceID">Device ID for IOMMU translation (0 for CPU)</param>
        /// <param name="address">Virtual address to read from</param>
        /// <param name="buffer">Destination buffer (caller-allocated)</param>
        /// <returns>True if read succeeded, false on error</returns>
        bool Read(ulong deviceID, ulong address, Span<byte> buffer);

        /// <summary>
        /// Write burst of bytes to memory.
        /// </summary>
        /// <param name="deviceID">Device ID for IOMMU translation (0 for CPU)</param>
        /// <param name="address">Virtual address to write to</param>
        /// <param name="buffer">Source buffer</param>
        /// <returns>True if write succeeded, false on error</returns>
        bool Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer);

        // ========== Phase 4: Accelerator DMA Support ==========

        /// <summary>
        /// Register accelerator as DMA-capable device (Phase 4)
        /// </summary>
        /// <param name="deviceId">Unique device ID for accelerator</param>
        /// <param name="capabilities">DMA capabilities</param>
        void RegisterAcceleratorDevice(ulong deviceId, AcceleratorDMACapabilities capabilities);

        /// <summary>
        /// Initiate DMA transfer for accelerator (Phase 4)
        /// </summary>
        /// <param name="deviceId">Accelerator device ID</param>
        /// <param name="srcAddr">Source address</param>
        /// <param name="dstAddr">Destination address</param>
        /// <param name="size">Transfer size</param>
        /// <returns>DMA transfer token</returns>
        DMATransferToken InitiateAcceleratorDMA(ulong deviceId, ulong srcAddr, ulong dstAddr, int size);
    }
}
