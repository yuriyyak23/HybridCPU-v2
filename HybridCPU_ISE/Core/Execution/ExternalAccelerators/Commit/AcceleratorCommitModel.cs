using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;

public enum AcceleratorCommitInvalidationTarget : byte
{
    SrfWindow = 0,
    CacheWindow = 1
}

public sealed record AcceleratorCommitFault
{
    public AcceleratorCommitFault(
        AcceleratorTokenFaultCode faultCode,
        string message,
        ulong faultAddress = 0,
        bool isWrite = false,
        AcceleratorGuardDecision? guardDecision = null)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "L7-SDC commit faults require a non-None fault code.",
                nameof(faultCode));
        }

        FaultCode = faultCode;
        Message = string.IsNullOrWhiteSpace(message)
            ? $"L7-SDC commit fault: {faultCode}."
            : message;
        FaultAddress = faultAddress;
        IsWrite = isWrite;
        GuardDecision = guardDecision;
    }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public string Message { get; }

    public ulong FaultAddress { get; }

    public bool IsWrite { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }
}

public sealed record AcceleratorRollbackRecord
{
    private AcceleratorRollbackRecord(
        IReadOnlyList<AcceleratorStagedWrite> attemptedWrites,
        IReadOnlyList<AcceleratorStagedWrite> backups,
        int writeFailureIndex,
        bool rollbackAttempted,
        bool rollbackSucceeded,
        string message)
    {
        AttemptedWrites = attemptedWrites;
        Backups = backups;
        WriteFailureIndex = writeFailureIndex;
        RollbackAttempted = rollbackAttempted;
        RollbackSucceeded = rollbackSucceeded;
        Message = message;
    }

    public IReadOnlyList<AcceleratorStagedWrite> AttemptedWrites { get; }

    public IReadOnlyList<AcceleratorStagedWrite> Backups { get; }

    public int WriteFailureIndex { get; }

    public bool RollbackAttempted { get; }

    public bool RollbackSucceeded { get; }

    public string Message { get; }

    public static AcceleratorRollbackRecord None { get; } =
        new(
            Array.Empty<AcceleratorStagedWrite>(),
            Array.Empty<AcceleratorStagedWrite>(),
            writeFailureIndex: -1,
            rollbackAttempted: false,
            rollbackSucceeded: true,
            "No L7-SDC rollback was required.");

    public static AcceleratorRollbackRecord SnapshotOnly(
        IReadOnlyList<AcceleratorStagedWrite> attemptedWrites,
        IReadOnlyList<AcceleratorStagedWrite> backups) =>
        new(
            FreezeWrites(attemptedWrites),
            FreezeWrites(backups),
            writeFailureIndex: -1,
            rollbackAttempted: false,
            rollbackSucceeded: true,
            "L7-SDC commit snapshot was captured; no rollback was required.");

    public static AcceleratorRollbackRecord Completed(
        IReadOnlyList<AcceleratorStagedWrite> attemptedWrites,
        IReadOnlyList<AcceleratorStagedWrite> backups,
        int writeFailureIndex,
        bool rollbackSucceeded,
        string message) =>
        new(
            FreezeWrites(attemptedWrites),
            FreezeWrites(backups),
            writeFailureIndex,
            rollbackAttempted: true,
            rollbackSucceeded,
            message);

