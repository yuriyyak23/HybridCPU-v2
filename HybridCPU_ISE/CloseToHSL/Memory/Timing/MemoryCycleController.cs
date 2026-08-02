using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.CloseToHSL.Memory.Timing;

/// <summary>
/// Identity of one request accepted by <see cref="MemoryCycleController"/>.
/// This identity is local to the memory controller and is not an ISA, replay,
/// FSP, DMA-channel, or retirement identity.
/// </summary>
public readonly record struct MemoryRequestId(ulong Value)
{
    public bool IsValid => Value != 0;
}

public enum MemoryAdmissionStatus : byte
{
    Accepted,
    Backpressured,
    Rejected
}

/// <summary>
/// Bounded admission result. Only <see cref="MemoryAdmissionStatus.Accepted"/>
/// carries a request identity.
/// </summary>
public readonly record struct MemoryAdmissionResult(
    MemoryAdmissionStatus Status,
    MemoryRequestId RequestId,
    string? Reason)
{
    public static MemoryAdmissionResult Accepted(MemoryRequestId requestId) =>
        new(MemoryAdmissionStatus.Accepted, requestId, null);

    public static MemoryAdmissionResult Backpressured() =>
        new(MemoryAdmissionStatus.Backpressured, default, null);

    public static MemoryAdmissionResult Rejected(string reason) =>
        new(MemoryAdmissionStatus.Rejected, default, reason);
}

/// <summary>
/// Immutable consumer-facing completion for one accepted request.
/// </summary>
public sealed class MemoryCompletion
{
    private readonly byte[] _data;

    internal MemoryCompletion(
        MemoryRequestId requestId,
        bool succeeded,
        byte[] data,
        string? failureReason,
        ulong publishedCycle)
    {
        RequestId = requestId;
        Succeeded = succeeded;
        _data = (byte[])data.Clone();
        FailureReason = failureReason;
        PublishedCycle = publishedCycle;
    }

    public MemoryRequestId RequestId { get; }
    public bool Succeeded { get; }
    public string? FailureReason { get; }
    public ulong PublishedCycle { get; }
    public ReadOnlyMemory<byte> Data => _data;
}

/// <summary>
/// RF-10 memory-cycle authority. The implemented ingress contains the two
/// RF-10.2/10.3 scalar-load classes, the RF-10.4/10.5 scalar-store readiness
/// classes, the RF-10.6 vector-segment read class and the RF-10.10 canonical
/// vector-transfer source-read class; every other request family remains on
/// its legacy owner.
/// </summary>
public sealed class MemoryCycleController
{
    public const int ExplicitPacketScalarLoadCapacity = 8;
    public const int SingleLaneScalarLoadCapacity = 8;
    public const int VectorSegmentLoadCapacity = 8;
    public const int CanonicalVectorTransferCapacity = 8;
    public const int ExplicitPacketScalarStoreCapacity = 8;
    public const int SingleLaneScalarStoreCapacity = 8;

    private readonly object _gate = new();
    private readonly MemorySubsystem _memorySubsystem;
    private readonly Queue<MemoryRequestId> _readQueue = new();
    private readonly Queue<MemoryRequestId> _scalarStoreQueue = new();
    private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding = new();
    private readonly Dictionary<MemoryRequestId, MemoryCompletion> _nextCompletions = new();
    private readonly Dictionary<MemoryRequestId, MemoryCompletion> _publishedCompletions = new();
    private ulong _nextRequestId = 1;
    private ulong _memoryCycle;
    private ulong _lastPlatformCycle;
    private int _outstandingExplicitPacketScalarLoads;
    private int _outstandingSingleLaneScalarLoads;
    private int _outstandingVectorSegmentLoads;
    private int _outstandingCanonicalVectorTransfers;
    private int _outstandingExplicitPacketScalarStores;
    private int _outstandingSingleLaneScalarStores;
    private ulong _telemetryControllerCycles;
    private ulong _telemetryReadServiceCycles;
    private ulong _telemetryStoreReadinessServiceCycles;
    private ulong _telemetryCompletionPublicationCycles;
    private ulong _telemetryAcceptedRequests;
    private ulong _telemetryCompletedRequests;
    private ulong _telemetryDataReadAcceptedRequests;
    private ulong _telemetryDataReadCompletedRequests;
    private ulong _telemetryDataWriteAcceptedRequests;
    private ulong _telemetryDataWriteCompletedRequests;
    private ulong _telemetryDataReadBytes;
    private ulong _telemetryCommittedDataWriteBytes;
    private ulong _telemetryInstructionFetchReadBytes;
    private ulong _telemetryQueueFullRejects;

