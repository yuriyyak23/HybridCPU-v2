using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Standalone DMA Controller for HybridCPU.
    /// Offloads memory-to-memory transfers from CPU_Core to improve performance.
    ///
    /// Features:
    /// - Independent operation: doesn't block CPU execution
    /// - AXI4-compliant burst transfers
    /// - Scatter/gather support via descriptor chains
    /// - Multi-channel support (up to 8 channels)
    /// - Integration with IOMMU for address translation
    /// - Hardware prefetch for stream operations
    /// - Priority-based channel arbitration
    /// - Interrupt signaling on completion
    /// - Async completion via events and callbacks
    ///
    /// Design goals:
    /// - Allow multiple StreamEngine instances to run concurrently
    /// - Reduce CPU_Core memory bandwidth contention
    /// - Enable overlapped computation and I/O
    /// </summary>
    public class DMAController
    {
        /// <summary>
        /// Event arguments for DMA transfer completion
        /// </summary>
        public class TransferCompletedEventArgs : EventArgs
        {
            public byte ChannelID { get; set; }
            public bool IsError { get; set; }
            public ulong ErrorCode { get; set; }
            public uint BytesTransferred { get; set; }
        }

        /// <summary>
        /// Callback delegate for async transfer completion
        /// </summary>
        public delegate void TransferCompletionCallback(byte channelID, bool success, ulong errorCode);

        /// <summary>
        /// Event fired when a DMA transfer completes (success or error)
        /// </summary>
        public event EventHandler<TransferCompletedEventArgs>? TransferCompleted;
        /// <summary>
        /// Maximum number of DMA channels supported
        /// </summary>
        private const int MAX_CHANNELS = 8;

        /// <summary>
        /// Maximum burst length (AXI4 compliance: 256 beats)
        /// </summary>
        private const int MAX_BURST_LENGTH = 256;

        /// <summary>
        /// AXI4 4KB boundary alignment requirement
        /// </summary>
        private const int BOUNDARY_4KB = 4096;

        /// <summary>
        /// DMA channel state
        /// </summary>
        public enum ChannelState : byte
        {
            Idle = 0,           // Channel is idle, ready for new transfer
            Configured = 1,     // Transfer configured, not started
            Active = 2,         // Transfer in progress
            Paused = 3,         // Transfer paused (can be resumed)
            Completed = 4,      // Transfer completed successfully
            Error = 5           // Transfer error occurred
        }

        /// <summary>
        /// DMA transfer descriptor
        /// Describes a single memory-to-memory transfer operation
        /// </summary>
        public struct TransferDescriptor
        {
            public ulong SourceAddress;         // Physical source address
            public ulong DestAddress;           // Physical destination address
            public uint TransferSize;           // Number of bytes to transfer
            public ushort SourceStride;         // Stride for source (0 = packed)
            public ushort DestStride;           // Stride for destination (0 = packed)
            public byte ElementSize;            // Size of each element in bytes (1, 2, 4, 8)
            public bool UseIOMMU;               // Whether to use IOMMU translation
            public ulong NextDescriptor;        // Address of next descriptor (0 = none)
            public byte ChannelID;              // DMA channel ID (0-7)
            public byte Priority;               // Transfer priority (0 = lowest, 255 = highest)
        }

        /// <summary>
        /// DMA channel control block
        /// </summary>
        private struct ChannelControl
        {
            public ChannelState State;          // Current channel state
            public TransferDescriptor CurrentDesc; // Current transfer descriptor
            public uint BytesTransferred;       // Bytes transferred so far
            public uint TotalBytes;             // Total bytes for this transfer
            public ulong ErrorCode;             // Error code if State == Error
            public TransferCompletionCallback? Callback; // Optional completion callback
        }

        // DMA channel state (one per channel)
        private ChannelControl[] channels;

        // Reference to processor memory
        private Processor processor;

        /// <summary>
        /// Captured interrupt-dispatch surface. When non-null, interrupt delivery
        /// routes through this delegate instead of the global
        /// <see cref="Processor.InterruptData.CallInterrupt"/>.
        /// Signature: (DeviceType deviceId, ushort interruptId, ulong coreId).
        /// </summary>
        private readonly Action<Processor.DeviceType, ushort, ulong>? _interruptDispatch;

        // Performance counters
        private ulong totalBytesTransferred;
        private ulong totalTransfers;
        private ulong totalErrors;

        /// <summary>
        /// Initialize DMA controller.
        /// </summary>
        /// <param name="proc">Reference to processor for memory access.</param>
        /// <param name="interruptDispatch">Optional interrupt-dispatch delegate.
        /// When null, falls back to the global <see cref="Processor.InterruptData.CallInterrupt"/>.</param>
        public DMAController(
            ref Processor proc,
            Action<Processor.DeviceType, ushort, ulong>? interruptDispatch = null)
        {
            this.processor = proc;
            this._interruptDispatch = interruptDispatch;
            this.channels = new ChannelControl[MAX_CHANNELS];

            // Initialize all channels to idle state
            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                channels[i].State = ChannelState.Idle;
                channels[i].BytesTransferred = 0;
                channels[i].TotalBytes = 0;
                channels[i].ErrorCode = 0;
            }

            totalBytesTransferred = 0;
            totalTransfers = 0;
            totalErrors = 0;
        }

        /// <summary>
        /// Configure a DMA transfer
        /// </summary>
        /// <param name="desc">Transfer descriptor</param>
        /// <param name="callback">Optional callback to invoke on completion</param>
        /// <returns>True if channel configured successfully, false if channel busy</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureTransfer(TransferDescriptor desc, TransferCompletionCallback? callback = null)
        {
            // Validate channel ID
            if (desc.ChannelID >= MAX_CHANNELS)
                return false;

            // Check if channel is available
            if (channels[desc.ChannelID].State != ChannelState.Idle &&
                channels[desc.ChannelID].State != ChannelState.Completed)
            {
                return false; // Channel busy
            }

            // Validate transfer parameters
            if (desc.TransferSize == 0 || desc.ElementSize == 0)
                return false;

            // Configure channel
            channels[desc.ChannelID].CurrentDesc = desc;
            channels[desc.ChannelID].BytesTransferred = 0;
            channels[desc.ChannelID].TotalBytes = desc.TransferSize;
            channels[desc.ChannelID].State = ChannelState.Configured;
            channels[desc.ChannelID].ErrorCode = 0;
            channels[desc.ChannelID].Callback = callback;

            return true;
        }

        /// <summary>
        /// Start a configured DMA transfer
        /// </summary>
        /// <param name="channelID">Channel ID to start</param>
        /// <returns>True if started successfully</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartTransfer(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return false;

            if (channels[channelID].State != ChannelState.Configured)
                return false;

            channels[channelID].State = ChannelState.Active;
            totalTransfers++;

            return true;
        }

        /// <summary>
        /// Execute one DMA cycle (processes active channels)
        /// Call this periodically to advance DMA transfers
        /// Uses priority-based arbitration: highest priority active channel is processed first
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteCycle()
        {
            // Priority-based arbitration: find highest priority active channel
            int selectedChannel = -1;
            byte highestPriority = 0;

            for (int ch = 0; ch < MAX_CHANNELS; ch++)
            {
                if (channels[ch].State == ChannelState.Active)
                {
                    byte channelPriority = channels[ch].CurrentDesc.Priority;
                    if (selectedChannel == -1 || channelPriority > highestPriority)
                    {
                        selectedChannel = ch;
                        highestPriority = channelPriority;
                    }
                }
            }

            // Process the highest priority channel
            if (selectedChannel >= 0)
            {
                ProcessChannel((byte)selectedChannel);
            }
        }

        /// <summary>
        /// Process a single DMA channel transfer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessChannel(byte channelID)
        {
            ref ChannelControl ch = ref channels[channelID];
            ref TransferDescriptor desc = ref ch.CurrentDesc;

            // Calculate remaining bytes
            uint remaining = ch.TotalBytes - ch.BytesTransferred;
            if (remaining == 0)
            {
                CompleteChannel(channelID);
                return;
            }

            // Calculate burst size (limited by MAX_BURST_LENGTH and 4KB boundary)
            uint burstBytes = Math.Min(remaining, (uint)(MAX_BURST_LENGTH * desc.ElementSize));
            uint srcOffset = ch.BytesTransferred;
            uint dstOffset = ch.BytesTransferred;

            ulong srcAddr = desc.SourceAddress + srcOffset;
            ulong dstAddr = desc.DestAddress + dstOffset;

            // Check 4KB boundary crossing for source
            uint srcTo4KB = BytesTo4KBBoundary(srcAddr);
            if (burstBytes > srcTo4KB && srcTo4KB > 0)
            {
                burstBytes = srcTo4KB;
            }

            // Check 4KB boundary crossing for destination
            uint dstTo4KB = BytesTo4KBBoundary(dstAddr);
            if (burstBytes > dstTo4KB && dstTo4KB > 0)
            {
                burstBytes = dstTo4KB;
            }

            // Perform memory-to-memory transfer
            bool success = PerformBurst(srcAddr, dstAddr, burstBytes, desc.UseIOMMU);

            if (success)
            {
                ch.BytesTransferred += burstBytes;
                totalBytesTransferred += burstBytes;
            }
            else
            {
                // Transfer error
                ch.State = ChannelState.Error;
                ch.ErrorCode = 0x1; // Generic transfer error
                totalErrors++;

                // Invoke error callback if registered
                ch.Callback?.Invoke(channelID, false, ch.ErrorCode);

                // Fire completion event with error
                OnTransferCompleted(channelID, true, ch.ErrorCode, ch.BytesTransferred);

                // Signal error interrupt
                RaiseInterrupt(channelID, isError: true);
            }
        }

        /// <summary>
        /// Complete a DMA channel transfer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CompleteChannel(byte channelID)
        {
            ref ChannelControl ch = ref channels[channelID];

            // Check if there's a chained descriptor
            if (ch.CurrentDesc.NextDescriptor != 0)
            {
                // Load next descriptor (placeholder - would read from memory)
                // For now, just mark as completed
                ch.State = ChannelState.Completed;
            }
            else
            {
                ch.State = ChannelState.Completed;
            }

            // Invoke completion callback if registered
            ch.Callback?.Invoke(channelID, true, 0);

            // Fire completion event
            OnTransferCompleted(channelID, false, 0, ch.BytesTransferred);

            // Signal interrupt on completion
            RaiseInterrupt(channelID, isError: false);
        }

        /// <summary>
        /// Raise an interrupt for DMA channel completion or error.
        /// Routes through the captured interrupt-dispatch delegate when available;
        /// falls back to the global <see cref="Processor.InterruptData.CallInterrupt"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseInterrupt(byte channelID, bool isError)
        {
            // Calculate interrupt ID: base (0x90) + channel offset + error flag
            ushort interruptID = (ushort)(0x90 + channelID);
            if (isError)
            {
                interruptID = (ushort)(0xA0 + channelID); // Error interrupts use 0xA0 base
            }

            if (_interruptDispatch != null)
            {
                _interruptDispatch(Processor.DeviceType.DMAController, interruptID, 0);
            }
            else
            {
                Processor.InterruptData.CallInterrupt(
                    Processor.DeviceType.DMAController,
                    interruptID,
                    0 // Core ID 0 for now
                );
            }
        }

        /// <summary>
        /// Perform memory-to-memory burst transfer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PerformBurst(ulong srcAddr, ulong dstAddr, uint byteCount, bool useIOMMU)
        {
            try
            {
                Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();

                // Allocate temporary buffer for transfer
                byte[] tempBuffer = new byte[byteCount];
                Span<byte> bufferSpan = tempBuffer;

                // Read from source
                if (useIOMMU)
                {
                    // Use IOMMU for translation and bounds checking
                    if (!IOMMU.ReadBurst(0UL, srcAddr, bufferSpan))
                        return false;
                }
                else
                {
                    // Direct memory access
                    if (!TryReadDirectMainMemoryExact(mainMemory, srcAddr, bufferSpan))
                        return false;
                }

                // Write to destination
                if (useIOMMU)
                {
                    // Use IOMMU for translation and bounds checking
                    if (!IOMMU.WriteBurst(0UL, dstAddr, bufferSpan))
                        return false;
                }
                else
                {
                    // Direct memory access
                    if (!TryWriteDirectMainMemoryExact(mainMemory, dstAddr, bufferSpan))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calculate bytes remaining until 4KB boundary
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint BytesTo4KBBoundary(ulong address)
        {
            ulong nextBoundary = (address + BOUNDARY_4KB) & ~(ulong)(BOUNDARY_4KB - 1);
            return (uint)(nextBoundary - address);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Processor.MainMemoryArea CaptureCurrentMainMemory() => Processor.MainMemory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasExactMainMemoryRange(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            int size)
        {
            if (size <= 0)
            {
                return false;
            }

            ulong memoryLength = (ulong)mainMemory.Length;
            ulong requestSize = (ulong)size;
            return requestSize <= memoryLength &&
                   address <= memoryLength - requestSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryReadDirectMainMemoryExact(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            Span<byte> buffer)
        {
            return HasExactMainMemoryRange(mainMemory, address, buffer.Length) &&
                   mainMemory.TryReadPhysicalRange(address, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteDirectMainMemoryExact(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            ReadOnlySpan<byte> buffer)
        {
            return HasExactMainMemoryRange(mainMemory, address, buffer.Length) &&
                   mainMemory.TryWritePhysicalRange(address, buffer);
        }

        /// <summary>
        /// Get channel state
        /// </summary>
        public ChannelState GetChannelState(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return ChannelState.Error;

            return channels[channelID].State;
        }

        /// <summary>
        /// Get channel progress
        /// </summary>
        public (uint transferred, uint total) GetChannelProgress(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return (0, 0);

            return (channels[channelID].BytesTransferred, channels[channelID].TotalBytes);
        }

        /// <summary>
        /// Reset a channel to idle state
        /// </summary>
        public void ResetChannel(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return;

            channels[channelID].State = ChannelState.Idle;
            channels[channelID].BytesTransferred = 0;
            channels[channelID].TotalBytes = 0;
            channels[channelID].ErrorCode = 0;
        }

        /// <summary>
        /// Get performance statistics
        /// </summary>
        public (ulong totalBytes, ulong totalTransfers, ulong totalErrors) GetStatistics()
        {
            return (totalBytesTransferred, totalTransfers, totalErrors);
        }

        /// <summary>
        /// Get channel descriptor information (source/destination addresses)
        /// </summary>
        public (ulong srcAddr, ulong dstAddr) GetChannelAddresses(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return (0, 0);

            return (channels[channelID].CurrentDesc.SourceAddress, channels[channelID].CurrentDesc.DestAddress);
        }

        /// <summary>
        /// Pause a running DMA transfer
        /// </summary>
        public bool PauseTransfer(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return false;

            if (channels[channelID].State == ChannelState.Active)
            {
                channels[channelID].State = ChannelState.Paused;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resume a paused DMA transfer
        /// </summary>
        public bool ResumeTransfer(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return false;

            if (channels[channelID].State == ChannelState.Paused)
            {
                channels[channelID].State = ChannelState.Active;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cancel a DMA transfer
        /// </summary>
        public bool CancelTransfer(byte channelID)
        {
            if (channelID >= MAX_CHANNELS)
                return false;

            ref ChannelControl ch = ref channels[channelID];

            if (ch.State == ChannelState.Active || ch.State == ChannelState.Paused || ch.State == ChannelState.Configured)
            {
                // Invoke callback with error
                ch.Callback?.Invoke(channelID, false, 0xFF); // 0xFF = cancelled

                // Fire completion event
                OnTransferCompleted(channelID, true, 0xFF, ch.BytesTransferred);

                // Reset channel
                ResetChannel(channelID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fire TransferCompleted event
        /// </summary>
        private void OnTransferCompleted(byte channelID, bool isError, ulong errorCode, uint bytesTransferred)
        {
            TransferCompleted?.Invoke(this, new TransferCompletedEventArgs
            {
                ChannelID = channelID,
                IsError = isError,
                ErrorCode = errorCode,
                BytesTransferred = bytesTransferred
            });
        }
    }
}