    internal static IReadOnlyList<AcceleratorStagedWrite> FreezeWrites(
        IReadOnlyList<AcceleratorStagedWrite> writes)
    {
        if (writes is null || writes.Count == 0)
        {
            return Array.Empty<AcceleratorStagedWrite>();
        }

        var copy = new AcceleratorStagedWrite[writes.Count];
        for (int index = 0; index < writes.Count; index++)
        {
            AcceleratorStagedWrite write = writes[index];
            copy[index] = write with
            {
                Data = write.Data.ToArray()
            };
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed record AcceleratorCommitInvalidationRecord(
    AcceleratorCommitInvalidationTarget Target,
    AcceleratorMemoryRange Window,
    AcceleratorMemoryRange CommittedRange,
    bool Overlapped,
    bool Invalidated);

public sealed class AcceleratorCommitInvalidationPlan
{
    private AcceleratorCommitInvalidationPlan(
        StreamRegisterFile? streamRegisterFile,
        IReadOnlyList<AcceleratorMemoryRange> srfWindows,
        IReadOnlyList<AcceleratorMemoryRange> cacheWindows,
        IReadOnlyList<AcceleratorCommitInvalidationRecord> records)
    {
        StreamRegisterFile = streamRegisterFile;
        SrfWindows = FreezeRanges(srfWindows);
        CacheWindows = FreezeRanges(cacheWindows);
        Records = FreezeRecords(records);
    }

    public StreamRegisterFile? StreamRegisterFile { get; }

    public IReadOnlyList<AcceleratorMemoryRange> SrfWindows { get; }

    public IReadOnlyList<AcceleratorMemoryRange> CacheWindows { get; }

    public IReadOnlyList<AcceleratorCommitInvalidationRecord> Records { get; }

    public int SrfInvalidationCount => CountInvalidated(AcceleratorCommitInvalidationTarget.SrfWindow);

    public int CacheInvalidationCount => CountInvalidated(AcceleratorCommitInvalidationTarget.CacheWindow);

    public bool HasInvalidationEvidence => Records.Count != 0;

    public static AcceleratorCommitInvalidationPlan None { get; } =
        new(
            streamRegisterFile: null,
            Array.Empty<AcceleratorMemoryRange>(),
            Array.Empty<AcceleratorMemoryRange>(),
            Array.Empty<AcceleratorCommitInvalidationRecord>());

    public static AcceleratorCommitInvalidationPlan Observe(
        IReadOnlyList<AcceleratorMemoryRange>? srfWindows = null,
        IReadOnlyList<AcceleratorMemoryRange>? cacheWindows = null,
        StreamRegisterFile? streamRegisterFile = null) =>
        new(
            streamRegisterFile,
            srfWindows ?? Array.Empty<AcceleratorMemoryRange>(),
            cacheWindows ?? Array.Empty<AcceleratorMemoryRange>(),
            Array.Empty<AcceleratorCommitInvalidationRecord>());

    public AcceleratorCommitInvalidationPlan WithRecords(
        IReadOnlyList<AcceleratorCommitInvalidationRecord> records) =>
        new(
            StreamRegisterFile,
            SrfWindows,
            CacheWindows,
            records);

    private int CountInvalidated(AcceleratorCommitInvalidationTarget target)
    {
        int count = 0;
        for (int index = 0; index < Records.Count; index++)
        {
            AcceleratorCommitInvalidationRecord record = Records[index];
            if (record.Target == target && record.Invalidated)
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<AcceleratorMemoryRange> FreezeRanges(
        IReadOnlyList<AcceleratorMemoryRange> ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<AcceleratorMemoryRange>();
        }

        var copy = new AcceleratorMemoryRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            copy[index] = ranges[index];
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<AcceleratorCommitInvalidationRecord> FreezeRecords(
        IReadOnlyList<AcceleratorCommitInvalidationRecord> records)
    {
        if (records is null || records.Count == 0)
        {
            return Array.Empty<AcceleratorCommitInvalidationRecord>();
        }

        var copy = new AcceleratorCommitInvalidationRecord[records.Count];
        for (int index = 0; index < records.Count; index++)
        {
            copy[index] = records[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed class AcceleratorStagedWriteSet
{
    public AcceleratorStagedWriteSet(IReadOnlyList<AcceleratorStagedWrite> writes)
    {
        Writes = AcceleratorRollbackRecord.FreezeWrites(
            writes ?? Array.Empty<AcceleratorStagedWrite>());
    }

    public IReadOnlyList<AcceleratorStagedWrite> Writes { get; }

    public int Count => Writes.Count;

    public ulong BytesStaged
    {
        get
        {
            ulong bytes = 0;
            for (int index = 0; index < Writes.Count; index++)
            {
                bytes += Writes[index].Length;
            }

            return bytes;
        }
    }

    public static AcceleratorStagedWriteSet FromStagingReadResult(
        AcceleratorStagingReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        return new AcceleratorStagedWriteSet(readResult.StagedWrites);
    }

    public IReadOnlyList<AcceleratorMemoryRange> NormalizeDestinationFootprint()
    {
        if (!TryNormalizeStagedWriteRanges(
                Writes,
                out IReadOnlyList<AcceleratorMemoryRange> normalized,
                out _,
                out _))
        {
            return Array.Empty<AcceleratorMemoryRange>();
        }

        return normalized;
    }

    public bool CoversExactly(
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationFootprint) =>
        CoversExactly(
            normalizedDestinationFootprint,
            out _,
            out _);

    public bool CoversExactly(
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationFootprint,
        out AcceleratorMemoryRange faultRange,
        out string message)
    {
        if (!TryNormalizeStagedWriteRanges(
                Writes,
                out IReadOnlyList<AcceleratorMemoryRange> stagedRanges,
                out faultRange,
                out message))
        {
            return false;
        }

        if (!TryNormalizeExpectedRanges(
                normalizedDestinationFootprint,
                out IReadOnlyList<AcceleratorMemoryRange> expectedRanges,
                out faultRange,
                out message))
        {
            return false;
        }

        if (stagedRanges.Count != expectedRanges.Count)
        {
            faultRange = expectedRanges.Count > 0
                ? expectedRanges[0]
                : default;
            message =
                "L7-SDC staged writes do not cover every normalized destination footprint range.";
            return false;
        }

        for (int index = 0; index < stagedRanges.Count; index++)
        {
            if (stagedRanges[index].Address == expectedRanges[index].Address &&
                stagedRanges[index].Length == expectedRanges[index].Length)
            {
                continue;
            }

            faultRange = expectedRanges[index];
            message =
                "L7-SDC staged writes do not exactly match the normalized destination footprint.";
            return false;
        }

        faultRange = default;
        message = "L7-SDC staged writes exactly cover the normalized destination footprint.";
        return true;
    }

    internal bool IsTokenBound(
        AcceleratorTokenHandle tokenHandle,
        out string message)
    {
        for (int index = 0; index < Writes.Count; index++)
        {
            if (Writes[index].TokenHandle.Equals(tokenHandle))
            {
                continue;
            }

            message =
                "L7-SDC staged write-set contains bytes not bound to the token selected for commit.";
            return false;
        }

        message = "L7-SDC staged write-set is token-bound.";
        return true;
    }

    private static bool TryNormalizeStagedWriteRanges(
        IReadOnlyList<AcceleratorStagedWrite> writes,
        out IReadOnlyList<AcceleratorMemoryRange> normalized,
        out AcceleratorMemoryRange faultRange,
        out string message)
    {
        if (writes is null || writes.Count == 0)
        {
            normalized = Array.Empty<AcceleratorMemoryRange>();
            faultRange = default;
            message =
                "L7-SDC all-or-none commit requires at least one staged destination write.";
            return false;
        }

        var ranges = new AcceleratorMemoryRange[writes.Count];
        for (int index = 0; index < writes.Count; index++)
        {
            AcceleratorStagedWrite write = writes[index];
            ranges[index] = new AcceleratorMemoryRange(write.Address, write.Length);
        }

        return TryNormalizeExpectedRanges(
            ranges,
            out normalized,
            out faultRange,
            out message);
    }

    private static bool TryNormalizeExpectedRanges(
        IReadOnlyList<AcceleratorMemoryRange> ranges,
        out IReadOnlyList<AcceleratorMemoryRange> normalized,
        out AcceleratorMemoryRange faultRange,
        out string message)
    {
        normalized = Array.Empty<AcceleratorMemoryRange>();
        faultRange = default;

        if (ranges is null || ranges.Count == 0)
        {
            message =
                "L7-SDC normalized destination footprint is empty; commit cannot publish memory.";
            return false;
        }

        var sorted = new AcceleratorMemoryRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            AcceleratorMemoryRange range = ranges[index];
            if (range.Length == 0 ||
                range.Address > ulong.MaxValue - range.Length)
            {
                faultRange = range;
                message =
                    "L7-SDC destination footprint contains a zero-length or overflowing range.";
                return false;
            }

            sorted[index] = range;
        }

        Array.Sort(
            sorted,
            static (left, right) =>
            {
                int addressCompare = left.Address.CompareTo(right.Address);
                return addressCompare != 0
                    ? addressCompare
                    : left.Length.CompareTo(right.Length);
            });

        var merged = new List<AcceleratorMemoryRange>(sorted.Length)
        {
            sorted[0]
        };

        for (int index = 1; index < sorted.Length; index++)
        {
            AcceleratorMemoryRange previous = merged[^1];
            AcceleratorMemoryRange current = sorted[index];
            ulong previousEnd = previous.Address + previous.Length;
            if (current.Address < previousEnd)
            {
                normalized = Array.Empty<AcceleratorMemoryRange>();
                faultRange = current;
                message =
                    "L7-SDC staged destination writes overlap; Phase 08 all-or-none commit rejects overlapping write evidence.";
                return false;
            }

            if (current.Address == previousEnd)
            {
                merged[^1] = new AcceleratorMemoryRange(
                    previous.Address,
                    previous.Length + current.Length);
                continue;
            }

            merged.Add(current);
        }

        normalized = Array.AsReadOnly(merged.ToArray());
        message = "L7-SDC destination footprint normalized.";
        return true;
    }
}

public sealed record AcceleratorCommitResult
{
    private AcceleratorCommitResult(
        bool succeeded,
        AcceleratorTokenState tokenState,
        AcceleratorCommitFault? fault,
        AcceleratorRollbackRecord rollback,
        AcceleratorCommitInvalidationPlan invalidationPlan,
        ulong bytesCommitted,
        string message)
    {
        Succeeded = succeeded;
        TokenState = tokenState;
        Fault = fault;
        Rollback = rollback;
        InvalidationPlan = invalidationPlan;
        BytesCommitted = bytesCommitted;
        Message = message;
    }

    public bool Succeeded { get; }

    public bool IsRejected => !Succeeded;

    public AcceleratorTokenState TokenState { get; }

    public AcceleratorCommitFault? Fault { get; }

    public AcceleratorTokenFaultCode FaultCode => Fault?.FaultCode ?? AcceleratorTokenFaultCode.None;

    public AcceleratorRollbackRecord Rollback { get; }

    public AcceleratorCommitInvalidationPlan InvalidationPlan { get; }

    public ulong BytesCommitted { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => Succeeded;

    public bool UserVisiblePublicationAllowed => Succeeded;

    public bool RequiresRetireExceptionPublication => false;

    public static AcceleratorCommitResult Success(
        AcceleratorToken token,
        ulong bytesCommitted,
        AcceleratorCommitInvalidationPlan invalidationPlan) =>
        new(
            succeeded: true,
            token.State,
            fault: null,
            AcceleratorRollbackRecord.None,
            invalidationPlan,
            bytesCommitted,
            "L7-SDC Phase 08 commit published staged writes architecturally through the guarded commit coordinator.");

    public static AcceleratorCommitResult Rejected(
        AcceleratorToken token,
        AcceleratorCommitFault fault,
        AcceleratorRollbackRecord? rollback = null,
        AcceleratorCommitInvalidationPlan? invalidationPlan = null) =>
        new(
            succeeded: false,
            token.State,
            fault,
            rollback ?? AcceleratorRollbackRecord.None,
            invalidationPlan ?? AcceleratorCommitInvalidationPlan.None,
            bytesCommitted: 0,
            fault.Message);
}

public sealed class AcceleratorCommitCoordinator
{
    private readonly AcceleratorTelemetry? _telemetry;

    public AcceleratorCommitCoordinator(AcceleratorTelemetry? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public AcceleratorCommitResult TryCommit(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        IAcceleratorStagingBuffer stagingBuffer,
        Processor.MainMemoryArea mainMemory,
        AcceleratorGuardEvidence? currentGuardEvidence,
        AcceleratorCommitInvalidationPlan invalidationPlan,
        bool commitConflictPlaceholderAccepted,
        bool directWriteViolationDetected = false,
        ExternalAcceleratorConflictManager? conflictManager = null,
        MemoryCoherencyObserver? coherencyObserver = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(stagingBuffer);
        ArgumentNullException.ThrowIfNull(mainMemory);
        invalidationPlan ??= AcceleratorCommitInvalidationPlan.None;

        if (!ValidateCommitPreconditions(
                token,
                descriptor,
                stagingBuffer,
                currentGuardEvidence,
                commitConflictPlaceholderAccepted,
                directWriteViolationDetected,
                conflictManager,
                out AcceleratorStagedWriteSet? stagedWriteSet,
                out AcceleratorCommitResult? preconditionReject))
        {
            ReleaseTerminalReservationIfNeeded(
                conflictManager,
                token,
                currentGuardEvidence);
            return RecordCommitResult(preconditionReject!, token);
        }

        if (token.State == AcceleratorTokenState.DeviceComplete)
        {
            AcceleratorTokenTransition pending =
                token.MarkCommitPendingFromCommitCoordinator(currentGuardEvidence!);
            if (pending.Rejected)
            {
                return RecordCommitResult(
                    AcceleratorCommitResult.Rejected(
                        token,
                        new AcceleratorCommitFault(
                            pending.FaultCode,
                            pending.Message,
                            guardDecision: token.SubmitGuardDecision)),
                    token);
            }
        }

        if (!ApplyAllOrNone(
                token,
                descriptor,
                mainMemory,
                stagedWriteSet!,
                currentGuardEvidence,
                out AcceleratorRollbackRecord rollback,
                out AcceleratorCommitFault? memoryFault,
                out ulong bytesCommitted))
        {
            token.MarkFaulted(
                memoryFault!.FaultCode,
                currentGuardEvidence!);
            ReleaseTerminalReservationIfNeeded(
                conflictManager,
                token,
                currentGuardEvidence);
            return RecordCommitResult(
                AcceleratorCommitResult.Rejected(
                    token,
                    memoryFault,
                    rollback),
                token);
        }

        if (!InvalidateSrfAndCache(
                token,
                descriptor,
                stagedWriteSet!,
                invalidationPlan,
                currentGuardEvidence,
                out AcceleratorCommitInvalidationPlan invalidated,
                out AcceleratorCommitFault? invalidationFault))
        {
            AcceleratorRollbackRecord invalidationRollback =
                Rollback(
                    token,
                    descriptor,
                    mainMemory,
                    rollback.Backups,
                    stagedWriteSet!.Writes,
                    writeFailureIndex: stagedWriteSet.Writes.Count - 1,
                    currentGuardEvidence);
            token.MarkFaulted(
                invalidationFault!.FaultCode,
                currentGuardEvidence!);
            ReleaseTerminalReservationIfNeeded(
                conflictManager,
                token,
                currentGuardEvidence);
            return RecordCommitResult(
                AcceleratorCommitResult.Rejected(
                    token,
                    invalidationFault,
                    invalidationRollback),
                token);
        }

        AcceleratorTokenTransition committed =
            token.MarkCommittedFromCommitCoordinator(currentGuardEvidence!);
        if (committed.Rejected)
        {
            AcceleratorRollbackRecord postWriteRollback =
                Rollback(
                    token,
                    descriptor,
                    mainMemory,
                    rollback.Backups,
                    stagedWriteSet!.Writes,
                    writeFailureIndex: stagedWriteSet.Writes.Count - 1,
                    currentGuardEvidence);
            token.MarkFaulted(
                committed.FaultCode == AcceleratorTokenFaultCode.None
                    ? AcceleratorTokenFaultCode.CommitMemoryFault
                    : committed.FaultCode,
                currentGuardEvidence!);
            ReleaseTerminalReservationIfNeeded(
                conflictManager,
                token,
                currentGuardEvidence);
            return RecordCommitResult(
                AcceleratorCommitResult.Rejected(
                    token,
                    new AcceleratorCommitFault(
                        committed.FaultCode == AcceleratorTokenFaultCode.None
                            ? AcceleratorTokenFaultCode.CommitMemoryFault
                            : committed.FaultCode,
                        committed.Message,
                        guardDecision: token.SubmitGuardDecision),
                    postWriteRollback),
                token);
        }

        conflictManager?.InvalidateSrfCacheOnCommit(
            token,
            currentGuardEvidence);
        conflictManager?.ReleaseTokenFootprint(
            token,
            currentGuardEvidence);
        NotifyCommittedWrites(
            coherencyObserver,
            descriptor,
            stagedWriteSet!);

        return RecordCommitResult(
            AcceleratorCommitResult.Success(
                token,
                bytesCommitted,
                invalidated),
            token);
    }

    private AcceleratorCommitResult RecordCommitResult(
        AcceleratorCommitResult result,
        AcceleratorToken token)
    {
        if (_telemetry is null)
        {
            return result;
        }

        if (result.Succeeded)
        {
            _telemetry.RecordBytesCommitted(
                result.BytesCommitted,
                result.Message);
            _telemetry.RecordSrfInvalidations(
                result.InvalidationPlan.SrfInvalidationCount,
                result.Message);
            _telemetry.RecordCacheInvalidations(
                result.InvalidationPlan.CacheInvalidationCount,
                result.Message);
        }
        else
        {
            _telemetry.RecordCommitReject(
                result.FaultCode,
                token,
                result.Message);
            _telemetry.RecordGuardReject(
                result.Fault?.GuardDecision,
                result.Message);
        }

        _telemetry.RecordCommitRollback(
            result.Rollback.RollbackAttempted,
            result.Rollback.RollbackSucceeded,
            result.Rollback.Message);
        return result;
    }

    private static void NotifyCommittedWrites(
        MemoryCoherencyObserver? coherencyObserver,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorStagedWriteSet stagedWriteSet)
    {
        if (coherencyObserver is null)
        {
            return;
        }

        ulong domainTag = descriptor.OwnerBinding.DomainTag;
        for (int index = 0; index < stagedWriteSet.Writes.Count; index++)
        {
            AcceleratorStagedWrite write = stagedWriteSet.Writes[index];
            coherencyObserver.NotifyWrite(
                new MemoryCoherencyWriteNotification(
                    write.Address,
                    write.Length,
                    domainTag,
                    MemoryCoherencyWriteSourceKind.L7AcceleratorCommit));
        }
    }

    public bool ValidateCommitPreconditions(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence,
        bool commitConflictPlaceholderAccepted,
        bool directWriteViolationDetected,
        ExternalAcceleratorConflictManager? conflictManager,
        out AcceleratorStagedWriteSet? stagedWriteSet,
        out AcceleratorCommitResult? rejection)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(stagingBuffer);

        stagedWriteSet = null;
        rejection = null;

        if (token.State is not AcceleratorTokenState.DeviceComplete and
            not AcceleratorTokenState.CommitPending)
        {
            AcceleratorTokenFaultCode faultCode = token.IsTerminal
                ? AcceleratorTokenFaultCode.TerminalState
                : AcceleratorTokenFaultCode.IllegalTransition;
            rejection = AcceleratorCommitResult.Rejected(
                token,
                new AcceleratorCommitFault(
                    faultCode,
                    $"L7-SDC commit requires DeviceComplete or CommitPending token state, but token is {token.State}."));
            return false;
        }

        AcceleratorGuardDecision guardDecision =
            ValidateOwnerDomainAndMapping(
                token,
                descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            rejection = AcceleratorCommitResult.Rejected(
                token,
                new AcceleratorCommitFault(
                    AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                    "L7-SDC commit requires fresh owner/domain and mapping/IOMMU epoch authority. " +
                    guardDecision.Message,
                    guardDecision: guardDecision));
            return false;
        }

        if (!IsDescriptorIdentityBound(token, descriptor))
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.DescriptorIdentityMismatch,
                "L7-SDC commit descriptor identity hash does not match the token-bound descriptor identity.",
                currentGuardEvidence!,
                guardDecision,
                descriptor.DescriptorReference.DescriptorAddress,
                isWrite: false);
            return false;
        }

        if (!IsNormalizedFootprintBound(token, descriptor))
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.NormalizedFootprintMismatch,
                "L7-SDC commit normalized footprint hash does not match the token-bound footprint identity.",
                currentGuardEvidence!,
                guardDecision,
                descriptor.DescriptorReference.DescriptorAddress,
                isWrite: false);
            return false;
        }

        if (directWriteViolationDetected)
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.DirectWriteViolation,
                "L7-SDC direct-write violation evidence cannot count as commit authority.",
                currentGuardEvidence!,
                guardDecision,
                faultAddress: 0,
                isWrite: true);
            return false;
        }

        if (conflictManager is null && !commitConflictPlaceholderAccepted)
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.CommitConflictRejected,
                "L7-SDC Phase 08 commit requires explicit placeholder conflict acceptance; Phase 10 conflict manager is not implemented.",
                currentGuardEvidence!,
                guardDecision,
                descriptor.NormalizedFootprint.DestinationRanges.Count > 0
                    ? descriptor.NormalizedFootprint.DestinationRanges[0].Address
                    : 0,
                isWrite: true);
            return false;
        }

        AcceleratorStagingReadResult stagingRead =
            stagingBuffer.GetStagedWriteSet(token, currentGuardEvidence);
        if (stagingRead.IsRejected)
        {
            rejection = AcceleratorCommitResult.Rejected(
                token,
                new AcceleratorCommitFault(
                    stagingRead.FaultCode,
                    stagingRead.Message,
                    guardDecision: stagingRead.GuardDecision));
            return false;
        }

        stagedWriteSet = AcceleratorStagedWriteSet.FromStagingReadResult(stagingRead);
        if (!stagedWriteSet.IsTokenBound(token.Handle, out string tokenBindingMessage))
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.TokenHandleNotAuthority,
                tokenBindingMessage,
                currentGuardEvidence!,
                guardDecision,
                faultAddress: 0,
                isWrite: true);
            return false;
        }