    internal MemoryCycleController(MemorySubsystem memorySubsystem)
    {
        _memorySubsystem = memorySubsystem ?? throw new ArgumentNullException(nameof(memorySubsystem));
    }

    public ulong MemoryCycle
    {
        get
        {
            lock (_gate)
            {
                return _memoryCycle;
            }
        }
    }

    public int OutstandingExplicitPacketScalarLoads
    {
        get
        {
            lock (_gate)
            {
                return _outstandingExplicitPacketScalarLoads;
            }
        }
    }

    public int OutstandingSingleLaneScalarLoads
    {
        get
        {
            lock (_gate)
            {
                return _outstandingSingleLaneScalarLoads;
            }
        }
    }

    public int OutstandingVectorSegmentLoads
    {
        get
        {
            lock (_gate)
            {
                return _outstandingVectorSegmentLoads;
            }
        }
    }

    public int OutstandingCanonicalVectorTransfers
    {
        get
        {
            lock (_gate)
            {
                return _outstandingCanonicalVectorTransfers;
            }
        }
    }

    public int OutstandingExplicitPacketScalarStores
    {
        get
        {
            lock (_gate)
            {
                return _outstandingExplicitPacketScalarStores;
            }
        }
    }

    public int OutstandingSingleLaneScalarStores
    {
        get
        {
            lock (_gate)
            {
                return _outstandingSingleLaneScalarStores;
            }
        }
    }

    public MemoryCycleTelemetrySnapshot GetTelemetrySnapshot()
    {
        lock (_gate)
        {
            return new MemoryCycleTelemetrySnapshot(
                _telemetryControllerCycles,
                _telemetryReadServiceCycles,
                _telemetryStoreReadinessServiceCycles,
                _telemetryCompletionPublicationCycles,
                _telemetryAcceptedRequests,
                _telemetryCompletedRequests,
                _telemetryDataReadAcceptedRequests,
                _telemetryDataReadCompletedRequests,
                _telemetryDataWriteAcceptedRequests,
                _telemetryDataWriteCompletedRequests,
                _telemetryDataReadBytes,
                _telemetryCommittedDataWriteBytes,
                _telemetryInstructionFetchReadBytes,
                _telemetryQueueFullRejects);
        }
    }

    internal void ResetTelemetry()
    {
        lock (_gate)
        {
            _telemetryControllerCycles = 0;
            _telemetryReadServiceCycles = 0;
            _telemetryStoreReadinessServiceCycles = 0;
            _telemetryCompletionPublicationCycles = 0;
            _telemetryAcceptedRequests = 0;
            _telemetryCompletedRequests = 0;
            _telemetryDataReadAcceptedRequests = 0;
            _telemetryDataReadCompletedRequests = 0;
            _telemetryDataWriteAcceptedRequests = 0;
            _telemetryDataWriteCompletedRequests = 0;
            _telemetryDataReadBytes = 0;
            _telemetryCommittedDataWriteBytes = 0;
            _telemetryInstructionFetchReadBytes = 0;
            _telemetryQueueFullRejects = 0;
        }
    }

    internal void RecordInstructionFetchReadBytes(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        lock (_gate)
        {
            _telemetryInstructionFetchReadBytes = checked(
                _telemetryInstructionFetchReadBytes + (ulong)byteCount);
        }
    }

    internal void RecordCommittedDataWriteBytes(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        lock (_gate)
        {
            _telemetryCommittedDataWriteBytes = checked(
                _telemetryCommittedDataWriteBytes + (ulong)byteCount);
        }
    }

