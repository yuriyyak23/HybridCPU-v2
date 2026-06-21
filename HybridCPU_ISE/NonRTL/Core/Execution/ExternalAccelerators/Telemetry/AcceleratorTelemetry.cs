using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

public enum AcceleratorEvidenceKind : byte
{
    CapabilityQuery = 0,
    DescriptorParse = 1,
    SubmitAdmission = 2,
    TokenTransition = 3,
    DeviceBusyReject = 4,
    QueueFullReject = 5,
    DomainReject = 6,
    OwnerDriftReject = 7,
    MappingEpochDriftReject = 8,
    IommuDomainEpochDriftReject = 9,
    FootprintConflictReject = 10,
    DirectWriteViolationReject = 11,
    CommitRollback = 12,
    BytesRead = 13,
    BytesStaged = 14,
    BytesCommitted = 15,
    Operation = 16,
    SrfInvalidation = 17,
    CacheInvalidation = 18,
    DmaStreamComputeConflictReject = 19,
    Lane7SubmitPollThrottleReject = 20,
    BackendReject = 21,
    CommitReject = 22
}

public sealed record AcceleratorEvidenceRecord
{
    public required ulong Sequence { get; init; }

    public required AcceleratorEvidenceKind Kind { get; init; }

    public bool Accepted { get; init; }

    public AcceleratorDescriptorFault DescriptorFault { get; init; }

    public AcceleratorTokenFaultCode TokenFaultCode { get; init; }

    public AcceleratorGuardFault GuardFault { get; init; }

    public AcceleratorConflictClass ConflictClass { get; init; }

    public AcceleratorTokenState? FromState { get; init; }

    public AcceleratorTokenState? ToState { get; init; }

    public ulong TokenId { get; init; }

    public ulong TokenHandle { get; init; }

    public ulong ByteCount { get; init; }

    public ulong OperationCount { get; init; }

    public ulong LatencyCycles { get; init; }

    public string? Message { get; init; }
}

public sealed record AcceleratorRejectCounters
{
    public long CapabilityQueryRejects { get; init; }

    public long DescriptorParseRejects { get; init; }

    public long SubmitRejects { get; init; }

    public long BackendRejects { get; init; }

    public long CommitRejects { get; init; }

    public long DeviceBusyRejects { get; init; }

    public long QueueFullRejects { get; init; }

    public long DomainRejects { get; init; }

    public long OwnerDriftRejects { get; init; }

    public long MappingEpochDriftRejects { get; init; }

    public long IommuDomainEpochDriftRejects { get; init; }

    public long FootprintConflictRejects { get; init; }

    public long DirectWriteViolationRejects { get; init; }

    public long Lane7SubmitPollThrottleRejects { get; init; }
}

public sealed record AcceleratorLifecycleCounters
{
    public long Created { get; init; }

    public long Validated { get; init; }

    public long Queued { get; init; }

    public long Running { get; init; }

    public long DeviceCompleted { get; init; }

    public long CommitPending { get; init; }

    public long Committed { get; init; }

    public long Faulted { get; init; }

    public long Canceled { get; init; }

    public long TimedOut { get; init; }

    public long Abandoned { get; init; }
}

public sealed record AcceleratorByteCounters
{
    public ulong BytesRead { get; init; }

    public ulong BytesStaged { get; init; }

    public ulong BytesCommitted { get; init; }
}

public sealed record AcceleratorConflictCounters
{
    public long FootprintConflictRejects { get; init; }

    public long DmaStreamComputeConflictRejects { get; init; }

    public long SrfAssistConflictRejects { get; init; }

    public long DirectWriteViolationRejects { get; init; }

    public long CommitRollbackCount { get; init; }

    public long SrfInvalidations { get; init; }

    public long CacheInvalidations { get; init; }
}

public sealed record AcceleratorTelemetrySnapshot
{
    public long CapabilityQueryAttempts { get; init; }

    public long CapabilityQuerySuccess { get; init; }

    public long CapabilityQueryReject { get; init; }

    public long DescriptorParseAttempts { get; init; }

    public long DescriptorAccepted { get; init; }

    public long DescriptorRejected { get; init; }

    public long SubmitAttempts { get; init; }

    public long SubmitAccepted { get; init; }

    public long SubmitRejected { get; init; }

