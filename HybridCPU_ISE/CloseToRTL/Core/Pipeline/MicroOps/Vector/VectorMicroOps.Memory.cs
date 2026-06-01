
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
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
    /// VGATHER indexed-memory read micro-operation.
    /// Reads Indexed2SrcDesc from Src2Pointer, stages gathered elements during execute,
    /// and publishes the destination memory update only through WB/retire follow-through.
    /// </summary>
    public class GatherMicroOp : VectorMicroOp
    {
        private const int Indexed2SrcDescriptorSize = 32;
        private const int Indexed2SrcReservedOffset = 20;
        private const int Uint32IndexType = 0;
        private const int Uint64IndexType = 1;

        private byte[]? _stagedDestinationBuffer;
        private ulong[]? _indices;
        private Indexed2SrcDescriptor _descriptor;
        private bool _descriptorResolved;

        public GatherMicroOp()
        {
            Class = MicroOpClass.Lsu;
            IsMemoryOp = false;
            HasSideEffects = true;
            IsStealable = false;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = InstructionClassifier.GetSerializationClass(
                Processor.CPU_Core.InstructionsEnum.VGATHER);
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            Class = MicroOpClass.Lsu;
            IsMemoryOp = false;
            HasSideEffects = true;
            IsStealable = false;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = InstructionClassifier.GetSerializationClass(
                Processor.CPU_Core.InstructionsEnum.VGATHER);
            SetClassFlexiblePlacement(SlotClass.LsuClass);
            ThrowIfUnsupportedPublicationContour();
            ThrowIfUnsupportedElementDataTypeForMetadata("GatherMicroOp.InitializeMetadata()");

            if (Instruction.StreamLength > 0)
            {
                try
                {
                    Indexed2SrcDescriptor descriptor =
                        ResolveDescriptorFromPublishedMemory("GatherMicroOp.InitializeMetadata()");
                    int elemSize = GetElementSize();
                    ulong[] indices = ReadPublishedIndexVector(
                        descriptor,
                        Instruction.StreamLength,
                        "GatherMicroOp.InitializeMetadata()");

                    ReadMemoryRanges = BuildReadRanges(
                        descriptor,
                        indices,
                        elemSize,
                        Instruction.StreamLength);
                    WriteMemoryRanges = new[]
                    {
                        BuildDestinationRange(elemSize, Instruction.StreamLength)
                    };
                }
                catch (DecodeProjectionFaultException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DecodeProjectionFaultException(
                        $"GatherMicroOp.InitializeMetadata() rejected VGATHER indexed publication before MicroOp follow-through: {ex.Message}",
                        ex);
                }
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
                ThrowIfUnsupportedRuntimeContour();
                ThrowIfZeroLengthVectorComputeContour("GatherMicroOp.Execute()");
                ThrowIfUnsupportedElementDataType("GatherMicroOp.Execute()");

                int elemSize = GetElementSize();
                _descriptor = ReadIndexedDescriptor(
                    ref core,
                    Instruction.Src2Pointer,
                    "GatherMicroOp.Execute()");
                _descriptorResolved = true;

                _stagedDestinationBuffer = new byte[checked((int)(_totalElements * (ulong)elemSize))];
                _indices = new ulong[checked((int)_totalElements)];

                for (ulong element = 0; element < _totalElements; element++)
                {
                    int elementIndex = checked((int)element);
                    ulong index = ReadIndexElement(
                        ref core,
                        _descriptor,
                        element,
                        "GatherMicroOp.Execute()");
                    _indices[elementIndex] = index;

                    int destinationOffset = checked((int)(element * (ulong)elemSize));
                    ulong destinationAddress = GetDestinationAddress(element, elemSize);
                    byte[] elementBuffer = new byte[elemSize];

                    if (IsLaneActiveForGather(ref core, element))
                    {
                        ulong sourceAddress = ResolveSourceAddress(
                            _descriptor,
                            index,
                            elemSize,
                            "GatherMicroOp.Execute()");
                        core.ReadBoundMainMemoryExact(
                            sourceAddress,
                            elementBuffer,
                            "GatherMicroOp.Execute()");
                    }
                    else
                    {
                        core.ReadBoundMainMemoryExact(
                            destinationAddress,
                            elementBuffer,
                            "GatherMicroOp.Execute(masked-undisturbed)");
                    }

                    Buffer.BlockCopy(
                        elementBuffer,
                        0,
                        _stagedDestinationBuffer,
                        destinationOffset,
                        elemSize);
                }

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            PublishStagedGatherResultAtWriteBack(ref core);
        }

        private void PublishStagedGatherResultAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedDestinationBuffer == null)
            {
                throw new InvalidOperationException(
                    "GatherMicroOp WB publication reached retire without a completed staged VGATHER result.");
            }

            int elemSize = GetElementSize();
            for (ulong element = 0; element < _totalElements; element++)
            {
                ulong destinationAddress = GetDestinationAddress(element, elemSize);
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    destinationAddress,
                    elemSize,
                    "GatherMicroOp.EmitWriteBackRetireRecords()");
            }

            byte[] elementBuffer = new byte[elemSize];
            for (ulong element = 0; element < _totalElements; element++)
            {
                Buffer.BlockCopy(
                    _stagedDestinationBuffer,
                    checked((int)(element * (ulong)elemSize)),
                    elementBuffer,
                    0,
                    elemSize);
                core.WriteBoundMainMemoryExact(
                    GetDestinationAddress(element, elemSize),
                    elementBuffer,
                    "GatherMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VGATHER ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VGATHER)
            {
                throw new DecodeProjectionFaultException(
                    "GatherMicroOp.InitializeMetadata() can only publish the VGATHER indexed-read contour.");
            }

            if (Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Is2D
                    ? "2D"
                    : "non-indexed";
            throw new DecodeProjectionFaultException(
                $"GatherMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VGATHER publication. " +
                "Phase 01 only opens the 1D indexed-read contour; VSCATTER, 2D, and indexed+2D remain fail-closed.");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (OpCode == (uint)Processor.CPU_Core.InstructionsEnum.VGATHER &&
                Instruction.OpCode == (uint)Processor.CPU_Core.InstructionsEnum.VGATHER &&
                Instruction.Indexed &&
                !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Is2D
                    ? "2D"
                    : "non-indexed";
            throw new InvalidOperationException(
                $"GatherMicroOp.Execute() rejected unsupported {addressingContour} VGATHER runtime contour. " +
                "The executable carrier is limited to 1D indexed memory reads.");
        }

        private Indexed2SrcDescriptor ResolveDescriptorFromPublishedMemory(string surface)
        {
            if (_descriptorResolved)
            {
                return _descriptor;
            }

            _descriptor = ReadIndexedDescriptorFromPublishedMemory(Instruction.Src2Pointer, surface);
            _descriptorResolved = true;
            return _descriptor;
        }

        private static Indexed2SrcDescriptor ReadIndexedDescriptorFromPublishedMemory(
            ulong descriptorAddress,
            string surface)
        {
            if (descriptorAddress == 0)
            {
                throw new DecodeProjectionFaultException(
                    $"{surface} rejected descriptor-less VGATHER publication. Word2 must carry an Indexed2SrcDesc address.");
            }

            byte[] descriptorBytes = new byte[Indexed2SrcDescriptorSize];
            BurstIO.BurstRead(
                descriptorAddress,
                descriptorBytes,
                elementCount: 1,
                elementSize: Indexed2SrcDescriptorSize,
                stride: Indexed2SrcDescriptorSize);
            return ParseIndexedDescriptor(
                descriptorBytes,
                descriptorAddress,
                surface,
                publication: true);
        }

        private static Indexed2SrcDescriptor ReadIndexedDescriptor(
            ref Processor.CPU_Core core,
            ulong descriptorAddress,
            string surface)
        {
            if (descriptorAddress == 0)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected descriptor-less VGATHER runtime execution. Word2 must carry an Indexed2SrcDesc address.");
            }

            byte[] descriptorBytes = new byte[Indexed2SrcDescriptorSize];
            core.ReadBoundMainMemoryExact(descriptorAddress, descriptorBytes, surface);
            return ParseIndexedDescriptor(
                descriptorBytes,
                descriptorAddress,
                surface,
                publication: false);
        }

        private static Indexed2SrcDescriptor ParseIndexedDescriptor(
            byte[] descriptorBytes,
            ulong descriptorAddress,
            string surface,
            bool publication)
        {
            if (descriptorBytes.Length != Indexed2SrcDescriptorSize)
            {
                throw CreateDescriptorException(
                    surface,
                    "Indexed2SrcDesc must be exactly 32 bytes.",
                    publication);
            }

            byte indexType = descriptorBytes[18];
            if (indexType is not (Uint32IndexType or Uint64IndexType))
            {
                throw CreateDescriptorException(
                    surface,
                    $"Indexed2SrcDesc IndexType {indexType} is unsupported; only 0=uint32 and 1=uint64 are executable.",
                    publication);
            }

            byte indexIsByteOffset = descriptorBytes[19];
            if (indexIsByteOffset > 1)
            {
                throw CreateDescriptorException(
                    surface,
                    $"Indexed2SrcDesc IndexIsByteOffset {indexIsByteOffset} is unsupported; only 0/1 are executable.",
                    publication);
            }

            for (int i = Indexed2SrcReservedOffset; i < Indexed2SrcDescriptorSize; i++)
            {
                if (descriptorBytes[i] != 0)
                {
                    throw CreateDescriptorException(
                        surface,
                        "Indexed2SrcDesc reserved bytes 20..31 must be zero for Phase 01 VGATHER.",
                        publication);
                }
            }

            return new Indexed2SrcDescriptor(
                descriptorAddress,
                BitConverter.ToUInt64(descriptorBytes, 0),
                BitConverter.ToUInt64(descriptorBytes, 8),
                BitConverter.ToUInt16(descriptorBytes, 16),
                indexType,
                indexIsByteOffset != 0);
        }

        private static Exception CreateDescriptorException(
            string surface,
            string message,
            bool publication)
        {
            string fullMessage = $"{surface} rejected malformed VGATHER Indexed2SrcDesc: {message}";
            return publication
                ? new DecodeProjectionFaultException(fullMessage)
                : new InvalidOperationException(fullMessage);
        }

        private static ulong[] ReadPublishedIndexVector(
            Indexed2SrcDescriptor descriptor,
            ulong elementCount,
            string surface)
        {
            ulong[] indices = new ulong[checked((int)elementCount)];
            int indexSize = descriptor.IndexElementSize;
            byte[] indexBuffer = new byte[indexSize];

            for (ulong element = 0; element < elementCount; element++)
            {
                int elementIndex = checked((int)element);
                ulong indexAddress = GetIndexAddress(descriptor, element, surface);
                BurstIO.BurstRead(
                    indexAddress,
                    indexBuffer,
                    elementCount: 1,
                    elementSize: indexSize,
                    stride: checked((ushort)indexSize));
                indices[elementIndex] = indexSize == sizeof(uint)
                    ? BitConverter.ToUInt32(indexBuffer, 0)
                    : BitConverter.ToUInt64(indexBuffer, 0);
            }

            return indices;
        }

        private static ulong ReadIndexElement(
            ref Processor.CPU_Core core,
            Indexed2SrcDescriptor descriptor,
            ulong element,
            string surface)
        {
            int indexSize = descriptor.IndexElementSize;
            byte[] indexBuffer = new byte[indexSize];
            core.ReadBoundMainMemoryExact(
                GetIndexAddress(descriptor, element, surface),
                indexBuffer,
                surface);
            return indexSize == sizeof(uint)
                ? BitConverter.ToUInt32(indexBuffer, 0)
                : BitConverter.ToUInt64(indexBuffer, 0);
        }

        private static (ulong Address, ulong Length)[] BuildReadRanges(
            Indexed2SrcDescriptor descriptor,
            IReadOnlyList<ulong> indices,
            int elemSize,
            ulong elementCount)
        {
            var ranges = new List<(ulong Address, ulong Length)>(3)
            {
                (descriptor.DescriptorAddress, Indexed2SrcDescriptorSize),
                BuildIndexRange(descriptor, elementCount)
            };
            ranges.Add(BuildSourceRange(descriptor, indices, elemSize));
            return ranges.ToArray();
        }

        private static (ulong Address, ulong Length) BuildIndexRange(
            Indexed2SrcDescriptor descriptor,
            ulong elementCount)
        {
            ulong length = checked(((elementCount - 1) * descriptor.EffectiveIndexStride) +
                                   (ulong)descriptor.IndexElementSize);
            return (descriptor.IndexBase, length);
        }

        private static (ulong Address, ulong Length) BuildSourceRange(
            Indexed2SrcDescriptor descriptor,
            IReadOnlyList<ulong> indices,
            int elemSize)
        {
            ulong min = ulong.MaxValue;
            ulong maxExclusive = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                ulong sourceAddress = ResolveSourceAddress(
                    descriptor,
                    indices[i],
                    elemSize,
                    "GatherMicroOp.InitializeMetadata()");
                min = Math.Min(min, sourceAddress);
                maxExclusive = Math.Max(maxExclusive, checked(sourceAddress + (ulong)elemSize));
            }

            return (min, checked(maxExclusive - min));
        }

        private (ulong Address, ulong Length) BuildDestinationRange(
            int elemSize,
            ulong elementCount)
        {
            ulong length = checked(((elementCount - 1) * GetDestinationStride(elemSize)) +
                                   (ulong)elemSize);
            return (Instruction.DestSrc1Pointer, length);
        }

        private static ulong GetIndexAddress(
            Indexed2SrcDescriptor descriptor,
            ulong element,
            string surface)
        {
            try
            {
                return checked(descriptor.IndexBase + (element * descriptor.EffectiveIndexStride));
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected VGATHER index address overflow.",
                    ex);
            }
        }

        private ulong GetDestinationAddress(ulong element, int elemSize)
        {
            return checked(Instruction.DestSrc1Pointer + (element * GetDestinationStride(elemSize)));
        }

        private ulong GetDestinationStride(int elemSize) =>
            Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;

        private static ulong ResolveSourceAddress(
            Indexed2SrcDescriptor descriptor,
            ulong index,
            int elemSize,
            string surface)
        {
            try
            {
                ulong offset = descriptor.IndexIsByteOffset
                    ? index
                    : checked(index * (ulong)elemSize);
                return checked(descriptor.SourceBase + offset);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected VGATHER source address overflow.",
                    ex);
            }
        }

        private bool IsLaneActiveForGather(ref Processor.CPU_Core core, ulong element)
        {
            if (element > int.MaxValue)
            {
                return false;
            }

            return core.LaneActive(Instruction.PredicateMask, (int)element);
        }

        public byte[]? GetLoadedBuffer() => _stagedDestinationBuffer;
        public ulong[]? GetIndices() => _indices;

        private readonly struct Indexed2SrcDescriptor
        {
            public Indexed2SrcDescriptor(
                ulong descriptorAddress,
                ulong sourceBase,
                ulong indexBase,
                ushort indexStride,
                byte indexType,
                bool indexIsByteOffset)
            {
                DescriptorAddress = descriptorAddress;
                SourceBase = sourceBase;
                IndexBase = indexBase;
                IndexStride = indexStride;
                IndexType = indexType;
                IndexIsByteOffset = indexIsByteOffset;
            }

            public ulong DescriptorAddress { get; }

            public ulong SourceBase { get; }

            public ulong IndexBase { get; }

            public ushort IndexStride { get; }

            public byte IndexType { get; }

            public bool IndexIsByteOffset { get; }

            public int IndexElementSize => IndexType == Uint32IndexType
                ? sizeof(uint)
                : sizeof(ulong);

            public ulong EffectiveIndexStride => IndexStride != 0
                ? IndexStride
                : (ulong)IndexElementSize;
        }
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
    /// VSCATTER indexed-memory write micro-operation.
    /// Reads source elements from Word1, reads Indexed2SrcDesc from Word2, stages
    /// indexed destination writes during execute, and mutates memory only at WB/retire.
    /// </summary>
    public class StoreScatterMicroOp : VectorMicroOp
    {
        private const int Indexed2SrcDescriptorSize = 32;
        private const int Indexed2SrcReservedOffset = 20;
        private const int Uint32IndexType = 0;
        private const int Uint64IndexType = 1;

        private StagedScatterWrite[]? _stagedWrites;
        private int _stagedWriteCount;
        private ulong[]? _indices;
        private Indexed2SrcDescriptor _descriptor;
        private bool _descriptorResolved;

        public StoreScatterMicroOp()
        {
            Class = MicroOpClass.Lsu;
            IsMemoryOp = false;
            HasSideEffects = true;
            IsStealable = false;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = InstructionClassifier.GetSerializationClass(
                Processor.CPU_Core.InstructionsEnum.VSCATTER);
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        public void SetStoreBuffer(byte[] buffer, ulong[] indices)
        {
            throw new InvalidOperationException(
                "StoreScatterMicroOp rejects manual buffer/index sideband. " +
                "Executable VSCATTER is scoped to the canonical Indexed2SrcDesc path.");
        }

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            Class = MicroOpClass.Lsu;
            IsMemoryOp = false;
            HasSideEffects = true;
            IsStealable = false;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = InstructionClassifier.GetSerializationClass(
                Processor.CPU_Core.InstructionsEnum.VSCATTER);
            SetClassFlexiblePlacement(SlotClass.LsuClass);
            ThrowIfUnsupportedPublicationContour();
            ThrowIfUnsupportedElementDataTypeForMetadata("StoreScatterMicroOp.InitializeMetadata()");

            if (Instruction.StreamLength > 0)
            {
                try
                {
                    Indexed2SrcDescriptor descriptor =
                        ResolveDescriptorFromPublishedMemory("StoreScatterMicroOp.InitializeMetadata()");
                    int elemSize = GetElementSize();
                    ulong[] indices = ReadPublishedIndexVector(
                        descriptor,
                        Instruction.StreamLength,
                        "StoreScatterMicroOp.InitializeMetadata()");

                    ReadMemoryRanges = BuildReadRanges(
                        descriptor,
                        elemSize,
                        Instruction.StreamLength);
                    WriteMemoryRanges = BuildWriteRanges(
                        descriptor,
                        indices,
                        elemSize);
                }
                catch (DecodeProjectionFaultException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DecodeProjectionFaultException(
                        $"StoreScatterMicroOp.InitializeMetadata() rejected VSCATTER indexed publication before MicroOp follow-through: {ex.Message}",
                        ex);
                }
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
                ThrowIfUnsupportedRuntimeContour();
                ThrowIfZeroLengthVectorComputeContour("StoreScatterMicroOp.Execute()");
                ThrowIfUnsupportedElementDataType("StoreScatterMicroOp.Execute()");

                int elemSize = GetElementSize();
                _descriptor = ReadIndexedDescriptor(
                    ref core,
                    Instruction.Src2Pointer,
                    "StoreScatterMicroOp.Execute()");
                _descriptorResolved = true;

                _indices = new ulong[checked((int)_totalElements)];
                _stagedWrites = new StagedScatterWrite[checked((int)_totalElements)];
                _stagedWriteCount = 0;

                for (ulong element = 0; element < _totalElements; element++)
                {
                    int elementIndex = checked((int)element);
                    ulong index = ReadIndexElement(
                        ref core,
                        _descriptor,
                        element,
                        "StoreScatterMicroOp.Execute()");
                    _indices[elementIndex] = index;

                    if (!IsLaneActiveForScatter(ref core, element))
                    {
                        continue;
                    }

                    byte[] elementBuffer = new byte[elemSize];
                    core.ReadBoundMainMemoryExact(
                        GetSourceDataAddress(element, elemSize),
                        elementBuffer,
                        "StoreScatterMicroOp.Execute()");

                    ulong targetAddress = ResolveTargetAddress(
                        _descriptor,
                        index,
                        elemSize,
                        "StoreScatterMicroOp.Execute()");
                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        targetAddress,
                        elemSize,
                        "StoreScatterMicroOp.Execute()");

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedScatterWrite(element, targetAddress, elementBuffer);
                }

                _state = ExecutionState.Complete;
                return true;
            }

            return _state == ExecutionState.Complete;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            PublishStagedScatterWritesAtWriteBack(ref core);
        }

        private void PublishStagedScatterWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "StoreScatterMicroOp WB publication reached retire without a completed staged VSCATTER result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedScatterWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "StoreScatterMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedScatterWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "StoreScatterMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER)
            {
                throw new DecodeProjectionFaultException(
                    "StoreScatterMicroOp.InitializeMetadata() can only publish the VSCATTER indexed-write contour.");
            }

            if (Instruction.Indexed && !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Is2D
                    ? "2D"
                    : "non-indexed";
            throw new DecodeProjectionFaultException(
                $"StoreScatterMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VSCATTER publication. " +
                "Phase 02 only opens the 1D indexed-write contour; 2D and indexed+2D remain fail-closed.");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (OpCode == (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER &&
                Instruction.OpCode == (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER &&
                Instruction.Indexed &&
                !Instruction.Is2D)
            {
                return;
            }

            string addressingContour = Instruction.Indexed && Instruction.Is2D
                ? "indexed+2D"
                : Instruction.Is2D
                    ? "2D"
                    : "non-indexed";
            throw new InvalidOperationException(
                $"StoreScatterMicroOp.Execute() rejected unsupported {addressingContour} VSCATTER runtime contour. " +
                "The executable carrier is limited to 1D indexed memory writes.");
        }

        private Indexed2SrcDescriptor ResolveDescriptorFromPublishedMemory(string surface)
        {
            if (_descriptorResolved)
            {
                return _descriptor;
            }

            _descriptor = ReadIndexedDescriptorFromPublishedMemory(Instruction.Src2Pointer, surface);
            _descriptorResolved = true;
            return _descriptor;
        }

        private static Indexed2SrcDescriptor ReadIndexedDescriptorFromPublishedMemory(
            ulong descriptorAddress,
            string surface)
        {
            if (descriptorAddress == 0)
            {
                throw new DecodeProjectionFaultException(
                    $"{surface} rejected descriptor-less VSCATTER publication. Word2 must carry an Indexed2SrcDesc address.");
            }

            byte[] descriptorBytes = new byte[Indexed2SrcDescriptorSize];
            BurstIO.BurstRead(
                descriptorAddress,
                descriptorBytes,
                elementCount: 1,
                elementSize: Indexed2SrcDescriptorSize,
                stride: Indexed2SrcDescriptorSize);
            return ParseIndexedDescriptor(
                descriptorBytes,
                descriptorAddress,
                surface,
                publication: true);
        }

        private static Indexed2SrcDescriptor ReadIndexedDescriptor(
            ref Processor.CPU_Core core,
            ulong descriptorAddress,
            string surface)
        {
            if (descriptorAddress == 0)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected descriptor-less VSCATTER runtime execution. Word2 must carry an Indexed2SrcDesc address.");
            }

            byte[] descriptorBytes = new byte[Indexed2SrcDescriptorSize];
            core.ReadBoundMainMemoryExact(descriptorAddress, descriptorBytes, surface);
            return ParseIndexedDescriptor(
                descriptorBytes,
                descriptorAddress,
                surface,
                publication: false);
        }

        private static Indexed2SrcDescriptor ParseIndexedDescriptor(
            byte[] descriptorBytes,
            ulong descriptorAddress,
            string surface,
            bool publication)
        {
            if (descriptorBytes.Length != Indexed2SrcDescriptorSize)
            {
                throw CreateDescriptorException(
                    surface,
                    "Indexed2SrcDesc must be exactly 32 bytes.",
                    publication);
            }

            byte indexType = descriptorBytes[18];
            if (indexType is not (Uint32IndexType or Uint64IndexType))
            {
                throw CreateDescriptorException(
                    surface,
                    $"Indexed2SrcDesc IndexType {indexType} is unsupported; only 0=uint32 and 1=uint64 are executable.",
                    publication);
            }

            byte indexIsByteOffset = descriptorBytes[19];
            if (indexIsByteOffset > 1)
            {
                throw CreateDescriptorException(
                    surface,
                    $"Indexed2SrcDesc IndexIsByteOffset {indexIsByteOffset} is unsupported; only 0/1 are executable.",
                    publication);
            }

            for (int i = Indexed2SrcReservedOffset; i < Indexed2SrcDescriptorSize; i++)
            {
                if (descriptorBytes[i] != 0)
                {
                    throw CreateDescriptorException(
                        surface,
                        "Indexed2SrcDesc reserved bytes 20..31 must be zero for Phase 02 VSCATTER.",
                        publication);
                }
            }

            return new Indexed2SrcDescriptor(
                descriptorAddress,
                BitConverter.ToUInt64(descriptorBytes, 0),
                BitConverter.ToUInt64(descriptorBytes, 8),
                BitConverter.ToUInt16(descriptorBytes, 16),
                indexType,
                indexIsByteOffset != 0);
        }

        private static Exception CreateDescriptorException(
            string surface,
            string message,
            bool publication)
        {
            string fullMessage = $"{surface} rejected malformed VSCATTER Indexed2SrcDesc: {message}";
            return publication
                ? new DecodeProjectionFaultException(fullMessage)
                : new InvalidOperationException(fullMessage);
        }

        private static ulong[] ReadPublishedIndexVector(
            Indexed2SrcDescriptor descriptor,
            ulong elementCount,
            string surface)
        {
            ulong[] indices = new ulong[checked((int)elementCount)];
            int indexSize = descriptor.IndexElementSize;
            byte[] indexBuffer = new byte[indexSize];

            for (ulong element = 0; element < elementCount; element++)
            {
                int elementIndex = checked((int)element);
                ulong indexAddress = GetIndexAddress(descriptor, element, surface);
                BurstIO.BurstRead(
                    indexAddress,
                    indexBuffer,
                    elementCount: 1,
                    elementSize: indexSize,
                    stride: checked((ushort)indexSize));
                indices[elementIndex] = indexSize == sizeof(uint)
                    ? BitConverter.ToUInt32(indexBuffer, 0)
                    : BitConverter.ToUInt64(indexBuffer, 0);
            }

            return indices;
        }

        private static ulong ReadIndexElement(
            ref Processor.CPU_Core core,
            Indexed2SrcDescriptor descriptor,
            ulong element,
            string surface)
        {
            int indexSize = descriptor.IndexElementSize;
            byte[] indexBuffer = new byte[indexSize];
            core.ReadBoundMainMemoryExact(
                GetIndexAddress(descriptor, element, surface),
                indexBuffer,
                surface);
            return indexSize == sizeof(uint)
                ? BitConverter.ToUInt32(indexBuffer, 0)
                : BitConverter.ToUInt64(indexBuffer, 0);
        }

        private (ulong Address, ulong Length)[] BuildReadRanges(
            Indexed2SrcDescriptor descriptor,
            int elemSize,
            ulong elementCount)
        {
            return
            [
                (descriptor.DescriptorAddress, Indexed2SrcDescriptorSize),
                BuildIndexRange(descriptor, elementCount),
                BuildSourceDataRange(elemSize, elementCount)
            ];
        }

        private static (ulong Address, ulong Length)[] BuildWriteRanges(
            Indexed2SrcDescriptor descriptor,
            IReadOnlyList<ulong> indices,
            int elemSize)
        {
            var ranges = new (ulong Address, ulong Length)[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                ranges[i] = (
                    ResolveTargetAddress(
                        descriptor,
                        indices[i],
                        elemSize,
                        "StoreScatterMicroOp.InitializeMetadata()"),
                    (ulong)elemSize);
            }

            return ranges;
        }

        private static (ulong Address, ulong Length) BuildIndexRange(
            Indexed2SrcDescriptor descriptor,
            ulong elementCount)
        {
            ulong length = checked(((elementCount - 1) * descriptor.EffectiveIndexStride) +
                                   (ulong)descriptor.IndexElementSize);
            return (descriptor.IndexBase, length);
        }

        private (ulong Address, ulong Length) BuildSourceDataRange(
            int elemSize,
            ulong elementCount)
        {
            ulong length = checked(((elementCount - 1) * GetSourceDataStride(elemSize)) +
                                   (ulong)elemSize);
            return (Instruction.DestSrc1Pointer, length);
        }

        private static ulong GetIndexAddress(
            Indexed2SrcDescriptor descriptor,
            ulong element,
            string surface)
        {
            try
            {
                return checked(descriptor.IndexBase + (element * descriptor.EffectiveIndexStride));
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected VSCATTER index address overflow.",
                    ex);
            }
        }

        private ulong GetSourceDataAddress(ulong element, int elemSize)
        {
            try
            {
                return checked(Instruction.DestSrc1Pointer + (element * GetSourceDataStride(elemSize)));
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    "StoreScatterMicroOp.Execute() rejected VSCATTER source-data address overflow.",
                    ex);
            }
        }

        private ulong GetSourceDataStride(int elemSize) =>
            Instruction.Stride > 0 ? Instruction.Stride : (ulong)elemSize;

        private static ulong ResolveTargetAddress(
            Indexed2SrcDescriptor descriptor,
            ulong index,
            int elemSize,
            string surface)
        {
            try
            {
                ulong offset = descriptor.IndexIsByteOffset
                    ? index
                    : checked(index * (ulong)elemSize);
                return checked(descriptor.MemoryBase + offset);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"{surface} rejected VSCATTER target address overflow.",
                    ex);
            }
        }

        private bool IsLaneActiveForScatter(ref Processor.CPU_Core core, ulong element)
        {
            if (element > int.MaxValue)
            {
                return false;
            }

            return core.LaneActive(Instruction.PredicateMask, (int)element);
        }

        public ulong[]? GetIndices() => _indices;

        public StagedScatterWrite[] GetStagedWrites()
        {
            if (_stagedWrites == null)
            {
                return Array.Empty<StagedScatterWrite>();
            }

            StagedScatterWrite[] writes = new StagedScatterWrite[_stagedWriteCount];
            Array.Copy(_stagedWrites, writes, _stagedWriteCount);
            return writes;
        }

        public readonly struct StagedScatterWrite
        {
            public StagedScatterWrite(
                ulong element,
                ulong address,
                byte[] data)
            {
                Element = element;
                Address = address;
                Data = data ?? throw new ArgumentNullException(nameof(data));
            }

            public ulong Element { get; }

            public ulong Address { get; }

            public byte[] Data { get; }
        }

        private readonly struct Indexed2SrcDescriptor
        {
            public Indexed2SrcDescriptor(
                ulong descriptorAddress,
                ulong memoryBase,
                ulong indexBase,
                ushort indexStride,
                byte indexType,
                bool indexIsByteOffset)
            {
                DescriptorAddress = descriptorAddress;
                MemoryBase = memoryBase;
                IndexBase = indexBase;
                IndexStride = indexStride;
                IndexType = indexType;
                IndexIsByteOffset = indexIsByteOffset;
            }

            public ulong DescriptorAddress { get; }

            public ulong MemoryBase { get; }

            public ulong IndexBase { get; }

            public ushort IndexStride { get; }

            public byte IndexType { get; }

            public bool IndexIsByteOffset { get; }

            public int IndexElementSize => IndexType == Uint32IndexType
                ? sizeof(uint)
                : sizeof(ulong);

            public ulong EffectiveIndexStride => IndexStride != 0
                ? IndexStride
                : (ulong)IndexElementSize;
        }
    }

    /// <summary>
    /// Vector configuration micro-operation.
    /// Sets VL (vector length) and VTYPE (encodes SEW, LMUL, tail/mask policy).
    ///
    /// This is a privileged-like operation that cannot be stolen.
}