    internal MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(
        int bankCount,
        int bankWidthBytes)
    {
        lock (_gate)
        {
            bool controllerIsQuiescent =
                _readQueue.Count == 0 &&
                _scalarStoreQueue.Count == 0 &&
                _outstanding.Count == 0 &&
                _nextCompletions.Count == 0 &&
                _publishedCompletions.Count == 0;

            return _memorySubsystem
                .TryReplacePhysicalMemoryBankGeometryUnderControllerGate(
                    bankCount,
                    bankWidthBytes,
                    controllerIsQuiescent);
        }
    }

    public MemoryAdmissionResult TryAcceptExplicitPacketScalarLoad(
        ulong deviceId,
        ulong address,
        int size) =>
        TryAcceptRead(
            ReadRequestClass.ExplicitPacketScalar,
            deviceId,
            address,
            size);

    public MemoryAdmissionResult TryAcceptSingleLaneScalarLoad(
        ulong deviceId,
        ulong address,
        int size) =>
        TryAcceptRead(
            ReadRequestClass.SingleLaneScalarMicroOp,
            deviceId,
            address,
            size);

    public MemoryAdmissionResult TryAcceptVectorSegmentLoad(
        ulong deviceId,
        ulong address,
        int size) =>
        TryAcceptRead(
            ReadRequestClass.VectorSegmentMicroOp,
            deviceId,
            address,
            size);

    public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(
        uint opcode,
        ulong deviceId,
        ulong sourceAddress,
        ulong destinationAddress,
        ulong elementCount,
        int elementSize,
        ushort stride)
    {
        if (opcode is not (
                global::YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues.VLOAD or
                global::YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues.VSTORE) ||
            elementCount == 0 || elementSize <= 0 || stride == 0)
        {
            return MemoryAdmissionResult.Rejected(
                "Canonical vector transfer requires VLOAD/VSTORE and positive element count, element size and stride.");
        }

        int packedSize;
        try
        {
            ulong totalBytes = checked(elementCount * (ulong)elementSize);
            packedSize = checked((int)totalBytes);
            _ = checked(sourceAddress + checked((elementCount - 1) * stride) + (ulong)elementSize);
            _ = checked(destinationAddress + checked((elementCount - 1) * stride) + (ulong)elementSize);
        }
        catch (OverflowException)
        {
            return MemoryAdmissionResult.Rejected(
                "Canonical vector transfer shape overflows the supported address or byte-count envelope.");
        }

        lock (_gate)
        {
            if (_outstandingCanonicalVectorTransfers >= CanonicalVectorTransferCapacity)
            {
                _telemetryQueueFullRejects = checked(_telemetryQueueFullRejects + 1);
                return MemoryAdmissionResult.Backpressured();
            }

            CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope =
                _memorySubsystem
                    .CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(
                        sourceAddress,
                        elementCount,
                        stride);
            MemoryRequestId requestId = AllocateRequestId();
            _outstanding.Add(
                requestId,
                ControllerRequest.CreateCanonicalVectorTransfer(
                    opcode,
                    deviceId,
                    sourceAddress,
                    destinationAddress,
                    elementCount,
                    elementSize,
                    stride,
                    packedSize,
                    physicalBankEnvelope));
            _outstandingCanonicalVectorTransfers++;
            _readQueue.Enqueue(requestId);
            RecordAcceptedRequest(
                _outstanding[requestId]);
            return MemoryAdmissionResult.Accepted(requestId);
        }
    }

    public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(
        ulong deviceId,
        ulong address,
        int size,
        ReadOnlySpan<byte> data) =>
        TryAcceptScalarStore(
            ScalarStoreRequestClass.ExplicitPacket,
            deviceId,
            address,
            size,
            data);

    public MemoryAdmissionResult TryAcceptSingleLaneScalarStore(
        ulong deviceId,
        ulong address,
        int size,
        ReadOnlySpan<byte> data) =>
        TryAcceptScalarStore(
            ScalarStoreRequestClass.SingleLaneMicroOp,
            deviceId,
            address,
            size,
            data);