    public AcceleratorRejectCounters Rejects { get; init; } = new();

    public AcceleratorLifecycleCounters Lifecycle { get; init; } = new();

    public AcceleratorByteCounters Bytes { get; init; } = new();

    public AcceleratorConflictCounters Conflicts { get; init; } = new();

    public ulong OperationCount { get; init; }

    public ulong LatencyCycles { get; init; }

    public IReadOnlyList<AcceleratorEvidenceRecord> EvidenceRecords { get; init; } =
        Array.Empty<AcceleratorEvidenceRecord>();
}

public sealed class AcceleratorTelemetry
{
    private readonly List<AcceleratorEvidenceRecord> _evidenceRecords = new();
    private ulong _nextSequence = 1;

    private long _capabilityQueryAttempts;
    private long _capabilityQuerySuccess;
    private long _capabilityQueryReject;
    private long _descriptorParseAttempts;
    private long _descriptorAccepted;
    private long _descriptorRejected;
    private long _submitAttempts;
    private long _submitAccepted;
    private long _submitRejected;
    private long _backendRejects;
    private long _commitRejects;
    private long _deviceBusyRejects;
    private long _queueFullRejects;
    private long _domainRejects;
    private long _ownerDriftRejects;
    private long _mappingEpochDriftRejects;
    private long _iommuDomainEpochDriftRejects;
    private long _footprintConflictRejects;
    private long _directWriteViolationRejects;
    private long _lane7SubmitPollThrottleRejects;
    private long _created;
    private long _validated;
    private long _queued;
    private long _running;
    private long _deviceCompleted;
    private long _commitPending;
    private long _committed;
    private long _faulted;
    private long _canceled;
    private long _timedOut;
    private long _abandoned;
    private ulong _bytesRead;
    private ulong _bytesStaged;
    private ulong _bytesCommitted;
    private long _dmaStreamComputeConflictRejects;
    private long _srfAssistConflictRejects;
    private long _commitRollbackCount;
    private long _srfInvalidations;
    private long _cacheInvalidations;
    private ulong _operationCount;
    private ulong _latencyCycles;

    public AcceleratorTelemetrySnapshot Snapshot()
    {
        return new AcceleratorTelemetrySnapshot
        {
            CapabilityQueryAttempts = _capabilityQueryAttempts,
            CapabilityQuerySuccess = _capabilityQuerySuccess,
            CapabilityQueryReject = _capabilityQueryReject,
            DescriptorParseAttempts = _descriptorParseAttempts,
            DescriptorAccepted = _descriptorAccepted,
            DescriptorRejected = _descriptorRejected,
            SubmitAttempts = _submitAttempts,
            SubmitAccepted = _submitAccepted,
            SubmitRejected = _submitRejected,
            Rejects = new AcceleratorRejectCounters
            {
                CapabilityQueryRejects = _capabilityQueryReject,
                DescriptorParseRejects = _descriptorRejected,
                SubmitRejects = _submitRejected,
                BackendRejects = _backendRejects,
                CommitRejects = _commitRejects,
                DeviceBusyRejects = _deviceBusyRejects,
                QueueFullRejects = _queueFullRejects,
                DomainRejects = _domainRejects,
                OwnerDriftRejects = _ownerDriftRejects,
                MappingEpochDriftRejects = _mappingEpochDriftRejects,
                IommuDomainEpochDriftRejects = _iommuDomainEpochDriftRejects,
                FootprintConflictRejects = _footprintConflictRejects,
                DirectWriteViolationRejects = _directWriteViolationRejects,
                Lane7SubmitPollThrottleRejects = _lane7SubmitPollThrottleRejects
            },
            Lifecycle = new AcceleratorLifecycleCounters
            {
                Created = _created,
                Validated = _validated,
                Queued = _queued,
                Running = _running,
                DeviceCompleted = _deviceCompleted,
                CommitPending = _commitPending,
                Committed = _committed,
                Faulted = _faulted,
                Canceled = _canceled,
                TimedOut = _timedOut,
                Abandoned = _abandoned
            },
            Bytes = new AcceleratorByteCounters
            {
                BytesRead = _bytesRead,
                BytesStaged = _bytesStaged,
                BytesCommitted = _bytesCommitted
            },
            Conflicts = new AcceleratorConflictCounters
            {
                FootprintConflictRejects = _footprintConflictRejects,
                DmaStreamComputeConflictRejects = _dmaStreamComputeConflictRejects,
                SrfAssistConflictRejects = _srfAssistConflictRejects,
                DirectWriteViolationRejects = _directWriteViolationRejects,
                CommitRollbackCount = _commitRollbackCount,
                SrfInvalidations = _srfInvalidations,
                CacheInvalidations = _cacheInvalidations
            },
            OperationCount = _operationCount,
            LatencyCycles = _latencyCycles,
            EvidenceRecords = Array.AsReadOnly(_evidenceRecords.ToArray())
        };
    }

