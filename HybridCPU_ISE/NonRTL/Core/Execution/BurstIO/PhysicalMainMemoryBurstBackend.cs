using System;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Explicit physical burst backend wrapper for future backend-selection tests.
    /// It models physical main-memory burst access and is not installed as the
    /// current lane6 DSC or lane7 L7 execution path.
    /// </summary>
    public sealed class PhysicalMainMemoryBurstBackend : IBurstBackend
    {
        private readonly Processor.MainMemoryArea _mainMemory;
        private readonly MemoryCoherencyObserver? _coherencyObserver;

        public PhysicalMainMemoryBurstBackend(
            Processor.MainMemoryArea mainMemory,
            MemoryCoherencyObserver? coherencyObserver = null)
        {
            ArgumentNullException.ThrowIfNull(mainMemory);
            _mainMemory = mainMemory;
            _coherencyObserver = coherencyObserver;
        }

        public Processor.MainMemoryArea MainMemory => _mainMemory;

        public bool Read(ulong deviceID, ulong address, Span<byte> buffer)
        {
            _ = deviceID;
            if (buffer.Length == 0)
            {
                return true;
            }

            return HasExactMainMemoryRange(address, buffer.Length) &&
                   _mainMemory.TryReadPhysicalRange(address, buffer);
        }

        public bool Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)
        {
            _ = deviceID;
            if (buffer.Length == 0)
            {
                return true;
            }

            if (!HasExactMainMemoryRange(address, buffer.Length) ||
                !_mainMemory.TryWritePhysicalRange(address, buffer))
            {
                return false;
            }

            _coherencyObserver?.NotifyWrite(
                new MemoryCoherencyWriteNotification(
                    address,
                    (ulong)buffer.Length,
                    0,
                    MemoryCoherencyWriteSourceKind.PhysicalBurstBackendWrite));
            return true;
        }

        public void RegisterAcceleratorDevice(
            ulong deviceId,
            AcceleratorDMACapabilities capabilities)
        {
            AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported();
        }

        public DMATransferToken InitiateAcceleratorDMA(
            ulong deviceId,
            ulong srcAddr,
            ulong dstAddr,
            int size)
        {
            return AcceleratorRuntimeFailClosed.ThrowTransferNotSupported();
        }

        private bool HasExactMainMemoryRange(ulong address, int size)
        {
            if (size <= 0)
            {
                return false;
            }

            ulong memoryLength = (ulong)_mainMemory.Length;
            ulong requestSize = (ulong)size;
            return requestSize <= memoryLength &&
                   address <= memoryLength - requestSize;
        }
    }
}