    private MemoryAdmissionResult TryAcceptScalarStore(
        ScalarStoreRequestClass requestClass,
        ulong deviceId,
        ulong address,
        int size,
        ReadOnlySpan<byte> data)
    {
        if (size is not (1 or 2 or 4 or 8))
        {
            return MemoryAdmissionResult.Rejected(
                $"{RenderRequestClass(requestClass)} scalar store size {size} is outside the RF-10 1/2/4/8-byte envelope.");
        }
        if (data.Length < size)
        {
            return MemoryAdmissionResult.Rejected(
                $"{RenderRequestClass(requestClass)} scalar store provides {data.Length} byte(s) for a {size}-byte request.");
        }

        lock (_gate)
        {
            int outstandingForClass = requestClass == ScalarStoreRequestClass.ExplicitPacket
                ? _outstandingExplicitPacketScalarStores
                : _outstandingSingleLaneScalarStores;
            int capacity = requestClass == ScalarStoreRequestClass.ExplicitPacket
                ? ExplicitPacketScalarStoreCapacity
                : SingleLaneScalarStoreCapacity;
            if (outstandingForClass >= capacity)
            {
                _telemetryQueueFullRejects = checked(_telemetryQueueFullRejects + 1);
                return MemoryAdmissionResult.Backpressured();
            }

            MemoryRequestId requestId = AllocateRequestId();
            PhysicalMemoryBankBinding physicalBankBinding = _memorySubsystem
                .CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(
                    address);
            _outstanding.Add(
                requestId,
                ControllerRequest.CreateScalarStore(
                    requestClass,
                    deviceId,
                    address,
                    size,
                    data[..size].ToArray(),
                    physicalBankBinding));
            IncrementOutstandingClass(requestClass);
            _scalarStoreQueue.Enqueue(requestId);
            RecordAcceptedRequest(_outstanding[requestId]);
            return MemoryAdmissionResult.Accepted(requestId);
        }
    }

    private MemoryAdmissionResult TryAcceptRead(
        ReadRequestClass requestClass,
        ulong deviceId,
        ulong address,
        int size)
    {
        if (requestClass != ReadRequestClass.VectorSegmentMicroOp &&
            size is not (1 or 2 or 4 or 8))
        {
            return MemoryAdmissionResult.Rejected(
                $"{RenderRequestClass(requestClass)} read size {size} is outside the RF-10 scalar 1/2/4/8-byte envelope.");
        }
        if (requestClass == ReadRequestClass.VectorSegmentMicroOp && size <= 0)
        {
            return MemoryAdmissionResult.Rejected(
                $"{RenderRequestClass(requestClass)} read size {size} must be positive.");
        }

        lock (_gate)
        {
            int outstandingForClass = requestClass switch
            {
                ReadRequestClass.ExplicitPacketScalar => _outstandingExplicitPacketScalarLoads,
                ReadRequestClass.SingleLaneScalarMicroOp => _outstandingSingleLaneScalarLoads,
                ReadRequestClass.VectorSegmentMicroOp => _outstandingVectorSegmentLoads,
                _ => throw new InvalidOperationException($"Unsupported read request class {requestClass}.")
            };
            int capacity = requestClass switch
            {
                ReadRequestClass.ExplicitPacketScalar => ExplicitPacketScalarLoadCapacity,
                ReadRequestClass.SingleLaneScalarMicroOp => SingleLaneScalarLoadCapacity,
                ReadRequestClass.VectorSegmentMicroOp => VectorSegmentLoadCapacity,
                _ => throw new InvalidOperationException($"Unsupported read request class {requestClass}.")
            };
            if (outstandingForClass >= capacity)
            {
                _telemetryQueueFullRejects = checked(_telemetryQueueFullRejects + 1);
                return MemoryAdmissionResult.Backpressured();
            }

            MemoryRequestId requestId = AllocateRequestId();
            PhysicalMemoryBankBinding physicalBankBinding = _memorySubsystem
                .CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(
                    address);
            _outstanding.Add(
                requestId,
                ControllerRequest.CreateRead(
                    requestClass,
                    deviceId,
                    address,
                    size,
                    physicalBankBinding));
            IncrementOutstandingClass(requestClass);
            _readQueue.Enqueue(requestId);
            RecordAcceptedRequest(_outstanding[requestId]);
            return MemoryAdmissionResult.Accepted(requestId);
        }
    }

