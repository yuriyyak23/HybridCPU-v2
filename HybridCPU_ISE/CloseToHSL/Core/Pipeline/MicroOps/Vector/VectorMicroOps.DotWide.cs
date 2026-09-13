using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class VectorDotWideMicroOp : VectorMicroOp
    {
        private StagedScalarWrite? _stagedResult;

        public override void InitializeMetadata()
        {
            base.InitializeMetadata();
            IsMemoryOp = false;
            ThrowIfUnsupportedPublicationContour();

            if (Instruction.StreamLength > 0)
            {
                int sourceElementSize = GetSupportedSourceElementSize(
                    Instruction.DataTypeValue,
                    "VectorDotWideMicroOp.InitializeMetadata()");
                ulong totalSourceBytes = checked(Instruction.StreamLength * (ulong)sourceElementSize);

                ReadMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSourceBytes),
                    (Instruction.Src2Pointer, totalSourceBytes)
                ];
                WriteMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, sizeof(float))
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
                ThrowIfZeroLengthVectorComputeContour("VectorDotWideMicroOp.Execute()");

                int sourceElementSize = GetSupportedSourceElementSize(
                    Instruction.DataTypeValue,
                    "VectorDotWideMicroOp.Execute()");
                int totalSourceBytes = checked((int)(_totalElements * (ulong)sourceElementSize));
                byte[] sourceA = new byte[totalSourceBytes];
                byte[] sourceB = new byte[totalSourceBytes];
                byte[] result = new byte[sizeof(float)];

                core.ReadBoundMainMemoryExact(
                    Instruction.DestSrc1Pointer,
                    sourceA,
                    "VectorDotWideMicroOp.Execute()");
                core.ReadBoundMainMemoryExact(
                    Instruction.Src2Pointer,
                    sourceB,
                    "VectorDotWideMicroOp.Execute()");
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    Instruction.DestSrc1Pointer,
                    sizeof(float),
                    "VectorDotWideMicroOp.Execute()");

                VectorALU.ApplyDotProduct(
                    OpCode,
                    Instruction.DataTypeValue,
                    sourceA,
                    sourceB,
                    result,
                    sourceElementSize,
                    _totalElements,
                    Instruction.PredicateMask,
                    ref core);

                _stagedResult = new StagedScalarWrite(Instruction.DestSrc1Pointer, result);
                _state = ExecutionState.Complete;
            }

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            _ = retireRecords;
            _ = retireRecordCount;
            PublishStagedResultAtWriteBack(ref core);
        }

        private void PublishStagedResultAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || !_stagedResult.HasValue)
            {
                throw new InvalidOperationException(
                    "VectorDotWideMicroOp WB publication reached retire without a completed staged VDOT.WIDE result.");
            }

            StagedScalarWrite write = _stagedResult.Value;
            core.ThrowIfBoundMainMemoryRangeUnavailable(
                write.Address,
                write.Data.Length,
                "VectorDotWideMicroOp.EmitWriteBackRetireRecords()");
            core.WriteBoundMainMemoryExact(
                write.Address,
                write.Data,
                "VectorDotWideMicroOp.EmitWriteBackRetireRecords()");
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VDOT_WIDE ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VDOT_WIDE)
            {
                throw new DecodeProjectionFaultException(
                    "VectorDotWideMicroOp.InitializeMetadata() can only publish the Phase 09 VDOT.WIDE contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorDotWideMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VDOT.WIDE publication. " +
                    "Phase 09 only opens the packed 1D FP16/BF16/FP8-to-FP32 plus INT8/UINT8-to-32-bit scalar-footprint contours.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorDotWideMicroOp.InitializeMetadata() rejected non-zero VDOT.WIDE immediate sideband.");
            }

            if (Instruction.Stride != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorDotWideMicroOp.InitializeMetadata() rejected non-zero VDOT.WIDE stride. " +
                    "The selected Phase 09 contour is packed 1D only.");
            }

            ThrowIfUnsupportedWideDotDataType(
                Instruction.DataTypeValue,
                "VectorDotWideMicroOp.InitializeMetadata()");
            ThrowIfUnsupportedAlignment("VectorDotWideMicroOp.InitializeMetadata()");
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.Immediate == 0 &&
                Instruction.Stride == 0)
            {
                ThrowIfUnsupportedWideDotDataType(
                    Instruction.DataTypeValue,
                    "VectorDotWideMicroOp.Execute()");
                ThrowIfUnsupportedAlignment("VectorDotWideMicroOp.Execute()");
                return;
            }

            throw new InvalidOperationException(
                "VectorDotWideMicroOp.Execute() rejected a non-canonical VDOT.WIDE runtime contour. " +
                "Only packed 1D FP16/BF16/FP8-to-FP32 plus INT8/UINT8-to-32-bit scalar-footprint semantics are executable.");
        }

        private void ThrowIfUnsupportedAlignment(string surface)
        {
            if ((Instruction.DestSrc1Pointer & 0x3UL) != 0)
            {
                throw new DecodeProjectionFaultException(
                    $"{surface} rejected unaligned VDOT.WIDE destination/source1 pointer. " +
                    "The FP32 scalar result requires 4-byte alignment.");
            }

            if (GetSupportedSourceElementSize(Instruction.DataTypeValue, surface) == 2 &&
                (Instruction.Src2Pointer & 0x1UL) != 0)
            {
                throw new DecodeProjectionFaultException(
                    $"{surface} rejected unaligned VDOT.WIDE source2 pointer. " +
                    "FP16/BF16 source elements require 2-byte alignment.");
            }
        }

        private static void ThrowIfUnsupportedWideDotDataType(
            DataTypeEnum dataType,
            string surface)
        {
            _ = GetSupportedSourceElementSize(dataType, surface);
        }

        private static int GetSupportedSourceElementSize(
            DataTypeEnum dataType,
            string surface)
        {
            if (dataType == DataTypeEnum.FLOAT16 ||
                dataType == DataTypeEnum.BFLOAT16)
            {
                return 2;
            }

            if (dataType == DataTypeEnum.FLOAT8_E4M3 ||
                dataType == DataTypeEnum.FLOAT8_E5M2 ||
                dataType == DataTypeEnum.INT8 ||
                dataType == DataTypeEnum.UINT8)
            {
                return 1;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VDOT.WIDE DataType {dataType}. " +
                "Phase 09 publishes only FP16/BF16/FP8 and INT8/UINT8 inputs with 32-bit scalar result publication.");
        }

        public StagedScalarWrite? GetStagedResult() => _stagedResult;

        public readonly struct StagedScalarWrite
        {
            public StagedScalarWrite(
                ulong address,
                byte[] data)
            {
                Address = address;
                Data = data ?? throw new ArgumentNullException(nameof(data));
            }

            public ulong Address { get; }
            public byte[] Data { get; }
        }
    }
}
