using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class VectorSlideOneUpMicroOp : VectorMicroOp
    {
        private StagedVectorWrite[]? _stagedWrites;
        private int _stagedWriteCount;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            IsMemoryOp = false;
            ThrowIfUnsupportedPublicationContour();

            if (Instruction.StreamLength > 0)
            {
                int elementSize = GetElementSize();
                ulong totalSize = checked(Instruction.StreamLength * (ulong)elementSize);

                ReadMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSize)
                ];
                WriteMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSize)
                ];
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
                ThrowIfZeroLengthVectorComputeContour("VectorSlideOneUpMicroOp.Execute()");

                int elementSize = GetElementSize();
                _stagedWrites = new StagedVectorWrite[checked((int)_totalElements)];
                _stagedWriteCount = 0;

                for (ulong element = 1; element < _totalElements; element++)
                {
                    if (!LaneMayOverwrite(ref core, element))
                    {
                        continue;
                    }

                    ulong sourceAddress = checked(
                        Instruction.DestSrc1Pointer + ((element - 1) * (ulong)elementSize));
                    ulong destinationAddress = checked(
                        Instruction.DestSrc1Pointer + (element * (ulong)elementSize));

                    byte[] sourceBuffer = new byte[elementSize];
                    core.ReadBoundMainMemoryExact(
                        sourceAddress,
                        sourceBuffer,
                        "VectorSlideOneUpMicroOp.Execute()");

                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        destinationAddress,
                        elementSize,
                        "VectorSlideOneUpMicroOp.Execute()");

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedVectorWrite(element, destinationAddress, sourceBuffer);
                }

                _state = ExecutionState.Complete;
            }

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            PublishStagedWritesAtWriteBack(ref core);
        }

        private void PublishStagedWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "VectorSlideOneUpMicroOp WB publication reached retire without a completed staged VSLIDE1UP result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorSlideOneUpMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorSlideOneUpMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1UP ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1UP)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneUpMicroOp.InitializeMetadata() can only publish the Phase 09 VSLIDE1UP contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorSlideOneUpMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VSLIDE1UP publication. " +
                    "Phase 09 only opens the packed 1D fixed-one-lane single-surface contour.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneUpMicroOp.InitializeMetadata() rejected non-zero VSLIDE1UP immediate sideband.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneUpMicroOp.InitializeMetadata() rejected non-zero VSLIDE1UP stride. " +
                    "The selected Phase 09 contour is packed 1D only.");
            }

            if (Instruction.Src2Pointer != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneUpMicroOp.InitializeMetadata() rejected non-zero VSLIDE1UP secondary pointer. " +
                    "The selected Phase 09 contour is single-surface only.");
            }

            ThrowIfUnsupportedSlideOneDataType(
                Instruction.DataTypeValue,
                "VectorSlideOneUpMicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.Immediate == 0 &&
                Instruction.Stride == 0 &&
                Instruction.Src2Pointer == 0)
            {
                ThrowIfUnsupportedSlideOneDataType(
                    Instruction.DataTypeValue,
                    "VectorSlideOneUpMicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorSlideOneUpMicroOp.Execute() rejected a non-canonical VSLIDE1UP runtime contour. " +
                "Only packed 1D fixed-one-lane single-surface semantics are executable.");
        }

        private static void ThrowIfUnsupportedSlideOneDataType(
            DataTypeEnum dataType,
            string surface)
        {
            if (DataTypeUtils.IsValid(dataType) &&
                DataTypeUtils.SizeOf(dataType) != 0)
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VSLIDE1UP DataType {dataType}. " +
                "Phase 09 publishes only element-sized packed slide-one semantics.");
        }

        private bool LaneMayOverwrite(ref Processor.CPU_Core core, ulong element)
        {
            if (element > int.MaxValue)
            {
                return false;
            }

            bool laneActive = core.LaneActive(Instruction.PredicateMask, (int)element);
            bool effectiveMaskAgnostic =
                Instruction.MaskAgnostic || core.VectorConfig.MaskAgnostic != 0;
            return laneActive || effectiveMaskAgnostic;
        }

        public StagedVectorWrite[] GetStagedWrites()
        {
            if (_stagedWrites == null)
            {
                return Array.Empty<StagedVectorWrite>();
            }

            StagedVectorWrite[] writes = new StagedVectorWrite[_stagedWriteCount];
            Array.Copy(_stagedWrites, writes, _stagedWriteCount);
            return writes;
        }

        public readonly struct StagedVectorWrite
        {
            public StagedVectorWrite(
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
    }

    public sealed class VectorSlideOneDownMicroOp : VectorMicroOp
    {
        private StagedVectorWrite[]? _stagedWrites;
        private int _stagedWriteCount;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            IsMemoryOp = false;
            ThrowIfUnsupportedPublicationContour();

            if (Instruction.StreamLength > 0)
            {
                int elementSize = GetElementSize();
                ulong totalSize = checked(Instruction.StreamLength * (ulong)elementSize);

                ReadMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSize)
                ];
                WriteMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSize)
                ];
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
                ThrowIfZeroLengthVectorComputeContour("VectorSlideOneDownMicroOp.Execute()");

                int elementSize = GetElementSize();
                _stagedWrites = new StagedVectorWrite[checked((int)_totalElements)];
                _stagedWriteCount = 0;

                for (ulong element = 0; element + 1 < _totalElements; element++)
                {
                    if (!LaneMayOverwrite(ref core, element))
                    {
                        continue;
                    }

                    ulong sourceAddress = checked(
                        Instruction.DestSrc1Pointer + ((element + 1) * (ulong)elementSize));
                    ulong destinationAddress = checked(
                        Instruction.DestSrc1Pointer + (element * (ulong)elementSize));

                    byte[] sourceBuffer = new byte[elementSize];
                    core.ReadBoundMainMemoryExact(
                        sourceAddress,
                        sourceBuffer,
                        "VectorSlideOneDownMicroOp.Execute()");

                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        destinationAddress,
                        elementSize,
                        "VectorSlideOneDownMicroOp.Execute()");

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedVectorWrite(element, destinationAddress, sourceBuffer);
                }

                _state = ExecutionState.Complete;
            }

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            PublishStagedWritesAtWriteBack(ref core);
        }

        private void PublishStagedWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "VectorSlideOneDownMicroOp WB publication reached retire without a completed staged VSLIDE1DOWN result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorSlideOneDownMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorSlideOneDownMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1DOWN ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1DOWN)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneDownMicroOp.InitializeMetadata() can only publish the Phase 09 VSLIDE1DOWN contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorSlideOneDownMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VSLIDE1DOWN publication. " +
                    "Phase 09 only opens the packed 1D fixed-one-lane single-surface contour.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneDownMicroOp.InitializeMetadata() rejected non-zero VSLIDE1DOWN immediate sideband.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneDownMicroOp.InitializeMetadata() rejected non-zero VSLIDE1DOWN stride. " +
                    "The selected Phase 09 contour is packed 1D only.");
            }

            if (Instruction.Src2Pointer != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSlideOneDownMicroOp.InitializeMetadata() rejected non-zero VSLIDE1DOWN secondary pointer. " +
                    "The selected Phase 09 contour is single-surface only.");
            }

            ThrowIfUnsupportedSlideOneDataType(
                Instruction.DataTypeValue,
                "VectorSlideOneDownMicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.Immediate == 0 &&
                Instruction.Stride == 0 &&
                Instruction.Src2Pointer == 0)
            {
                ThrowIfUnsupportedSlideOneDataType(
                    Instruction.DataTypeValue,
                    "VectorSlideOneDownMicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorSlideOneDownMicroOp.Execute() rejected a non-canonical VSLIDE1DOWN runtime contour. " +
                "Only packed 1D fixed-one-lane single-surface semantics are executable.");
        }

        private static void ThrowIfUnsupportedSlideOneDataType(
            DataTypeEnum dataType,
            string surface)
        {
            if (DataTypeUtils.IsValid(dataType) &&
                DataTypeUtils.SizeOf(dataType) != 0)
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VSLIDE1DOWN DataType {dataType}. " +
                "Phase 09 publishes only element-sized packed slide-one semantics.");
        }

        private bool LaneMayOverwrite(ref Processor.CPU_Core core, ulong element)
        {
            if (element > int.MaxValue)
            {
                return false;
            }

            bool laneActive = core.LaneActive(Instruction.PredicateMask, (int)element);
            bool effectiveMaskAgnostic =
                Instruction.MaskAgnostic || core.VectorConfig.MaskAgnostic != 0;
            return laneActive || effectiveMaskAgnostic;
        }

        public StagedVectorWrite[] GetStagedWrites()
        {
            if (_stagedWrites == null)
            {
                return Array.Empty<StagedVectorWrite>();
            }

            StagedVectorWrite[] writes = new StagedVectorWrite[_stagedWriteCount];
            Array.Copy(_stagedWrites, writes, _stagedWriteCount);
            return writes;
        }

        public readonly struct StagedVectorWrite
        {
            public StagedVectorWrite(
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
    }
}