    public bool TryTakeCompletion(MemoryRequestId requestId, out MemoryCompletion? completion)
    {
        lock (_gate)
        {
            if (!_publishedCompletions.Remove(requestId, out completion))
            {
                completion = null;
                return false;
            }

            if (_outstanding.Remove(requestId, out ControllerRequest request))
            {
                DecrementOutstandingClass(request);
            }
            return true;
        }
    }

    /// <summary>
    /// Terminal cancellation for a squashed consumer. A canceled identity no
    /// longer reserves capacity and can never publish a completion.
    /// </summary>
    public bool TryCancel(MemoryRequestId requestId)
    {
        lock (_gate)
        {
            if (!_outstanding.Remove(requestId, out ControllerRequest request))
            {
                return false;
            }

            DecrementOutstandingClass(request);
            _nextCompletions.Remove(requestId);
            _publishedCompletions.Remove(requestId);
            return true;
        }
    }

    internal bool OwnsOutstandingSingleLaneScalarLoad(
        MemoryRequestId requestId,
        ulong deviceId,
        ulong address,
        int size)
    {
        lock (_gate)
        {
            return _outstanding.TryGetValue(requestId, out ControllerRequest request) &&
                request.ReadRequestClass == ReadRequestClass.SingleLaneScalarMicroOp &&
                request.DeviceId == deviceId &&
                request.Address == address &&
                request.Size == size;
        }
    }

    internal bool OwnsOutstandingVectorSegmentLoad(
        MemoryRequestId requestId,
        ulong deviceId,
        ulong address,
        int size)
    {
        lock (_gate)
        {
            return _outstanding.TryGetValue(requestId, out ControllerRequest request) &&
                request.ReadRequestClass == ReadRequestClass.VectorSegmentMicroOp &&
                request.DeviceId == deviceId &&
                request.Address == address &&
                request.Size == size;
        }
    }

    internal bool OwnsOutstandingCanonicalVectorTransfer(
        MemoryRequestId requestId,
        uint opcode,
        ulong deviceId,
        ulong sourceAddress,
        ulong destinationAddress,
        ulong elementCount,
        int elementSize,
        ushort stride)
    {
        lock (_gate)
        {
            return _outstanding.TryGetValue(requestId, out ControllerRequest request) &&
                request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer &&
                request.Opcode == opcode &&
                request.DeviceId == deviceId &&
                request.Address == sourceAddress &&
                request.DestinationAddress == destinationAddress &&
                request.ElementCount == elementCount &&
                request.ElementSize == elementSize &&
                request.Stride == stride;
        }
    }