        if (!ValidateExactCoverage(
                stagedWriteSet,
                token.Descriptor.NormalizedFootprint.DestinationRanges,
                out AcceleratorMemoryRange coverageFaultRange,
                out string coverageMessage))
        {
            rejection = FaultToken(
                token,
                AcceleratorTokenFaultCode.StagedCoverageMismatch,
                coverageMessage,
                currentGuardEvidence!,
                guardDecision,
                coverageFaultRange.Address,
                isWrite: true);
            return false;
        }

        if (conflictManager is not null)
        {
            AcceleratorConflictDecision conflictDecision =
                conflictManager.ValidateBeforeCommit(
                    token,
                    currentGuardEvidence);
            if (conflictDecision.IsRejected)
            {
                AcceleratorTokenFaultCode faultCode =
                    conflictDecision.TokenFaultCode == AcceleratorTokenFaultCode.None
                        ? AcceleratorTokenFaultCode.CommitConflictRejected
                        : conflictDecision.TokenFaultCode;
                rejection = FaultToken(
                    token,
                    faultCode,
                    conflictDecision.Message,
                    currentGuardEvidence!,
                    conflictDecision.GuardDecision ?? guardDecision,
                    conflictDecision.Fault?.FaultAddress ??
                    (token.Descriptor.NormalizedFootprint.DestinationRanges.Count > 0
                        ? token.Descriptor.NormalizedFootprint.DestinationRanges[0].Address
                        : 0),
                    isWrite: true);
                return false;
            }
        }