    public void RecordCapabilityQuery(bool accepted, string? message = null)
    {
        _capabilityQueryAttempts++;
        if (accepted)
        {
            _capabilityQuerySuccess++;
        }
        else
        {
            _capabilityQueryReject++;
        }

        AddEvidence(AcceleratorEvidenceKind.CapabilityQuery, accepted, message: message);
    }

    public void RecordDescriptorParse(
        bool accepted,
        AcceleratorDescriptorFault fault = AcceleratorDescriptorFault.None,
        string? message = null)
    {
        _descriptorParseAttempts++;
        if (accepted)
        {
            _descriptorAccepted++;
        }
        else
        {
            _descriptorRejected++;
        }

        AddEvidence(
            AcceleratorEvidenceKind.DescriptorParse,
            accepted,
            descriptorFault: fault,
            message: message);
    }

    public void RecordSubmit(
        bool accepted,
        AcceleratorTokenFaultCode faultCode = AcceleratorTokenFaultCode.None,
        AcceleratorGuardDecision? guardDecision = null,
        string? message = null)
    {
        _submitAttempts++;
        if (accepted)
        {
            _submitAccepted++;
        }
        else
        {
            _submitRejected++;
            RecordRejectFault(faultCode, guardDecision);
        }

        AddEvidence(
            AcceleratorEvidenceKind.SubmitAdmission,
            accepted,
            tokenFaultCode: faultCode,
            guardFault: guardDecision?.Fault ?? AcceleratorGuardFault.None,
            message: message);
    }

    public void RecordTokenTransition(
        AcceleratorToken token,
        AcceleratorTokenState fromState,
        AcceleratorTokenState toState,
        AcceleratorTokenFaultCode faultCode = AcceleratorTokenFaultCode.None,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        IncrementLifecycle(toState);
        AddEvidence(
            AcceleratorEvidenceKind.TokenTransition,
            accepted: true,
            tokenFaultCode: faultCode,
            fromState: fromState,
            toState: toState,
            token: token,
            message: message);
    }