    internal bool OwnsOutstandingExplicitPacketScalarStore(
        MemoryRequestId requestId,
        ulong deviceId,
        ulong address,
        int size,
        ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            return _outstanding.TryGetValue(requestId, out ControllerRequest request) &&
                request.StoreRequestClass == ScalarStoreRequestClass.ExplicitPacket &&
                request.DeviceId == deviceId &&
                request.Address == address &&
                request.Size == size &&
                data.Length >= size &&
                request.Data.AsSpan().SequenceEqual(data[..size]);
        }
    }

    internal bool OwnsOutstandingSingleLaneScalarStore(
        MemoryRequestId requestId,
        ulong deviceId,
        ulong address,
        int size,
        ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            return _outstanding.TryGetValue(requestId, out ControllerRequest request) &&
                request.StoreRequestClass == ScalarStoreRequestClass.SingleLaneMicroOp &&
                request.DeviceId == deviceId &&
                request.Address == address &&
                request.Size == size &&
                data.Length >= size &&
                request.Data.AsSpan().SequenceEqual(data[..size]);
        }
    }

    internal bool AdvancePlatformEdge(ulong platformCycle)
    {
        lock (_gate)
        {
            if (platformCycle <= _lastPlatformCycle)
            {
                return false;
            }

            _lastPlatformCycle = platformCycle;
            TickOneCycle();
            return true;
        }
    }

    internal void AdvanceCompatibilityCycles(long cycles)
    {
        if (cycles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cycles));
        }

        lock (_gate)
        {
            for (long cycle = 0; cycle < cycles; cycle++)
            {
                _lastPlatformCycle = checked(_lastPlatformCycle + 1);
                TickOneCycle();
            }
        }
    }

    private void TickOneCycle()
    {
        // RF-12.6ac lock order: both callers hold _gate. Geometry-dependent
        // owner calls below acquire the inner MemorySubsystem
        // geometryLifecycleGate and never call back into this controller.
        _memoryCycle = checked(_memoryCycle + 1);
        _telemetryControllerCycles = checked(_telemetryControllerCycles + 1);

        bool publishedCompletionThisCycle = false;
        foreach ((MemoryRequestId requestId, MemoryCompletion completion) in _nextCompletions)
        {
            if (_outstanding.TryGetValue(requestId, out ControllerRequest request))
            {
                _publishedCompletions.Add(
                    requestId,
                    new MemoryCompletion(
                        requestId,
                        completion.Succeeded,
                        completion.Data.ToArray(),
                        completion.FailureReason,
                        _memoryCycle));
                RecordPublishedCompletion(request, completion);
                publishedCompletionThisCycle = true;
            }
        }
        _nextCompletions.Clear();
        if (publishedCompletionThisCycle)
        {
            _telemetryCompletionPublicationCycles = checked(
                _telemetryCompletionPublicationCycles + 1);
        }

        // All non-migrated queues keep their exact legacy single-cycle service
        // owner. The controller merely owns when that service edge occurs.
        _memorySubsystem.AdvanceLegacyAgentOneCycle();

        // RF-10.13: the explicitly bound persistent DMA agent advances at
        // most once on this controller edge. No caller may recursively drive
        // DMA completion.
        _memorySubsystem.AdvanceBoundDmaAgentOneCycle();

        while (_readQueue.Count > 0)
        {
            MemoryRequestId requestId = _readQueue.Dequeue();
            if (!_outstanding.TryGetValue(requestId, out ControllerRequest request) ||
                !request.ReadRequestClass.HasValue)
            {
                continue;
            }

            byte[] data = new byte[request.Size];
            bool succeeded = request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer
                ? _memorySubsystem.ExecuteControllerVectorTransferReadStep(
                    request.DeviceId,
                    request.Address,
                    request.ElementCount,
                    request.ElementSize,
                    request.Stride,
                    request.PhysicalBankEnvelope,
                    data)
                : _memorySubsystem.ExecuteControllerReadStep(
                    request.DeviceId,
                    request.Address,
                    request.PhysicalBankBinding,
                    data);
            string? failureReason = succeeded
                ? null
                : $"MemoryCycleController {RenderRequestClass(request.ReadRequestClass.Value)} read failed at 0x{request.Address:X} for {request.Size} byte(s).";
            _nextCompletions.Add(
                requestId,
                new MemoryCompletion(requestId, succeeded, data, failureReason, publishedCycle: 0));
            _telemetryReadServiceCycles = checked(_telemetryReadServiceCycles + 1);
            break;
        }

        while (_scalarStoreQueue.Count > 0)
        {
            MemoryRequestId requestId = _scalarStoreQueue.Dequeue();
            if (!_outstanding.TryGetValue(requestId, out ControllerRequest request) ||
                !request.StoreRequestClass.HasValue)
            {
                continue;
            }

            // RF-10.4 completion establishes readiness only. The immutable
            // request snapshot is retained for identity/cancellation proof,
            // but physical publication remains selected-retire-owned.
            _nextCompletions.Add(
                requestId,
                new MemoryCompletion(
                    requestId,
                    succeeded: true,
                    Array.Empty<byte>(),
                    failureReason: null,
                    publishedCycle: 0));
            _telemetryStoreReadinessServiceCycles = checked(
                _telemetryStoreReadinessServiceCycles + 1);
            break;
        }
    }

    private void RecordAcceptedRequest(ControllerRequest request)
    {
        _telemetryAcceptedRequests = checked(_telemetryAcceptedRequests + 1);
        if (request.ReadRequestClass.HasValue)
        {
            _telemetryDataReadAcceptedRequests = checked(
                _telemetryDataReadAcceptedRequests + 1);
        }
        if (request.StoreRequestClass.HasValue ||
            request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer)
        {
            _telemetryDataWriteAcceptedRequests = checked(
                _telemetryDataWriteAcceptedRequests + 1);
        }
    }

    private void RecordPublishedCompletion(
        ControllerRequest request,
        MemoryCompletion completion)
    {
        _telemetryCompletedRequests = checked(_telemetryCompletedRequests + 1);
        if (request.ReadRequestClass.HasValue)
        {
            _telemetryDataReadCompletedRequests = checked(
                _telemetryDataReadCompletedRequests + 1);
            if (completion.Succeeded)
            {
                _telemetryDataReadBytes = checked(
                    _telemetryDataReadBytes + (ulong)completion.Data.Length);
            }
        }
        if (request.StoreRequestClass.HasValue ||
            request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer)
        {
            _telemetryDataWriteCompletedRequests = checked(
                _telemetryDataWriteCompletedRequests + 1);
        }
    }

    private MemoryRequestId AllocateRequestId()
    {
        while (true)
        {
            ulong value = _nextRequestId++;
            if (value == 0)
            {
                continue;
            }

            MemoryRequestId requestId = new(value);
            if (!_outstanding.ContainsKey(requestId))
            {
                return requestId;
            }
        }
    }

    private void IncrementOutstandingClass(ReadRequestClass requestClass)
    {
        if (requestClass == ReadRequestClass.ExplicitPacketScalar)
        {
            _outstandingExplicitPacketScalarLoads++;
        }
        else if (requestClass == ReadRequestClass.SingleLaneScalarMicroOp)
        {
            _outstandingSingleLaneScalarLoads++;
        }
        else
        {
            _outstandingVectorSegmentLoads++;
        }
    }

    private void IncrementOutstandingClass(ScalarStoreRequestClass requestClass)
    {
        if (requestClass == ScalarStoreRequestClass.ExplicitPacket)
        {
            _outstandingExplicitPacketScalarStores++;
        }
        else
        {
            _outstandingSingleLaneScalarStores++;
        }
    }

    private void DecrementOutstandingClass(ControllerRequest request)
    {
        if (request.StoreRequestClass == ScalarStoreRequestClass.ExplicitPacket)
        {
            _outstandingExplicitPacketScalarStores--;
        }
        else if (request.StoreRequestClass == ScalarStoreRequestClass.SingleLaneMicroOp)
        {
            _outstandingSingleLaneScalarStores--;
        }
        else if (request.ReadRequestClass == ReadRequestClass.ExplicitPacketScalar)
        {
            _outstandingExplicitPacketScalarLoads--;
        }
        else if (request.ReadRequestClass == ReadRequestClass.SingleLaneScalarMicroOp)
        {
            _outstandingSingleLaneScalarLoads--;
        }
        else if (request.ReadRequestClass == ReadRequestClass.VectorSegmentMicroOp)
        {
            _outstandingVectorSegmentLoads--;
        }
        else if (request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer)
        {
            _outstandingCanonicalVectorTransfers--;
        }
        else
        {
            throw new InvalidOperationException("Controller request has no recognized request class.");
        }
    }

    private static string RenderRequestClass(ReadRequestClass requestClass) =>
        requestClass switch
        {
            ReadRequestClass.ExplicitPacketScalar => "explicit-packet scalar",
            ReadRequestClass.SingleLaneScalarMicroOp => "single-lane scalar",
            ReadRequestClass.VectorSegmentMicroOp => "vector-segment",
            ReadRequestClass.CanonicalVectorTransfer => "canonical vector-transfer",
            _ => throw new InvalidOperationException($"Unsupported read request class {requestClass}.")
        };

    private static string RenderRequestClass(ScalarStoreRequestClass requestClass) =>
        requestClass == ScalarStoreRequestClass.ExplicitPacket
            ? "explicit-packet"
            : "single-lane";

    private enum ReadRequestClass : byte
    {
        ExplicitPacketScalar,
        SingleLaneScalarMicroOp,
        VectorSegmentMicroOp,
        CanonicalVectorTransfer
    }

    private enum ScalarStoreRequestClass : byte
    {
        ExplicitPacket,
        SingleLaneMicroOp
    }

    private readonly record struct ControllerRequest(
        ReadRequestClass? ReadRequestClass,
        ScalarStoreRequestClass? StoreRequestClass,
        ulong DeviceId,
        ulong Address,
        int Size,
        byte[] Data,
        PhysicalMemoryBankBinding PhysicalBankBinding,
        uint Opcode = 0,
        ulong DestinationAddress = 0,
        ulong ElementCount = 0,
        int ElementSize = 0,
        ushort Stride = 0,
        CanonicalVectorPhysicalBankEnvelope PhysicalBankEnvelope = default)
    {
        internal static ControllerRequest CreateRead(
            ReadRequestClass requestClass,
            ulong deviceId,
            ulong address,
            int size,
            PhysicalMemoryBankBinding physicalBankBinding) =>
            new(
                requestClass,
                null,
                deviceId,
                address,
                size,
                Array.Empty<byte>(),
                physicalBankBinding);

        internal static ControllerRequest CreateCanonicalVectorTransfer(
            uint opcode,
            ulong deviceId,
            ulong sourceAddress,
            ulong destinationAddress,
            ulong elementCount,
            int elementSize,
            ushort stride,
            int packedSize,
            CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope) =>
            new(
                MemoryCycleController.ReadRequestClass.CanonicalVectorTransfer,
                null,
                deviceId,
                sourceAddress,
                packedSize,
                Array.Empty<byte>(),
                default,
                opcode,
                destinationAddress,
                elementCount,
                elementSize,
                stride,
                physicalBankEnvelope);

        internal static ControllerRequest CreateScalarStore(
            ScalarStoreRequestClass requestClass,
            ulong deviceId,
            ulong address,
            int size,
            byte[] data,
            PhysicalMemoryBankBinding physicalBankBinding) =>
            new(
                null,
                requestClass,
                deviceId,
                address,
                size,
                data,
                physicalBankBinding);
    }
}

