
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using AddressGen = YAKSys_Hybrid_CPU.Execution.AddressGen;
using PortType = YAKSys_Hybrid_CPU.Execution.PortType;


namespace YAKSys_Hybrid_CPU.Core
{
    public class LoadSegmentMicroOp : VectorMicroOp
    {
        private YAKSys_Hybrid_CPU.Memory.MemorySubsystem.MemoryRequestToken? _requestToken;
        private byte[]? _loadBuffer;

        public LoadSegmentMicroOp()
        {
            IsMemoryOp = true;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                // Read from source memory
                ReadMemoryRanges = new[] { (Instruction.DestSrc1Pointer, totalSize) };
                WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: false);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();
                ushort stride = Instruction.Stride;
                if (stride == 0) stride = (ushort)elemSize;

                // Allocate buffer for loaded data
                _loadBuffer = new byte[_totalElements * (ulong)elemSize];

                // Initiate async load using BurstIO
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    _requestToken = memSub.EnqueueRead(
                        0, // CPU Device ID
                        Instruction.DestSrc1Pointer,
                        (int)_loadBuffer.Length,
                        _loadBuffer);
                    _state = ExecutionState.LoadingOperands;
                    return false; // Not complete, needs retry
                }
                else
                {
                    // Fallback: synchronous load
                    YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(
                        Instruction.DestSrc1Pointer,
                        _loadBuffer,
                        _totalElements,
                        elemSize,
                        stride);
                    _state = ExecutionState.Complete;
                    return true;
                }
            }

            if (_state == ExecutionState.LoadingOperands)
            {
                if (_requestToken != null && _requestToken.IsComplete)
                {
                    _requestToken.ThrowIfFailed("LoadSegmentMicroOp.Execute()");
                    // Load complete, data is in _loadBuffer
                    _state = ExecutionState.Complete;
                    return true;
                }
                return false; // Still waiting
            }

            return _state == ExecutionState.Complete;
        }

        /// <summary>
        /// Get loaded buffer for consumption by downstream MicroOps
        /// </summary>
        public byte[]? GetLoadedBuffer() => _loadBuffer;
    }

    /// <summary>
    /// Load 2D micro-operation for 2D vector loads.
    /// Uses BurstPlanner.Plan2D for complex addressing patterns.
    /// </summary>
    public class Load2DMicroOp : VectorMicroOp
    {
        private YAKSys_Hybrid_CPU.Memory.MemorySubsystem.MemoryRequestToken? _requestToken;
        private byte[]? _loadBuffer;

        public Load2DMicroOp()
        {
            IsMemoryOp = true;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                // 2D addressing: rowStride and colStride from instruction
                // Total memory footprint is complex, use conservative estimate
                ulong estimatedSize = Instruction.StreamLength * (ulong)elemSize;
                ReadMemoryRanges = new[] { (Instruction.DestSrc1Pointer, estimatedSize) };
                WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: false);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();

                _loadBuffer = new byte[_totalElements * (ulong)elemSize];

                // Use BurstPlanner for 2D addressing
                // For now, delegate to BurstIO.BurstRead2D
                uint rowLength = (uint)Instruction.Immediate; // Row length encoded in immediate
                YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead2D(
                    Instruction.DestSrc1Pointer,
                    _loadBuffer,
                    _totalElements,
                    elemSize,
                    rowLength,
                    Instruction.RowStride,
                    Instruction.Stride,
                    0); // startOffset

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }

        public byte[]? GetLoadedBuffer() => _loadBuffer;
    }

    /// <summary>
    /// Gather micro-operation for indexed (scatter-gather) loads.
    /// Works with index lists to perform non-contiguous memory access.
    /// </summary>
    public class GatherMicroOp : VectorMicroOp
    {
        private byte[]? _loadBuffer;
        private ulong[]? _indices;

        public GatherMicroOp()
        {
            IsMemoryOp = true;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                // Gather operations have discontinuous memory access
                // Memory footprint class should be higher (3 = stream)
                ulong maxRange = Instruction.StreamLength * (ulong)elemSize * 16; // Conservative estimate
                ReadMemoryRanges = new[] { (Instruction.DestSrc1Pointer, maxRange) };
                WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            }

            RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: false);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();

                _loadBuffer = new byte[_totalElements * (ulong)elemSize];

                // Load indices from index pointer
                _indices = new ulong[_totalElements];
                byte[] indexBuffer = new byte[_totalElements * sizeof(ulong)];
                // Indices are stored at Src2Pointer
                YAKSys_Hybrid_CPU.Execution.BurstIO.BurstRead(
                    Instruction.Src2Pointer,
                    indexBuffer,
                    _totalElements,
                    sizeof(ulong),
                    sizeof(ulong));

                // Copy indices from buffer
                Buffer.BlockCopy(indexBuffer, 0, _indices, 0, indexBuffer.Length);

                // Perform gather using BurstIO
                YAKSys_Hybrid_CPU.Execution.BurstIO.BurstGather(
                    Instruction.DestSrc1Pointer, // Base address
                    _loadBuffer,
                    _totalElements,
                    elemSize,
                    indexBuffer,
                    sizeof(ulong),
                    0); // indices are element indices, not byte offsets

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }

        public byte[]? GetLoadedBuffer() => _loadBuffer;
        public ulong[]? GetIndices() => _indices;
    }

    /// <summary>
    /// Store segment micro-operation for 1D vector stores.
    /// Writes segment back to memory through EnqueueWrite.
    /// </summary>
    public class StoreSegmentMicroOp : VectorMicroOp
    {
        private YAKSys_Hybrid_CPU.Memory.MemorySubsystem.MemoryRequestToken? _requestToken;
        private byte[]? _storeBuffer;

        public StoreSegmentMicroOp()
        {
            IsMemoryOp = true;
        }

        /// <summary>
        /// Set the buffer to be stored
        /// </summary>
        public void SetStoreBuffer(byte[] buffer)
        {
            _storeBuffer = buffer;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong stride = Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;
                ulong totalSize = Instruction.StreamLength * stride;

                ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
                WriteMemoryRanges = new[] { (Instruction.DestSrc1Pointer, totalSize) };
            }

            RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_storeBuffer == null)
            {
                // No data to store, operation complete
                _state = ExecutionState.Complete;
                return true;
            }

            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();
                ushort stride = Instruction.Stride;
                if (stride == 0) stride = (ushort)elemSize;

                // Initiate async write using BurstIO
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    _requestToken = memSub.EnqueueWrite(
                        0, // CPU Device ID
                        Instruction.DestSrc1Pointer,
                        (int)_storeBuffer.Length,
                        _storeBuffer);
                    _state = ExecutionState.StoringResults;
                    return false; // Not complete, needs retry
                }
                else
                {
                    // Fallback: synchronous store
                    YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite(
                        Instruction.DestSrc1Pointer,
                        _storeBuffer,
                        _totalElements,
                        elemSize,
                        stride);
                    _state = ExecutionState.Complete;
                    return true;
                }
            }

            if (_state == ExecutionState.StoringResults)
            {
                if (_requestToken != null && _requestToken.IsComplete)
                {
                    _requestToken.ThrowIfFailed("StoreSegmentMicroOp.Execute()");
                    _state = ExecutionState.Complete;
                    return true;
                }
                return false; // Still waiting
            }

            return _state == ExecutionState.Complete;
        }
    }

    /// <summary>
    /// Store 2D micro-operation for 2D pattern stores.
    /// </summary>
    public class Store2DMicroOp : VectorMicroOp
    {
        private byte[]? _storeBuffer;

        public Store2DMicroOp()
        {
            IsMemoryOp = true;
        }

        public void SetStoreBuffer(byte[] buffer)
        {
            _storeBuffer = buffer;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong estimatedSize = Instruction.StreamLength * (ulong)elemSize;
                ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
                WriteMemoryRanges = new[] { (Instruction.DestSrc1Pointer, estimatedSize) };
            }

            RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_storeBuffer == null)
            {
                _state = ExecutionState.Complete;
                return true;
            }

            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();

                // Use BurstIO for 2D store
                uint rowLength = (uint)Instruction.Immediate; // Row length encoded in immediate
                YAKSys_Hybrid_CPU.Execution.BurstIO.BurstWrite2D(
                    Instruction.DestSrc1Pointer,
                    _storeBuffer,
                    _totalElements,
                    elemSize,
                    rowLength,
                    Instruction.RowStride,
                    Instruction.Stride,
                    0); // startOffset

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }
    }

    /// <summary>
    /// Scatter store micro-operation for indexed writes.
    /// </summary>
    public class StoreScatterMicroOp : VectorMicroOp
    {
        private byte[]? _storeBuffer;
        private ulong[]? _indices;

        public StoreScatterMicroOp()
        {
            IsMemoryOp = true;
        }

        public void SetStoreBuffer(byte[] buffer, ulong[] indices)
        {
            _storeBuffer = buffer;
            _indices = indices;
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();

            if (Instruction.StreamLength > 0)
            {
                int elemSize = GetElementSize();
                ulong maxRange = Instruction.StreamLength * (ulong)elemSize * 16;
                ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
                WriteMemoryRanges = new[] { (Instruction.DestSrc1Pointer, maxRange) };
            }

            RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: true);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (_storeBuffer == null || _indices == null)
            {
                _state = ExecutionState.Complete;
                return true;
            }

            if (_state == ExecutionState.NotStarted)
            {
                _totalElements = Instruction.StreamLength;
                int elemSize = GetElementSize();

                // Perform scatter using BurstIO
                byte[] indexBuffer = new byte[_totalElements * sizeof(ulong)];
                Buffer.BlockCopy(_indices, 0, indexBuffer, 0, indexBuffer.Length);

                YAKSys_Hybrid_CPU.Execution.BurstIO.BurstScatter(
                    Instruction.DestSrc1Pointer, // Base address
                    _storeBuffer,
                    _totalElements,
                    elemSize,
                    indexBuffer,
                    sizeof(ulong),
                    0); // indices are element indices, not byte offsets

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }
    }

    /// <summary>
    /// Vector configuration micro-operation.
    /// Sets VL (vector length) and VTYPE (encodes SEW, LMUL, tail/mask policy).
    ///
    /// This is a privileged-like operation that cannot be stolen.
}
