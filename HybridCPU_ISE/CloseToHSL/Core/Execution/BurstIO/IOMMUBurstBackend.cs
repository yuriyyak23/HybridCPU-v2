using System;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// IOMMU-based burst backend implementation.
    ///
    /// Purpose:
    /// - Provides memory access through IOMMU virtual address translation
    /// - Simplest backend: direct delegation to IOMMU.ReadBurst/WriteBurst
    /// - Used for small transfers and default memory access path
    ///
    /// Characteristics:
    /// - No state (stateless, thread-safe by design)
    /// - Minimal overhead: thin wrapper around IOMMU
    /// - Synchronous: operations block until completion
    ///
    /// Alternative backends (future):
    /// - CachingBackend: Adds L2/L3 cache simulation
    /// - TracingBackend: Logs all memory operations for debugging
    /// </summary>
    public sealed class IOMMUBurstBackend : IBurstBackend
    {
        /// <summary>
        /// Read burst through IOMMU.
        /// Delegates directly to Memory.IOMMU.ReadBurst with virtual address translation.
        /// </summary>
        public bool Read(ulong deviceID, ulong address, Span<byte> buffer)
        {
            return YAKSys_Hybrid_CPU.Memory.IOMMU.ReadBurst(deviceID, address, buffer);
        }

        /// <summary>
        /// Write burst through IOMMU.
        /// Delegates directly to Memory.IOMMU.WriteBurst with virtual address translation.
        /// </summary>
        public bool Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)
        {
            return YAKSys_Hybrid_CPU.Memory.IOMMU.WriteBurst(deviceID, address, buffer);
        }

        // Phase 4: Accelerator DMA support remains fail-closed until a truthful runtime exists.

        /// <summary>
        /// Register accelerator device.
        /// </summary>
        public void RegisterAcceleratorDevice(ulong deviceId, AcceleratorDMACapabilities capabilities)
        {
            AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported();
        }

        /// <summary>
        /// Initiate accelerator DMA transfer.
        /// </summary>
        public DMATransferToken InitiateAcceleratorDMA(ulong deviceId, ulong srcAddr, ulong dstAddr, int size)
        {
            return AcceleratorRuntimeFailClosed.ThrowTransferNotSupported();
        }
    }
}
