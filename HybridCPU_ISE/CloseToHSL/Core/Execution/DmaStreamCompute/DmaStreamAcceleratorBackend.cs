using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public sealed record DmaStreamComputeBackendTelemetry
    {
        public bool UsedLane6Backend { get; init; }

        public int ReadBurstCount { get; init; }

        public ulong BytesRead { get; init; }

        public int StagedWriteCount { get; init; }

        public ulong BytesStaged { get; init; }

        public ulong ModeledLatencyCycles { get; init; }

        public ulong ElementOperations { get; init; }

        public ulong CopyOperations { get; init; }

        public ulong AddOperations { get; init; }

        public ulong MulOperations { get; init; }

        public ulong FmaOperations { get; init; }

        public ulong ReduceOperations { get; init; }

        public int AluLaneOccupancyDelta { get; init; }

        public int DirectDestinationWriteCount { get; init; }
    }

    public sealed class DmaStreamAcceleratorBackend
    {
        private readonly Processor.MainMemoryArea _mainMemory;
        private int _readBurstCount;
        private ulong _bytesRead;
        private int _stagedWriteCount;
        private ulong _bytesStaged;
        private ulong _modeledLatencyCycles;
        private ulong _elementOperations;
        private ulong _copyOperations;
        private ulong _addOperations;
        private ulong _mulOperations;
        private ulong _fmaOperations;
        private ulong _reduceOperations;
        private DmaStreamComputeTelemetryCounters? _telemetry;

        public DmaStreamAcceleratorBackend(
            Processor.MainMemoryArea mainMemory,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(mainMemory);
            _mainMemory = mainMemory;
            _telemetry = telemetry;
        }

        public Processor.MainMemoryArea MainMemory => _mainMemory;

        public void AttachTelemetry(DmaStreamComputeTelemetryCounters? telemetry)
        {
            _telemetry = telemetry;
        }

        public bool TryReadRange(
            DmaStreamComputeMemoryRange range,
            Span<byte> buffer,
            out string message)
        {
            message = string.Empty;
            if ((ulong)buffer.Length != range.Length)
            {
                message =
                    $"DmaStreamCompute lane6 backend read buffer length {buffer.Length} does not match range length {range.Length}.";
                return false;
            }

            if (range.Length > int.MaxValue ||
                !HasExactMainMemoryRange(range.Address, checked((int)range.Length)))
            {
                message =
                    $"DmaStreamCompute lane6 backend rejected out-of-range source read at 0x{range.Address:X} for {range.Length} byte(s).";
                return false;
            }

            if (!_mainMemory.TryReadPhysicalRange(range.Address, buffer))
            {
                message =
                    $"DmaStreamCompute lane6 backend could not read source range at 0x{range.Address:X} for {range.Length} byte(s).";
                return false;
            }

            _readBurstCount++;
            _bytesRead += range.Length;
            _modeledLatencyCycles += 4 + ((range.Length + 63) / 64);
            _telemetry?.RecordRuntimeRead(range.Length);
            return true;
        }

        public void RecordComputeElements(
            ulong elementCount,
            DmaStreamComputeOperationKind operation)
        {
            ulong effectiveElementCount = elementCount == 0 ? 1UL : elementCount;
            _elementOperations += effectiveElementCount;
            _modeledLatencyCycles += effectiveElementCount;
            switch (operation)
            {
                case DmaStreamComputeOperationKind.Copy:
                    _copyOperations += effectiveElementCount;
                    break;
                case DmaStreamComputeOperationKind.Add:
                    _addOperations += effectiveElementCount;
                    break;
                case DmaStreamComputeOperationKind.Mul:
                    _mulOperations += effectiveElementCount;
                    break;
                case DmaStreamComputeOperationKind.Fma:
                    _fmaOperations += effectiveElementCount;
                    break;
                case DmaStreamComputeOperationKind.Reduce:
                    _reduceOperations += effectiveElementCount;
                    break;
            }

            _telemetry?.RecordElementOperations(operation, effectiveElementCount);
        }

        public void RecordStagedWrite(ulong byteCount)
        {
            _stagedWriteCount++;
            _bytesStaged += byteCount;
            _modeledLatencyCycles += 1 + ((byteCount + 63) / 64);
        }

        public DmaStreamComputeBackendTelemetry SnapshotTelemetry() =>
            new()
            {
                UsedLane6Backend = true,
                ReadBurstCount = _readBurstCount,
                BytesRead = _bytesRead,
                StagedWriteCount = _stagedWriteCount,
                BytesStaged = _bytesStaged,
                ModeledLatencyCycles = _modeledLatencyCycles,
                ElementOperations = _elementOperations,
                CopyOperations = _copyOperations,
                AddOperations = _addOperations,
                MulOperations = _mulOperations,
                FmaOperations = _fmaOperations,
                ReduceOperations = _reduceOperations,
                AluLaneOccupancyDelta = 0,
                DirectDestinationWriteCount = 0
            };

        private bool HasExactMainMemoryRange(ulong address, int size)
        {
            if (size <= 0)
            {
                return false;
            }

            ulong memoryLength = (ulong)_mainMemory.Length;
            ulong requestSize = (ulong)size;
            return requestSize <= memoryLength &&
                   address <= memoryLength - requestSize;
        }
    }
}
