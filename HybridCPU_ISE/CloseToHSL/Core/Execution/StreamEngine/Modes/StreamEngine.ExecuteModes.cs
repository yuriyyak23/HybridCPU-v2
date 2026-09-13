
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using HybridCPU_ISE.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class StreamEngine
    {
        private static int ResolveExecutionVtId(ref Processor.CPU_Core core, int ownerThreadId)
        {
            if ((uint)ownerThreadId < (uint)Processor.CPU_Core.SmtWays)
                return ownerThreadId;

            return core.ReadActiveVirtualThreadId();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolvePreferredStreamChunkShape(
            ulong remaining,
            int elementSize,
            int scratchByteCapacity,
            ushort stride)
        {
            if (remaining == 0 || elementSize <= 0)
            {
                return 0;
            }

            ulong chunkElements = Math.Min(remaining, 32UL);
            ulong scratchBudget = (ulong)Math.Max(1, scratchByteCapacity / elementSize);
            chunkElements = Math.Min(chunkElements, scratchBudget);

            if (stride == elementSize)
            {
                uint srfBudget = ResolveSrfResidentChunkBudget(elementSize, chunkElements);
                if (srfBudget != 0)
                {
                    chunkElements = Math.Min(chunkElements, (ulong)srfBudget);
                }
            }

            return Math.Max(1UL, chunkElements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolvePreferredStreamChunkShape(
            ulong remaining,
            int elementSize,
            int scratchByteCapacity,
            uint rowLength,
            ushort rowStride,
            ushort colStride,
            ulong elementsProcessed)
        {
            ulong chunkElements = ResolvePreferredStreamChunkShape(
                remaining,
                elementSize,
                scratchByteCapacity,
                colStride);
            if (chunkElements == 0 ||
                rowLength == 0 ||
                colStride != elementSize)
            {
                return chunkElements;
            }

            ulong fullyContiguousRowBytes = (ulong)rowLength * (ulong)elementSize;
            if ((ulong)rowStride != fullyContiguousRowBytes)
            {
                ulong rowOffset = elementsProcessed % rowLength;
                ulong rowRemaining = rowLength - rowOffset;
                chunkElements = Math.Min(chunkElements, rowRemaining);
            }

            return Math.Max(1UL, chunkElements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolvePreferredStreamChunkShape(
            ulong remaining,
            int elementSize,
            int scratchByteCapacity,
            int indexElementSize,
            int scratchIndexByteCapacity,
            ushort indexStride)
        {
            ulong chunkElements = ResolvePreferredStreamChunkShape(
                remaining,
                elementSize,
                scratchByteCapacity,
                (ushort)elementSize);
            if (chunkElements == 0 || indexElementSize <= 0)
            {
                return chunkElements;
            }

            ulong indexScratchBudget = (ulong)Math.Max(1, scratchIndexByteCapacity / indexElementSize);
            chunkElements = Math.Min(chunkElements, indexScratchBudget);

            if (indexStride == indexElementSize)
            {
                uint indexSrfBudget = ResolveSrfResidentChunkBudget(indexElementSize, chunkElements);
                if (indexSrfBudget != 0)
                {
                    chunkElements = Math.Min(chunkElements, (ulong)indexSrfBudget);
                }
            }

            return Math.Max(1UL, chunkElements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryApplyIngressWarmPolicy(
            ulong destPtr,
            ulong src2Ptr,
            int elementSize,
            ulong chunkElements,
            ushort stride,
            bool isBinary)
        {
            if (chunkElements == 0 ||
                elementSize <= 0 ||
                stride != elementSize ||
                chunkElements > uint.MaxValue)
            {
                return;
            }

            uint exactChunkElements = (uint)chunkElements;
            PrefetchToStreamRegister(destPtr, (byte)elementSize, exactChunkElements);
            if (isBinary)
            {
                PrefetchToStreamRegister(src2Ptr, (byte)elementSize, exactChunkElements);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryApplyIngressWarmPolicy(
            ulong baseAddr,
            int elementSize,
            ulong chunkElements,
            uint rowLength,
            ushort rowStride,
            ushort colStride,
            ulong startOffset)
        {
            if (chunkElements == 0)
            {
                return;
            }

            TryWarmPlanned2DChunk(
                baseAddr,
                elementSize,
                chunkElements,
                rowLength,
                rowStride,
                colStride,
                startOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryApplyIngressWarmPolicy(
            ulong destBase,
            ulong indexBase,
            int elementSize,
            int indexElementSize,
            ushort indexStride,
            ulong startElement,
            ulong chunkElements)
        {
            if (chunkElements == 0)
            {
                return;
            }

            TryWarmIndexedIngressChunk(
                destBase,
                indexBase,
                elementSize,
                indexElementSize,
                indexStride,
                startElement,
                chunkElements);
        }

        /// <summary>
        /// Execute binary vector operation with double-buffered scratch.
        /// Models overlapped load→compute→store pipeline:
        ///   Cycle N  : LOAD chunk[N] → active buffer
        ///   Cycle N+1: COMPUTE active | LOAD chunk[N+1] → inactive
        ///   Cycle N+2: STORE active   | COMPUTE inactive | LOAD chunk[N+2] → active
        /// In single-threaded emulation the overlap is modelled by counter arithmetic:
        ///   OverlappedCycles += min(loadLatency, computeLatency) per iteration.
        /// Result is bit-identical to Execute1D (correctness invariant).
        /// Only binary ops go through this path; special ops (FMA, reduction, etc.)
        /// continue to use the single-buffer Execute1D.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Execute1D_DoubleBuffered(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength)
        {
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ulong ptrSrc2 = request.Src2Pointer;
            ushort stride = request.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            // Latency model constants (matching FSM timing, Appendix A.3)
            const ulong MEMORY_LATENCY_PER_BURST = 4;  // cycles per burst read/write
            const ulong COMPUTE_LATENCY_BASE = 1;      // 1 cycle for simple ALU

            // Local accumulators — flushed to core at the end
            ulong accReadCycles = 0;
            ulong accWriteCycles = 0;
            ulong accComputeCycles = 0;
            ulong accOverlappedCycles = 0;

            bool isBinary = IsBinaryOp(opCode);

            ulong remaining = streamLength;

            // ---------- Iteration 0: initial load into active buffer ----------
            Span<byte> bufA = core.GetActiveScratchA();
            Span<byte> bufB = core.GetActiveScratchB();
            Span<byte> bufDst = core.GetActiveScratchDst();

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "double-buffered raw 1D stream/vector execution",
                requiresScratchB: isBinary,
                scratchALength: bufA.Length,
                scratchBLength: bufB.Length,
                scratchDstLength: bufDst.Length);

            ulong vl = ResolvePreferredStreamChunkShape(
                remaining,
                elemSize,
                bufA.Length,
                stride);
            ulong bytes = vl * (ulong)elemSize;

            BurstIO.BurstRead(ptrDestSrc1, bufA.Slice(0, (int)bytes), vl, elemSize, stride);
            accReadCycles += MEMORY_LATENCY_PER_BURST;

            if (isBinary)
            {
                BurstIO.BurstRead(ptrSrc2, bufB.Slice(0, (int)bytes), vl, elemSize, stride);
                accReadCycles += MEMORY_LATENCY_PER_BURST;
            }

            ulong curPtrDst = ptrDestSrc1;
            ulong curPtrSrc2 = ptrSrc2;

            while (remaining > 0)
            {
                ulong curVl = ResolvePreferredStreamChunkShape(
                    remaining,
                    elemSize,
                    bufA.Length,
                    stride);
                ulong curBytes = curVl * (ulong)elemSize;

                // ── Compute on current active buffer ──
                Span<byte> cA = bufA.Slice(0, (int)curBytes);
                Span<byte> cDst = bufDst.Slice(0, (int)curBytes);

                if (isBinary)
                {
                    Span<byte> cB = bufB.Slice(0, (int)curBytes);
                    VectorALU.ApplyBinary(opCode, dataType, cA, cB, cDst, elemSize, curVl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);
                }
                else
                {
                    VectorALU.ApplyUnary(opCode, dataType, cA, cDst, elemSize, curVl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);
                }
                accComputeCycles += COMPUTE_LATENCY_BASE;

                // ── Prefetch next chunk into INACTIVE buffer (if remaining) ──
                ulong nextPtrDst = curPtrDst + (ulong)stride * curVl;
                ulong nextPtrSrc2 = curPtrSrc2 + (ulong)stride * curVl;
                ulong nextRemaining = remaining - curVl;
                ulong prefetchLoadCycles = 0;

                if (nextRemaining > 0)
                {
                    core.ToggleDoubleBuffer();
                    Span<byte> nextA = core.GetActiveScratchA();
                    Span<byte> nextB = core.GetActiveScratchB();

                    ulong nextVl = ResolvePreferredStreamChunkShape(
                        nextRemaining,
                        elemSize,
                        nextA.Length,
                        stride);
                    ulong nextBytes = nextVl * (ulong)elemSize;

                    TryApplyIngressWarmPolicy(
                        nextPtrDst,
                        nextPtrSrc2,
                        elemSize,
                        nextVl,
                        stride,
                        isBinary);
                    BurstIO.BurstRead(nextPtrDst, nextA.Slice(0, (int)nextBytes), nextVl, elemSize, stride);
                    prefetchLoadCycles += MEMORY_LATENCY_PER_BURST;

                    if (isBinary)
                    {
                        BurstIO.BurstRead(nextPtrSrc2, nextB.Slice(0, (int)nextBytes), nextVl, elemSize, stride);
                        prefetchLoadCycles += MEMORY_LATENCY_PER_BURST;
                    }

                    accReadCycles += prefetchLoadCycles;

                    // Overlap model: prefetch happens in parallel with compute of current chunk
                    accOverlappedCycles += Math.Min(prefetchLoadCycles, COMPUTE_LATENCY_BASE);
                }

                // ── Store result from current active buffer ──
                BurstIO.BurstWrite(curPtrDst, cDst, curVl, elemSize, stride);
                accWriteCycles += MEMORY_LATENCY_PER_BURST;

                // Advance
                curPtrDst = nextPtrDst;
                curPtrSrc2 = nextPtrSrc2;
                remaining = nextRemaining;

                // Swap active/inactive
                if (remaining > 0)
                {
                    bufA = core.GetActiveScratchA();
                    bufB = core.GetActiveScratchB();
                    bufDst = core.GetActiveScratchDst();
                }
            }

            // Flush accumulated counters to core
            core.AccumulateOverlapCounters(accReadCycles, accWriteCycles, accComputeCycles, accOverlappedCycles);
        }

        /// <summary>
        /// Execute vector operation with 2D addressing pattern.
        /// Uses BurstRead2D/Write2D for row-major access with row and column strides.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Execute2D(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength)
        {
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ulong ptrSrc2 = request.Src2Pointer;
            ushort colStride = request.Stride;
            ushort rowStride = request.RowStride;
            uint rowLength = request.Immediate; // Number of elements per row

            ThrowIfUnsupportedZeroRowLength2DContour(
                opCode,
                streamLength,
                rowLength,
                "raw 2D stream/vector execution");

            // Default strides
            if (colStride == 0) colStride = (ushort)elemSize;
            if (rowStride == 0) rowStride = (ushort)(colStride * rowLength);

            // Get scratch buffers from core
            Span<byte> scratchA = core.GetScratchA();
            Span<byte> scratchB = core.GetScratchB();
            Span<byte> scratchDst = core.GetScratchDst();
            bool isBinary = IsBinaryOp(opCode);

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "raw 2D stream/vector execution",
                requiresScratchB: isBinary,
                scratchALength: scratchA.Length,
                scratchBLength: scratchB.Length,
                scratchDstLength: scratchDst.Length);

            ulong remaining = streamLength;
            ulong elementsProcessed = 0;

            // Strip-mining loop: process VLMAX elements per iteration
            while (remaining > 0)
            {
                ulong vl = ResolvePreferredStreamChunkShape(
                    remaining,
                    elemSize,
                    scratchA.Length,
                    rowLength,
                    rowStride,
                    colStride,
                    elementsProcessed);

                if (isBinary)
                {
                    // Read both operands using 2D addressing
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read with 2D pattern (dest/src1)
                    BurstIO.BurstRead2D(ptrDestSrc1, bufA, vl, elemSize, rowLength, rowStride, colStride, elementsProcessed);

                    // Burst read with 2D pattern (src2)
                    BurstIO.BurstRead2D(ptrSrc2, bufB, vl, elemSize, rowLength, rowStride, colStride, elementsProcessed);

                    // Execute ALU operation
                    VectorALU.ApplyBinary(opCode, dataType, bufA, bufB, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Burst write result with 2D pattern
                    BurstIO.BurstWrite2D(ptrDestSrc1, bufDst, vl, elemSize, rowLength, rowStride, colStride, elementsProcessed);
                }
                else
                {
                    // Unary operation: read only one operand
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read with 2D pattern
                    BurstIO.BurstRead2D(ptrDestSrc1, bufA, vl, elemSize, rowLength, rowStride, colStride, elementsProcessed);

                    // Execute ALU operation
                    VectorALU.ApplyUnary(opCode, dataType, bufA, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Burst write result with 2D pattern
                    BurstIO.BurstWrite2D(ptrDestSrc1, bufDst, vl, elemSize, rowLength, rowStride, colStride, elementsProcessed);
                }

                ulong nextRemaining = remaining - vl;
                if (nextRemaining > 0)
                {
                    ulong nextVl = ResolvePreferredStreamChunkShape(
                        nextRemaining,
                        elemSize,
                        scratchA.Length,
                        rowLength,
                        rowStride,
                        colStride,
                        elementsProcessed + vl);
                    ulong nextOffset = elementsProcessed + vl;
                    TryApplyIngressWarmPolicy(
                        ptrDestSrc1,
                        elemSize,
                        nextVl,
                        rowLength,
                        rowStride,
                        colStride,
                        nextOffset);

                    if (isBinary)
                    {
                        TryApplyIngressWarmPolicy(
                            ptrSrc2,
                            elemSize,
                            nextVl,
                            rowLength,
                            rowStride,
                            colStride,
                            nextOffset);
                    }
                }

                // Update remaining count
                elementsProcessed += vl;
                remaining -= vl;
            }
        }

        /// <summary>
        /// Execute vector operation with indexed addressing (gather/scatter).
        /// Reads descriptor from memory to get source addresses and index array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExecuteIndexed(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength)
        {
            if (opCode is
                (uint)Processor.CPU_Core.InstructionsEnum.VGATHER or
                (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER)
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached indexed StreamEngine.Execute(...) on a non-representable raw gather/scatter contour. " +
                    "Legacy indexed fallback must fail closed for VGATHER/VSCATTER instead of publishing hidden success on a surface that still has no authoritative mainline MicroOp.");
            }

            // Word1: destination/source1 base address (for output)
            // Word2: descriptor address (points to Indexed2SrcDesc)
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ulong descriptorAddr = request.Src2Pointer;

            // Read descriptor from memory
            Span<byte> descBuffer = stackalloc byte[32]; // Indexed2SrcDesc is ~20 bytes, allocate 32 for safety
            if (!YAKSys_Hybrid_CPU.Memory.IOMMU.ReadBurst(0, descriptorAddr, descBuffer))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached indexed StreamEngine.Execute(...) contour with unreadable descriptor at 0x{descriptorAddr:X}. " +
                    "Indexed mainline execution must fail closed instead of collapsing into hidden success/no-op when descriptor fetch cannot be materialized.");
            }

            // Parse descriptor (manual parsing for HLS compatibility)
            ulong src2Base = BitConverter.ToUInt64(descBuffer.Slice(0, 8));
            ulong indexBase = BitConverter.ToUInt64(descBuffer.Slice(8, 8));
            ushort indexStride = BitConverter.ToUInt16(descBuffer.Slice(16, 2));
            byte indexType = descBuffer[18]; // 0=uint32, 1=uint64
            byte indexIsByteOffset = descBuffer[19]; // 0=element index, 1=byte offset

            int indexElemSize = (indexType == 0) ? 4 : 8; // uint32 or uint64
            if (indexStride == 0) indexStride = (ushort)indexElemSize;

            // Get scratch buffers from core
            Span<byte> scratchA = core.GetScratchA();
            Span<byte> scratchB = core.GetScratchB();
            Span<byte> scratchDst = core.GetScratchDst();
            Span<byte> scratchIndex = core.GetScratchIndex(); // Additional buffer for indices
            bool isBinary = IsBinaryOp(opCode);

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "raw indexed stream/vector execution",
                requiresScratchB: isBinary,
                scratchALength: scratchA.Length,
                scratchBLength: scratchB.Length,
                scratchDstLength: scratchDst.Length,
                scratchIndexLength: scratchIndex.Length);

            ulong remaining = streamLength;
            ulong elementsProcessed = 0;

            // Strip-mining loop: process VLMAX elements per iteration
            while (remaining > 0)
            {
                ulong vl = ResolvePreferredStreamChunkShape(
                    remaining,
                    elemSize,
                    scratchA.Length,
                    indexElemSize,
                    scratchIndex.Length,
                    indexStride);

                if (isBinary)
                {
                    // Read operands using indexed addressing
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Read destination/src1 (sequential access)
                    BurstIO.BurstRead(ptrDestSrc1 + (elementsProcessed * (ulong)elemSize), bufA, vl, elemSize, (ushort)elemSize);

                    // Read indices
                    ulong indexBytes = vl * (ulong)indexElemSize;
                    Span<byte> indexBuf = scratchIndex.Slice(0, (int)indexBytes);
                    BurstIO.BurstRead(indexBase + (elementsProcessed * (ulong)indexStride), indexBuf, vl, indexElemSize, indexStride);

                    // Gather from src2 using indices
                    BurstIO.BurstGather(src2Base, bufB, vl, elemSize, indexBuf, indexElemSize, indexIsByteOffset);

                    // Execute ALU operation
                    VectorALU.ApplyBinary(opCode, dataType, bufA, bufB, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Scatter result back to destination (sequential write)
                    BurstIO.BurstWrite(ptrDestSrc1 + (elementsProcessed * (ulong)elemSize), bufDst, vl, elemSize, (ushort)elemSize);
                }
                else
                {
                    // Unary operation: read only one operand
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Read operand (sequential access)
                    BurstIO.BurstRead(ptrDestSrc1 + (elementsProcessed * (ulong)elemSize), bufA, vl, elemSize, (ushort)elemSize);

                    // Execute ALU operation
                    VectorALU.ApplyUnary(opCode, dataType, bufA, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Write result (sequential write)
                    BurstIO.BurstWrite(ptrDestSrc1 + (elementsProcessed * (ulong)elemSize), bufDst, vl, elemSize, (ushort)elemSize);
                }

                ulong nextRemaining = remaining - vl;
                if (nextRemaining > 0)
                {
                    ulong nextVl = ResolvePreferredStreamChunkShape(
                        nextRemaining,
                        elemSize,
                        scratchA.Length,
                        indexElemSize,
                        scratchIndex.Length,
                        indexStride);
                    ulong nextElement = elementsProcessed + vl;
                    TryApplyIngressWarmPolicy(
                        ptrDestSrc1,
                        indexBase,
                        elemSize,
                        indexElemSize,
                        indexStride,
                        nextElement,
                        nextVl);
                }

                // Update remaining count
                elementsProcessed += vl;
                remaining -= vl;
            }
        }
    }
}

