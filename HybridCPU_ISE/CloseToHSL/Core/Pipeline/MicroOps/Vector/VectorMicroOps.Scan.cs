using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Core
{
    public class VectorScanSumMicroOp : VectorMicroOp
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
                    (Instruction.Src2Pointer, totalSize)
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
                ThrowIfZeroLengthVectorComputeContour("VectorScanSumMicroOp.Execute()");

                DataTypeEnum dataType = Instruction.DataTypeValue;
                int elementSize = DataTypeUtils.SizeOf(dataType);
                long signedAccumulator = 0;
                ulong unsignedAccumulator = 0;

                _stagedWrites = new StagedVectorWrite[checked((int)_totalElements)];
                _stagedWriteCount = 0;

                for (ulong element = 0; element < _totalElements; element++)
                {
                    if (!IsLaneActiveForScan(ref core, element))
                    {
                        continue;
                    }

                    ulong sourceAddress = checked(
                        Instruction.Src2Pointer + (element * (ulong)elementSize));
                    ulong destinationAddress = checked(
                        Instruction.DestSrc1Pointer + (element * (ulong)elementSize));

                    byte[] sourceBuffer = new byte[elementSize];
                    core.ReadBoundMainMemoryExact(
                        sourceAddress,
                        sourceBuffer,
                        "VectorScanSumMicroOp.Execute()");

                    byte[] destinationBuffer = new byte[elementSize];
                    VectorALU.ApplyScanSumElement(
                        OpCode,
                        dataType,
                        sourceBuffer,
                        destinationBuffer,
                        ref signedAccumulator,
                        ref unsignedAccumulator,
                        ref core);

                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        destinationAddress,
                        elementSize,
                        "VectorScanSumMicroOp.Execute()");

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedVectorWrite(element, destinationAddress, destinationBuffer);
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
                    "VectorScanSumMicroOp WB publication reached retire without a completed staged VSCAN.SUM result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorScanSumMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorScanSumMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSCAN_SUM ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VSCAN_SUM)
            {
                throw new DecodeProjectionFaultException(
                    "VectorScanSumMicroOp.InitializeMetadata() can only publish the Phase 05A VSCAN.SUM contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorScanSumMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VSCAN.SUM publication. " +
                    "Phase 05A only opens the packed 1D integer prefix-sum contour.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorScanSumMicroOp.InitializeMetadata() rejected non-zero VSCAN.SUM immediate sideband.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorScanSumMicroOp.InitializeMetadata() rejected non-zero VSCAN.SUM stride. " +
                    "The selected Phase 05A contour is packed 1D only.");
            }

            ThrowIfUnsupportedScanDataType(
                Instruction.DataTypeValue,
                "VectorScanSumMicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed && !Instruction.Is2D &&
                Instruction.Immediate == 0 &&
                Instruction.Stride == 0)
            {
                ThrowIfUnsupportedScanDataType(
                    Instruction.DataTypeValue,
                    "VectorScanSumMicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorScanSumMicroOp.Execute() rejected a non-canonical VSCAN.SUM runtime contour. " +
                "Only packed 1D integer prefix-sum semantics are executable.");
        }

        private static void ThrowIfUnsupportedScanDataType(
            DataTypeEnum dataType,
            string surface)
        {
            if (DataTypeUtils.IsInteger(dataType))
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VSCAN.SUM DataType {dataType}. " +
                "Phase 05A publishes only integer prefix-sum semantics.");
        }

        private bool IsLaneActiveForScan(ref Processor.CPU_Core core, ulong element)
        {
            if (element > int.MaxValue)
            {
                return false;
            }

            return core.LaneActive(Instruction.PredicateMask, (int)element);
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