        return true;
    }

    public bool ValidateExactCoverage(
        AcceleratorStagedWriteSet stagedWriteSet,
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationFootprint,
        out AcceleratorMemoryRange faultRange,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(stagedWriteSet);
        return stagedWriteSet.CoversExactly(
            normalizedDestinationFootprint,
            out faultRange,
            out message);
    }

    public AcceleratorGuardDecision ValidateOwnerDomainAndMapping(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);

        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            return tokenGuardDecision;
        }

        AcceleratorGuardDecision commitGuardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeCommit(
                descriptor,
                currentGuardEvidence);
        if (!commitGuardDecision.IsAllowed)
        {
            return commitGuardDecision;
        }

        if (tokenGuardDecision.DescriptorOwnerBinding is null ||
            commitGuardDecision.DescriptorOwnerBinding is null ||
            !tokenGuardDecision.DescriptorOwnerBinding.Equals(commitGuardDecision.DescriptorOwnerBinding))
        {
            return AcceleratorGuardDecision.Reject(
                AcceleratorGuardSurface.Commit,
                AcceleratorGuardFault.DescriptorOwnerBindingMismatch,
                descriptor.OwnerBinding,
                currentGuardEvidence,
                RejectKind.OwnerMismatch,
                "L7-SDC commit token guard and descriptor commit guard bind different owners.");
        }

        return commitGuardDecision;
    }

    public bool ApplyAllOrNone(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        Processor.MainMemoryArea mainMemory,
        AcceleratorStagedWriteSet stagedWriteSet,
        AcceleratorGuardEvidence? currentGuardEvidence,
        out AcceleratorRollbackRecord rollbackRecord,
        out AcceleratorCommitFault? fault,
        out ulong bytesCommitted)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(mainMemory);
        ArgumentNullException.ThrowIfNull(stagedWriteSet);

        rollbackRecord = AcceleratorRollbackRecord.None;
        fault = null;
        bytesCommitted = 0;

        if (token.State != AcceleratorTokenState.CommitPending)
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.IllegalTransition,
                $"L7-SDC all-or-none publication requires CommitPending token state, but token is {token.State}.");
            return false;
        }

        AcceleratorGuardDecision guardDecision =
            ValidateOwnerDomainAndMapping(
                token,
                descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "L7-SDC all-or-none publication requires fresh owner/domain and mapping/IOMMU epoch authority. " +
                guardDecision.Message,
                guardDecision: guardDecision);
            return false;
        }

        if (!ValidateDescriptorAndFootprintBinding(
                token,
                descriptor,
                guardDecision,
                out fault))
        {
            return false;
        }

        if (!ValidateExactCoverage(
                stagedWriteSet,
                token.Descriptor.NormalizedFootprint.DestinationRanges,
                out AcceleratorMemoryRange coverageFaultRange,
                out string coverageMessage))
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.StagedCoverageMismatch,
                coverageMessage,
                coverageFaultRange.Address,
                isWrite: true,
                guardDecision: guardDecision);
            return false;
        }

        return ApplyAllOrNoneAfterValidation(
            token,
            descriptor,
            mainMemory,
            stagedWriteSet,
            currentGuardEvidence!,
            out rollbackRecord,
            out fault,
            out bytesCommitted);
    }

    private bool ApplyAllOrNoneAfterValidation(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        Processor.MainMemoryArea mainMemory,
        AcceleratorStagedWriteSet stagedWriteSet,
        AcceleratorGuardEvidence currentGuardEvidence,
        out AcceleratorRollbackRecord rollbackRecord,
        out AcceleratorCommitFault? fault,
        out ulong bytesCommitted)
    {
        rollbackRecord = AcceleratorRollbackRecord.None;
        fault = null;
        bytesCommitted = 0;

        IReadOnlyList<AcceleratorStagedWrite> writes = stagedWriteSet.Writes;
        var backups = new List<AcceleratorStagedWrite>(writes.Count);
        for (int index = 0; index < writes.Count; index++)
        {
            AcceleratorStagedWrite stagedWrite = writes[index];
            if (stagedWrite.Length > int.MaxValue ||
                !HasExactMainMemoryRange(
                    mainMemory,
                    stagedWrite.Address,
                    checked((int)stagedWrite.Length)))
            {
                fault = new AcceleratorCommitFault(
                    AcceleratorTokenFaultCode.CommitMemoryFault,
                    $"L7-SDC commit reached out-of-range destination 0x{stagedWrite.Address:X} covering {stagedWrite.Length} byte(s).",
                    stagedWrite.Address,
                    isWrite: true);
                return false;
            }

            byte[] backup = new byte[(int)stagedWrite.Length];
            if (!mainMemory.TryReadPhysicalRange(stagedWrite.Address, backup))
            {
                fault = new AcceleratorCommitFault(
                    AcceleratorTokenFaultCode.CommitMemoryFault,
                    $"L7-SDC commit could not snapshot destination 0x{stagedWrite.Address:X} before all-or-none publication.",
                    stagedWrite.Address,
                    isWrite: true);
                return false;
            }

            backups.Add(new AcceleratorStagedWrite(
                stagedWrite.TokenHandle,
                stagedWrite.Address,
                backup));
        }

        rollbackRecord = AcceleratorRollbackRecord.SnapshotOnly(writes, backups);
        for (int index = 0; index < writes.Count; index++)
        {
            AcceleratorStagedWrite stagedWrite = writes[index];
            if (mainMemory.TryWritePhysicalRange(stagedWrite.Address, stagedWrite.Data.Span))
            {
                bytesCommitted += stagedWrite.Length;
                continue;
            }

            rollbackRecord = Rollback(
                token,
                descriptor,
                mainMemory,
                backups,
                writes,
                index,
                currentGuardEvidence);
            fault = new AcceleratorCommitFault(
                rollbackRecord.RollbackSucceeded
                    ? AcceleratorTokenFaultCode.CommitMemoryFault
                    : AcceleratorTokenFaultCode.RollbackFailed,
                rollbackRecord.RollbackSucceeded
                    ? $"L7-SDC commit failed to write destination 0x{stagedWrite.Address:X}; staged writes were rolled back and not reported as visible success."
                    : $"L7-SDC commit failed to write destination 0x{stagedWrite.Address:X} and rollback failed.",
                stagedWrite.Address,
                isWrite: true);
            bytesCommitted = 0;
            return false;
        }

        return true;
    }

    public AcceleratorRollbackRecord Rollback(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        Processor.MainMemoryArea mainMemory,
        IReadOnlyList<AcceleratorStagedWrite> backups,
        IReadOnlyList<AcceleratorStagedWrite> attemptedWrites,
        int writeFailureIndex,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(mainMemory);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(attemptedWrites);

        if (token.State != AcceleratorTokenState.CommitPending)
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                $"L7-SDC rollback requires CommitPending token state, but token is {token.State}; rollback side effects were rejected.");
        }

        AcceleratorGuardDecision guardDecision =
            ValidateOwnerDomainAndMapping(
                token,
                descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                "L7-SDC rollback requires fresh owner/domain and mapping/IOMMU epoch authority; rollback side effects were rejected. " +
                guardDecision.Message);
        }

        if (!IsDescriptorIdentityBound(token, descriptor))
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                "L7-SDC rollback descriptor identity hash does not match the token-bound descriptor identity; rollback side effects were rejected.");
        }

        if (!IsNormalizedFootprintBound(token, descriptor))
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                "L7-SDC rollback normalized footprint hash does not match the token-bound footprint identity; rollback side effects were rejected.");
        }

        if (!IsRollbackFailureIndexValid(writeFailureIndex, backups.Count))
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                "L7-SDC rollback failure index is outside the token-bound rollback evidence; rollback side effects were rejected.");
        }

        if (!ValidateRollbackWriteEvidence(
                token,
                backups,
                guardDecision,
                "backup",
                out string backupMessage))
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                backupMessage);
        }

        if (!ValidateRollbackWriteEvidence(
                token,
                attemptedWrites,
                guardDecision,
                "attempted",
                out string attemptedMessage))
        {
            return AcceleratorRollbackRecord.Completed(
                attemptedWrites,
                backups,
                writeFailureIndex,
                rollbackSucceeded: false,
                attemptedMessage);
        }

        return RollbackAfterValidation(
            mainMemory,
            backups,
            attemptedWrites,
            writeFailureIndex);
    }

    private static AcceleratorRollbackRecord RollbackAfterValidation(
        Processor.MainMemoryArea mainMemory,
        IReadOnlyList<AcceleratorStagedWrite> backups,
        IReadOnlyList<AcceleratorStagedWrite> attemptedWrites,
        int writeFailureIndex)
    {
        int start = Math.Min(writeFailureIndex, backups.Count - 1);
        for (int index = start; index >= 0; index--)
        {
            AcceleratorStagedWrite backup = backups[index];
            if (!mainMemory.TryWritePhysicalRange(backup.Address, backup.Data.Span))
            {
                return AcceleratorRollbackRecord.Completed(
                    attemptedWrites,
                    backups,
                    writeFailureIndex,
                    rollbackSucceeded: false,
                    $"L7-SDC rollback failed while restoring destination 0x{backup.Address:X}.");
            }
        }

        return AcceleratorRollbackRecord.Completed(
            attemptedWrites,
            backups,
            writeFailureIndex,
            rollbackSucceeded: true,
            "L7-SDC all-or-none rollback restored every destination snapshot.");
    }

    public bool InvalidateSrfAndCache(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorStagedWriteSet stagedWriteSet,
        AcceleratorCommitInvalidationPlan invalidationPlan,
        AcceleratorGuardEvidence? currentGuardEvidence,
        out AcceleratorCommitInvalidationPlan invalidated,
        out AcceleratorCommitFault? fault)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(stagedWriteSet);
        invalidationPlan ??= AcceleratorCommitInvalidationPlan.None;
        invalidated = invalidationPlan;
        fault = null;

        if (token.State != AcceleratorTokenState.CommitPending)
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.IllegalTransition,
                $"L7-SDC SRF/cache invalidation requires CommitPending token state, but token is {token.State}.");
            return false;
        }

        AcceleratorGuardDecision guardDecision =
            ValidateOwnerDomainAndMapping(
                token,
                descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "L7-SDC SRF/cache invalidation requires fresh owner/domain and mapping/IOMMU epoch authority. " +
                guardDecision.Message,
                guardDecision: guardDecision);
            return false;
        }

        if (!ValidateDescriptorAndFootprintBinding(
                token,
                descriptor,
                guardDecision,
                out fault))
        {
            return false;
        }

        if (!ValidateExactCoverage(
                stagedWriteSet,
                token.Descriptor.NormalizedFootprint.DestinationRanges,
                out AcceleratorMemoryRange coverageFaultRange,
                out string coverageMessage))
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.StagedCoverageMismatch,
                coverageMessage,
                coverageFaultRange.Address,
                isWrite: true,
                guardDecision: guardDecision);
            return false;
        }

        invalidated = InvalidateSrfAndCacheAfterValidation(
            stagedWriteSet,
            invalidationPlan);
        return true;
    }

    private static AcceleratorCommitInvalidationPlan InvalidateSrfAndCacheAfterValidation(
        AcceleratorStagedWriteSet stagedWriteSet,
        AcceleratorCommitInvalidationPlan invalidationPlan)
    {
        var records = new List<AcceleratorCommitInvalidationRecord>();
        for (int writeIndex = 0; writeIndex < stagedWriteSet.Writes.Count; writeIndex++)
        {
            AcceleratorStagedWrite write = stagedWriteSet.Writes[writeIndex];
            AcceleratorMemoryRange committedRange =
                new(write.Address, write.Length);

            if (write.Length <= uint.MaxValue)
            {
                invalidationPlan.StreamRegisterFile?.InvalidateOverlappingRange(
                    write.Address,
                    (uint)write.Length);
            }

            RecordInvalidations(
                AcceleratorCommitInvalidationTarget.SrfWindow,
                invalidationPlan.SrfWindows,
                committedRange,
                records);
            RecordInvalidations(
                AcceleratorCommitInvalidationTarget.CacheWindow,
                invalidationPlan.CacheWindows,
                committedRange,
                records);
        }

        return invalidationPlan.WithRecords(records);
    }

    private static bool ValidateDescriptorAndFootprintBinding(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardDecision guardDecision,
        out AcceleratorCommitFault? fault)
    {
        if (!IsDescriptorIdentityBound(token, descriptor))
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.DescriptorIdentityMismatch,
                "L7-SDC side-effect helper descriptor identity hash does not match the token-bound descriptor identity.",
                descriptor.DescriptorReference.DescriptorAddress,
                isWrite: false,
                guardDecision: guardDecision);
            return false;
        }

        if (!IsNormalizedFootprintBound(token, descriptor))
        {
            fault = new AcceleratorCommitFault(
                AcceleratorTokenFaultCode.NormalizedFootprintMismatch,
                "L7-SDC side-effect helper normalized footprint hash does not match the token-bound footprint identity.",
                descriptor.DescriptorReference.DescriptorAddress,
                isWrite: false,
                guardDecision: guardDecision);
            return false;
        }

        fault = null;
        return true;
    }

    private bool ValidateRollbackWriteEvidence(
        AcceleratorToken token,
        IReadOnlyList<AcceleratorStagedWrite> writes,
        AcceleratorGuardDecision guardDecision,
        string evidenceName,
        out string message)
    {
        var writeSet = new AcceleratorStagedWriteSet(writes);
        if (!writeSet.IsTokenBound(token.Handle, out string tokenBindingMessage))
        {
            message =
                $"L7-SDC rollback {evidenceName} write evidence is not token-bound; {tokenBindingMessage} Rollback side effects were rejected.";
            return false;
        }

        if (!ValidateExactCoverage(
                writeSet,
                token.Descriptor.NormalizedFootprint.DestinationRanges,
                out AcceleratorMemoryRange coverageFaultRange,
                out string coverageMessage))
        {
            message =
                $"L7-SDC rollback {evidenceName} write evidence does not exactly cover the token-bound destination footprint at 0x{coverageFaultRange.Address:X}; {coverageMessage} Rollback side effects were rejected.";
            return false;
        }

        message =
            $"L7-SDC rollback {evidenceName} write evidence is token-bound and exact for {guardDecision.Surface}.";
        return true;
    }

    private static bool IsRollbackFailureIndexValid(
        int writeFailureIndex,
        int backupCount)
    {
        if (backupCount == 0)
        {
            return writeFailureIndex == -1;
        }

        return writeFailureIndex >= 0 && writeFailureIndex < backupCount;
    }

    private static AcceleratorCommitResult FaultToken(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        string message,
        AcceleratorGuardEvidence currentGuardEvidence,
        AcceleratorGuardDecision guardDecision,
        ulong faultAddress,
        bool isWrite)
    {
        AcceleratorTokenTransition fault =
            token.MarkFaulted(
                faultCode,
                currentGuardEvidence);
        AcceleratorTokenFaultCode visibleFault = fault.Succeeded
            ? faultCode
            : fault.FaultCode;
        string visibleMessage = fault.Succeeded
            ? message
            : fault.Message;
        return AcceleratorCommitResult.Rejected(
            token,
            new AcceleratorCommitFault(
                visibleFault,
                visibleMessage,
                faultAddress,
                isWrite,
                guardDecision));
    }

    private static void ReleaseTerminalReservationIfNeeded(
        ExternalAcceleratorConflictManager? conflictManager,
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (conflictManager is null || !token.IsTerminal)
        {
            return;
        }

        conflictManager.ReleaseTokenFootprint(
            token,
            currentGuardEvidence);
    }

    private static bool IsDescriptorIdentityBound(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor)
    {
        return token.Descriptor.Identity.DescriptorIdentityHash ==
                   descriptor.Identity.DescriptorIdentityHash &&
               token.Descriptor.DescriptorReference.DescriptorIdentityHash ==
                   descriptor.DescriptorReference.DescriptorIdentityHash;
    }

    private static bool IsNormalizedFootprintBound(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor)
    {
        return token.Descriptor.Identity.NormalizedFootprintHash ==
                   descriptor.Identity.NormalizedFootprintHash &&
               token.Descriptor.NormalizedFootprint.Hash ==
                   descriptor.NormalizedFootprint.Hash;
    }

    private static void RecordInvalidations(
        AcceleratorCommitInvalidationTarget target,
        IReadOnlyList<AcceleratorMemoryRange> windows,
        AcceleratorMemoryRange committedRange,
        List<AcceleratorCommitInvalidationRecord> records)
    {
        for (int index = 0; index < windows.Count; index++)
        {
            AcceleratorMemoryRange window = windows[index];
            bool overlapped = RangesOverlap(committedRange, window);
            records.Add(new AcceleratorCommitInvalidationRecord(
                target,
                window,
                committedRange,
                overlapped,
                overlapped));
        }
    }

    private static bool RangesOverlap(
        AcceleratorMemoryRange left,
        AcceleratorMemoryRange right)
    {
        if (left.Length == 0 ||
            right.Length == 0 ||
            left.Address > ulong.MaxValue - left.Length ||
            right.Address > ulong.MaxValue - right.Length)
        {
            return false;
        }

        ulong leftEnd = left.Address + left.Length;
        ulong rightEnd = right.Address + right.Length;
        return left.Address < rightEnd && right.Address < leftEnd;
    }

    private static bool HasExactMainMemoryRange(
        Processor.MainMemoryArea mainMemory,
        ulong address,
        int size)
    {
        if (size <= 0)
        {
            return false;
        }

        ulong memoryLength = (ulong)mainMemory.Length;
        ulong requestSize = (ulong)size;
        return requestSize <= memoryLength &&
               address <= memoryLength - requestSize;
    }
}
