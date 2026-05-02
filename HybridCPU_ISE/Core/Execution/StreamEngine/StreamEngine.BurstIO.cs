using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Burst I/O operations for stream engine.
    /// Implements AXI4-compliant burst rules:
    /// - Maximum 256 beats (transfers) per burst
    /// - Cannot cross 4KB boundaries
    /// - No early termination
    ///
    /// Design philosophy (ref2.md refactoring):
    /// - Separation of concerns: BurstPlanner (math) vs. IBurstBackend (I/O)
    /// - Backend abstraction enables testing, DMA offloading, simulation
    /// - Deterministic: same input → same burst pattern
    /// - Hardware-oriented: models real FPGA memory controller
    ///
    /// Enhanced with DMA support (ref2.md):
    /// - Large transfers (> DMA_THRESHOLD) use DMA Controller
    /// - Current helper drives DMA cycles synchronously before returning
    /// - Reduces CPU memory bandwidth contention
    ///
    /// HLS Note: Each burst operation takes MEMORY_LATENCY cycles (4 cycles for L1 cache hit).
    /// Actual hardware latency depends on:
    /// - Cache hit/miss (L1: 4 cycles, L2: 12 cycles, DRAM: 100+ cycles)
    /// - Burst length (longer bursts have higher latency)
    /// - Memory controller congestion
    /// </summary>
    internal static class BurstIO
    {
        private const ulong CPU_DEVICE_ID = 0;
        private const int AXI4_MAX_BURST_LENGTH = 256;
        private const ulong AXI4_4KB_BOUNDARY = 4096;

        /// <summary>
        /// DMA threshold: transfers larger than this use DMA Controller (ref2.md)
        /// Default: 4KB (one page). Transfers smaller than this use direct IOMMU path.
        /// </summary>
        private const ulong DMA_THRESHOLD = 4096;

        /// <summary>
        /// Backend for memory operations.
        /// Defaults to IOMMU backend for virtual address translation.
        /// Can be swapped for testing or alternative implementations (DMA, simulation, etc.)
        ///
        /// Updated (ref3.md - PerfModel): Now uses MemorySubsystem if available,
        /// otherwise falls back to IOMMUBurstBackend for backwards compatibility.
        /// </summary>
        private static IBurstBackend _backend = new IOMMUBurstBackend();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Processor.MainMemoryArea CaptureCurrentMainMemory() => Processor.MainMemory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static YAKSys_Hybrid_CPU.Memory.MemorySubsystem? ResolveActiveMemorySubsystem(
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub)
        {
            return memSub ?? Processor.Memory;
        }

        /// <summary>
        /// Get the active backend (MemorySubsystem if available, otherwise fallback)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IBurstBackend GetBackend(YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub)
        {
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? activeMemorySubsystem =
                ResolveActiveMemorySubsystem(memSub);
            if (activeMemorySubsystem != null)
            {
                return activeMemorySubsystem;
            }
            // Fallback to direct IOMMU backend for backwards compatibility
            return _backend;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasExactBoundMainMemoryRange(
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
        private static bool TryReadBoundMainMemoryExact(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return true;
            }

            return HasExactBoundMainMemoryRange(mainMemory, address, buffer.Length) &&
                   mainMemory.TryReadPhysicalRange(address, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteBoundMainMemoryExact(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return true;
            }

            return HasExactBoundMainMemoryRange(mainMemory, address, buffer.Length) &&
                   mainMemory.TryWritePhysicalRange(address, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryReadThroughActiveBackend(
            ulong address,
            Span<byte> buffer,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (buffer.Length == 0)
            {
                return true;
            }

            return GetBackend(memSub).Read(CPU_DEVICE_ID, address, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryConsumePrefetchedSrfChunk(
            ulong baseAddr,
            Span<byte> buffer,
            ulong elementCount,
            int elementSize,
            ushort stride,
            YAKSys_Hybrid_CPU.Memory.StreamRegisterFile? srf)
        {
            if (stride != elementSize ||
                elementCount == 0 ||
                elementSize <= 0 ||
                elementCount > uint.MaxValue ||
                srf == null)
            {
                return false;
            }

            ulong totalBytes = elementCount * (ulong)elementSize;
            if (totalBytes == 0 || totalBytes > (ulong)buffer.Length || totalBytes > int.MaxValue)
            {
                return false;
            }

            return srf.TryReadPrefetchedChunk(
                baseAddr,
                (byte)elementSize,
                (uint)elementCount,
                buffer.Slice(0, (int)totalBytes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InvalidatePrefetchedSrfWindow(
            ulong address,
            ulong byteCount,
            YAKSys_Hybrid_CPU.Memory.StreamRegisterFile? srf)
        {
            if (byteCount == 0 ||
                byteCount > uint.MaxValue ||
                srf == null)
            {
                return;
            }

            srf.InvalidateOverlappingRange(address, (uint)byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedZeroRowLength2DTransferContour(
            string operationName,
            ulong elementCount,
            uint rowLength)
        {
            if (elementCount == 0 || rowLength != 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{operationName} reached a non-zero 2D transfer request with rowLength == 0. " +
                "Shared 2D vector/stream transfer helpers must fail closed instead of collapsing into hidden success/no-op when row geometry is non-representable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfIncompleteTransfer(
            string operationName,
            ulong baseAddr,
            ulong requestedElements,
            ulong completedElements,
            int elementSize)
        {
            if (completedElements == requestedElements)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{operationName} reached a non-zero vector/stream transfer request at base address 0x{baseAddr:X} " +
                $"but materialized only {completedElements} of {requestedElements} element(s) with elementSize == {elementSize}. " +
                "Shared BurstIO helpers must fail closed instead of letting raw StreamEngine or authoritative vector carriers continue on partially materialized data, hidden partial success or stale scratch contents.");
        }

        /// <summary>
        /// Read elements from memory using AXI4-compliant bursts.
        /// Automatically splits at 4KB boundaries and respects burst length limits.
        /// Large transfers (> DMA_THRESHOLD) are routed through DMA Controller for offloading.
        ///
        /// Refactored (ref2.md): Uses BurstPlanner for page-split logic and IBurstBackend for I/O.
        /// </summary>
        /// <param name="baseAddr">Base IO virtual address</param>
        /// <param name="buffer">Destination buffer (must be pre-allocated)</param>
        /// <param name="elementCount">Number of elements to read</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="stride">Byte stride between elements (0 = packed/contiguous)</param>
        /// <returns>Number of elements successfully read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstRead(ulong baseAddr, Span<byte> buffer, ulong elementCount, int elementSize, ushort stride,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 || elementSize <= 0) return 0;

            // Default to packed stride if not specified
            if (stride == 0) stride = (ushort)elementSize;

            // Calculate total transfer size
            ulong totalBytes = elementCount * (ulong)elementSize;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;

            if (TryConsumePrefetchedSrfChunk(baseAddr, buffer, elementCount, elementSize, stride, srf))
            {
                return elementCount;
            }

            // For large contiguous transfers above DMA threshold, use DMA Controller
            if (stride == elementSize && totalBytes > DMA_THRESHOLD && Processor.DMAController != null)
            {
                ulong completedElements = BurstReadViaDMA(baseAddr, buffer, elementCount, elementSize);
                ThrowIfIncompleteTransfer(
                    nameof(BurstRead),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // For contiguous packed transfers, use BurstPlanner for page splitting
            if (stride == elementSize)
            {
                ulong totalRead = 0;
                var backend = GetBackend(memSub);

                foreach (var segment in BurstPlanner.Plan(baseAddr, totalBytes))
                {
                    Span<byte> slice = buffer.Slice((int)totalRead, segment.Length);

                    if (!backend.Read(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstRead),
                            baseAddr,
                            elementCount,
                            totalRead / (ulong)elementSize,
                            elementSize);
                    }

                    totalRead += (ulong)segment.Length;
                }

                ulong completedElements = totalRead / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstRead),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // For strided access, fall back to element-by-element reads
            // Future optimization: hardware scatter-gather DMA
            ulong elementsRead = 0;
            ulong bufferOffset = 0;
            var stridedBackend = GetBackend(memSub);

            for (ulong i = 0; i < elementCount; i++)
            {
                ulong elemAddr = baseAddr + (i * stride);
                Span<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);

                if (!stridedBackend.Read(CPU_DEVICE_ID, elemAddr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstRead),
                        baseAddr,
                        elementCount,
                        elementsRead,
                        elementSize);
                }

                bufferOffset += (ulong)elementSize;
                elementsRead++;
            }

            return elementsRead;
        }

        /// <summary>
        /// Read elements via DMA Controller for large transfers.
        /// Current ISE helper drives completion synchronously by calling
        /// DMAController.ExecuteCycle() before returning.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong BurstReadViaDMA(ulong baseAddr, Span<byte> buffer, ulong elementCount, int elementSize)
        {
            YAKSys_Hybrid_CPU.Memory.DMAController? dmaController = Processor.DMAController;
            if (dmaController == null) return 0;

            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();

            ulong totalBytes = elementCount * (ulong)elementSize;
            if (totalBytes > uint.MaxValue || totalBytes > int.MaxValue) return 0; // DMA transfer size limit

            // Allocate temporary physical buffer for DMA transfer
            int totalByteCount = checked((int)totalBytes);
            byte[] tempBuffer = new byte[totalByteCount];

            // Track completion status
            bool transferComplete = false;
            bool transferSuccess = false;

            // Create DMA descriptor with completion callback
            var descriptor = new YAKSys_Hybrid_CPU.Memory.DMAController.TransferDescriptor
            {
                SourceAddress = baseAddr,
                DestAddress = 0, // Will use temporary buffer
                TransferSize = (uint)totalBytes,
                SourceStride = 0, // Packed
                DestStride = 0, // Packed
                ElementSize = (byte)elementSize,
                UseIOMMU = true,
                NextDescriptor = 0,
                ChannelID = 0, // Use channel 0 for StreamEngine read transfers
                Priority = 128 // Medium priority
            };

            // Configure and start DMA transfer with callback
            if (!dmaController.ConfigureTransfer(descriptor, (channelID, success, errorCode) =>
            {
                transferSuccess = success;
                transferComplete = true;
            }))
                return 0; // Channel busy

            if (!dmaController.StartTransfer(0))
                return 0; // Failed to start

            // Execute DMA cycles synchronously until transfer completes.
            // This helper does not define architectural CPU/DMA overlap.
            int maxCycles = 10000; // Safety limit
            int cycles = 0;
            while (!transferComplete && cycles < maxCycles)
            {
                dmaController.ExecuteCycle();
                cycles++;
            }

            // Check if transfer completed successfully
            if (!transferComplete || !transferSuccess)
            {
                dmaController.ResetChannel(0);
                return 0; // Transfer failed or timed out
            }

            // Read the data from memory (DMA has completed the transfer)
            if (TryReadBoundMainMemoryExact(mainMemory, baseAddr, tempBuffer))
            {
                // Copy from temporary buffer to destination span
                tempBuffer.AsSpan(0, totalByteCount).CopyTo(buffer);

                // Reset channel for next transfer
                dmaController.ResetChannel(0);

                return elementCount;
            }

            // Reset channel on failure
            dmaController.ResetChannel(0);
            return 0;
        }

        /// <summary>
        /// Write elements to memory using AXI4-compliant bursts.
        /// Automatically splits at 4KB boundaries and respects burst length limits.
        /// Large transfers (> DMA_THRESHOLD) are routed through DMA Controller for offloading.
        ///
        /// Refactored (ref2.md): Uses BurstPlanner for page-split logic and IBurstBackend for I/O.
        /// </summary>
        /// <param name="baseAddr">Base IO virtual address</param>
        /// <param name="buffer">Source buffer</param>
        /// <param name="elementCount">Number of elements to write</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="stride">Byte stride between elements (0 = packed/contiguous)</param>
        /// <returns>Number of elements successfully written</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstWrite(ulong baseAddr, ReadOnlySpan<byte> buffer, ulong elementCount, int elementSize, ushort stride,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 || elementSize <= 0) return 0;

            // Default to packed stride if not specified
            if (stride == 0) stride = (ushort)elementSize;

            // Calculate total transfer size
            ulong totalBytes = elementCount * (ulong)elementSize;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;

            // For large contiguous transfers above DMA threshold, use DMA Controller
            if (stride == elementSize && totalBytes > DMA_THRESHOLD && Processor.DMAController != null)
            {
                ulong completedElements = BurstWriteViaDMA(baseAddr, buffer, elementCount, elementSize);
                ThrowIfIncompleteTransfer(
                    nameof(BurstWrite),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                if (completedElements == elementCount)
                {
                    InvalidatePrefetchedSrfWindow(baseAddr, totalBytes, srf);
                }
                return completedElements;
            }

            // For contiguous packed transfers, use BurstPlanner for page splitting
            if (stride == elementSize)
            {
                ulong totalWritten = 0;
                var backend = GetBackend(memSub);

                foreach (var segment in BurstPlanner.Plan(baseAddr, totalBytes))
                {
                    ReadOnlySpan<byte> slice = buffer.Slice((int)totalWritten, segment.Length);

                    if (!backend.Write(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstWrite),
                            baseAddr,
                            elementCount,
                            totalWritten / (ulong)elementSize,
                            elementSize);
                    }

                    InvalidatePrefetchedSrfWindow(segment.Address, (ulong)segment.Length, srf);
                    totalWritten += (ulong)segment.Length;
                }

                ulong completedElements = totalWritten / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstWrite),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // For strided access, fall back to element-by-element writes
            ulong elementsWritten = 0;
            ulong bufferOffset = 0;
            var stridedBackend = GetBackend(memSub);

            for (ulong i = 0; i < elementCount; i++)
            {
                ulong elemAddr = baseAddr + (i * stride);
                ReadOnlySpan<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);

                if (!stridedBackend.Write(CPU_DEVICE_ID, elemAddr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstWrite),
                        baseAddr,
                        elementCount,
                        elementsWritten,
                        elementSize);
                }

                InvalidatePrefetchedSrfWindow(elemAddr, (ulong)elementSize, srf);
                bufferOffset += (ulong)elementSize;
                elementsWritten++;
            }

            return elementsWritten;
        }

        /// <summary>
        /// Write elements via DMA Controller for large transfers.
        /// Current ISE helper writes the destination surface first, then drives
        /// DMA bookkeeping synchronously before returning.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong BurstWriteViaDMA(ulong baseAddr, ReadOnlySpan<byte> buffer, ulong elementCount, int elementSize)
        {
            YAKSys_Hybrid_CPU.Memory.DMAController? dmaController = Processor.DMAController;
            if (dmaController == null) return 0;

            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();

            ulong totalBytes = elementCount * (ulong)elementSize;
            if (totalBytes > uint.MaxValue || totalBytes > int.MaxValue) return 0; // DMA transfer size limit

            // Copy from source span to temporary buffer for DMA
            int totalByteCount = checked((int)totalBytes);
            byte[] tempBuffer = new byte[totalByteCount];
            buffer.Slice(0, totalByteCount).CopyTo(tempBuffer);

            // Write to memory first (DMA source)
            if (!TryWriteBoundMainMemoryExact(mainMemory, baseAddr, tempBuffer))
            {
                return 0;
            }

            // Track completion status
            bool transferComplete = false;

            // Create DMA descriptor with completion callback
            var descriptor = new YAKSys_Hybrid_CPU.Memory.DMAController.TransferDescriptor
            {
                SourceAddress = baseAddr,
                DestAddress = baseAddr, // Memory-to-memory
                TransferSize = (uint)totalBytes,
                SourceStride = 0, // Packed
                DestStride = 0, // Packed
                ElementSize = (byte)elementSize,
                UseIOMMU = true,
                NextDescriptor = 0,
                ChannelID = 1, // Use channel 1 for StreamEngine write transfers
                Priority = 128 // Medium priority
            };

            // Configure and start DMA transfer with callback
            if (!dmaController.ConfigureTransfer(descriptor, (channelID, success, errorCode) =>
            {
                transferComplete = true;
            }))
            {
                // Channel busy, but data already written
                return elementCount;
            }

            if (!dmaController.StartTransfer(1))
            {
                dmaController.ResetChannel(1);
                return elementCount; // Data already written
            }

            // Execute DMA cycles synchronously until transfer completes.
            // This helper does not define architectural CPU/DMA overlap.
            int maxCycles = 10000; // Safety limit
            int cycles = 0;
            while (!transferComplete && cycles < maxCycles)
            {
                dmaController.ExecuteCycle();
                cycles++;
            }

            // Reset channel for next transfer
            dmaController.ResetChannel(1);

            // The current emulation transport already committed the source span to the
            // destination memory surface above, so post-start DMA bookkeeping must not
            // reclassify that architectural write as a zero-element failure.
            return elementCount;
        }

        /// <summary>
        /// Read elements with 2D addressing pattern.
        /// Optimized: Uses BurstPlanner.Plan2D for efficient row-based burst planning.
        ///
        /// Optimization (per problem statement):
        /// - For contiguous rows (colStride == elementSize), uses burst planning per row
        /// - Reduces number of backend calls from O(rows*cols) to O(rows*segments_per_row)
        /// - Fully contiguous 2D regions treated as single 1D burst
        ///
        /// Refactored (ref2.md): Uses IBurstBackend for I/O operations.
        /// </summary>
        /// <param name="baseAddr">Base IO virtual address</param>
        /// <param name="buffer">Destination buffer</param>
        /// <param name="elementCount">Total number of elements (rows * cols)</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="rowLength">Number of elements per row</param>
        /// <param name="rowStride">Byte pitch between rows</param>
        /// <param name="colStride">Byte stride between columns</param>
        /// <param name="startOffset">Starting linear element index (for strip-mining)</param>
        /// <returns>Number of elements successfully read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstRead2D(ulong baseAddr, Span<byte> buffer, ulong elementCount, int elementSize,
                                         uint rowLength, ushort rowStride, ushort colStride, ulong startOffset = 0,
                                         YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            ThrowIfUnsupportedZeroRowLength2DTransferContour(
                nameof(BurstRead2D),
                elementCount,
                rowLength);

            if (elementCount == 0 || elementSize <= 0) return 0;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;
            var backend = GetBackend(memSub);

            // Check if we can use optimized burst planning
            if (colStride == elementSize)
            {
                // Columns within rows are contiguous - use burst planning
                ulong totalRead = 0;

                foreach (var segment in BurstPlanner.Plan2D(baseAddr + (startOffset * colStride),
                                                            elementCount, elementSize, rowLength, rowStride, colStride))
                {
                    Span<byte> slice = buffer.Slice((int)totalRead, segment.Length);

                    ulong segmentElements = (ulong)segment.Length / (ulong)elementSize;
                    if (!TryConsumePrefetchedSrfChunk(
                            segment.Address,
                            slice,
                            segmentElements,
                            elementSize,
                            (ushort)elementSize,
                            srf) &&
                        !backend.Read(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstRead2D),
                            baseAddr,
                            elementCount,
                            totalRead / (ulong)elementSize,
                            elementSize);
                    }

                    totalRead += (ulong)segment.Length;
                }

                ulong completedElements = totalRead / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstRead2D),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // Fall back to element-by-element for non-contiguous columns
            ulong elementsRead = 0;
            ulong bufferOffset = 0;

            for (ulong i = 0; i < elementCount; i++)
            {
                ulong linearIdx = startOffset + i;
                ulong addr = AddressGen.Gen2D(baseAddr, linearIdx, rowLength, rowStride, colStride);
                Span<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);

                if (!backend.Read(CPU_DEVICE_ID, addr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstRead2D),
                        baseAddr,
                        elementCount,
                        elementsRead,
                        elementSize);
                }

                bufferOffset += (ulong)elementSize;
                elementsRead++;
            }

            return elementsRead;
        }

        /// <summary>
        /// Write elements with 2D addressing pattern.
        /// Optimized: Uses BurstPlanner.Plan2D for efficient row-based burst planning.
        ///
        /// Optimization (per problem statement):
        /// - For contiguous rows (colStride == elementSize), uses burst planning per row
        /// - Reduces number of backend calls from O(rows*cols) to O(rows*segments_per_row)
        /// - Fully contiguous 2D regions treated as single 1D burst
        ///
        /// Refactored (ref2.md): Uses IBurstBackend for I/O operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstWrite2D(ulong baseAddr, ReadOnlySpan<byte> buffer, ulong elementCount, int elementSize,
                                          uint rowLength, ushort rowStride, ushort colStride, ulong startOffset = 0,
                                          YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            ThrowIfUnsupportedZeroRowLength2DTransferContour(
                nameof(BurstWrite2D),
                elementCount,
                rowLength);

            if (elementCount == 0 || elementSize <= 0) return 0;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;
            var backend = GetBackend(memSub);

            // Check if we can use optimized burst planning
            if (colStride == elementSize)
            {
                // Columns within rows are contiguous - use burst planning
                ulong totalWritten = 0;

                foreach (var segment in BurstPlanner.Plan2D(baseAddr + (startOffset * colStride),
                                                            elementCount, elementSize, rowLength, rowStride, colStride))
                {
                    ReadOnlySpan<byte> slice = buffer.Slice((int)totalWritten, segment.Length);

                    if (!backend.Write(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstWrite2D),
                            baseAddr,
                            elementCount,
                            totalWritten / (ulong)elementSize,
                            elementSize);
                    }

                    InvalidatePrefetchedSrfWindow(segment.Address, (ulong)segment.Length, srf);
                    totalWritten += (ulong)segment.Length;
                }

                ulong completedElements = totalWritten / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstWrite2D),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // Fall back to element-by-element for non-contiguous columns
            ulong elementsWritten = 0;
            ulong bufferOffset = 0;

            for (ulong i = 0; i < elementCount; i++)
            {
                ulong linearIdx = startOffset + i;
                ulong addr = AddressGen.Gen2D(baseAddr, linearIdx, rowLength, rowStride, colStride);
                ReadOnlySpan<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);

                if (!backend.Write(CPU_DEVICE_ID, addr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstWrite2D),
                        baseAddr,
                        elementCount,
                        elementsWritten,
                        elementSize);
                }

                InvalidatePrefetchedSrfWindow(addr, (ulong)elementSize, srf);
                bufferOffset += (ulong)elementSize;
                elementsWritten++;
            }

            return elementsWritten;
        }

        /// <summary>
        /// Gather elements from memory using index array (hardware-optimized scatter-gather).
        /// Reads elements from non-contiguous addresses specified by indices.
        ///
        /// Optimization (per problem statement):
        /// - For large index arrays with contiguous patterns, uses BurstPlanner.PlanIndexed
        /// - Groups contiguous accesses into bursts, reducing backend calls
        /// - Falls back to element-by-element for small or highly fragmented accesses
        ///
        /// Refactored (ref2.md): Uses IBurstBackend for I/O operations.
        /// </summary>
        /// <param name="baseAddr">Base address of source array</param>
        /// <param name="buffer">Destination buffer</param>
        /// <param name="elementCount">Number of elements to gather</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="indices">Buffer containing indices</param>
        /// <param name="indexSize">Size of each index (4 or 8 bytes)</param>
        /// <param name="indexIsByteOffset">True if indices are byte offsets, false if element indices</param>
        /// <returns>Number of elements successfully gathered</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstGather(ulong baseAddr, Span<byte> buffer, ulong elementCount, int elementSize,
                                        ReadOnlySpan<byte> indices, int indexSize, byte indexIsByteOffset,
                                        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 || elementSize <= 0) return 0;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;
            var backend = GetBackend(memSub);

            // For larger index counts, try to optimize with burst planning
            if (elementCount >= 8)
            {
                // Extract indices into ulong array for analysis
                ulong[] indexArray = new ulong[elementCount];
                for (ulong i = 0; i < elementCount; i++)
                {
                    if (indexSize == 4)
                    {
                        indexArray[i] = BitConverter.ToUInt32(indices.Slice((int)(i * 4), 4));
                    }
                    else // indexSize == 8
                    {
                        indexArray[i] = BitConverter.ToUInt64(indices.Slice((int)(i * 8), 8));
                    }
                }

                // Use optimized burst planning for indexed access
                ulong totalRead = 0;
                foreach (var segment in BurstPlanner.PlanIndexed(baseAddr, elementSize, indexArray, indexIsByteOffset != 0))
                {
                    Span<byte> slice = buffer.Slice((int)totalRead, segment.Length);

                    ulong segmentElements = (ulong)segment.Length / (ulong)elementSize;
                    if (!TryConsumePrefetchedSrfChunk(
                            segment.Address,
                            slice,
                            segmentElements,
                            elementSize,
                            (ushort)elementSize,
                            srf) &&
                        !backend.Read(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstGather),
                            baseAddr,
                            elementCount,
                            totalRead / (ulong)elementSize,
                            elementSize);
                    }

                    totalRead += (ulong)segment.Length;
                }

                ulong completedElements = totalRead / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstGather),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // Fall back to element-by-element for small accesses
            ulong elementsRead = 0;
            ulong bufferOffset = 0;

            for (ulong i = 0; i < elementCount; i++)
            {
                // Read index value
                ulong index;
                if (indexSize == 4)
                {
                    index = BitConverter.ToUInt32(indices.Slice((int)(i * 4), 4));
                }
                else // indexSize == 8
                {
                    index = BitConverter.ToUInt64(indices.Slice((int)(i * 8), 8));
                }

                // Calculate element address
                ulong elemAddr;
                if (indexIsByteOffset != 0)
                {
                    // Index is byte offset
                    elemAddr = baseAddr + index;
                }
                else
                {
                    // Index is element index
                    elemAddr = baseAddr + (index * (ulong)elementSize);
                }

                // Read element
                Span<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);
                if (!backend.Read(CPU_DEVICE_ID, elemAddr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstGather),
                        baseAddr,
                        elementCount,
                        elementsRead,
                        elementSize);
                }

                bufferOffset += (ulong)elementSize;
                elementsRead++;
            }

            return elementsRead;
        }

        /// <summary>
        /// Scatter elements to memory using index array (hardware-optimized scatter-gather).
        /// Writes elements to non-contiguous addresses specified by indices.
        ///
        /// Optimization (per problem statement):
        /// - For large index arrays with contiguous patterns, uses BurstPlanner.PlanIndexed
        /// - Groups contiguous accesses into bursts, reducing backend calls
        /// - Falls back to element-by-element for small or highly fragmented accesses
        ///
        /// Refactored (ref2.md): Uses IBurstBackend for I/O operations.
        /// </summary>
        /// <param name="baseAddr">Base address of destination array</param>
        /// <param name="buffer">Source buffer</param>
        /// <param name="elementCount">Number of elements to scatter</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="indices">Buffer containing indices</param>
        /// <param name="indexSize">Size of each index (4 or 8 bytes)</param>
        /// <param name="indexIsByteOffset">True if indices are byte offsets, false if element indices</param>
        /// <returns>Number of elements successfully scattered</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BurstScatter(ulong baseAddr, ReadOnlySpan<byte> buffer, ulong elementCount, int elementSize,
                                         ReadOnlySpan<byte> indices, int indexSize, byte indexIsByteOffset,
                                         YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 || elementSize <= 0) return 0;

            var srf = ResolveActiveMemorySubsystem(memSub)?.StreamRegisters;
            var backend = GetBackend(memSub);

            // For larger index counts, try to optimize with burst planning
            if (elementCount >= 8)
            {
                // Extract indices into ulong array for analysis
                ulong[] indexArray = new ulong[elementCount];
                for (ulong i = 0; i < elementCount; i++)
                {
                    if (indexSize == 4)
                    {
                        indexArray[i] = BitConverter.ToUInt32(indices.Slice((int)(i * 4), 4));
                    }
                    else // indexSize == 8
                    {
                        indexArray[i] = BitConverter.ToUInt64(indices.Slice((int)(i * 8), 8));
                    }
                }

                // Use optimized burst planning for indexed access
                ulong totalWritten = 0;
                foreach (var segment in BurstPlanner.PlanIndexed(baseAddr, elementSize, indexArray, indexIsByteOffset != 0))
                {
                    ReadOnlySpan<byte> slice = buffer.Slice((int)totalWritten, segment.Length);

                    if (!backend.Write(CPU_DEVICE_ID, segment.Address, slice))
                    {
                        ThrowIfIncompleteTransfer(
                            nameof(BurstScatter),
                            baseAddr,
                            elementCount,
                            totalWritten / (ulong)elementSize,
                            elementSize);
                    }

                    InvalidatePrefetchedSrfWindow(segment.Address, (ulong)segment.Length, srf);
                    totalWritten += (ulong)segment.Length;
                }

                ulong completedElements = totalWritten / (ulong)elementSize;
                ThrowIfIncompleteTransfer(
                    nameof(BurstScatter),
                    baseAddr,
                    elementCount,
                    completedElements,
                    elementSize);
                return completedElements;
            }

            // Fall back to element-by-element for small accesses
            ulong elementsWritten = 0;
            ulong bufferOffset = 0;

            for (ulong i = 0; i < elementCount; i++)
            {
                // Read index value
                ulong index;
                if (indexSize == 4)
                {
                    index = BitConverter.ToUInt32(indices.Slice((int)(i * 4), 4));
                }
                else // indexSize == 8
                {
                    index = BitConverter.ToUInt64(indices.Slice((int)(i * 8), 8));
                }

                // Calculate element address
                ulong elemAddr;
                if (indexIsByteOffset != 0)
                {
                    // Index is byte offset
                    elemAddr = baseAddr + index;
                }
                else
                {
                    // Index is element index
                    elemAddr = baseAddr + (index * (ulong)elementSize);
                }

                // Write element
                ReadOnlySpan<byte> elemSlice = buffer.Slice((int)bufferOffset, elementSize);
                if (!backend.Write(CPU_DEVICE_ID, elemAddr, elemSlice))
                {
                    ThrowIfIncompleteTransfer(
                        nameof(BurstScatter),
                        baseAddr,
                        elementCount,
                        elementsWritten,
                        elementSize);
                }

                InvalidatePrefetchedSrfWindow(elemAddr, (ulong)elementSize, srf);
                bufferOffset += (ulong)elementSize;
                elementsWritten++;
            }

            return elementsWritten;
        }
    }
}
