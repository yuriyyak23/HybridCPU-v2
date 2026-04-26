using HybridCPU_ISE.Arch;

using YAKSys_Hybrid_CPU.Execution;
using AddressGen = YAKSys_Hybrid_CPU.Execution.AddressGen;
using PortType = YAKSys_Hybrid_CPU.Execution.PortType;


namespace YAKSys_Hybrid_CPU.Core
{
    public class VectorPermutationMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for permutation vector operations.
        /// Permutation operations read from source vector and index vector.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Permutation operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorPermutationMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from source vector and index vector
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize),
                    (Instruction.Src2Pointer, totalSize) // Index vector
                };

                // Write permuted result
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                ThrowIfUnsupportedAddressingContour();
                ThrowIfZeroLengthVectorComputeContour("VectorPermutationMicroOp.Execute()");

                ThrowIfUnsupportedElementDataType("VectorPermutationMicroOp.Execute()");

                _state = ExecutionState.LoadingOperands;
            }

            // Permutation requires loading entire vector to avoid read-after-write hazards
            if (_state == ExecutionState.LoadingOperands)
            {
                LoadAllElements(ref core);
                _state = ExecutionState.Computing;
            }

            if (_state == ExecutionState.Computing)
            {
                ComputePermutation(ref core);
                _state = ExecutionState.StoringResults;
            }

            if (_state == ExecutionState.StoringResults)
            {
                StoreAllElements(ref core);
                _state = ExecutionState.Complete;
            }

            return true;
        }

        private void ThrowIfUnsupportedAddressingContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Indexed
                    ? "indexed"
                    : "2D";
            throw new InvalidOperationException(
                $"VectorPermutationMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline permutation contour. " +
                "This carrier only follows through 1D two-surface memory semantics and must fail closed instead of hidden success on a non-representable compat surface.");
        }

        private void LoadAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptrSrc = Instruction.DestSrc1Pointer;
            ulong ptrIndex = Instruction.Src2Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufIndex = core.GetScratchB();
            Span<byte> bufDst = core.GetScratchDst();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrc, bufSrc, _totalElements, elemSize, stride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrIndex, bufIndex, _totalElements, elemSize, stride);
            // Permute semantics preserve masked-off destination lanes, so seed the destination
            // scratch buffer from the current vector surface before ApplyPermute mutates it.
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrc, bufDst, _totalElements, elemSize, stride);
        }

        private void ComputePermutation(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufIndex = core.GetScratchB();
            Span<byte> bufDst = core.GetScratchDst();

            // Use VectorALU.ApplyPermute for permutation logic
            VectorALU.ApplyPermute(
                Instruction.DataTypeValue,
                bufSrc,
                bufIndex,
                bufDst,
                elemSize,
                _totalElements,
                Instruction.PredicateMask,
                Instruction.TailAgnostic,
                Instruction.MaskAgnostic,
                ref core);
        }

        private void StoreAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptrDst = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufDst = core.GetScratchDst();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptrDst, bufDst, _totalElements, elemSize, stride);
        }
    }

    /// <summary>
    /// Vector slide operation micro-operation (shift elements up/down)
    /// Examples: VSLIDEUP, VSLIDEDOWN
    ///
    /// Slide operations shift vector elements by a constant offset.
    /// </summary>
    public class VectorSlideMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for slide vector operations.
        /// Slide operations read from source vector and write shifted result.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Slide operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorSlideMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from source vector
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };

                // Write shifted result
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                ThrowIfUnsupportedAddressingContour();
                ThrowIfZeroLengthVectorComputeContour("VectorSlideMicroOp.Execute()");

                ThrowIfUnsupportedElementDataType("VectorSlideMicroOp.Execute()");

                _state = ExecutionState.LoadingOperands;
            }

            // Slide requires loading entire vector
            if (_state == ExecutionState.LoadingOperands)
            {
                LoadAllElements(ref core);
                _state = ExecutionState.Computing;
            }

            if (_state == ExecutionState.Computing)
            {
                ComputeSlide(ref core);
                _state = ExecutionState.StoringResults;
            }

            if (_state == ExecutionState.StoringResults)
            {
                StoreAllElements(ref core);
                _state = ExecutionState.Complete;
            }

            return true;
        }

        private void ThrowIfUnsupportedAddressingContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Indexed
                    ? "indexed"
                    : "2D";
            throw new InvalidOperationException(
                $"VectorSlideMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline slide contour. " +
                "This carrier only follows through 1D single-surface memory semantics and must fail closed instead of hidden success on a non-representable compat surface.");
        }

        private void LoadAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptrSrc = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufDst = core.GetScratchDst();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrc, bufSrc, _totalElements, elemSize, stride);
            // Slide semantics preserve undisturbed destination lanes, so seed the destination
            // scratch buffer from the current vector surface before ApplySlideUp/Down mutates it.
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrc, bufDst, _totalElements, elemSize, stride);
        }

        private void ComputeSlide(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufDst = core.GetScratchDst();
            ushort slideAmount = (ushort)Instruction.Immediate;

            // Determine if this is slide up or slide down based on opcode
            bool isSlideUp = OpCode == Processor.CPU_Core.IsaOpcodeValues.VSLIDEUP;

            if (isSlideUp)
            {
                VectorALU.ApplySlideUp(
                    Instruction.DataTypeValue,
                    bufSrc,
                    bufDst,
                    elemSize,
                    _totalElements,
                    slideAmount,
                    Instruction.PredicateMask,
                    Instruction.TailAgnostic,
                    Instruction.MaskAgnostic,
                    ref core);
            }
            else
            {
                VectorALU.ApplySlideDown(
                    Instruction.DataTypeValue,
                    bufSrc,
                    bufDst,
                    elemSize,
                    _totalElements,
                    slideAmount,
                    Instruction.PredicateMask,
                    Instruction.TailAgnostic,
                    Instruction.MaskAgnostic,
                    ref core);
            }
        }

        private void StoreAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptrDst = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufDst = core.GetScratchDst();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptrDst, bufDst, _totalElements, elemSize, stride);
        }
    }

    /// <summary>
    /// Vector predicative movement micro-operation.
    /// Examples: VCOMPRESS, VEXPAND.
    ///
    /// These opcodes are not generic two-source binary math. The authoritative mainline
    /// path must not silently collapse them into VectorBinaryOpMicroOp default-zero
    /// behavior while their dedicated follow-through remains unresolved.
    /// </summary>
    public class VectorPredicativeMovementMicroOp : VectorMicroOp
    {
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorPredicativeMovementMicroOp.InitializeMetadata()");

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Predicative movement operates in-place over one packed vector surface.
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.Complete)
            {
                return true;
            }

            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                ThrowIfUnsupportedAddressingContour();
                ThrowIfZeroLengthVectorComputeContour("VectorPredicativeMovementMicroOp.Execute()");
                ThrowIfUnsupportedElementDataType("VectorPredicativeMovementMicroOp.Execute()");
                ThrowIfUnsupportedBufferFootprintContour(ref core);
                _state = ExecutionState.LoadingOperands;
            }

            if (_state == ExecutionState.LoadingOperands)
            {
                LoadAllElements(ref core);
                _state = ExecutionState.Computing;
            }

            if (_state == ExecutionState.Computing)
            {
                ComputePredicativeMovement(ref core);
                _state = ExecutionState.StoringResults;
            }

            if (_state == ExecutionState.StoringResults)
            {
                StoreAllElements(ref core);
                _state = ExecutionState.Complete;
            }

            return true;
        }

        private void ThrowIfUnsupportedAddressingContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Indexed
                    ? "indexed"
                    : "2D";
            throw new InvalidOperationException(
                $"VectorPredicativeMovementMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline predicative-movement contour. " +
                "This carrier only follows through 1D single-surface truth and must fail closed instead of hidden compat success on a non-representable surface.");
        }

        private void ThrowIfUnsupportedBufferFootprintContour(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong maxRepresentableElements = (ulong)Math.Min(
                core.GetScratchA().Length / elemSize,
                core.GetScratchDst().Length / elemSize);

            if (_totalElements <= maxRepresentableElements)
            {
                return;
            }

            throw new InvalidOperationException(
                $"VectorPredicativeMovementMicroOp.Execute() rejected StreamLength {_totalElements} for {Arch.OpcodeRegistry.GetMnemonicOrHex(OpCode)} because the authoritative mainline 1D single-surface contour currently materializes the full vector through scratch-backed in-place follow-through. " +
                $"This runtime surface only represents up to {maxRepresentableElements} element(s) at the current element width and must fail closed instead of publishing partial compaction/expansion truth.");
        }

        private void LoadAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptr = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            int byteCount = checked((int)(_totalElements * (ulong)elemSize));
            Span<byte> bufSrc = core.GetScratchA().Slice(0, byteCount);
            Span<byte> bufDst = core.GetScratchDst().Slice(0, byteCount);

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptr, bufSrc, _totalElements, elemSize, stride);

            // Both VCOMPRESS and VEXPAND operate in-place on a single published vector surface.
            // Seed the destination buffer before the ALU mutates it so untouched lanes stay truthful.
            bufSrc.CopyTo(bufDst);
        }

        private void ComputePredicativeMovement(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            int byteCount = checked((int)(_totalElements * (ulong)elemSize));
            Span<byte> bufSrc = core.GetScratchA().Slice(0, byteCount);
            Span<byte> bufDst = core.GetScratchDst().Slice(0, byteCount);

            switch (OpCode)
            {
                case Processor.CPU_Core.IsaOpcodeValues.VCOMPRESS:
                    VectorALU.ApplyCompress(
                        Instruction.DataTypeValue,
                        bufSrc,
                        bufDst,
                        elemSize,
                        _totalElements,
                        Instruction.PredicateMask,
                        ref core);
                    break;

                case Processor.CPU_Core.IsaOpcodeValues.VEXPAND:
                    VectorALU.ApplyExpand(
                        Instruction.DataTypeValue,
                        bufSrc,
                        bufDst,
                        elemSize,
                        _totalElements,
                        Instruction.PredicateMask,
                        Instruction.TailAgnostic,
                        Instruction.MaskAgnostic,
                        ref core);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"VectorPredicativeMovementMicroOp.Execute() rejected unsupported opcode 0x{OpCode:X} on the authoritative mainline predicative-movement contour.");
            }
        }

        private void StoreAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptr = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            int byteCount = checked((int)(_totalElements * (ulong)elemSize));
            Span<byte> bufDst = core.GetScratchDst().Slice(0, byteCount);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptr, bufDst, _totalElements, elemSize, stride);
        }
    }

    /// <summary>
    /// Vector dot product operation micro-operation (two vectors -> scalar)
    /// Examples: VDOT, VDOTU, VDOTF
    ///
    /// Dot product computes sum(vs1[i] * vs2[i]) for all active elements.
    /// Result is a scalar written to element 0 of destination.
    /// </summary>
    public class VectorDotProductMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for dot product vector operations.
        /// Dot product reads from two vectors and writes one scalar result.
        /// For widening operations (VDOT_E4M3, VDOT_E5M2): reads 1-byte elements, writes 4-byte result.
        /// Includes alignment checks for widening operations.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Dot product operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedDotProductElementDataTypeForMetadata();

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetDotProductElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;

                // Check if this is a widening operation
                bool isWidening = AddressGen.IsWideningOpcode(OpCode);

                // For widening operations: Alignment check (Early-fail)
                // Destination must be 4-byte aligned for FP32 result
                if (isWidening)
                {
                    if ((Instruction.DestSrc1Pointer & 0x3) != 0)
                    {
                        throw new System.Exception(
                            $"Alignment Fault: Destination address 0x{Instruction.DestSrc1Pointer:X} " +
                            $"must be 4-byte aligned for widening operation (opcode={OpCode})");
                    }
                }

                // Calculate effective element sizes for memory ranges
                uint srcA_ElemSize = AddressGen.GetEffectiveElementSize(OpCode, PortType.SourceA, (byte)elemSize);
                uint srcB_ElemSize = AddressGen.GetEffectiveElementSize(OpCode, PortType.SourceB, (byte)elemSize);
                uint dst_ElemSize = AddressGen.GetEffectiveElementSize(OpCode, PortType.Destination, (byte)elemSize);

                // Calculate total memory footprint (source: 1-byte per element, dest: 4-byte for widening)
                ulong totalSrcBytes = Instruction.StreamLength * srcA_ElemSize;
                ulong totalDstBytes = dst_ElemSize; // Only one scalar result

                // Read from two input vectors (asymmetric read: 1-byte elements for FP8)
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSrcBytes),
                    (Instruction.Src2Pointer, totalSrcBytes)
                };

                // Write only one scalar element (asymmetric write: 4-byte for FP32 result in widening)
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalDstBytes)
                };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                ThrowIfUnsupportedAddressingContour();
                ThrowIfZeroLengthVectorComputeContour("VectorDotProductMicroOp.Execute()");

                ThrowIfUnsupportedDotProductElementDataType();

                _state = ExecutionState.LoadingOperands;
            }

            // Dot product requires loading entire vectors
            if (_state == ExecutionState.LoadingOperands)
            {
                LoadAllElements(ref core);
                _state = ExecutionState.Computing;
            }

            if (_state == ExecutionState.Computing)
            {
                ComputeDotProduct(ref core);
                _state = ExecutionState.StoringResults;
            }

            if (_state == ExecutionState.StoringResults)
            {
                StoreScalarResult(ref core);
                _state = ExecutionState.Complete;
            }

            return true;
        }

        private void ThrowIfUnsupportedAddressingContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Indexed
                    ? "indexed"
                    : "2D";
            throw new InvalidOperationException(
                $"VectorDotProductMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline dot-product contour. " +
                "This carrier only follows through 1D scalar-footprint truth and must fail closed instead of hidden compat success on a non-representable surface.");
        }

        private void LoadAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetDotProductElementSize();
            ulong ptrA = Instruction.DestSrc1Pointer;
            ulong ptrB = Instruction.Src2Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrA, bufA, _totalElements, elemSize, stride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrB, bufB, _totalElements, elemSize, stride);
        }

        private void ComputeDotProduct(ref Processor.CPU_Core core)
        {
            int elemSize = GetDotProductElementSize();
            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();
            Span<byte> bufDst = core.GetScratchDst();

            // ApplyDotProduct computes scalar result and writes to element 0
            VectorALU.ApplyDotProduct(
                OpCode,
                Instruction.DataTypeValue,
                bufA,
                bufB,
                bufDst,
                elemSize,
                _totalElements,
                Instruction.PredicateMask,
                ref core);
        }

        private void StoreScalarResult(ref Processor.CPU_Core core)
        {
            int elemSize = GetDotProductElementSize();
            ulong ptrDst = Instruction.DestSrc1Pointer;
            uint effectiveDestElemSize =
                AddressGen.GetEffectiveElementSize(OpCode, PortType.Destination, (byte)elemSize);

            Span<byte> bufDst = core.GetScratchDst();

            // Widening FP8 dot-product writes a single FP32 scalar result even though the
            // source element size remains 1 byte. Reuse the shared address-gen contract so
            // runtime follow-through matches the already-published metadata footprint.
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(
                ptrDst,
                bufDst,
                1,
                (int)effectiveDestElemSize,
                (ushort)effectiveDestElemSize);
        }

        private int GetDotProductElementSize()
        {
            int elemSize = GetElementSize();
            if (elemSize != 0)
            {
                return elemSize;
            }

            return OpCode == Processor.CPU_Core.IsaOpcodeValues.VDOT_FP8 &&
                   (Instruction.DataTypeValue == DataTypeEnum.FLOAT8_E4M3 ||
                    Instruction.DataTypeValue == DataTypeEnum.FLOAT8_E5M2)
                ? 1
                : 0;
        }

        private void ThrowIfUnsupportedDotProductElementDataType()
        {
            if (GetDotProductElementSize() != 0)
            {
                return;
            }

            throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                $"VectorDotProductMicroOp.Execute() rejected unsupported element DataType 0x{Instruction.DataType:X2} on the authoritative mainline vector compute contour instead of hidden success/no-op.");
        }

        private void ThrowIfUnsupportedDotProductElementDataTypeForMetadata()
        {
            if (Instruction.StreamLength == 0 || GetDotProductElementSize() != 0)
            {
                return;
            }

            DecodeProjectionFaultException exception = new(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.UnsupportedVectorElementType,
                    $"VectorDotProductMicroOp.InitializeMetadata() rejected unsupported element DataType 0x{Instruction.DataType:X2} on the authoritative mainline vector publication contour. " +
                    "Materialized dot-product carriers must fail closed during metadata publication instead of emitting scalar-footprint facts for an invalid element type."));
            ExecutionFaultContract.Stamp(exception, ExecutionFaultCategory.UnsupportedVectorElementType);
            throw exception;
        }
    }

    // ===== Phase 6: New Stream/Vector Load/Store MicroOps =====
    // These classes implement the gradual migration of StreamEngine logic into micro-operations

    /// <summary>
    /// Compatibility micro-operation for legacy vector transfer opcodes that still execute
    /// through the StreamEngine but now must carry explicit WB retire authority.
    /// </summary>
    public sealed class VectorTransferMicroOp : VectorMicroOp
    {
        private byte[]? _transferBuffer;

        public VectorTransferMicroOp()
        {
            Class = MicroOpClass.Vector;
            IsStealable = false;
            HasSideEffects = true;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorTransferMicroOp.InitializeMetadata()");

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong totalSize = ResolveTransferTotalSize(elemSize);
                ResolveTransferEndpoints(
                    out ulong readPointer,
                    out ulong writePointer);

                ReadMemoryRanges = new[] { (readPointer, totalSize) };
                WriteMemoryRanges = new[] { (writePointer, totalSize) };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            ThrowIfUnsupportedAddressingContour();

            if (Instruction.StreamLength == 0)
            {
                throw new InvalidOperationException(
                    "VectorTransferMicroOp.Execute() rejected StreamLength == 0 on the authoritative mainline VLOAD/VSTORE carrier contour instead of hidden success/no-op.");
            }

            int elemSize = GetElementSize();
            ThrowIfUnsupportedElementDataType("VectorTransferMicroOp.Execute()");

            ulong stride = ResolveTransferStride(elemSize);
            ResolveTransferEndpoints(
                out ulong readPointer,
                out ulong writePointer);

            int transferByteCount = checked((int)ResolveTransferByteCount(elemSize));
            _transferBuffer ??= new byte[transferByteCount];
            if (_transferBuffer.Length != transferByteCount)
            {
                _transferBuffer = new byte[transferByteCount];
            }

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(
                readPointer,
                _transferBuffer,
                Instruction.StreamLength,
                elemSize,
                (ushort)stride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(
                writePointer,
                _transferBuffer,
                Instruction.StreamLength,
                elemSize,
                (ushort)stride);

            return true;
        }

        private void ThrowIfUnsupportedAddressingContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Indexed
                    ? "indexed"
                    : "2D";
            throw new InvalidOperationException(
                $"VectorTransferMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline VLOAD/VSTORE carrier contour. " +
                "This carrier only publishes 1D transfer-shape truth and must fail closed instead of hidden compat success on a non-representable surface.");
        }

        private ulong ResolveTransferStride(int elemSize) =>
            Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;

        private ulong ResolveTransferByteCount(int elemSize) =>
            Instruction.StreamLength * (ulong)elemSize;

        private ulong ResolveTransferTotalSize(int elemSize) =>
            Instruction.StreamLength * ResolveTransferStride(elemSize);

        private void ResolveTransferEndpoints(
            out ulong readPointer,
            out ulong writePointer)
        {
            switch (OpCode)
            {
                case Processor.CPU_Core.IsaOpcodeValues.VLOAD:
                    readPointer = Instruction.Src2Pointer;
                    writePointer = Instruction.DestSrc1Pointer;
                    return;

                case Processor.CPU_Core.IsaOpcodeValues.VSTORE:
                    readPointer = Instruction.DestSrc1Pointer;
                    writePointer = Instruction.Src2Pointer;
                    return;

                default:
                    throw new InvalidOperationException(
                        $"VectorTransferMicroOp received unsupported transfer opcode 0x{OpCode:X} on the authoritative VLOAD/VSTORE carrier contour.");
            }
        }
    }

    /// <summary>
    /// Load segment micro-operation for 1D vector loads.
    /// Initiates BurstIO.Read and manages async memory request tokens.
    ///
    /// Phase 6 Design:
    /// - Separates load operation from computation
    /// - Enables scheduler to insert other operations during memory wait
    /// - Uses async MemorySubsystem for burst transfers

    public class VConfigMicroOp : MicroOp
    {
        private VectorConfigRetireEffect _resolvedRetireEffect;
        private ulong _actualVectorLength;

        public VectorConfigOperationKind OperationKind { get; private set; }
        public ushort SrcReg1ID { get; private set; } = VLIW_Instruction.NoReg;
        public ushort SrcReg2ID { get; private set; } = VLIW_Instruction.NoReg;
        public ulong EncodedVTypeImmediate { get; private set; }
        public ulong EncodedAvlImmediate { get; private set; }

        public VConfigMicroOp()
        {
            // VConfig cannot be stolen (modifies architectural state)
            IsStealable = false;
            HasSideEffects = true;
            Latency = 1;
            Class = MicroOpClass.Other;
            ResourceMask = ResourceBitset.Zero;

            // ISA v4 Phase 02: vector config is System class, FullSerial serialization
            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            ulong avl = ResolveApplicationVectorLength(ref core, vtId);
            ulong vtype = ResolveVType(ref core, vtId);

            _actualVectorLength = Math.Min(avl, Processor.CPU_Core.RVV_Config.VLMAX);
            byte tailAgnostic = (byte)((vtype >> 6) & 0x1);
            byte maskAgnostic = (byte)((vtype >> 7) & 0x1);

            bool hasRegisterWriteback = HasArchitecturalDestinationRegister();
            _resolvedRetireEffect = VectorConfigRetireEffect.Create(
                OperationKind,
                _actualVectorLength,
                vtype,
                tailAgnostic,
                maskAgnostic,
                hasRegisterWriteback,
                DestRegID);
            return true;
        }

        public void ConfigureForRegisterVType(
            byte rs1,
            byte rs2)
        {
            OperationKind = VectorConfigOperationKind.Vsetvl;
            SrcReg1ID = rs1;
            SrcReg2ID = rs2;
            EncodedAvlImmediate = 0;
            EncodedVTypeImmediate = 0;
        }

        public void ConfigureForImmediateVType(
            VectorConfigOperationKind operationKind,
            byte rs1,
            ulong encodedVTypeImmediate)
        {
            OperationKind = operationKind;
            SrcReg1ID = rs1;
            SrcReg2ID = VLIW_Instruction.NoReg;
            EncodedAvlImmediate = 0;
            EncodedVTypeImmediate = encodedVTypeImmediate;
        }

        public void ConfigureForImmediateAvlAndVType(
            ulong encodedAvlImmediate,
            ulong encodedVTypeImmediate)
        {
            OperationKind = VectorConfigOperationKind.Vsetivli;
            SrcReg1ID = VLIW_Instruction.NoReg;
            SrcReg2ID = VLIW_Instruction.NoReg;
            EncodedAvlImmediate = encodedAvlImmediate;
            EncodedVTypeImmediate = encodedVTypeImmediate;
        }

        public void InitializeMetadata()
        {
            bool writesRegister = HasArchitecturalDestinationRegister();
            WritesRegister = writesRegister;

            ReadRegisters = OperationKind switch
            {
                VectorConfigOperationKind.Vsetvl => BuildReadRegisterList(SrcReg1ID, SrcReg2ID),
                VectorConfigOperationKind.Vsetvli => BuildReadRegisterList(SrcReg1ID),
                _ => Array.Empty<int>()
            };

            WriteRegisters = writesRegister
                ? new[] { (int)DestRegID }
                : Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            ResourceMask = ResourceBitset.Zero;
            foreach (int registerId in ReadRegisters)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(registerId);
            }

            if (writesRegister)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public VectorConfigRetireEffect CreateRetireEffect() => _resolvedRetireEffect;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _actualVectorLength;
            return HasArchitecturalDestinationRegister();
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _actualVectorLength = value;

        public override string GetDescription()
        {
            return $"{OperationKind}: VL={_actualVectorLength}, VTYPE=0x{ResolveDescriptionVType():X}";
        }

        private static int[] BuildReadRegisterList(params ushort[] rawRegisters)
        {
            var registers = new List<int>(rawRegisters.Length);
            foreach (ushort rawRegister in rawRegisters)
            {
                if (rawRegister == 0 ||
                    rawRegister == VLIW_Instruction.NoReg)
                {
                    continue;
                }

                registers.Add(rawRegister);
            }

            return registers.Count == 0
                ? Array.Empty<int>()
                : registers.ToArray();
        }

        private bool HasArchitecturalDestinationRegister()
        {
            return DestRegID != 0 &&
                   DestRegID != VLIW_Instruction.NoReg;
        }

        private ulong ResolveApplicationVectorLength(ref Processor.CPU_Core core, int vtId)
        {
            return OperationKind == VectorConfigOperationKind.Vsetivli
                ? EncodedAvlImmediate
                : ReadUnifiedScalarSourceOperand(ref core, vtId, SrcReg1ID);
        }

        private ulong ResolveVType(ref Processor.CPU_Core core, int vtId)
        {
            return OperationKind == VectorConfigOperationKind.Vsetvl
                ? ReadUnifiedScalarSourceOperand(ref core, vtId, SrcReg2ID)
                : EncodedVTypeImmediate;
        }

        private ulong ResolveDescriptionVType()
        {
            return OperationKind == VectorConfigOperationKind.Vsetvl
                ? 0
                : EncodedVTypeImmediate;
        }
    }
}
