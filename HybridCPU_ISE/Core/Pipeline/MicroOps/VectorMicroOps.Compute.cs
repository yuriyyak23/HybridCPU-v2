
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using AddressGen = YAKSys_Hybrid_CPU.Execution.AddressGen;
using PortType = YAKSys_Hybrid_CPU.Execution.PortType;
using HybridCPU_ISE.Arch;


namespace YAKSys_Hybrid_CPU.Core
{
    public class VectorBinaryOpMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for binary vector operations.
        /// Binary operations read from two memory ranges and write to one.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Binary operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorBinaryOpMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from two sources
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize),
                    (Instruction.Src2Pointer, totalSize)
                };

                // Write to destination (same as first source for in-place operations)
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
                ThrowIfZeroLengthVectorComputeContour("VectorBinaryOpMicroOp.Execute()");

                ThrowIfUnsupportedElementDataType("VectorBinaryOpMicroOp.Execute()");

                _state = ExecutionState.LoadingOperands;
            }

            // Process strip-mining chunks
            while (_elementsProcessed < _totalElements)
            {
                ulong remaining = _totalElements - _elementsProcessed;
                ulong chunkSize = GetChunkSize(remaining);

                if (_state == ExecutionState.LoadingOperands)
                {
                    LoadChunk(ref core, chunkSize);
                    _state = ExecutionState.Computing;
                }

                if (_state == ExecutionState.Computing)
                {
                    ComputeChunk(ref core, chunkSize);
                    _state = ExecutionState.StoringResults;
                }

                if (_state == ExecutionState.StoringResults)
                {
                    StoreChunk(ref core, chunkSize);
                    _elementsProcessed += chunkSize;
                    _state = ExecutionState.LoadingOperands; // Next chunk
                }
            }

            _state = ExecutionState.Complete;
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
                $"VectorBinaryOpMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline binary contour. " +
                "This carrier only follows through 1D in-place memory semantics and must fail closed instead of hidden success on a non-representable compat surface.");
        }

        private void LoadChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong ptrA = Instruction.DestSrc1Pointer + (_elementsProcessed * (ulong)elemSize);
            ulong ptrB = Instruction.Src2Pointer + (_elementsProcessed * (ulong)elemSize);
            ushort stride = Instruction.Stride;

            if (stride == 0)
                stride = (ushort)elemSize;

            // Get scratch buffers
            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();
            Span<byte> bufDst = core.GetScratchDst();

            // Use BurstIO for memory transfers
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrA, bufA, chunkSize, elemSize, stride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrB, bufB, chunkSize, elemSize, stride);

            // Binary vector semantics preserve masked-off destination lanes, and the current
            // in-place carrier aliases destination with source A on the published contour.
            int chunkByteCount = checked((int)(chunkSize * (ulong)elemSize));
            bufA.Slice(0, chunkByteCount).CopyTo(bufDst);
        }

        private void ComputeChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();
            Span<byte> bufDst = core.GetScratchDst();

            // Delegate to VectorALU for type-agnostic computation
            VectorALU.ApplyBinary(
                OpCode,
                Instruction.DataTypeValue,
                bufA,
                bufB,
                bufDst,
                elemSize,
                chunkSize,
                Instruction.PredicateMask,
                Instruction.TailAgnostic,
                Instruction.MaskAgnostic,
                ref core);
        }

        private void StoreChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong ptrDst = Instruction.DestSrc1Pointer + (_elementsProcessed * (ulong)elemSize);
            ushort stride = Instruction.Stride;

            if (stride == 0)
                stride = (ushort)elemSize;

            Span<byte> bufDst = core.GetScratchDst();

            // Use BurstIO for memory transfers
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptrDst, bufDst, chunkSize, elemSize, stride);
        }
    }

    /// <summary>
    /// Vector unary operation micro-operation (one operand > one result)
    /// Examples: VSQRT, VNOT, VABS, VNEG
    /// </summary>
    public class VectorUnaryOpMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for unary vector operations.
        /// Unary operations read from one memory range and write to one.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Unary operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorUnaryOpMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from one source
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };

                // Write to destination (same as source for in-place operations)
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
                ThrowIfZeroLengthVectorComputeContour("VectorUnaryOpMicroOp.Execute()");

                ThrowIfUnsupportedElementDataType("VectorUnaryOpMicroOp.Execute()");

                _state = ExecutionState.LoadingOperands;
            }

            // Process strip-mining chunks
            while (_elementsProcessed < _totalElements)
            {
                ulong remaining = _totalElements - _elementsProcessed;
                ulong chunkSize = GetChunkSize(remaining);

                if (_state == ExecutionState.LoadingOperands)
                {
                    LoadChunk(ref core, chunkSize);
                    _state = ExecutionState.Computing;
                }

                if (_state == ExecutionState.Computing)
                {
                    ComputeChunk(ref core, chunkSize);
                    _state = ExecutionState.StoringResults;
                }

                if (_state == ExecutionState.StoringResults)
                {
                    StoreChunk(ref core, chunkSize);
                    _elementsProcessed += chunkSize;
                    _state = ExecutionState.LoadingOperands;
                }
            }

            _state = ExecutionState.Complete;
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
                $"VectorUnaryOpMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline unary contour. " +
                "This carrier only follows through 1D in-place memory semantics and must fail closed instead of hidden success on a non-representable compat surface.");
        }

        private void LoadChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong ptr = Instruction.DestSrc1Pointer + (_elementsProcessed * (ulong)elemSize);
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufDst = core.GetScratchDst();
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptr, bufSrc, chunkSize, elemSize, stride);

            // Unary in-place semantics preserve masked-off destination lanes, so seed the
            // destination scratch surface from the current vector contents before ApplyUnary mutates it.
            int chunkByteCount = checked((int)(chunkSize * (ulong)elemSize));
            bufSrc.Slice(0, chunkByteCount).CopyTo(bufDst);
        }

        private void ComputeChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufDst = core.GetScratchDst();

            VectorALU.ApplyUnary(
                OpCode,
                Instruction.DataTypeValue,
                bufSrc,
                bufDst,
                elemSize,
                chunkSize,
                Instruction.PredicateMask,
                Instruction.TailAgnostic,
                Instruction.MaskAgnostic,
                ref core);
        }

        private void StoreChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong ptr = Instruction.DestSrc1Pointer + (_elementsProcessed * (ulong)elemSize);
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufDst = core.GetScratchDst();
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptr, bufDst, chunkSize, elemSize, stride);
        }
    }

    /// <summary>
    /// Vector FMA (Fused Multiply-Add) operation micro-operation (three operands > one result)
    /// Examples: VFMADD, VFMSUB, VFNMADD, VFNMSUB
    ///
    /// FMA operations are ternary (three-operand):
    /// - VFMADD: vd[i] = (vd[i] * vs1[i]) + vs2[i]
    /// - VFMSUB: vd[i] = (vd[i] * vs1[i]) - vs2[i]
    /// - VFNMADD: vd[i] = -(vd[i] * vs1[i]) + vs2[i]
    /// - VFNMSUB: vd[i] = -(vd[i] * vs1[i]) - vs2[i]
    ///
    /// Note: vd is both source and destination (destructive operation)
    /// </summary>
    public class VectorFmaMicroOp : VectorMicroOp
    {
        private ulong _srcAPointer;
        private ulong _srcBPointer;
        private ushort _srcAStride;
        private ushort _srcBStride;
        private bool _descriptorResolved;

        /// <summary>
        /// Initialize FSP metadata for FMA vector operations.
        /// FMA operations read from three memory ranges and write to one.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // FMA operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorFmaMicroOp.InitializeMetadata()");
            ThrowIfUnsupportedAddressingContour("VectorFmaMicroOp.InitializeMetadata()", isPublicationContour: true);

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                ResolveTriOperandDescriptor("VectorFmaMicroOp.InitializeMetadata()", isPublicationContour: true);

                int elemSize = GetElementSize();
                ulong destStride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalDestSize = Instruction.StreamLength * destStride;
                ulong totalSrcASize = Instruction.StreamLength * (ulong)_srcAStride;
                ulong totalSrcBSize = Instruction.StreamLength * (ulong)_srcBStride;

                // Read from accumulator/destination and both descriptor-backed sources.
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalDestSize),
                    (_srcAPointer, totalSrcASize),
                    (_srcBPointer, totalSrcBSize)
                };

                // Write to destination (same as vd)
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalDestSize)
                };
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                ThrowIfUnsupportedAddressingContour("VectorFmaMicroOp.Execute()", isPublicationContour: false);
                ThrowIfZeroLengthVectorComputeContour("VectorFmaMicroOp.Execute()");
                ThrowIfUnsupportedElementDataType("VectorFmaMicroOp.Execute()");
                ResolveTriOperandDescriptor("VectorFmaMicroOp.Execute()", isPublicationContour: false);

                _state = ExecutionState.LoadingOperands;
            }

            while (_elementsProcessed < _totalElements)
            {
                ulong remaining = _totalElements - _elementsProcessed;
                ulong chunkSize = GetChunkSize(remaining);

                if (_state == ExecutionState.LoadingOperands)
                {
                    LoadChunk(ref core, chunkSize);
                    _state = ExecutionState.Computing;
                }

                if (_state == ExecutionState.Computing)
                {
                    ComputeChunk(ref core, chunkSize);
                    _state = ExecutionState.StoringResults;
                }

                if (_state == ExecutionState.StoringResults)
                {
                    StoreChunk(ref core, chunkSize);
                    _elementsProcessed += chunkSize;
                    _state = _elementsProcessed >= _totalElements
                        ? ExecutionState.Complete
                        : ExecutionState.LoadingOperands;
                }
            }

            return _state == ExecutionState.Complete;
        }

        private void LoadChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong destStride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
            ulong ptrDst = Instruction.DestSrc1Pointer + (_elementsProcessed * destStride);
            ulong ptrSrcA = _srcAPointer + (_elementsProcessed * (ulong)_srcAStride);
            ulong ptrSrcB = _srcBPointer + (_elementsProcessed * (ulong)_srcBStride);

            Span<byte> bufDst = core.GetScratchDst();
            Span<byte> bufSrcA = core.GetScratchA();
            Span<byte> bufSrcB = core.GetScratchB();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrDst, bufDst, chunkSize, elemSize, (ushort)destStride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrcA, bufSrcA, chunkSize, elemSize, _srcAStride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrSrcB, bufSrcB, chunkSize, elemSize, _srcBStride);
        }

        private void ComputeChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            Span<byte> bufDst = core.GetScratchDst();
            Span<byte> bufSrcA = core.GetScratchA();
            Span<byte> bufSrcB = core.GetScratchB();

            VectorALU.ApplyFMA(
                OpCode,
                Instruction.DataTypeValue,
                bufDst,
                bufSrcA,
                bufSrcB,
                elemSize,
                chunkSize,
                Instruction.PredicateMask,
                Instruction.TailAgnostic,
                Instruction.MaskAgnostic,
                ref core);
        }

        private void StoreChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong destStride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
            ulong ptrDst = Instruction.DestSrc1Pointer + (_elementsProcessed * destStride);

            Span<byte> bufDst = core.GetScratchDst();
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptrDst, bufDst, chunkSize, elemSize, (ushort)destStride);
        }

        private void ResolveTriOperandDescriptor(string surface, bool isPublicationContour)
        {
            if (_descriptorResolved)
            {
                return;
            }

            if (Instruction.Src2Pointer == 0)
            {
                string contour = isPublicationContour ? "publication" : "runtime";
                string message =
                    $"{surface} rejected descriptor-less tri-operand FMA {contour} on the authoritative descriptor-backed contour. " +
                    "Current VFM* encoding requires Src2Pointer to reference a TriOpDesc, so this carrier must fail closed instead of reviving the legacy descriptor-less side path.";
                if (isPublicationContour)
                {
                    throw new DecodeProjectionFaultException(message);
                }

                throw new InvalidOperationException(message);
            }

            int elemSize = GetElementSize();
            TriOpDesc desc = ReadTriOpDesc(Instruction.Src2Pointer);
            _srcAPointer = desc.SrcA;
            _srcBPointer = desc.SrcB;
            _srcAStride = desc.StrideA != 0 ? desc.StrideA : (ushort)elemSize;
            _srcBStride = desc.StrideB != 0 ? desc.StrideB : (ushort)elemSize;
            _descriptorResolved = true;
        }

        private static TriOpDesc ReadTriOpDesc(ulong descriptorAddr)
        {
            Span<byte> descBuf = stackalloc byte[20];
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(descriptorAddr, descBuf, 1, 20, 20);

            TriOpDesc desc;
            desc.SrcA = BitConverter.ToUInt64(descBuf.Slice(0, 8));
            desc.SrcB = BitConverter.ToUInt64(descBuf.Slice(8, 8));
            desc.StrideA = BitConverter.ToUInt16(descBuf.Slice(16, 2));
            desc.StrideB = BitConverter.ToUInt16(descBuf.Slice(18, 2));
            return desc;
        }

        private void ThrowIfUnsupportedAddressingContour(string surface, bool isPublicationContour)
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
            string contour = isPublicationContour ? "publication" : "runtime";
            throw new InvalidOperationException(
                $"{surface} rejected unsupported {addressingContour} vector-FMA {contour} on the authoritative descriptor-backed tri-operand contour. " +
                "This carrier does not publish indexed/2D follow-through truth and must fail closed instead of hidden compat success on a non-representable surface.");
        }
    }

    /// <summary>
    /// Vector reduction operation micro-operation (vector > scalar)
    /// Examples: VREDSUM, VREDMAX, VREDMIN, VREDAND, VREDOR, VREDXOR
    ///
    /// Reduction operations collapse a vector to a single scalar value.
    /// Result is written to element 0 of destination.
    /// </summary>
    public class VectorReductionMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for reduction vector operations.
        /// Reduction operations read from entire vector and write one scalar result.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            // Reduction operations can be stolen
            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorReductionMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from entire vector
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize)
                };

                // Write only one scalar element (element 0)
                WriteMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, (ulong)elemSize)
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
                ThrowIfZeroLengthVectorComputeContour("VectorReductionMicroOp.Execute()");

                ThrowIfUnsupportedElementDataType("VectorReductionMicroOp.Execute()");

                _state = ExecutionState.LoadingOperands;
            }

            // For reductions, we process the entire vector as a single chunk
            // since we need to accumulate across all elements
            if (_state == ExecutionState.LoadingOperands)
            {
                LoadAllElements(ref core);
                _state = ExecutionState.Computing;
            }

            if (_state == ExecutionState.Computing)
            {
                ComputeReduction(ref core);
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
                $"VectorReductionMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline reduction contour. " +
                "This carrier only follows through 1D scalar-footprint memory semantics and must fail closed instead of hidden success on a non-representable compat surface.");
        }

        private void LoadAllElements(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptr = Instruction.DestSrc1Pointer;
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufSrc = core.GetScratchA();
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptr, bufSrc, _totalElements, elemSize, stride);
        }

        private void ComputeReduction(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            Span<byte> bufSrc = core.GetScratchA();
            Span<byte> bufDst = core.GetScratchDst();

            // ApplyReduction writes result to element 0 of destination
            VectorALU.ApplyReduction(
                OpCode,
                Instruction.DataTypeValue,
                bufSrc,
                bufDst,
                elemSize,
                _totalElements,
                Instruction.PredicateMask,
                ref core);
        }

        private void StoreScalarResult(ref Processor.CPU_Core core)
        {
            int elemSize = GetElementSize();
            ulong ptrDst = Instruction.DestSrc1Pointer;

            Span<byte> bufDst = core.GetScratchDst();

            // Write only the first element (scalar result)
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(ptrDst, bufDst, 1, elemSize, (ushort)elemSize);
        }
    }

    /// <summary>
    /// Vector comparison operation micro-operation (two operands > predicate mask)
    /// Examples: VCMPEQ, VCMPNE, VCMPLT, VCMPLE, VCMPGT, VCMPGE
    ///
    /// Comparison operations generate predicate masks (1 bit per element).
    /// Result is written to a predicate register, not memory.
    /// </summary>
    public class VectorComparisonMicroOp : VectorMicroOp
    {
        private ulong _pendingChunkResultMask;
        private ulong _accumulatedResultMask;

        /// <summary>
        /// Initialize FSP metadata for comparison vector operations.
        /// Comparison operations read from two sources and write predicate mask.
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            IsMemoryOp = false;
            ThrowIfUnsupportedElementDataTypeForMetadata("VectorComparisonMicroOp.InitializeMetadata()");

            // Calculate memory ranges based on instruction parameters
            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Comparison reads vector operands from memory but publishes only
                // predicate-state follow-through on the authoritative mainline contour.
                ReadMemoryRanges = new[]
                {
                    (Instruction.DestSrc1Pointer, totalSize),
                    (Instruction.Src2Pointer, totalSize)
                };

                WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: false);
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
                if (_totalElements == 0)
                {
                    throw new InvalidOperationException(
                        "VectorComparisonMicroOp.Execute() rejected StreamLength == 0 on the authoritative mainline predicate-publication contour instead of hidden success/no-op.");
                }

                int elemSize = GetElementSize();
                if (elemSize == 0)
                {
                    throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                        $"VectorComparisonMicroOp.Execute() rejected unsupported element DataType 0x{Instruction.DataType:X2} on the authoritative mainline predicate-publication contour instead of hidden success/no-op.");
                }

                _elementsProcessed = 0;
                _pendingChunkResultMask = 0;
                _accumulatedResultMask = 0;
                _state = ExecutionState.LoadingOperands;
            }

            // Process strip-mining chunks
            while (_elementsProcessed < _totalElements)
            {
                ulong remaining = _totalElements - _elementsProcessed;
                ulong chunkSize = GetChunkSize(remaining);

                if (_state == ExecutionState.LoadingOperands)
                {
                    LoadChunk(ref core, chunkSize);
                    _state = ExecutionState.Computing;
                }

                if (_state == ExecutionState.Computing)
                {
                    ComputeChunk(ref core, chunkSize);
                    _state = ExecutionState.StoringResults;
                }

                if (_state == ExecutionState.StoringResults)
                {
                    StoreChunk(ref core, chunkSize);
                    _elementsProcessed += chunkSize;
                    _state = ExecutionState.LoadingOperands;
                }
            }

            PublishPredicateMask(ref core);
            _state = ExecutionState.Complete;
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
                $"VectorComparisonMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline predicate-publication contour. " +
                "This carrier only follows through 1D predicate-state truth and must fail closed instead of hidden compat success on a non-representable surface.");
        }

        private void LoadChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            ulong ptrA = Instruction.DestSrc1Pointer + (_elementsProcessed * (ulong)elemSize);
            ulong ptrB = Instruction.Src2Pointer + (_elementsProcessed * (ulong)elemSize);
            ushort stride = Instruction.Stride;
            if (stride == 0) stride = (ushort)elemSize;

            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();

            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrA, bufA, chunkSize, elemSize, stride);
            YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(ptrB, bufB, chunkSize, elemSize, stride);
        }

        private void ComputeChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int elemSize = GetElementSize();
            Span<byte> bufA = core.GetScratchA();
            Span<byte> bufB = core.GetScratchB();

            _pendingChunkResultMask = VectorALU.ApplyComparison(
                OpCode,
                Instruction.DataTypeValue,
                bufA,
                bufB,
                elemSize,
                chunkSize,
                (byte)Instruction.PredicateMask,
                ref core);
        }

        private void StoreChunk(ref Processor.CPU_Core core, ulong chunkSize)
        {
            int shift = (int)Math.Min(_elementsProcessed, 64UL);
            if (shift < 64)
            {
                ulong shiftedMask = shift == 0
                    ? _pendingChunkResultMask
                    : _pendingChunkResultMask << shift;
                _accumulatedResultMask |= shiftedMask;
            }
        }

        private void PublishPredicateMask(ref Processor.CPU_Core core)
        {
            byte destPredReg = (byte)(Instruction.Immediate & 0x0F);
            core.SetPredicateRegister(destPredReg, _accumulatedResultMask);
        }
    }

    /// <summary>
    /// Vector mask operation micro-operation (predicate register operations)
    /// Examples: VMAND, VMOR, VMXOR, VMNOT
    ///
    /// Mask operations work directly on predicate registers (1 bit per element).
    /// These are logical operations on mask bits, not data elements.
    /// </summary>
    public class VectorMaskOpMicroOp : VectorMicroOp
    {
        /// <summary>
        /// Initialize FSP metadata for mask vector operations.
        /// Mask operations work on predicate registers (byte-packed masks).
        /// </summary>
        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            IsMemoryOp = false;
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: false);
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
                if (_totalElements == 0)
                {
                    throw new InvalidOperationException(
                        "VectorMaskOpMicroOp.Execute() rejected StreamLength == 0 on the authoritative mainline predicate-publication contour instead of hidden success/no-op.");
                }

                _state = ExecutionState.Computing;
            }

            if (OpCode == Processor.CPU_Core.IsaOpcodeValues.VPOPC)
            {
                throw new InvalidOperationException(
                    "VectorMaskOpMicroOp.Execute() rejected scalar-writing VPOPC on the authoritative mainline predicate-publication contour; the opcode still requires dedicated scalar retire/apply follow-through instead of hidden predicate-only success.");
            }

            ExecutePredicateMaskManipulation(ref core);
            _state = ExecutionState.Complete;
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
                $"VectorMaskOpMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline predicate-publication contour. " +
                "This carrier does not materialize any addressed vector surface and must fail closed instead of hidden compat success on an addressing-tag surface.");
        }

        private void ExecutePredicateMaskManipulation(ref Processor.CPU_Core core)
        {
            byte srcPred1 = (byte)((Instruction.Immediate >> 0) & 0x0F);
            byte srcPred2 = (byte)((Instruction.Immediate >> 4) & 0x0F);
            byte destPred = (byte)((Instruction.Immediate >> 8) & 0x0F);

            ulong mask1 = core.GetPredicateRegister(srcPred1);

            ulong resultMask = OpCode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.VMAND =>
                    VectorALU.ApplyMaskBinary(
                        OpCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                Processor.CPU_Core.IsaOpcodeValues.VMOR =>
                    VectorALU.ApplyMaskBinary(
                        OpCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                Processor.CPU_Core.IsaOpcodeValues.VMXOR =>
                    VectorALU.ApplyMaskBinary(
                        OpCode,
                        mask1,
                        core.GetPredicateRegister(srcPred2)),
                Processor.CPU_Core.IsaOpcodeValues.VMNOT =>
                    VectorALU.ApplyMaskUnary(OpCode, mask1),
                _ => throw new InvalidOperationException(
                    $"VectorMaskOpMicroOp.Execute() rejected unsupported opcode 0x{OpCode:X} on the authoritative mainline predicate-publication contour.")
            };

            core.SetPredicateRegister(destPred, resultMask);
        }
    }

    /// <summary>
    /// Dedicated scalar-result vector mask population-count micro-operation.
    /// Example: VPOPC
    /// </summary>
    public sealed class VectorMaskPopCountMicroOp : VectorMicroOp
    {
        private ulong _result;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            IsMemoryOp = false;
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            ushort destRegId = (ushort)((Instruction.Immediate >> 8) & 0x0F);
            if (destRegId < YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
            {
                DestRegID = destRegId;
                WritesRegister = true;
                WriteRegisters = new[] { (int)destRegId };
            }
            else
            {
                DestRegID = VLIW_Instruction.NoReg;
                WritesRegister = false;
                WriteRegisters = Array.Empty<int>();
            }

            RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: false);
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
                if (_totalElements == 0)
                {
                    throw new InvalidOperationException(
                        "VectorMaskPopCountMicroOp.Execute() rejected StreamLength == 0 on the authoritative mainline scalar-result contour instead of hidden success/no-op.");
                }

                byte srcPred = (byte)(Instruction.Immediate & 0x0F);
                ulong vl = Math.Min(Instruction.StreamLength, Processor.CPU_Core.RVV_Config.VLMAX);
                _result = VectorALU.MaskPopCount(core.GetPredicateRegister(srcPred), vl);
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
                $"VectorMaskPopCountMicroOp.Execute() rejected {addressingContour} addressing on the authoritative mainline scalar-result contour. " +
                "This carrier only follows through predicate-to-scalar writeback truth and must fail closed instead of hidden compat success on an addressing-tag surface.");
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (!WritesRegister)
            {
                return;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestRegID, _result));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _result;
            return WritesRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _result = value;
    }

    /// <summary>
    /// Vector permutation operation micro-operation (single operand with index vector)
    /// Examples: VPERMUTE, VRGATHER
    ///
    /// Permutation operations rearrange elements based on an index vector.
}