/// <summary>
/// Compatibility platform-edge adapter for existing per-core drivers. Cores
/// sharing a controller report the same platform-cycle number; only the first
/// observation supplies the controller edge.
/// </summary>
public static class MemoryCyclePlatformOrchestrator
{
    private static readonly ConditionalWeakTable<MemoryCycleController, DomainState> Domains = new();

    public static bool AdvanceCoreObservedPlatformEdge(
        MemoryCycleController controller,
        uint coreId,
        ulong platformCycle)
    {
        ArgumentNullException.ThrowIfNull(controller);
        DomainState domain = Domains.GetValue(controller, static _ => new DomainState());
        lock (domain.Gate)
        {
            if (!domain.CoreClocks.TryGetValue(coreId, out CoreClockState coreClock))
            {
                coreClock = new CoreClockState(0, 0);
            }

            if (platformCycle == coreClock.LastLocalCycle)
            {
                return false;
            }

            ulong epochOffset = coreClock.EpochOffset;
            if (platformCycle < coreClock.LastLocalCycle)
            {
                // Compatibility drivers can recreate the same core while the
                // shared MemorySubsystem remains alive (for example repeated
                // diagnostic slices). Rebase that local cycle epoch strictly
                // after the last supplied domain edge.
                epochOffset = checked(domain.LastNormalizedEdge + 1 - platformCycle);
            }

            ulong normalizedEdge = checked(epochOffset + platformCycle);
            domain.CoreClocks[coreId] = new CoreClockState(epochOffset, platformCycle);
            if (normalizedEdge <= domain.LastNormalizedEdge)
            {
                return false;
            }

            domain.LastNormalizedEdge = normalizedEdge;
            return controller.AdvancePlatformEdge(normalizedEdge);
        }
    }

    private sealed class DomainState
    {
        internal object Gate { get; } = new();
        internal Dictionary<uint, CoreClockState> CoreClocks { get; } = new();
        internal ulong LastNormalizedEdge { get; set; }
    }

    private readonly record struct CoreClockState(ulong EpochOffset, ulong LastLocalCycle);
}
