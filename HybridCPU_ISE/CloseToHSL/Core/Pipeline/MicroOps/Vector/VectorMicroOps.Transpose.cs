using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class VectorTransposeMicroOp : VectorMicroOp
    {
        private const ulong FixedTransposeElementCount = 4;

        private StagedVectorWrite[]? _stagedWrites;
        private int _stagedWriteCount;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            IsMemoryOp = false;
            ThrowIfUnsupportedPublicationContour();

            int elementSize = GetElementSize();
            ulong totalSize = checked(FixedTransposeElementCount * (ulong)elementSize);

            ReadMemoryRanges =
            [
                (Instruction.DestSrc1Pointer, totalSize)
            ];
            WriteMemoryRanges =
            [
                (Instruction.DestSrc1Pointer, totalSize)
            ];

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
                ThrowIfZeroLengthVectorComputeContour("VectorTransposeMicroOp.Execute()");

                int elementSize = GetElementSize();
                byte[] lane1 = ReadElement(ref core, lane: 1, elementSize);
                byte[] lane2 = ReadElement(ref core, lane: 2, elementSize);

                _stagedWrites = new StagedVectorWrite[2];
                _stagedWriteCount = 0;

                StageLaneIfWritable(ref core, lane: 1, elementSize, lane2);
                StageLaneIfWritable(ref core, lane: 2, elementSize, lane1);

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

        private byte[] ReadElement(
            ref Processor.CPU_Core core,
            ulong lane,
            int elementSize)
        {
            ulong address = checked(Instruction.DestSrc1Pointer + (lane * (ulong)elementSize));
            byte[] buffer = new byte[elementSize];
            core.ReadBoundMainMemoryExact(
                address,
                buffer,
                "VectorTransposeMicroOp.Execute()");
            return buffer;
        }

        private void StageLaneIfWritable(
            ref Processor.CPU_Core core,
            ulong lane,
            int elementSize,
            byte[] sourceBytes)
        {
            if (!LaneMayOverwrite(ref core, lane))
            {
                return;
            }

            ulong destinationAddress = checked(
                Instruction.DestSrc1Pointer + (lane * (ulong)elementSize));
            core.ThrowIfBoundMainMemoryRangeUnavailable(
                destinationAddress,
                elementSize,
                "VectorTransposeMicroOp.Execute()");

            byte[] data = new byte[elementSize];
            Array.Copy(sourceBytes, data, elementSize);
            _stagedWrites![_stagedWriteCount++] =
                new StagedVectorWrite(lane, destinationAddress, data);
        }

        private void PublishStagedWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "VectorTransposeMicroOp WB publication reached retire without a completed staged VTRANSPOSE result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorTransposeMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorTransposeMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VTRANSPOSE ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VTRANSPOSE)
            {
                throw new DecodeProjectionFaultException(
                    "VectorTransposeMicroOp.InitializeMetadata() can only publish the Phase 09 VTRANSPOSE contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorTransposeMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VTRANSPOSE publication. " +
                    "Phase 09 only opens the packed 1D single-surface fixed 2x2 contour.");
            }

            if (Instruction.StreamLength != FixedTransposeElementCount)
            {
                throw new DecodeProjectionFaultException(
                    "VectorTransposeMicroOp.InitializeMetadata() rejected VTRANSPOSE StreamLength outside the fixed four-lane 2x2 contour.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorTransposeMicroOp.InitializeMetadata() rejected non-zero VTRANSPOSE immediate sideband.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorTransposeMicroOp.InitializeMetadata() rejected non-zero VTRANSPOSE stride. " +
                    "The selected Phase 09 contour is packed 1D only.");
            }

            if (Instruction.Src2Pointer != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorTransposeMicroOp.InitializeMetadata() rejected non-zero VTRANSPOSE secondary pointer. " +
                    "The selected Phase 09 contour is single-surface only.");
            }

            ThrowIfUnsupportedTransposeDataType(
                Instruction.DataTypeValue,
                "VectorTransposeMicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.StreamLength == FixedTransposeElementCount &&
                Instruction.Immediate == 0 &&
                Instruction.Stride == 0 &&
                Instruction.Src2Pointer == 0)
            {
                ThrowIfUnsupportedTransposeDataType(
                    Instruction.DataTypeValue,
                    "VectorTransposeMicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorTransposeMicroOp.Execute() rejected a non-canonical VTRANSPOSE runtime contour. " +
                "Only packed 1D single-surface fixed 2x2 transpose semantics are executable.");
        }

        private static void ThrowIfUnsupportedTransposeDataType(
            DataTypeEnum dataType,
            string surface)
        {
            if (DataTypeUtils.IsValid(dataType) &&
                DataTypeUtils.SizeOf(dataType) != 0)
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VTRANSPOSE DataType {dataType}. " +
                "Phase 09 publishes only element-sized packed transpose semantics.");
        }

        private bool LaneMayOverwrite(ref Processor.CPU_Core core, ulong lane)
        {
            if (lane > int.MaxValue)
            {
                return false;
            }

            bool laneActive = core.LaneActive(Instruction.PredicateMask, (int)lane);
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
                ulong lane,
                ulong address,
                byte[] data)
            {
                Lane = lane;
                Address = address;
                Data = data ?? throw new ArgumentNullException(nameof(data));
            }

            public ulong Lane { get; }
            public ulong Address { get; }
            public byte[] Data { get; }
        }
    }
}
