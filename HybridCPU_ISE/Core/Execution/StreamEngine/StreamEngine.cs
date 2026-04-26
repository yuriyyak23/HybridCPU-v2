
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using HybridCPU_ISE.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Central execution engine for stream/vector instructions.
    /// Implements RVV-inspired strip-mining FSM with type-agnostic operation dispatch.
    ///
    /// Architecture goals:
    /// - Single point of truth for stream instruction semantics
    /// - Type-agnostic: handles all INT/UINT/FLOAT widths (8-64 bits)
    /// - Address mode agnostic: unit-stride, strided, 2D, indexed (via descriptors)
    /// - Strip-mining: chunks large streams into VLMAX-sized hardware iterations
    /// - Burst I/O: uses AXI4-compliant BurstIO for memory transfers
    /// - Zero allocation: all buffers pre-allocated in CPU_Core scratch space
    /// - RVV semantics: predicate masks, inactive lanes don't write/trap
    /// </summary>
    internal static partial class StreamEngine
    {
        /// <summary>
        /// Execute a stream instruction.
        /// Main entry point from the explicit direct-stream compat/test seam and
        /// materialized vector micro-op execution.
        ///
        /// Execution flow:
        /// 1. Extract instruction parameters (opcode, type, addresses, stride, length)
        /// 2. Strip-mine: loop over chunks of VLMAX elements
        /// 3. For each chunk:
        ///    a. Read operands via BurstIO (respecting AXI4 constraints)
        ///    b. Execute operation via VectorALU (with predicate masking)
        ///    c. Write results via BurstIO
        /// 4. Update pointers for next chunk
        /// </summary>
        /// <param name="core">Reference to CPU core (for predicates, scratch buffers)</param>
        /// <param name="inst">Instruction structure (parsed VLIW slot)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ref Processor.CPU_Core core,
            in VLIW_Instruction inst,
            int ownerThreadId = -1)
        {
            StreamExecutionRequest request =
                StreamExecutionRequest.CreateValidatedCompatIngress(in inst);
            Execute(ref core, in request, ownerThreadId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            int ownerThreadId = -1)
        {
            ulong streamLength = request.StreamLength;
            if (streamLength == 0)
            {
                throw new InvalidOperationException(
                    $"Stream opcode 0x{request.OpCode:X} reached StreamEngine.Execute(...) with StreamLength == 0 without an authoritative mainline execute/retire contour. " +
                    "Mainline vector callers must not rely on an inner StreamEngine no-op for zero-length requests.");
            }

            uint opCode = request.OpCode;
            DataTypeEnum dataType = request.DataTypeValue;
            byte predIndex = request.PredicateMask;

            int executionVtId = ResolveExecutionVtId(ref core, ownerThreadId);
            ThrowIfUnsupportedRawTransferVectorContour(in request, opCode);
            ThrowIfUnsupportedNonRepresentableVectorAddressing(in request, opCode);
            ThrowIfUnsupportedRawDescriptorlessFmaContour(in request, opCode);

            // --- Scalar register path (StreamLength = 1, register IDs in Word1) ---
            if (streamLength == 1 && request.IsScalar)
            {
                // HLS Note: Scalar operations take EXECUTE_LATENCY_MIN cycles (1 cycle)
                ThrowIfUnsupportedScalarRegisterStreamOpcode(opCode);
                ExecuteScalarRegister(ref core, request, opCode, dataType, predIndex, executionVtId);
                return;
            }

            if (IsMaskManipOp(opCode))
            {
                ExecuteMaskManipulation(ref core, request, opCode, executionVtId);
                return;
            }

            int elemSize = ResolveRequiredElementSizeOrThrow(opCode, dataType);

            // --- Vector Memory-to-Memory path ---
            // HLS Note: Vector operations take variable cycles based on:
            // - Number of elements (streamLength)
            // - Memory access latency (MEMORY_LATENCY per burst)
            // - ALU operation latency (EXECUTE_LATENCY_MIN per element)
            ExecuteVectorMemory(ref core, request, opCode, dataType, predIndex, elemSize, streamLength, executionVtId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CaptureRetireWindowPublications(
            ref Processor.CPU_Core core,
            in VLIW_Instruction inst,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            int ownerThreadId = -1)
        {
            StreamExecutionRequest request =
                StreamExecutionRequest.CreateValidatedCompatIngress(in inst);
            CaptureRetireWindowPublications(ref core, in request, ref retireBatch, ownerThreadId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CaptureRetireWindowPublications(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            int ownerThreadId = -1)
        {
            CaptureRetireWindowPublicationsCore(
                ref core,
                in request,
                ref retireBatch,
                ownerThreadId,
                "direct stream retire-window publication");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CaptureRetireWindowPublicationsCore(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            ref Processor.CPU_Core.RetireWindowBatch retireBatch,
            int ownerThreadId,
            string executionContour)
        {
            ulong streamLength = request.StreamLength;
            if (streamLength == 0)
            {
                throw new InvalidOperationException(
                    $"Stream opcode 0x{request.OpCode:X} reached {executionContour} with StreamLength == 0 without an authoritative retire/apply contour. " +
                    "Direct callers must not treat zero-length direct stream requests as implicit success/no-op; use the canonical mainline path or materialize an explicit retire record before reopening this contour.");
            }

            uint opCode = request.OpCode;
            int executionVtId = ResolveExecutionVtId(ref core, ownerThreadId);
            ThrowIfUnsupportedDirectRetireVectorAddressing(
                in request,
                opCode,
                executionContour);

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)
            {
                CaptureVpopcRetireWindowPublication(
                    ref core,
                    request,
                    ref retireBatch,
                    executionVtId);
                return;
            }

            if (!request.IsScalar &&
                (IsComparisonOp(opCode) ||
                 (IsMaskManipOp(opCode) &&
                  opCode != (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)))
            {
                CapturePredicateStateRetireWindowPublication(
                    ref core,
                    request,
                    ref retireBatch,
                    opCode,
                    request.DataTypeValue,
                    request.PredicateMask,
                    executionVtId);
                return;
            }

            if (streamLength == 1 && request.IsScalar)
            {
                ThrowIfUnsupportedScalarRegisterStreamOpcode(opCode);
                CaptureScalarRegisterRetireWindowPublication(
                    ref core,
                    request,
                    ref retireBatch,
                    opCode,
                    request.PredicateMask,
                    executionVtId);
                return;
            }

            throw new InvalidOperationException(
                $"{executionContour} is only authoritative for scalar-writing or predicate-writing stream instructions that materialize explicit retire/apply follow-through.");
        }

        /// <summary>
        /// Execute scalar operation on registers.
        /// StreamLength = 1, operands are register IDs packed in Word1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExecuteScalarRegister(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int executionVtId)
        {
            _ = dataType;
            ThrowIfUnsupportedScalarRegisterStreamOpcode(opCode);

            if (TryResolveScalarRegisterRetireRecord(
                ref core,
                request,
                opCode,
                predIndex,
                executionVtId,
                out RetireRecord retireRecord))
            {
                PublishScalarRetireRegisterWrite(
                    ref core,
                    in retireRecord);
            }
        }

        /// <summary>
        /// Execute vector operation on memory buffers.
        /// Implements strip-mining with burst I/O.
        /// Supports 1D, 2D, and indexed addressing modes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExecuteVectorMemory(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            DataTypeEnum dataType,
            byte predIndex,
            int elemSize,
            ulong streamLength,
            int executionVtId)
        {
            // Check for indexed addressing mode (gather/scatter)
            if (request.Indexed)
            {
                ExecuteIndexed(ref core, request, opCode, dataType, predIndex, elemSize, streamLength);
                return;
            }

            // Check for 2D addressing mode
            if (request.Is2D)
            {
                Execute2D(ref core, request, opCode, dataType, predIndex, elemSize, streamLength);
                return;
            }

            if (IsPredicativeMovementOp(opCode))
            {
                ExecutePredicativeMovement1D(
                    ref core,
                    request,
                    opCode,
                    dataType,
                    predIndex,
                    elemSize,
                    streamLength);
                return;
            }

            // Default: 1D strided addressing
            // Use double-buffered path when pipeline mode is enabled and op is
            // a simple binary/unary (not FMA, reduction, comparison, mask, etc.)
            if (core.GetPipelineControl().Enabled &&
                !IsFMAOp(opCode) && !IsReductionOp(opCode) && !IsComparisonOp(opCode) &&
                !IsMaskManipOp(opCode) && !IsDotProductOp(opCode) && !IsPredicativeMovementOp(opCode) &&
                !IsPermutationOp(opCode) && !IsSlideOp(opCode))
            {
                Execute1D_DoubleBuffered(ref core, request, opCode, dataType, predIndex, elemSize, streamLength);
                return;
            }

            Execute1D(ref core, request, opCode, dataType, predIndex, elemSize, streamLength, executionVtId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveRequiredElementSizeOrThrow(
            uint opCode,
            DataTypeEnum dataType)
        {
            if (!DataTypeUtils.IsValid(dataType))
            {
                throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported element DataType '{dataType}' (raw 0x{(byte)dataType:X2}) on an element-sized execution contour. " +
                    "Element-sized vector/stream execution must fail closed instead of collapsing into hidden success/no-op for an invalid data type.");
            }

            int elemSize = DataTypeUtils.SizeOf(dataType);
            if (elemSize != 0)
            {
                return elemSize;
            }

            if (IsDotProductOp(opCode) &&
                (dataType == DataTypeEnum.FLOAT8_E4M3 ||
                 dataType == DataTypeEnum.FLOAT8_E5M2))
            {
                // Dedicated FP8 dot-product contours already widen through VectorALU.ApplyDotProduct(...).
                // Keep the raw StreamEngine gate narrow so only the authoritative dot-product family
                // reuses 1-byte element sizing instead of reopening broad generic FP8 compat execution.
                return 1;
            }

            throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported element DataType '{dataType}' (raw 0x{(byte)dataType:X2}) on an element-sized execution contour. " +
                "Element-sized vector/stream execution must fail closed instead of collapsing into hidden success/no-op for an invalid data type.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExecuteMaskManipulation(
            ref Processor.CPU_Core core,
            in StreamExecutionRequest request,
            uint opCode,
            int executionVtId)
        {
            byte srcPred1 = (byte)(request.Immediate & 0x0F);
            byte destPred = ResolveMaskManipulationDestinationPredicateRegister(in request);
            ulong mask1 = core.GetPredicateRegister(srcPred1);

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)
            {
                ulong vl = Math.Min(request.StreamLength, Processor.CPU_Core.RVV_Config.VLMAX);
                ulong popCount = VectorALU.MaskPopCount(mask1, vl);
                if (destPred < YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
                {
                    PublishScalarRetireRegisterWrite(
                        ref core,
                        executionVtId,
                        destPred,
                        popCount);
                }

                return;
            }

            core.SetPredicateRegister(
                destPred,
                ResolveMaskManipulationResultMask(
                    ref core,
                    in request,
                    opCode));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedNonRepresentableVectorAddressing(
            in StreamExecutionRequest request,
            uint opCode)
        {
            if (!request.Indexed && !request.Is2D)
            {
                return;
            }

            string addressingContour = DescribeNonRepresentableVectorAddressingContour(in request);
            if (IsMaskManipOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} predicate-mask addressing. " +
                    "Raw predicate-state execution does not materialize any addressed vector surface and must fail closed instead of hidden compat success/no-op.");
            }

            if (IsComparisonOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-comparison addressing. " +
                    "Raw comparison fallback only follows through 1D predicate-publication truth and must fail closed instead of hidden compat success.");
            }

            if (IsFMAOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-FMA addressing. " +
                    "Raw execution must fail closed instead of collapsing indexed/2D tri-operand requests into generic compute fallback while no authoritative mainline follow-through exists.");
            }

            if (IsDotProductOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-dot-product addressing. " +
                    "Raw dot-product execution only follows through 1D scalar-result truth and must fail closed instead of hidden compat success.");
            }

            if (IsPredicativeMovementOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-predicative-movement addressing. " +
                    "Raw predicative-movement execution only follows through 1D single-surface truth and must fail closed instead of hidden compat success.");
            }

            if (IsReductionOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-reduction addressing. " +
                    "Raw reduction execution only follows through 1D scalar-footprint truth and must fail closed instead of hidden compat success.");
            }

            if (IsPermutationOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-permutation addressing. " +
                    "Raw permutation execution only follows through 1D two-surface truth and must fail closed instead of hidden compat success.");
            }

            if (IsSlideOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} vector-slide addressing. " +
                    "Raw slide execution only follows through 1D single-surface truth and must fail closed instead of hidden compat success.");
            }

            if (IsGenericInPlaceVectorComputeOp(opCode))
            {
                string computeContour = IsBinaryOp(opCode)
                    ? "vector-binary"
                    : "vector-unary";
                throw new InvalidOperationException(
                    $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) with unsupported {addressingContour} {computeContour} addressing. " +
                    "Raw in-place compute execution only follows through 1D memory-shape truth and must fail closed instead of hidden compat success.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedRawTransferVectorContour(
            in StreamExecutionRequest request,
            uint opCode)
        {
            if (request.IsScalar)
            {
                return;
            }

            if (opCode is not
                (uint)Processor.CPU_Core.InstructionsEnum.VLOAD and not
                (uint)Processor.CPU_Core.InstructionsEnum.VSTORE)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) on the legacy raw VLOAD/VSTORE contour. " +
                "Dedicated transfer opcodes must fail closed on this raw stream engine surface instead of collapsing into generic compute-style memory traffic or hidden compat success.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedDirectRetireVectorAddressing(
            in StreamExecutionRequest request,
            uint opCode,
            string executionContour)
        {
            if (!request.Indexed && !request.Is2D)
            {
                return;
            }

            string addressingContour = DescribeNonRepresentableVectorAddressingContour(in request);
            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPOPC)
            {
                throw new InvalidOperationException(
                    $"Stream opcode 0x{opCode:X} reached {executionContour} with unsupported {addressingContour} vector-mask-popcount addressing. " +
                    "Direct VPOPC helper flow only publishes predicate-to-scalar retire truth and must fail closed instead of reopening an addressing-tag compat surface.");
            }

            if (IsComparisonOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream opcode 0x{opCode:X} reached {executionContour} with unsupported {addressingContour} vector-comparison addressing. " +
                    "Direct helper follow-through for `VCMP*` only publishes 1D predicate-state truth and must fail closed instead of reopening a non-representable compat surface.");
            }

            if (IsMaskManipOp(opCode))
            {
                throw new InvalidOperationException(
                    $"Stream opcode 0x{opCode:X} reached {executionContour} with unsupported {addressingContour} vector-mask addressing. " +
                    "Direct helper follow-through for `VM*` only publishes predicate-state truth and must fail closed instead of reopening an addressing-tag compat surface.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedRawDescriptorlessFmaContour(
            in StreamExecutionRequest request,
            uint opCode)
        {
            if (!IsFMAOp(opCode) || request.Indexed || request.Is2D || request.Src2Pointer != 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Stream/vector opcode 0x{opCode:X} reached StreamEngine.Execute(...) on a descriptor-less raw tri-operand FMA contour. " +
                "The authoritative mainline FMA carrier already fails closed when the third source descriptor is missing, so raw StreamEngine callers must reject this legacy Immediate-address fallback instead of publishing hidden success on a false 1D contour.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnavailableRawScratchContour(
            uint opCode,
            string executeContour,
            bool requiresScratchB,
            int scratchALength,
            int scratchBLength,
            int scratchDstLength,
            int scratchIndexLength = -1)
        {
            bool hasPrimaryScratch = scratchALength != 0 && scratchDstLength != 0;
            bool hasSecondaryScratch = !requiresScratchB || scratchBLength != 0;
            bool hasIndexScratch = scratchIndexLength < 0 || scratchIndexLength != 0;
            if (hasPrimaryScratch && hasSecondaryScratch && hasIndexScratch)
            {
                return;
            }

            string scratchSurface = scratchIndexLength >= 0
                ? "preallocated data/index scratch surfaces"
                : "preallocated data scratch surfaces";
            throw new InvalidOperationException(
                $"Stream/vector opcode 0x{opCode:X} reached {executeContour} without initialized scratch buffers. " +
                $"Raw execution must fail closed instead of publishing hidden success/no-op or opaque slice faults when the {scratchSurface} are unavailable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfUnsupportedZeroRowLength2DContour(
            uint opCode,
            ulong streamLength,
            uint rowLength,
            string executeContour)
        {
            if (streamLength == 0 || rowLength != 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Stream/vector opcode 0x{opCode:X} reached {executeContour} with rowLength == 0 on a non-zero 2D request. " +
                "2D vector/stream execution must fail closed instead of collapsing into hidden success/no-op when row geometry is non-representable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string DescribeNonRepresentableVectorAddressingContour(
            in StreamExecutionRequest request)
        {
            return request.Indexed && request.Is2D
                ? "indexed+2D"
                : request.Indexed
                    ? "indexed"
                    : "2D";
        }
    }
}

