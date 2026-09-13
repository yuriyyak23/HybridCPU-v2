using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class VectorSaturatingAddMicroOp : VectorMicroOp
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
                int elemSize = GetElementSize();
                ulong stride = ResolveEffectiveStride(elemSize);
                ulong totalSize = checked(Instruction.StreamLength * stride);

                ReadMemoryRanges =
                [
                    (Instruction.DestSrc1Pointer, totalSize),
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
                ThrowIfZeroLengthVectorComputeContour("VectorSaturatingAddMicroOp.Execute()");

                DataTypeEnum dataType = Instruction.DataTypeValue;
                int elemSize = GetElementSize();
                ulong stride = ResolveEffectiveStride(elemSize);

                _stagedWrites = new StagedVectorWrite[checked((int)_totalElements)];
                _stagedWriteCount = 0;

                for (ulong element = 0; element < _totalElements; element++)
                {
                    if (!IsLaneActiveForSaturatingAdd(ref core, element))
                    {
                        continue;
                    }

                    ulong offset = checked(element * stride);
                    ulong sourceAAddress = checked(Instruction.DestSrc1Pointer + offset);
                    ulong sourceBAddress = checked(Instruction.Src2Pointer + offset);
                    ulong destinationAddress = sourceAAddress;

                    byte[] sourceA = new byte[elemSize];
                    byte[] sourceB = new byte[elemSize];
                    byte[] destination = new byte[elemSize];

                    core.ReadBoundMainMemoryExact(
                        sourceAAddress,
                        sourceA,
                        "VectorSaturatingAddMicroOp.Execute()");
                    core.ReadBoundMainMemoryExact(
                        sourceBAddress,
                        sourceB,
                        "VectorSaturatingAddMicroOp.Execute()");

                    VectorALU.ApplySaturatingAddElement(
                        OpCode,
                        dataType,
                        sourceA,
                        sourceB,
                        destination,
                        ref core);

                    core.ThrowIfBoundMainMemoryRangeUnavailable(
                        destinationAddress,
                        elemSize,
                        "VectorSaturatingAddMicroOp.Execute()");

                    _stagedWrites[_stagedWriteCount++] =
                        new StagedVectorWrite(element, destinationAddress, destination);
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
            _ = retireRecords;
            _ = retireRecordCount;
            PublishStagedWritesAtWriteBack(ref core);
        }

        private void PublishStagedWritesAtWriteBack(ref Processor.CPU_Core core)
        {
            if (_state != ExecutionState.Complete || _stagedWrites == null)
            {
                throw new InvalidOperationException(
                    "VectorSaturatingAddMicroOp WB publication reached retire without a completed staged VADD.SAT result.");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.ThrowIfBoundMainMemoryRangeUnavailable(
                    write.Address,
                    write.Data.Length,
                    "VectorSaturatingAddMicroOp.EmitWriteBackRetireRecords()");
            }

            for (int i = 0; i < _stagedWriteCount; i++)
            {
                StagedVectorWrite write = _stagedWrites[i];
                core.WriteBoundMainMemoryExact(
                    write.Address,
                    write.Data,
                    "VectorSaturatingAddMicroOp.EmitWriteBackRetireRecords()");
            }
        }

        private void ThrowIfUnsupportedPublicationContour()
        {
            if (OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VADD ||
                Instruction.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VADD ||
                !Instruction.Saturating)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSaturatingAddMicroOp.InitializeMetadata() can only publish the Phase 05B VADD.SAT policy contour.");
            }

            if (Instruction.Indexed || Instruction.Is2D)
            {
                string addressingContour = Instruction.Indexed && Instruction.Is2D
                    ? "indexed+2D"
                    : Instruction.Indexed
                        ? "indexed"
                        : "2D";
                throw new DecodeProjectionFaultException(
                    $"VectorSaturatingAddMicroOp.InitializeMetadata() rejected unsupported {addressingContour} VADD.SAT publication. " +
                    "Phase 05B opens only the 1D memory-to-memory saturating add policy contour.");
            }

            if (Instruction.Immediate != 0)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSaturatingAddMicroOp.InitializeMetadata() rejected non-zero VADD.SAT immediate sideband.");
            }

            ThrowIfUnsupportedIntegerDataType("VectorSaturatingAddMicroOp.InitializeMetadata()");
            _ = ResolveEffectiveStride(GetElementSize());
        }

        private void ThrowIfUnsupportedRuntimeContour()
        {
            if (!Instruction.Indexed &&
                !Instruction.Is2D &&
                Instruction.Immediate == 0 &&
                Instruction.Saturating)
            {
                ThrowIfUnsupportedIntegerDataType("VectorSaturatingAddMicroOp.Execute()");
                _ = ResolveEffectiveStride(GetElementSize());
                return;
            }

            throw new InvalidOperationException(
                "VectorSaturatingAddMicroOp.Execute() rejected a non-canonical VADD.SAT runtime contour. " +
                "Only 1D integer signed/unsigned saturating add is executable.");
        }

        private void ThrowIfUnsupportedIntegerDataType(string surface)
        {
            DataTypeEnum dataType = Instruction.DataTypeValue;
            if (DataTypeUtils.IsSignedInteger(dataType) ||
                DataTypeUtils.IsUnsignedInteger(dataType))
            {
                return;
            }

            throw new DecodeProjectionFaultException(
                $"{surface} rejected unsupported VADD.SAT DataType {dataType}. " +
                "Phase 05B opens saturating add only for integer signed/unsigned element types.");
        }

        private ulong ResolveEffectiveStride(int elemSize)
        {
            if (elemSize <= 0)
            {
                throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                    $"VADD.SAT rejected unsupported element DataType 0x{Instruction.DataType:X2}.");
            }

            if (Instruction.Stride == 0)
            {
                return (ulong)elemSize;
            }

            if (Instruction.Stride < elemSize)
            {
                throw new DecodeProjectionFaultException(
                    "VectorSaturatingAddMicroOp rejected VADD.SAT stride smaller than element size. " +
                    "Overlapping element publication is outside the selected Phase 05B contour.");
            }

            return Instruction.Stride;
        }

        private bool IsLaneActiveForSaturatingAdd(ref Processor.CPU_Core core, ulong element)
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
