using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public sealed class AcceleratorToken
{
    internal AcceleratorToken(
        ulong tokenId,
        AcceleratorTokenHandle handle,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance,
        AcceleratorGuardDecision submitGuardDecision,
        AcceleratorTelemetry? telemetry = null)
    {
        if (tokenId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenId),
                "L7-SDC token id zero is reserved.");
        }

        if (!handle.IsValid)
        {
            throw new ArgumentException(
                "L7-SDC token handle zero is invalid.",
                nameof(handle));
        }

        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(capabilityAcceptance);
        if (!submitGuardDecision.IsAllowed)
        {
            throw new ArgumentException(
                "L7-SDC token creation requires accepted submit guard authority.",
                nameof(submitGuardDecision));
        }

        TokenId = tokenId;
        Handle = handle;
        Descriptor = descriptor;
        CapabilityAcceptance = capabilityAcceptance;
        SubmitGuardDecision = submitGuardDecision;
        Telemetry = telemetry;
        State = AcceleratorTokenState.Created;
        FaultCode = AcceleratorTokenFaultCode.None;
        StatusSequence = 1;
    }

    public ulong TokenId { get; }

    public AcceleratorTokenHandle Handle { get; }

    public AcceleratorCommandDescriptor Descriptor { get; }

    public AcceleratorCapabilityAcceptanceResult CapabilityAcceptance { get; }

    public AcceleratorGuardDecision SubmitGuardDecision { get; }

    internal AcceleratorTelemetry? Telemetry { get; }

    public AcceleratorTokenState State { get; private set; }

    public AcceleratorTokenFaultCode FaultCode { get; private set; }

    public uint StatusSequence { get; private set; }

    public AcceleratorMappingEpoch MappingEpoch => SubmitGuardDecision.MappingEpoch;

    public AcceleratorIommuDomainEpoch IommuDomainEpoch => SubmitGuardDecision.IommuDomainEpoch;

    public bool IsTerminal =>
        State is AcceleratorTokenState.Committed
            or AcceleratorTokenState.Faulted
            or AcceleratorTokenState.Canceled
            or AcceleratorTokenState.TimedOut
            or AcceleratorTokenState.Abandoned;

    public bool HasBackendExecution => false;

    public bool HasQueueExecution => false;

    public bool HasStagedWrites => false;

    public bool HasArchitecturalCommit => State == AcceleratorTokenState.Committed;

    public bool UserVisiblePublicationAllowed => State == AcceleratorTokenState.Committed;

    public AcceleratorTokenStatusWord GetStatusWord() =>
        AcceleratorTokenStatusWord.FromToken(this);

    public AcceleratorTokenTransition MarkValidated(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.Validated,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.Created,
            "ACCEL_SUBMIT token validated as model-only Phase 06 state.");
    }

    public AcceleratorTokenTransition MarkQueued(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.Queued,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.Validated,
            "L7-SDC token entered guarded Phase 07 model Queued state; no architectural publication authority is granted.");
    }

    public AcceleratorTokenTransition MarkRunning(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.Running,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.Queued,
            "L7-SDC token entered guarded Phase 07 model Running state; backend execution remains non-publishing.");
    }

    public AcceleratorTokenTransition MarkDeviceComplete(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.DeviceComplete,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.Running,
            "L7-SDC token entered model DeviceComplete state; no staged writes are published.");
    }

    public AcceleratorTokenTransition MarkCommitPending(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        if (IsTerminal)
        {
            return AcceleratorTokenTransition.Reject(
                State,
                AcceleratorTokenState.CommitPending,
                AcceleratorTokenFaultCode.TerminalState,
                $"L7-SDC token {Handle} is terminal ({State}) and cannot transition to CommitPending.");
        }

        AcceleratorGuardDecision guardDecision = ValidateCurrentGuard(currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RejectGuardedTransition(
                AcceleratorTokenState.CommitPending,
                guardDecision,
                "L7-SDC token CommitPending promotion requires a fresh owner/domain and epoch guard.");
        }

        if (State != AcceleratorTokenState.DeviceComplete)
        {
            return AcceleratorTokenTransition.Reject(
                State,
                AcceleratorTokenState.CommitPending,
                AcceleratorTokenFaultCode.IllegalTransition,
                $"Illegal L7-SDC token transition {State} -> CommitPending.");
        }

        return AcceleratorTokenTransition.Reject(
            State,
            AcceleratorTokenState.CommitPending,
            AcceleratorTokenFaultCode.CommitCoordinatorRequired,
            "L7-SDC DeviceComplete -> CommitPending promotion is owned by the Phase 08 commit coordinator; token identity cannot promote itself.");
    }

    public AcceleratorTokenTransition MarkCommitted(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        if (IsTerminal)
        {
            return AcceleratorTokenTransition.Reject(
                State,
                AcceleratorTokenState.Committed,
                AcceleratorTokenFaultCode.TerminalState,
                $"L7-SDC token {Handle} is terminal ({State}) and cannot transition to Committed.");
        }

        AcceleratorGuardDecision guardDecision = ValidateCurrentGuard(currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RejectGuardedTransition(
                AcceleratorTokenState.Committed,
                guardDecision,
                "L7-SDC token commit requires a fresh owner/domain and epoch guard.");
        }

        if (State == AcceleratorTokenState.DeviceComplete)
        {
            return AcceleratorTokenTransition.Reject(
                State,
                AcceleratorTokenState.Committed,
                AcceleratorTokenFaultCode.CommitCoordinatorRequired,
                "L7-SDC DeviceComplete tokens must enter CommitPending through the Phase 08 commit coordinator before architectural commit.");
        }

        if (State != AcceleratorTokenState.CommitPending)
        {
            return AcceleratorTokenTransition.Reject(
                State,
                AcceleratorTokenState.Committed,
                AcceleratorTokenFaultCode.IllegalTransition,
                $"Illegal L7-SDC token transition {State} -> Committed.");
        }

        return AcceleratorTokenTransition.Reject(
            State,
            AcceleratorTokenState.Committed,
            AcceleratorTokenFaultCode.CommitNotImplemented,
            "Phase 06 has no staged-write or architectural commit contour; token identity cannot commit.");
    }

    internal AcceleratorTokenTransition MarkCommitPendingFromCommitCoordinator(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.CommitPending,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.DeviceComplete,
            "L7-SDC token entered CommitPending through the guarded Phase 08 commit coordinator.");
    }

    internal AcceleratorTokenTransition MarkCommittedFromCommitCoordinator(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.Committed,
            currentGuardEvidence,
            static state => state == AcceleratorTokenState.CommitPending,
            "L7-SDC staged writes were published architecturally by the guarded Phase 08 commit coordinator.");
    }

    public AcceleratorTokenTransition MarkFaulted(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Faulted token transition requires a non-None fault code.",
                nameof(faultCode));
        }

        return TryTerminalTransition(
            AcceleratorTokenState.Faulted,
            faultCode,
            currentGuardEvidence,
            "L7-SDC token marked Faulted after guard-backed model transition.");
    }

    public AcceleratorTokenTransition MarkCanceled(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTransition(
            AcceleratorTokenState.Canceled,
            currentGuardEvidence,
            static state => state is
                AcceleratorTokenState.Created
                or AcceleratorTokenState.Validated
                or AcceleratorTokenState.Queued
                or AcceleratorTokenState.Running,
            "L7-SDC token canceled in Phase 06 model; no backend cancel path is invoked.");
    }

    public AcceleratorTokenTransition MarkTimedOut(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTerminalTransition(
            AcceleratorTokenState.TimedOut,
            AcceleratorTokenFaultCode.TimedOut,
            currentGuardEvidence,
            "L7-SDC token timed out in Phase 06 model; no backend wait path is invoked.");
    }

    public AcceleratorTokenTransition MarkAbandoned(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return TryTerminalTransition(
            AcceleratorTokenState.Abandoned,
            AcceleratorTokenFaultCode.OwnerDomainRejected,
            currentGuardEvidence,
            "L7-SDC token abandoned; user-visible publication remains forbidden.");
    }

    private AcceleratorTokenTransition TryTerminalTransition(
        AcceleratorTokenState targetState,
        AcceleratorTokenFaultCode terminalFaultCode,
        AcceleratorGuardEvidence currentGuardEvidence,
        string message)
    {
        return TryTransition(
            targetState,
            currentGuardEvidence,
            static state => state is not AcceleratorTokenState.Committed
                and not AcceleratorTokenState.Faulted
                and not AcceleratorTokenState.Canceled
                and not AcceleratorTokenState.TimedOut
                and not AcceleratorTokenState.Abandoned,
            message,
            terminalFaultCode);
    }

    private AcceleratorTokenTransition TryTransition(
        AcceleratorTokenState targetState,
        AcceleratorGuardEvidence currentGuardEvidence,
        Func<AcceleratorTokenState, bool> isLegalSource,
        string successMessage,
        AcceleratorTokenFaultCode targetFaultCode = AcceleratorTokenFaultCode.None)
    {
        ArgumentNullException.ThrowIfNull(isLegalSource);

        AcceleratorTokenState originalState = State;
        if (IsTerminal)
        {
            return AcceleratorTokenTransition.Reject(
                originalState,
                targetState,
                AcceleratorTokenFaultCode.TerminalState,
                $"L7-SDC token {Handle} is terminal ({State}) and cannot transition to {targetState}.");
        }

        AcceleratorGuardDecision guardDecision = ValidateCurrentGuard(currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RejectGuardedTransition(targetState, guardDecision, successMessage);
        }

        if (!isLegalSource(originalState))
        {
            return AcceleratorTokenTransition.Reject(
                originalState,
                targetState,
                AcceleratorTokenFaultCode.IllegalTransition,
                $"Illegal L7-SDC token transition {originalState} -> {targetState}.");
        }

        State = targetState;
        FaultCode = targetFaultCode;
        AdvanceStatusSequence();
        Telemetry?.RecordTokenTransition(
            this,
            originalState,
            targetState,
            targetFaultCode,
            successMessage);
        return AcceleratorTokenTransition.Success(
            originalState,
            targetState,
            successMessage);
    }

    private AcceleratorTokenTransition RejectGuardedTransition(
        AcceleratorTokenState targetState,
        AcceleratorGuardDecision guardDecision,
        string surfaceMessage)
    {
        Telemetry?.RecordGuardReject(
            guardDecision,
            $"{surfaceMessage} {guardDecision.Message}");
        return AcceleratorTokenTransition.Reject(
            State,
            targetState,
            AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
            $"{surfaceMessage} {guardDecision.Message}");
    }

    private AcceleratorGuardDecision ValidateCurrentGuard(
        AcceleratorGuardEvidence currentGuardEvidence)
    {
        return AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
            SubmitGuardDecision,
            currentGuardEvidence);
    }

    private void AdvanceStatusSequence()
    {
        unchecked
        {
            StatusSequence++;
            if (StatusSequence == 0)
            {
                StatusSequence = 1;
            }
        }
    }
}
