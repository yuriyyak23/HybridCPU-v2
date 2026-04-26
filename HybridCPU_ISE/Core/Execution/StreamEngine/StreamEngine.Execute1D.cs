
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
        private static void ExecutePredicativeMovement1D(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength)
        {
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ushort stride = request.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> scratchA = core.GetScratchA();
            Span<byte> scratchDst = core.GetScratchDst();

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "raw 1D stream/vector execution",
                requiresScratchB: false,
                scratchALength: scratchA.Length,
                scratchBLength: 0,
                scratchDstLength: scratchDst.Length);
            ThrowIfUnsupportedRawPredicativeMovementBufferFootprint(
                opCode,
                streamLength,
                elemSize,
                scratchA.Length,
                scratchDst.Length);

            int byteCount = checked((int)(streamLength * (ulong)elemSize));
            Span<byte> bufSrc = scratchA.Slice(0, byteCount);
            Span<byte> bufDst = scratchDst.Slice(0, byteCount);

            BurstIO.BurstRead(ptrDestSrc1, bufSrc, streamLength, elemSize, stride);

            // Raw 1D predicative movement now follows the authoritative single-surface carrier:
            // seed destination before compaction/expansion so untouched lanes stay truthful.
            bufSrc.CopyTo(bufDst);

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VCOMPRESS)
            {
                VectorALU.ApplyCompress(dataType, bufSrc, bufDst, elemSize, streamLength, predIndex, ref core);
            }
            else
            {
                VectorALU.ApplyExpand(
                    dataType,
                    bufSrc,
                    bufDst,
                    elemSize,
                    streamLength,
                    predIndex,
                    request.TailAgnostic,
                    request.MaskAgnostic,
                    ref core);
            }

            BurstIO.BurstWrite(ptrDestSrc1, bufDst, streamLength, elemSize, stride);
        }

        private static void ThrowIfUnsupportedRawPredicativeMovementBufferFootprint(
            uint opCode,
            ulong streamLength,
            int elemSize,
            int scratchALength,
            int scratchDstLength)
        {
            ulong maxRepresentableElements = (ulong)Math.Min(
                scratchALength / elemSize,
                scratchDstLength / elemSize);

            if (streamLength <= maxRepresentableElements)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Stream/vector opcode 0x{opCode:X} reached raw 1D stream/vector execution with StreamLength {streamLength} on the predicative-movement contour, but the truthful in-place VCOMPRESS/VEXPAND follow-through currently materializes the full source/destination surface through scratch-backed execution. " +
                $"Raw execution only represents up to {maxRepresentableElements} element(s) at the current element width and must fail closed instead of publishing partial compaction/expansion truth.");
        }

        private static void Execute1D(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength,
            int executionVtId)
        {
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ulong ptrSrc2 = request.Src2Pointer;
            ushort stride = request.Stride;

            // Default stride: packed (element size)
            if (stride == 0) stride = (ushort)elemSize;

            // Hardware vector length (VLMAX) - from RVV_Config
            const ulong VLMAX = 32;

            // Get scratch buffers from core
            Span<byte> scratchA = core.GetScratchA();
            Span<byte> scratchB = core.GetScratchB();
            Span<byte> scratchDst = core.GetScratchDst();

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "raw 1D stream/vector execution",
                requiresScratchB: true,
                scratchALength: scratchA.Length,
                scratchBLength: scratchB.Length,
                scratchDstLength: scratchDst.Length);

            ulong remaining = streamLength;
            bool isComparisonOp = IsComparisonOp(opCode);
            byte comparisonDestPredReg = 0;
            ulong accumulatedComparisonResultMask = 0;
            if (isComparisonOp)
            {
                comparisonDestPredReg = ResolveComparisonDestinationPredicateRegister(in request);
            }

            // Strip-mining loop: process VLMAX elements per iteration
            while (remaining > 0)
            {
                // Compute vector length for this iteration
                ulong vl = remaining < VLMAX ? remaining : VLMAX;

                // Clamp vl to fit within scratch buffer capacity
                ulong maxVL = (ulong)(scratchA.Length / elemSize);
                if (vl > maxVL) vl = maxVL;

                // Handle mask manipulation instructions (operate on predicate registers)
                if (IsMaskManipOp(opCode))
                {
                    // Mask operations work on predicate registers directly, no memory access
                    // Source predicate registers encoded in instruction fields
                    byte srcPred1 = (byte)(request.Immediate & 0x0F); // Bits 0-3: src1 predicate reg
                    byte destPred = ResolveMaskManipulationDestinationPredicateRegister(in request); // Bits 8-11: dest predicate reg

                    ulong mask1 = core.GetPredicateRegister(srcPred1);

                    // Population count (VPOPC_M) - special case, writes to scalar register
                    if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)
                    {
                        ulong popCount = VectorALU.MaskPopCount(mask1, vl);
                        // Write result to scalar register (destPred used as register index)
                        if (destPred < YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
                        {
                            PublishScalarRetireRegisterWrite(
                                ref core,
                                executionVtId,
                                destPred,
                                popCount);
                        }
                        break; // VPOPC doesn't iterate, just counts once
                    }

                    // Write result mask to destination predicate register
                    core.SetPredicateRegister(
                        destPred,
                        ResolveMaskManipulationResultMask(
                            ref core,
                            in request,
                            opCode));

                    // Mask operations don't iterate (operate on 64-bit mask directly)
                    break;
                }
                // Handle comparison instructions separately (generate predicate mask)
                else if (IsComparisonOp(opCode))
                {
                    // Read both source operands
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);

                    // Burst read source operands
                    BurstIO.BurstRead(ptrDestSrc1, bufA, vl, elemSize, stride);
                    BurstIO.BurstRead(ptrSrc2, bufB, vl, elemSize, stride);

                    // Execute comparison and generate mask
                    ulong resultMask = VectorALU.ApplyComparison(opCode, dataType, bufA, bufB, elemSize, vl, predIndex, ref core);

                    AccumulateComparisonPredicateChunk(
                        ref accumulatedComparisonResultMask,
                        resultMask,
                        streamLength - remaining);
                }
                // Handle dot-product instructions (ML/DSP optimized)
                else if (IsDotProductOp(opCode))
                {
                    // The opcode registry intentionally tags VDOT* as reduction-family ops for
                    // structural/resource taxonomy, but the raw execute contour must still reach
                    // the dedicated two-source dot-product datapath instead of generic unary
                    // reduction semantics.
                    // Read both source operands
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);
                    int resultElemSize =
                        AddressGen.IsWideningOpcode(opCode) &&
                        (dataType == DataTypeEnum.FLOAT8_E4M3 ||
                         dataType == DataTypeEnum.FLOAT8_E5M2)
                            ? sizeof(float)
                            : elemSize;
                    Span<byte> bufDst = scratchDst.Slice(0, resultElemSize); // Only 1 scalar result

                    // Burst read operands
                    BurstIO.BurstRead(ptrDestSrc1, bufA, vl, elemSize, stride);
                    BurstIO.BurstRead(ptrSrc2, bufB, vl, elemSize, stride);

                    // Execute dot-product operation (reduce to scalar)
                    VectorALU.ApplyDotProduct(opCode, dataType, bufA, bufB, bufDst, elemSize, vl, predIndex, ref core);

                    // Write scalar result back to first element of destination
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, 1, resultElemSize, stride);

                    // Dot-product only processes one chunk
                    break;
                }
                // Handle reduction instructions (collapse vector to scalar)
                else if (IsReductionOp(opCode))
                {
                    // Read source operand
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufSrc = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, elemSize); // Only 1 element result

                    // Burst read operand
                    BurstIO.BurstRead(ptrDestSrc1, bufSrc, vl, elemSize, stride);

                    // Execute reduction operation (all elements -> single result)
                    VectorALU.ApplyReduction(opCode, dataType, bufSrc, bufDst, elemSize, vl, predIndex, ref core);

                    // Write scalar result back to first element of destination
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, 1, elemSize, stride);

                    // For reduction, only process one chunk (no strip-mining continuation needed)
                    break;
                }
                // Handle predicative movement instructions (compress/expand)
                else if (IsPredicativeMovementOp(opCode))
                {
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufSrc = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read source
                    BurstIO.BurstRead(ptrDestSrc1, bufSrc, vl, elemSize, stride);

                    // Execute compress or expand operation
                    if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VCOMPRESS)
                    {
                        VectorALU.ApplyCompress(dataType, bufSrc, bufDst, elemSize, vl, predIndex, ref core);
                    }
                    else // VEXPAND
                    {
                        VectorALU.ApplyExpand(dataType, bufSrc, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);
                    }

                    // Burst write result
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                }
                // Handle permutation/gather instructions (indexed reordering)
                else if (IsPermutationOp(opCode))
                {
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufSrc = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufIndices = scratchB.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read source and index vectors
                    BurstIO.BurstRead(ptrDestSrc1, bufSrc, vl, elemSize, stride);
                    BurstIO.BurstRead(ptrSrc2, bufIndices, vl, elemSize, stride); // Index vector
                    // Raw permutation must preserve masked-off destination lanes the same way
                    // the authoritative dedicated carrier does, so seed dst before ApplyPermute mutates it.
                    BurstIO.BurstRead(ptrDestSrc1, bufDst, vl, elemSize, stride);

                    // Execute permute/gather operation
                    VectorALU.ApplyPermute(dataType, bufSrc, bufIndices, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Burst write result
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                }
                // Handle slide instructions (element shifting)
                else if (IsSlideOp(opCode))
                {
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufSrc = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read source
                    BurstIO.BurstRead(ptrDestSrc1, bufSrc, vl, elemSize, stride);
                    // Raw slide must preserve undisturbed destination lanes and pre-offset gap
                    // lanes the same way as the authoritative dedicated carrier.
                    BurstIO.BurstRead(ptrDestSrc1, bufDst, vl, elemSize, stride);

                    // Execute slide operation (offset is in immediate field)
                    ushort slideOffset = request.Immediate;
                    if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSLIDEUP)
                    {
                        VectorALU.ApplySlideUp(dataType, bufSrc, bufDst, elemSize, vl, slideOffset, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);
                    }
                    else // VSLIDEDOWN
                    {
                        VectorALU.ApplySlideDown(dataType, bufSrc, bufDst, elemSize, vl, slideOffset, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);
                    }

                    // Burst write result
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                }
                // Handle FMA instructions (ternary operation: vd = vd * vs1 + vs2)
                // FMA with TriOpDesc: Src2Pointer → descriptor address, descriptor holds SrcA, SrcB, StrideA, StrideB
                // Src2Pointer references a TriOpDesc descriptor holding SrcA, SrcB, StrideA, StrideB.
                else if (IsFMAOp(opCode))
                {
                    if (ptrSrc2 == 0)
                    {
                        throw new InvalidOperationException(
                            "StreamEngine.Execute(...) rejected descriptor-less raw VFM* execution on the descriptor-backed tri-operand contour. " +
                            "Current FMA encoding requires Src2Pointer to reference a TriOpDesc, so raw execution must fail closed instead of reviving the legacy descriptor-less fallback path.");
                    }
                    if (ptrSrc2 != 0)
                    {
                        // Descriptor mode: read TriOpDesc from memory once
                        TriOpDesc desc = ReadTriOpDesc(ptrSrc2);

                        ushort strideA = desc.StrideA != 0 ? desc.StrideA : (ushort)elemSize;
                        ushort strideB = desc.StrideB != 0 ? desc.StrideB : (ushort)elemSize;

                        ulong offsetDst = 0;
                        ulong offsetA = 0;
                        ulong offsetB = 0;
                        ulong fmaRemaining = remaining;

                        while (fmaRemaining > 0)
                        {
                            ulong fmaVl = fmaRemaining < VLMAX ? fmaRemaining : VLMAX;
                            ulong bytesToRead = fmaVl * (ulong)elemSize;
                            Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);
                            Span<byte> bufVs1 = scratchA.Slice(0, (int)bytesToRead);
                            Span<byte> bufVs2 = scratchB.Slice(0, (int)bytesToRead);

                            BurstIO.BurstRead(ptrDestSrc1 + offsetDst, bufDst, fmaVl, elemSize, stride);
                            BurstIO.BurstRead(desc.SrcA + offsetA, bufVs1, fmaVl, elemSize, strideA);
                            BurstIO.BurstRead(desc.SrcB + offsetB, bufVs2, fmaVl, elemSize, strideB);

                            VectorALU.ApplyFMA(opCode, dataType, bufDst, bufVs1, bufVs2, elemSize, fmaVl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                            BurstIO.BurstWrite(ptrDestSrc1 + offsetDst, bufDst, fmaVl, elemSize, stride);

                            offsetDst += fmaVl * (ulong)stride;
                            offsetA += fmaVl * (ulong)strideA;
                            offsetB += fmaVl * (ulong)strideB;
                            fmaRemaining -= fmaVl;
                        }
                    }
                    else
                    {
                        // Legacy fallback: Src2Pointer = 0, use Immediate as vs2 address
                        ulong bytesToRead = vl * (ulong)elemSize;
                        Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);
                        Span<byte> bufVs1 = scratchA.Slice(0, (int)bytesToRead);
                        Span<byte> bufVs2 = scratchB.Slice(0, (int)bytesToRead);

                        BurstIO.BurstRead(ptrDestSrc1, bufDst, vl, elemSize, stride);
                        // In legacy mode, no second source pointer — use Immediate as address
                        ulong ptrVs2 = request.Immediate;
                        BurstIO.BurstRead(ptrVs2, bufVs1, vl, elemSize, stride);
                        BurstIO.BurstRead(ptrVs2, bufVs2, vl, elemSize, stride);

                        VectorALU.ApplyFMA(opCode, dataType, bufDst, bufVs1, bufVs2, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                        BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                    }

                    // FMA handles its own strip-mining; skip outer loop advancement
                    break;
                }
                // Determine if operation is binary or unary
                else if (IsBinaryOp(opCode))
                {
                    // Read both operands
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read operand A (dest/src1)
                    BurstIO.BurstRead(ptrDestSrc1, bufA, vl, elemSize, stride);

                    // Burst read operand B (src2)
                    BurstIO.BurstRead(ptrSrc2, bufB, vl, elemSize, stride);

                    // Execute ALU operation
                    VectorALU.ApplyBinary(opCode, dataType, bufA, bufB, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Burst write result (destructive: overwrites dest/src1 location)
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                }
                else
                {
                    // Unary operation: read only one operand
                    ulong bytesToRead = vl * (ulong)elemSize;
                    Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                    Span<byte> bufDst = scratchDst.Slice(0, (int)bytesToRead);

                    // Burst read operand
                    BurstIO.BurstRead(ptrDestSrc1, bufA, vl, elemSize, stride);

                    // Execute ALU operation
                    VectorALU.ApplyUnary(opCode, dataType, bufA, bufDst, elemSize, vl, predIndex, request.TailAgnostic, request.MaskAgnostic, ref core);

                    // Burst write result
                    BurstIO.BurstWrite(ptrDestSrc1, bufDst, vl, elemSize, stride);
                }

                // Update pointers and remaining count
                ulong strideBytes = (ulong)stride * vl;
                ptrDestSrc1 += strideBytes;
                ptrSrc2 += strideBytes;
                remaining -= vl;

                // Prefetch next chunk into StreamRegisterFile (req.md §2, §3)
                if (remaining > 0)
                {
                    ulong nextVl = remaining < VLMAX ? remaining : VLMAX;
                    PrefetchToStreamRegister(ptrDestSrc1, (byte)elemSize, (uint)nextVl);
                    if (ptrSrc2 != 0)
                        PrefetchToStreamRegister(ptrSrc2, (byte)elemSize, (uint)nextVl);
                }
            }

            if (isComparisonOp)
            {
                core.SetPredicateRegister(
                    comparisonDestPredReg,
                    accumulatedComparisonResultMask);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PublishScalarRetireRegisterWrite(
            ref Processor.CPU_Core core,
            int executionVtId,
            ushort archRegId,
            ulong value)
        {
            core.AppendGeneratedExecutingLaneRetireRecord(
                RetireRecord.RegisterWrite(
                    executionVtId,
                    archRegId,
                    value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PublishScalarRetireRegisterWrite(
            ref Processor.CPU_Core core,
            in RetireRecord retireRecord)
        {
            core.AppendGeneratedExecutingLaneRetireRecord(retireRecord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveScalarRegisterRetireRecord(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            byte predIndex,
            int executionVtId,
            out RetireRecord retireRecord)
        {
            retireRecord = default;

            ResolveRequiredScalarRegisterOperandsOrThrow(
                in request,
                opCode,
                out byte destinationRegister,
                out byte sourceRegister1,
                out byte sourceRegister2);

            if (!core.LaneActive(predIndex, 0))
            {
                return false;
            }

            retireRecord = RetireRecord.RegisterWrite(
                executionVtId,
                destinationRegister,
                ResolveScalarRegisterResult(
                    ref core,
                    in request,
                    opCode,
                    executionVtId,
                    sourceRegister1,
                    sourceRegister2));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CaptureScalarRegisterRetireWindowPublication(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            uint opCode,
            byte predIndex,
            int executionVtId)
        {
            ThrowIfUnsupportedScalarRegisterStreamOpcode(opCode);

            if (TryResolveScalarRegisterRetireRecord(
                ref core,
                request,
                opCode,
                predIndex,
                executionVtId,
                out RetireRecord retireRecord))
            {
                retireBatch.AppendRetireRecord(retireRecord);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveScalarRegisterResult(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            int executionVtId,
            byte sourceRegister1,
            byte sourceRegister2)
        {
            ulong val1 = core.ReadArch(executionVtId, sourceRegister1);
            ulong val2 = core.ReadArch(executionVtId, sourceRegister2);

            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.Addition => val1 + val2,

                (uint)Processor.CPU_Core.InstructionsEnum.Subtraction => val1 - val2,

                (uint)Processor.CPU_Core.InstructionsEnum.Multiplication => val1 * val2,

                (uint)Processor.CPU_Core.InstructionsEnum.Division => val2 != 0 ? val1 / val2 : 0,

                (uint)Processor.CPU_Core.InstructionsEnum.Modulus => val2 != 0 ? val1 % val2 : 0,

                (uint)Processor.CPU_Core.InstructionsEnum.XOR => val1 ^ val2,

                (uint)Processor.CPU_Core.InstructionsEnum.OR => val1 | val2,

                (uint)Processor.CPU_Core.InstructionsEnum.AND => val1 & val2,

                (uint)Processor.CPU_Core.InstructionsEnum.ShiftLeft => val1 << (int)(request.Immediate & 0x3F),

                (uint)Processor.CPU_Core.InstructionsEnum.ShiftRight => val1 >> (int)(request.Immediate & 0x3F),

                _ => throw CreateUnsupportedScalarRegisterStreamOpcodeException(opCode)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveRequiredScalarRegisterOperandsOrThrow(
            in StreamExecutionRequest request,
            uint opCode,
            out byte destinationRegister,
            out byte sourceRegister1,
            out byte sourceRegister2)
        {
            if (request.TryUnpackArchRegs(
                    out destinationRegister,
                    out sourceRegister1,
                    out sourceRegister2) &&
                destinationRegister != StreamExecutionRequest.NoArchReg &&
                sourceRegister1 != StreamExecutionRequest.NoArchReg &&
                sourceRegister2 != StreamExecutionRequest.NoArchReg)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Stream opcode 0x{opCode:X} reached the scalar direct stream helper contour with non-canonical architectural register fields. " +
                "This direct-retire contour is authoritative only for fully representable rd/rs1/rs2 arch-register triples; direct callers must reject malformed register encodings instead of observing silent no-op or synthetic zero-valued helper success.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedScalarRegisterStreamOpcode(uint opCode)
        {
            if (!IsSupportedScalarRegisterStreamOpcode(opCode))
            {
                throw CreateUnsupportedScalarRegisterStreamOpcodeException(opCode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSupportedScalarRegisterStreamOpcode(uint opCode)
        {
            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.Addition => true,
                (uint)Processor.CPU_Core.InstructionsEnum.Subtraction => true,
                (uint)Processor.CPU_Core.InstructionsEnum.Multiplication => true,
                (uint)Processor.CPU_Core.InstructionsEnum.Division => true,
                (uint)Processor.CPU_Core.InstructionsEnum.Modulus => true,
                (uint)Processor.CPU_Core.InstructionsEnum.XOR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.OR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.AND => true,
                (uint)Processor.CPU_Core.InstructionsEnum.ShiftLeft => true,
                (uint)Processor.CPU_Core.InstructionsEnum.ShiftRight => true,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException CreateUnsupportedScalarRegisterStreamOpcodeException(uint opCode)
        {
            return new InvalidOperationException(
                $"Stream opcode 0x{opCode:X} reached the scalar direct stream helper contour without an authoritative scalar retire/apply contract. " +
                "This contour is limited to scalar ALU register-write helpers; vector compute opcodes must not synthesize scalar retire truth from streamLength == 1 / IsScalar encoding, and dedicated scalar-result VPOPC remains on its explicit direct compat path.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CaptureVpopcRetireWindowPublication(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            int executionVtId)
        {
            if (TryResolveVpopcRetireRecord(
                ref core,
                request,
                executionVtId,
                out RetireRecord retireRecord))
            {
                retireBatch.AppendRetireRecord(retireRecord);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveVpopcRetireRecord(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            int executionVtId,
            out RetireRecord retireRecord)
        {
            retireRecord = default;

            byte srcPred = (byte)(request.Immediate & 0x0F);
            byte destReg = (byte)((request.Immediate >> 8) & 0x0F);
            if (destReg >= YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
            {
                return false;
            }

            const ulong VLMAX = 32;
            ulong vl = request.StreamLength < VLMAX ? request.StreamLength : VLMAX;
            ulong popCount = VectorALU.MaskPopCount(core.GetPredicateRegister(srcPred), vl);
            retireRecord = RetireRecord.RegisterWrite(executionVtId, destReg, popCount);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CapturePredicateStateRetireWindowPublication(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int executionVtId)
        {
            if (IsComparisonOp(opCode))
            {
                int elemSize = ResolveRequiredElementSizeOrThrow(opCode, dataType);
                retireBatch.CaptureRetireWindowPredicateState(
                    ResolveComparisonDestinationPredicateRegister(in request),
                    ResolveComparisonPredicateMask(
                        ref core,
                        in request,
                        opCode,
                        dataType,
                        predIndex,
                        elemSize));
                return;
            }

            if (IsMaskManipOp(opCode) &&
                opCode != (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)
            {
                retireBatch.CaptureRetireWindowPredicateState(
                    ResolveMaskManipulationDestinationPredicateRegister(in request),
                    ResolveMaskManipulationResultMask(
                        ref core,
                        in request,
                        opCode));
                return;
            }

            throw new InvalidOperationException(
                $"Stream opcode 0x{opCode:X} reached direct stream retire-window predicate-state publication without an authoritative predicate-publication contour.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveComparisonPredicateMask(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize)
        {
            ulong ptrDestSrc1 = request.DestSrc1Pointer;
            ulong ptrSrc2 = request.Src2Pointer;
            ushort stride = request.Stride;
            if (stride == 0)
            {
                stride = (ushort)elemSize;
            }

            const ulong VLMAX = 32;
            Span<byte> scratchA = core.GetScratchA();
            Span<byte> scratchB = core.GetScratchB();
            Span<byte> scratchDst = core.GetScratchDst();

            ThrowIfUnavailableRawScratchContour(
                opCode,
                "direct stream compat retire resolution",
                requiresScratchB: true,
                scratchALength: scratchA.Length,
                scratchBLength: scratchB.Length,
                scratchDstLength: scratchDst.Length);

            ulong remaining = request.StreamLength;
            ulong accumulatedResultMask = 0;

            while (remaining > 0)
            {
                ulong vl = remaining < VLMAX ? remaining : VLMAX;
                ulong maxVL = (ulong)(scratchA.Length / elemSize);
                if (vl > maxVL)
                {
                    vl = maxVL;
                }

                ulong bytesToRead = vl * (ulong)elemSize;
                Span<byte> bufA = scratchA.Slice(0, (int)bytesToRead);
                Span<byte> bufB = scratchB.Slice(0, (int)bytesToRead);

                BurstIO.BurstRead(ptrDestSrc1, bufA, vl, elemSize, stride);
                BurstIO.BurstRead(ptrSrc2, bufB, vl, elemSize, stride);

                ulong resultMask = VectorALU.ApplyComparison(
                    opCode,
                    dataType,
                    bufA,
                    bufB,
                    elemSize,
                    vl,
                    predIndex,
                    ref core);

                AccumulateComparisonPredicateChunk(
                    ref accumulatedResultMask,
                    resultMask,
                    request.StreamLength - remaining);

                ulong strideBytes = (ulong)stride * vl;
                ptrDestSrc1 += strideBytes;
                ptrSrc2 += strideBytes;
                remaining -= vl;
            }

            return accumulatedResultMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveComparisonDestinationPredicateRegister(
            in StreamExecutionRequest request)
        {
            return (byte)(request.Immediate & 0x0F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveMaskManipulationDestinationPredicateRegister(
            in StreamExecutionRequest request)
        {
            return (byte)((request.Immediate >> 8) & 0x0F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveMaskManipulationResultMask(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode)
        {
            byte srcPred1 = (byte)(request.Immediate & 0x0F);
            byte srcPred2 = (byte)((request.Immediate >> 4) & 0x0F);
            ulong mask1 = core.GetPredicateRegister(srcPred1);

            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VMAND =>
                    VectorALU.ApplyMaskBinary(
                        opCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                (uint)Processor.CPU_Core.InstructionsEnum.VMOR =>
                    VectorALU.ApplyMaskBinary(
                        opCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                (uint)Processor.CPU_Core.InstructionsEnum.VMXOR =>
                    VectorALU.ApplyMaskBinary(
                        opCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                (uint)Processor.CPU_Core.InstructionsEnum.VMNOT =>
                    VectorALU.ApplyMaskUnary(opCode, mask1),
                _ => throw new InvalidOperationException(
                    $"Stream opcode 0x{opCode:X} reached predicate-mask resolution without an authoritative `VM*` follow-through contour.")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AccumulateComparisonPredicateChunk(
            ref ulong accumulatedResultMask,
            ulong chunkResultMask,
            ulong elementsProcessed)
        {
            int shift = (int)Math.Min(elementsProcessed, 64UL);
            if (shift >= 64)
            {
                return;
            }

            ulong shiftedMask = shift == 0
                ? chunkResultMask
                : chunkResultMask << shift;
            accumulatedResultMask |= shiftedMask;
        }
    }
}

