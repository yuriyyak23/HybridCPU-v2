using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class VectorPermute2MicroOp : VectorMicroOp
    {
        private StagedVectorWrite[]? _stagedWrites;
        private int _stagedWriteCount;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            IsMemoryOp = false;
            ThrowIfUnsupportedPublicationContour();

            int elementSize = GetElementSize();
            ulong totalSize = checked(2UL * (ulong)elementSize);

            ReadMemoryRanges =
            [
                (Instruction.DestSrc1Pointer, totalSize),
                (Instruction.Src2Pointer, totalSize)
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
                ThrowIfZeroLengthVectorComputeContour("VectorPermute2MicroOp.Execute()");

                int elementSize = GetElementSize();
                byte[][] sourceElements =
                [
                    ReadElement(ref core, Instruction.DestSrc1Pointer, 0, elementSize),
                    ReadElement(ref core, Instruction.DestSrc1Pointer, 1, elementSize),
                    ReadElement(ref core, Instruction.Src2Pointer, 0, elementSize),
                    ReadElement(ref core, Instruction.Src2Pointer, 1, elementSize)
                ];

                _stagedWrites = new StagedVectorWrite[2];
                _stagedWriteCount = 0;

                for (ulong lane = 0; lane < 2; lane++)
                {
                    if (!LaneMayOverwrite(ref core, lane))
                    {
                        continue;
                    }

                    ulong destinationAddress = checked(
                        Instruction.DestSrc1Pointer + (lane * (ulong)elementSize));
                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        destinationAddress,
                        elementSize,
                        "VectorPermute2MicroOp.Execute()");

                    int selector = (Instruction.Immediate >> ((int)lane * 2)) & 0x3;
                    byte[] selected = new byte[elementSize];
                    Array.Copy(sourceElements[selector], selected, elementSize);

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedVectorWrite(lane, destinationAddress, selected);
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

        private byte[] ReadElement(
            ref Processor.CPU_Core core,
            ulong baseAddress,
            ulong lane,
            int elementSize)
        {
            ulong address = checked(baseAddress + (lane * (ulong)elementSize));
            byte[] buffer = new byte[elementSize];
            core.ReadBoundMainMemoryExact(
                address,
                buffer,
                "VectorPermute2MicroOp.Execute()");
            return buffer;
        }

        private void PublishStagedWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "VectorPermute2MicroOp WB publication reached retire without a completed staged VPERM2 result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorPermute2MicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorPermute2MicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VPERM2 ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VPERM2)
            {
                throw new DecodeProjectionFaultException(
                    "VectorPermute2MicroOp.InitializeMetadata() can only publish the Phase 09 VPERM2 contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorPermute2MicroOp.InitializeMetadata() rejected unsupported {addressingContour} VPERM2 publication. " +
                    "Phase 09 only opens the packed 1D two-source two-lane immediate-controlled contour.");
            }

            if (Instruction.StreamLength != 2)
            {
                throw new DecodeProjectionFaultException(
                    "VectorPermute2MicroOp.InitializeMetadata() rejected VPERM2 StreamLength outside the fixed two-lane contour.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorPermute2MicroOp.InitializeMetadata() rejected non-zero VPERM2 stride. " +
                    "The selected Phase 09 contour is packed 1D only.");
            }

            if ((Instruction.Immediate & 0xFFF0) != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorPermute2MicroOp.InitializeMetadata() rejected reserved high VPERM2 immediate bits. " +
                    "Only two 2-bit selectors are executable in the selected Phase 09 contour.");
            }

            ThrowIfUnsupportedPermute2DataType(
                Instruction.DataTypeValue,
                "VectorPermute2MicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.StreamLength == 2 &&
                Instruction.Stride == 0 &&
                (Instruction.Immediate & 0xFFF0) == 0)
            {
                ThrowIfUnsupportedPermute2DataType(
                    Instruction.DataTypeValue,
                    "VectorPermute2MicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorPermute2MicroOp.Execute() rejected a non-canonical VPERM2 runtime contour. " +
                "Only packed 1D two-source two-lane immediate-controlled semantics are executable.");
        }

        private static void ThrowIfUnsupportedPermute2DataType(
            DataTypeEnum dataType,
            string surface)
        {
            if (DataTypeUtils.IsValid(dataType) &&
                DataTypeUtils.SizeOf(dataType) != 0)
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VPERM2 DataType {dataType}. " +
                "Phase 09 publishes only element-sized packed permute2 semantics.");
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