    public void RecordDeviceBusyReject(AcceleratorTokenFaultCode faultCode, string? message = null)
    {
        _deviceBusyRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.DeviceBusyReject,
            accepted: false,
            tokenFaultCode: faultCode,
            message: message);
    }

    public void RecordQueueFullReject(AcceleratorTokenFaultCode faultCode, string? message = null)
    {
        _queueFullRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.QueueFullReject,
            accepted: false,
            tokenFaultCode: faultCode,
            message: message);
    }

    public void RecordDomainReject(AcceleratorGuardDecision guardDecision, string? message = null)
    {
        _domainRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.DomainReject,
            accepted: false,
            guardFault: guardDecision.Fault,
            message: message ?? guardDecision.Message);
    }

    public void RecordOwnerDriftReject(AcceleratorGuardDecision guardDecision, string? message = null)
    {
        _ownerDriftRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.OwnerDriftReject,
            accepted: false,
            guardFault: guardDecision.Fault,
            message: message ?? guardDecision.Message);
    }

    public void RecordMappingEpochDriftReject(AcceleratorGuardDecision guardDecision, string? message = null)
    {
        _mappingEpochDriftRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.MappingEpochDriftReject,
            accepted: false,
            guardFault: guardDecision.Fault,
            message: message ?? guardDecision.Message);
    }

    public void RecordIommuDomainEpochDriftReject(AcceleratorGuardDecision guardDecision, string? message = null)
    {
        _iommuDomainEpochDriftRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.IommuDomainEpochDriftReject,
            accepted: false,
            guardFault: guardDecision.Fault,
            message: message ?? guardDecision.Message);
    }

    public void RecordGuardReject(AcceleratorGuardDecision? guardDecision, string? message = null)
    {
        if (guardDecision is null || guardDecision.Value.Fault == AcceleratorGuardFault.None)
        {
            return;
        }

        switch (guardDecision.Value.Fault)
        {
            case AcceleratorGuardFault.OwnerMismatch:
            case AcceleratorGuardFault.DescriptorOwnerBindingMismatch:
            case AcceleratorGuardFault.InvalidOwnerCompletion:
                RecordOwnerDriftReject(guardDecision.Value, message);
                break;
            case AcceleratorGuardFault.DomainMismatch:
                RecordDomainReject(guardDecision.Value, message);
                break;
            case AcceleratorGuardFault.MappingEpochDrift:
                RecordMappingEpochDriftReject(guardDecision.Value, message);
                break;
            case AcceleratorGuardFault.IommuDomainEpochDrift:
                RecordIommuDomainEpochDriftReject(guardDecision.Value, message);
                break;
        }
    }

    public void RecordFootprintConflictReject(
        AcceleratorConflictClass conflictClass,
        AcceleratorTokenFaultCode faultCode,
        string? message = null)
    {
        _footprintConflictRejects++;
        if (conflictClass == AcceleratorConflictClass.DmaStreamComputeOverlapsAcceleratorWrite)
        {
            _dmaStreamComputeConflictRejects++;
            AddEvidence(
                AcceleratorEvidenceKind.DmaStreamComputeConflictReject,
                accepted: false,
                tokenFaultCode: faultCode,
                conflictClass: conflictClass,
                message: message);
            return;
        }

        if (conflictClass is AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow
            or AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite)
        {
            _srfAssistConflictRejects++;
        }

        AddEvidence(
            AcceleratorEvidenceKind.FootprintConflictReject,
            accepted: false,
            tokenFaultCode: faultCode,
            conflictClass: conflictClass,
            message: message);
    }

    public void RecordDirectWriteViolationReject(
        AcceleratorToken? token = null,
        string? message = null)
    {
        _directWriteViolationRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.DirectWriteViolationReject,
            accepted: false,
            tokenFaultCode: AcceleratorTokenFaultCode.DirectWriteViolation,
            token: token,
            message: message);
    }

    public void RecordCommitRollback(bool attempted, bool succeeded, string? message = null)
    {
        if (!attempted)
        {
            return;
        }

        _commitRollbackCount++;
        AddEvidence(
            AcceleratorEvidenceKind.CommitRollback,
            accepted: succeeded,
            message: message);
    }

    public void RecordBytesRead(ulong byteCount, string? message = null)
    {
        if (byteCount == 0)
        {
            return;
        }

        _bytesRead += byteCount;
        AddEvidence(
            AcceleratorEvidenceKind.BytesRead,
            accepted: true,
            byteCount: byteCount,
            message: message);
    }

    public void RecordBytesStaged(ulong byteCount, string? message = null)
    {
        if (byteCount == 0)
        {
            return;
        }

        _bytesStaged += byteCount;
        AddEvidence(
            AcceleratorEvidenceKind.BytesStaged,
            accepted: true,
            byteCount: byteCount,
            message: message);
    }

    public void RecordBytesCommitted(ulong byteCount, string? message = null)
    {
        if (byteCount == 0)
        {
            return;
        }

        _bytesCommitted += byteCount;
        AddEvidence(
            AcceleratorEvidenceKind.BytesCommitted,
            accepted: true,
            byteCount: byteCount,
            message: message);
    }

    public void RecordOperation(ulong operationCount, ulong latencyCycles = 0, string? message = null)
    {
        if (operationCount == 0 && latencyCycles == 0)
        {
            return;
        }

        _operationCount += operationCount;
        _latencyCycles += latencyCycles;
        AddEvidence(
            AcceleratorEvidenceKind.Operation,
            accepted: true,
            operationCount: operationCount,
            latencyCycles: latencyCycles,
            message: message);
    }

    public void RecordSrfInvalidations(int count, string? message = null)
    {
        if (count <= 0)
        {
            return;
        }

        _srfInvalidations += count;
        AddEvidence(
            AcceleratorEvidenceKind.SrfInvalidation,
            accepted: true,
            operationCount: (ulong)count,
            message: message);
    }

    public void RecordCacheInvalidations(int count, string? message = null)
    {
        if (count <= 0)
        {
            return;
        }

        _cacheInvalidations += count;
        AddEvidence(
            AcceleratorEvidenceKind.CacheInvalidation,
            accepted: true,
            operationCount: (ulong)count,
            message: message);
    }

    public void RecordLane7SubmitPollThrottleReject(string? message = null)
    {
        _lane7SubmitPollThrottleRejects++;
        AddEvidence(
            AcceleratorEvidenceKind.Lane7SubmitPollThrottleReject,
            accepted: false,
            tokenFaultCode: AcceleratorTokenFaultCode.Lane7PressureRejected,
            message: message);
    }

    public void RecordBackendReject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorToken? token = null,
        string? message = null)
    {
        _backendRejects++;
        RecordRejectFault(faultCode, guardDecision: null);
        AddEvidence(
            AcceleratorEvidenceKind.BackendReject,
            accepted: false,
            tokenFaultCode: faultCode,
            token: token,
            message: message);
    }

    public void RecordCommitReject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorToken? token = null,
        string? message = null)
    {
        _commitRejects++;
        RecordRejectFault(faultCode, guardDecision: null);
        AddEvidence(
            AcceleratorEvidenceKind.CommitReject,
            accepted: false,
            tokenFaultCode: faultCode,
            token: token,
            message: message);
    }

    private void RecordRejectFault(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision)
    {
        RecordGuardReject(guardDecision);
        if (faultCode == AcceleratorTokenFaultCode.DirectWriteViolation)
        {
            _directWriteViolationRejects++;
        }
    }

    private void IncrementLifecycle(AcceleratorTokenState state)
    {
        switch (state)
        {
            case AcceleratorTokenState.Created:
                _created++;
                break;
            case AcceleratorTokenState.Validated:
                _validated++;
                break;
            case AcceleratorTokenState.Queued:
                _queued++;
                break;
            case AcceleratorTokenState.Running:
                _running++;
                break;
            case AcceleratorTokenState.DeviceComplete:
                _deviceCompleted++;
                break;
            case AcceleratorTokenState.CommitPending:
                _commitPending++;
                break;
            case AcceleratorTokenState.Committed:
                _committed++;
                break;
            case AcceleratorTokenState.Faulted:
                _faulted++;
                break;
            case AcceleratorTokenState.Canceled:
                _canceled++;
                break;
            case AcceleratorTokenState.TimedOut:
                _timedOut++;
                break;
            case AcceleratorTokenState.Abandoned:
                _abandoned++;
                break;
        }
    }

    private void AddEvidence(
        AcceleratorEvidenceKind kind,
        bool accepted,
        AcceleratorDescriptorFault descriptorFault = AcceleratorDescriptorFault.None,
        AcceleratorTokenFaultCode tokenFaultCode = AcceleratorTokenFaultCode.None,
        AcceleratorGuardFault guardFault = AcceleratorGuardFault.None,
        AcceleratorConflictClass conflictClass = AcceleratorConflictClass.None,
        AcceleratorTokenState? fromState = null,
        AcceleratorTokenState? toState = null,
        AcceleratorToken? token = null,
        ulong byteCount = 0,
        ulong operationCount = 0,
        ulong latencyCycles = 0,
        string? message = null)
    {
        _evidenceRecords.Add(new AcceleratorEvidenceRecord
        {
            Sequence = AllocateSequence(),
            Kind = kind,
            Accepted = accepted,
            DescriptorFault = descriptorFault,
            TokenFaultCode = tokenFaultCode,
            GuardFault = guardFault,
            ConflictClass = conflictClass,
            FromState = fromState,
            ToState = toState,
            TokenId = token?.TokenId ?? 0,
            TokenHandle = token?.Handle.Value ?? 0,
            ByteCount = byteCount,
            OperationCount = operationCount,
            LatencyCycles = latencyCycles,
            Message = message
        });
    }

    private ulong AllocateSequence()
    {
        ulong sequence = _nextSequence++;
        if (_nextSequence == 0)
        {
            _nextSequence = 1;
        }

        return sequence;
    }
}
