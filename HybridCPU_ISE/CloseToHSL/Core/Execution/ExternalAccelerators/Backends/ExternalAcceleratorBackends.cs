using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

public interface IAcceleratorBackendClock
{
    ulong TickCount { get; }

    ulong Advance();
}

public interface IExternalAcceleratorBackend
{
    AcceleratorBackendResult TrySubmit(
        AcceleratorQueueAdmissionRequest request,
        IAcceleratorCommandQueue queue,
        AcceleratorGuardEvidence? currentGuardEvidence);

    AcceleratorBackendResult Tick(
        IAcceleratorCommandQueue queue,
        IAcceleratorMemoryPortal memoryPortal,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence);

    AcceleratorBackendResult TryCancel(
        AcceleratorTokenStore tokenStore,
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence);
}

public enum NullExternalAcceleratorBackendPolicy : byte
{
    RejectSubmit = 0,
    FaultToken = 1
}

public enum AcceleratorBackendResultKind : byte
{
    Submitted = 0,
    Rejected = 1,
    Faulted = 2,
    DeviceCompleted = 3,
    Canceled = 4
}

public sealed record AcceleratorBackendResult
{
    private AcceleratorBackendResult(
        AcceleratorBackendResultKind kind,
        AcceleratorToken? token,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorQueueAdmissionResult? queueResult,
        AcceleratorTokenLookupResult? tokenLookupResult,
        ulong bytesRead,
        ulong bytesStaged,
        int stagedWriteCount,
        bool directWriteViolationDetected,
        ulong backendTick,
        string message)
    {
        Kind = kind;
        Token = token;
        FaultCode = faultCode;
        QueueResult = queueResult;
        TokenLookupResult = tokenLookupResult;
        BytesRead = bytesRead;
        BytesStaged = bytesStaged;
        StagedWriteCount = stagedWriteCount;
        DirectWriteViolationDetected = directWriteViolationDetected;
        BackendTick = backendTick;
        Message = message;
    }

    public AcceleratorBackendResultKind Kind { get; }

    public bool IsAccepted => Kind is AcceleratorBackendResultKind.Submitted
        or AcceleratorBackendResultKind.DeviceCompleted
        or AcceleratorBackendResultKind.Canceled;

    public bool IsRejected => Kind == AcceleratorBackendResultKind.Rejected;

    public bool IsFaulted => Kind == AcceleratorBackendResultKind.Faulted;

    public AcceleratorToken? Token { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorQueueAdmissionResult? QueueResult { get; }

    public AcceleratorTokenLookupResult? TokenLookupResult { get; }

    public ulong BytesRead { get; }

    public ulong BytesStaged { get; }

    public int StagedWriteCount { get; }

    public bool DirectWriteViolationDetected { get; }

    public ulong BackendTick { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;

    public bool UserVisiblePublicationAllowed => false;

    public static AcceleratorBackendResult Submitted(
        AcceleratorQueueAdmissionResult queueResult,
        ulong backendTick)
    {
        ArgumentNullException.ThrowIfNull(queueResult);
        return new AcceleratorBackendResult(
            AcceleratorBackendResultKind.Submitted,
            queueResult.Command?.Token,
            AcceleratorTokenFaultCode.None,
            queueResult,
            tokenLookupResult: null,
            bytesRead: 0,
            bytesStaged: 0,
            stagedWriteCount: 0,
            directWriteViolationDetected: false,
            backendTick,
            queueResult.Message);
    }

    public static AcceleratorBackendResult DeviceCompleted(
        AcceleratorToken token,
        ulong bytesRead,
        ulong bytesStaged,
        int stagedWriteCount,
        ulong backendTick,
        string message)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new AcceleratorBackendResult(
            AcceleratorBackendResultKind.DeviceCompleted,
            token,
            AcceleratorTokenFaultCode.None,
            queueResult: null,
            tokenLookupResult: null,
            bytesRead,
            bytesStaged,
            stagedWriteCount,
            directWriteViolationDetected: false,
            backendTick,
            message);
    }

    public static AcceleratorBackendResult Canceled(
        AcceleratorTokenLookupResult lookupResult,
        ulong backendTick)
    {
        ArgumentNullException.ThrowIfNull(lookupResult);
        return new AcceleratorBackendResult(
            AcceleratorBackendResultKind.Canceled,
            lookupResult.Token,
            AcceleratorTokenFaultCode.None,
            queueResult: null,
            lookupResult,
            bytesRead: 0,
            bytesStaged: 0,
            stagedWriteCount: 0,
            directWriteViolationDetected: false,
            backendTick,
            lookupResult.Message);
    }

    public static AcceleratorBackendResult Rejected(
        AcceleratorTokenFaultCode faultCode,
        string message,
        ulong backendTick,
        AcceleratorToken? token = null,
        AcceleratorQueueAdmissionResult? queueResult = null,
        AcceleratorTokenLookupResult? tokenLookupResult = null)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC backend operations require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorBackendResult(
            AcceleratorBackendResultKind.Rejected,
            token,
            faultCode,
            queueResult,
            tokenLookupResult,
            bytesRead: 0,
            bytesStaged: 0,
            stagedWriteCount: 0,
            directWriteViolationDetected: false,
            backendTick,
            message);
    }

    public static AcceleratorBackendResult Faulted(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        string message,
        ulong backendTick,
        bool directWriteViolationDetected = false)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Faulted L7-SDC backend operations require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorBackendResult(
            AcceleratorBackendResultKind.Faulted,
            token,
            faultCode,
            queueResult: null,
            tokenLookupResult: null,
            bytesRead: 0,
            bytesStaged: 0,
            stagedWriteCount: 0,
            directWriteViolationDetected,
            backendTick,
            message);
    }
}

public sealed class ManualAcceleratorBackendClock : IAcceleratorBackendClock
{
    public ulong TickCount { get; private set; }

    public ulong Advance()
    {
        unchecked
        {
            TickCount++;
            if (TickCount == 0)
            {
                TickCount = 1;
            }
        }

        return TickCount;
    }
}

public sealed class NullExternalAcceleratorBackend : IExternalAcceleratorBackend
{
    private readonly IAcceleratorBackendClock _clock;
    private readonly NullExternalAcceleratorBackendPolicy _policy;
    private readonly AcceleratorTelemetry? _telemetry;
    private readonly ExternalAcceleratorFeatureSwitch _featureSwitch;

    public NullExternalAcceleratorBackend(
        NullExternalAcceleratorBackendPolicy policy = NullExternalAcceleratorBackendPolicy.RejectSubmit,
        IAcceleratorBackendClock? clock = null,
        AcceleratorTelemetry? telemetry = null,
        ExternalAcceleratorFeatureSwitch? featureSwitch = null)
    {
        _policy = policy;
        _clock = clock ?? new ManualAcceleratorBackendClock();
        _telemetry = telemetry;
        _featureSwitch = featureSwitch ?? ExternalAcceleratorFeatureSwitch.Enabled;
    }

    public AcceleratorBackendResult TrySubmit(
        AcceleratorQueueAdmissionRequest request,
        IAcceleratorCommandQueue queue,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(queue);
        ulong tick = _clock.Advance();

        if (!_featureSwitch.BackendExecutionEnabled)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                "L7-SDC backend execution is disabled by rollback feature switch; queue, staging, commit, and direct writes were not reached.",
                tick,
                request.TokenAdmission.Token));
        }

        if (_policy == NullExternalAcceleratorBackendPolicy.FaultToken &&
            request.TokenAdmission.IsAccepted &&
            request.TokenAdmission.Token is not null &&
            currentGuardEvidence is not null)
        {
            if (!TryValidateFaultPolicyRequest(
                    request,
                    currentGuardEvidence,
                    out AcceleratorToken? token,
                    out AcceleratorTokenFaultCode rejectFaultCode,
                    out string rejectMessage))
            {
                return RecordResult(AcceleratorBackendResult.Rejected(
                    rejectFaultCode,
                    rejectMessage,
                    tick,
                    request.TokenAdmission.Token));
            }

            AcceleratorTokenTransition fault =
                token!.MarkFaulted(
                    AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                    currentGuardEvidence);
            return RecordResult(fault.Succeeded
                ? AcceleratorBackendResult.Faulted(
                    token,
                    AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                    "Null L7-SDC backend faulted the token by explicit policy; no queue or memory write occurred.",
                    tick)
                : AcceleratorBackendResult.Rejected(
                    fault.FaultCode,
                    fault.Message,
                    tick,
                    token));
        }

        return RecordResult(AcceleratorBackendResult.Rejected(
            AcceleratorTokenFaultCode.BackendExecutionUnavailable,
            "Null L7-SDC backend deterministically rejects submit; no queue admission or memory write occurred.",
            tick,
            request.TokenAdmission.Token));
    }

    public AcceleratorBackendResult Tick(
        IAcceleratorCommandQueue queue,
        IAcceleratorMemoryPortal memoryPortal,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(memoryPortal);
        ArgumentNullException.ThrowIfNull(stagingBuffer);
        if (!_featureSwitch.BackendExecutionEnabled)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                "L7-SDC backend tick is disabled by rollback feature switch; no command execution or staged write occurred.",
                _clock.Advance()));
        }

        return RecordResult(AcceleratorBackendResult.Rejected(
            AcceleratorTokenFaultCode.BackendExecutionUnavailable,
            "Null L7-SDC backend has no execution engine and cannot tick commands.",
            _clock.Advance()));
    }

    public AcceleratorBackendResult TryCancel(
        AcceleratorTokenStore tokenStore,
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        AcceleratorTokenLookupResult cancel =
            tokenStore.Cancel(handle, currentGuardEvidence);
        ulong tick = _clock.Advance();
        return RecordResult(cancel.IsAllowed
            ? AcceleratorBackendResult.Canceled(cancel, tick)
            : AcceleratorBackendResult.Rejected(
                cancel.FaultCode,
                cancel.Message,
                tick,
                cancel.Token,
                tokenLookupResult: cancel));
    }

    private AcceleratorBackendResult RecordResult(AcceleratorBackendResult result)
    {
        if (_telemetry is not null && result.IsRejected)
        {
            _telemetry.RecordBackendReject(
                result.FaultCode,
                result.Token,
                result.Message);
        }

        return result;
    }

    private static bool TryValidateFaultPolicyRequest(
        AcceleratorQueueAdmissionRequest request,
        AcceleratorGuardEvidence currentGuardEvidence,
        out AcceleratorToken? token,
        out AcceleratorTokenFaultCode faultCode,
        out string message)
    {
        token = request.TokenAdmission.Token;
        faultCode = AcceleratorTokenFaultCode.None;
        message = string.Empty;

        if (token is null)
        {
            faultCode = AcceleratorTokenFaultCode.SubmitAdmissionRejected;
            message = "Null L7-SDC backend fault policy requires accepted Phase 06 token admission evidence.";
            return false;
        }

        if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(
                request.Descriptor,
                out string descriptorMessage))
        {
            faultCode = AcceleratorTokenFaultCode.DescriptorNotGuardBacked;
            message = descriptorMessage;
            return false;
        }

        AcceleratorGuardDecision capabilityGuardDecision =
            request.CapabilityAcceptance.GuardDecision;
        if (!request.CapabilityAcceptance.IsAccepted ||
            !capabilityGuardDecision.IsAllowed ||
            capabilityGuardDecision.Evidence?.Source != AcceleratorGuardEvidenceSource.GuardPlane ||
            capabilityGuardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
            capabilityGuardDecision.LegalityDecision.AttemptedReplayCertificateReuse ||
            capabilityGuardDecision.DescriptorOwnerBinding is null ||
            !capabilityGuardDecision.DescriptorOwnerBinding.Equals(request.Descriptor.OwnerBinding))
        {
            faultCode = request.CapabilityAcceptance.IsAccepted
                ? AcceleratorTokenFaultCode.CapabilityNotAccepted
                : AcceleratorTokenFaultCode.CapabilityRejected;
            message =
                "Null L7-SDC backend fault policy requires guard-backed capability acceptance bound to the descriptor.";
            return false;
        }

        if ((!ReferenceEquals(token.Descriptor, request.Descriptor) && !token.Descriptor.Equals(request.Descriptor)) ||
            !ReferenceEquals(token.CapabilityAcceptance, request.CapabilityAcceptance))
        {
            faultCode = AcceleratorTokenFaultCode.QueueAdmissionRejected;
            message =
                "Null L7-SDC backend fault policy requires request descriptor and capability evidence to match the token admission.";
            return false;
        }

        AcceleratorGuardDecision submitGuardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                request.Descriptor,
                currentGuardEvidence);
        if (!submitGuardDecision.IsAllowed)
        {
            faultCode = AcceleratorTokenStore.MapGuardFault(submitGuardDecision.Fault);
            message =
                "Null L7-SDC backend fault policy requires fresh guarded submit authority. " +
                submitGuardDecision.Message;
            return false;
        }

        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            faultCode = AcceleratorTokenStore.MapGuardFault(tokenGuardDecision.Fault);
            message =
                "Null L7-SDC backend fault policy requires token-bound owner/domain and epoch revalidation. " +
                tokenGuardDecision.Message;
            return false;
        }

        message = "Null L7-SDC backend fault policy request is guard-backed and token-bound.";
        return true;
    }
}

public sealed class FakeExternalAcceleratorBackend : IExternalAcceleratorBackend
{
    private readonly IAcceleratorBackendClock _clock;
    private readonly AcceleratorTelemetry? _telemetry;
    private readonly ExternalAcceleratorFeatureSwitch _featureSwitch;

    public FakeExternalAcceleratorBackend(
        IAcceleratorBackendClock? clock = null,
        AcceleratorTelemetry? telemetry = null,
        ExternalAcceleratorFeatureSwitch? featureSwitch = null)
    {
        _clock = clock ?? new ManualAcceleratorBackendClock();
        _telemetry = telemetry;
        _featureSwitch = featureSwitch ?? ExternalAcceleratorFeatureSwitch.Enabled;
    }

    public bool IsTestOnly => true;

    public AcceleratorBackendResult TrySubmit(
        AcceleratorQueueAdmissionRequest request,
        IAcceleratorCommandQueue queue,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(queue);
        ulong tick = _clock.Advance();
        if (!_featureSwitch.BackendExecutionEnabled)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                "L7-SDC fake backend execution is disabled by rollback feature switch; queue admission and staged writes were not reached.",
                tick,
                request.TokenAdmission.Token));
        }

        AcceleratorQueueAdmissionResult queueResult =
            queue.TryEnqueue(request, currentGuardEvidence);
        AcceleratorBackendResult result = queueResult.IsAccepted
            ? AcceleratorBackendResult.Submitted(queueResult, tick)
            : AcceleratorBackendResult.Rejected(
                queueResult.FaultCode,
                queueResult.Message,
                tick,
                queueResult.Command?.Token,
                queueResult);
        return RecordResult(result);
    }

    public AcceleratorBackendResult Tick(
        IAcceleratorCommandQueue queue,
        IAcceleratorMemoryPortal memoryPortal,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(memoryPortal);
        ArgumentNullException.ThrowIfNull(stagingBuffer);
        ulong tick = _clock.Advance();

        if (!_featureSwitch.BackendExecutionEnabled)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                "L7-SDC fake backend tick is disabled by rollback feature switch; queued commands remain unexecuted and no staged write occurred.",
                tick));
        }

        if (!queue.TryDequeueReady(
                currentGuardEvidence,
                out AcceleratorQueuedCommand? command,
                out AcceleratorQueueAdmissionResult dequeueResult))
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                dequeueResult.FaultCode,
                dequeueResult.Message,
                tick,
                dequeueResult.Command?.Token,
                dequeueResult));
        }

        AcceleratorToken token = command!.Token;
        if (currentGuardEvidence is null)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.MissingGuardEvidence,
                "L7-SDC fake backend tick requires fresh guard evidence before Running transition.",
                tick,
                token));
        }

        AcceleratorTokenTransition running =
            token.MarkRunning(currentGuardEvidence);
        if (running.Rejected)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                running.FaultCode,
                running.Message,
                tick,
                token));
        }

        AcceleratorMemoryPortalReadResult reads =
            memoryPortal.ReadSourceRanges(token, command.Descriptor, currentGuardEvidence);
        if (reads.IsRejected)
        {
            token.MarkFaulted(reads.FaultCode, currentGuardEvidence);
            return RecordResult(AcceleratorBackendResult.Rejected(
                reads.FaultCode,
                reads.Message,
                tick,
                token));
        }

        ulong bytesStaged = 0;
        int stagedWriteCount = 0;
        IReadOnlyList<AcceleratorMemoryRange> destinations = command.Descriptor.DestinationRanges;
        for (int index = 0; index < destinations.Count; index++)
        {
            AcceleratorMemoryRange destination = destinations[index];
            if (destination.Length > int.MaxValue)
            {
                token.MarkFaulted(
                    AcceleratorTokenFaultCode.StagingRejected,
                    currentGuardEvidence);
                return RecordResult(AcceleratorBackendResult.Rejected(
                    AcceleratorTokenFaultCode.StagingRejected,
                    "L7-SDC fake backend cannot materialize destination staging range larger than Int32.MaxValue bytes.",
                    tick,
                    token));
            }

            byte[] stagedBytes = BuildDeterministicStagedBytes(
                token,
                reads.Reads,
                checked((int)destination.Length));
            AcceleratorStagingResult staged =
                stagingBuffer.StageWrite(
                    token,
                    destination,
                    stagedBytes,
                    currentGuardEvidence);
            if (staged.IsRejected)
            {
                token.MarkFaulted(staged.FaultCode, currentGuardEvidence);
                return RecordResult(AcceleratorBackendResult.Rejected(
                    staged.FaultCode,
                    staged.Message,
                    tick,
                    token));
            }

            bytesStaged += destination.Length;
            stagedWriteCount++;
        }

        AcceleratorTokenTransition complete =
            token.MarkDeviceComplete(currentGuardEvidence);
        if (complete.Rejected)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                complete.FaultCode,
                complete.Message,
                tick,
                token));
        }

        return RecordResult(
            AcceleratorBackendResult.DeviceCompleted(
                token,
                reads.BytesRead,
                bytesStaged,
                stagedWriteCount,
                tick,
                "Fake L7-SDC backend completed device-private model execution and staged bytes only; token is DeviceComplete, not Committed."));
    }

    public AcceleratorBackendResult TryCancel(
        AcceleratorTokenStore tokenStore,
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        AcceleratorTokenLookupResult cancel =
            tokenStore.Cancel(handle, currentGuardEvidence);
        ulong tick = _clock.Advance();
        AcceleratorBackendResult result = cancel.IsAllowed
            ? AcceleratorBackendResult.Canceled(cancel, tick)
            : AcceleratorBackendResult.Rejected(
                cancel.FaultCode,
                cancel.Message,
                tick,
                cancel.Token,
                tokenLookupResult: cancel);
        return RecordResult(result);
    }

    private AcceleratorBackendResult RecordResult(AcceleratorBackendResult result)
    {
        if (_telemetry is null)
        {
            return result;
        }

        if (result.Kind == AcceleratorBackendResultKind.DeviceCompleted)
        {
            _telemetry.RecordBytesRead(result.BytesRead, result.Message);
            _telemetry.RecordBytesStaged(result.BytesStaged, result.Message);
            _telemetry.RecordOperation(
                operationCount: 1,
                latencyCycles: 1,
                result.Message);
        }
        else if (result.IsRejected)
        {
            _telemetry.RecordBackendReject(
                result.FaultCode,
                result.Token,
                result.Message);
        }

        return result;
    }

    private static byte[] BuildDeterministicStagedBytes(
        AcceleratorToken token,
        IReadOnlyList<AcceleratorMemoryRead> reads,
        int length)
    {
        byte[] output = new byte[length];
        if (length == 0)
        {
            return output;
        }

        byte seed = (byte)(token.TokenId ^ token.Descriptor.Identity.NormalizedFootprintHash);
        int offset = 0;
        for (int readIndex = 0; readIndex < reads.Count && offset < output.Length; readIndex++)
        {
            ReadOnlySpan<byte> source = reads[readIndex].Data.Span;
            for (int sourceIndex = 0; sourceIndex < source.Length && offset < output.Length; sourceIndex++)
            {
                output[offset++] = unchecked((byte)(source[sourceIndex] ^ seed));
            }
        }

        while (offset < output.Length)
        {
            output[offset] = unchecked((byte)(seed + offset));
            offset++;
        }

        return output;
    }
}

public sealed class DirectWriteViolationBackend : IExternalAcceleratorBackend
{
    private readonly IAcceleratorBackendClock _clock;
    private readonly AcceleratorTelemetry? _telemetry;

    public DirectWriteViolationBackend(
        IAcceleratorBackendClock? clock = null,
        AcceleratorTelemetry? telemetry = null)
    {
        _clock = clock ?? new ManualAcceleratorBackendClock();
        _telemetry = telemetry;
    }

    public bool IsTestOnly => true;

    public AcceleratorBackendResult TrySubmit(
        AcceleratorQueueAdmissionRequest request,
        IAcceleratorCommandQueue queue,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(queue);
        return RecordResult(AcceleratorBackendResult.Rejected(
            AcceleratorTokenFaultCode.DirectWriteViolation,
            "Direct-write violation backend is a test-only negative control and cannot accept production submit.",
            _clock.Advance(),
            request.TokenAdmission.Token));
    }

    public AcceleratorBackendResult Tick(
        IAcceleratorCommandQueue queue,
        IAcceleratorMemoryPortal memoryPortal,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(memoryPortal);
        ArgumentNullException.ThrowIfNull(stagingBuffer);
        return RecordResult(AcceleratorBackendResult.Rejected(
            AcceleratorTokenFaultCode.DirectWriteViolation,
            "Direct-write violation backend has no legal tick path; direct architectural publication must fault closed.",
            _clock.Advance()));
    }

    public AcceleratorBackendResult TryCancel(
        AcceleratorTokenStore tokenStore,
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        AcceleratorTokenLookupResult cancel =
            tokenStore.Cancel(handle, currentGuardEvidence);
        ulong tick = _clock.Advance();
        return RecordResult(cancel.IsAllowed
            ? AcceleratorBackendResult.Canceled(cancel, tick)
            : AcceleratorBackendResult.Rejected(
                cancel.FaultCode,
                cancel.Message,
                tick,
                cancel.Token,
                tokenLookupResult: cancel));
    }

    public AcceleratorBackendResult AttemptDirectWriteForTest(
        AcceleratorQueuedCommand command,
        AcceleratorGuardEvidence? currentGuardEvidence,
        ulong address,
        ReadOnlySpan<byte> attemptedData)
    {
        ArgumentNullException.ThrowIfNull(command);
        ulong tick = _clock.Advance();
        if (currentGuardEvidence is null)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.MissingGuardEvidence,
                "Direct-write violation test requires current guard evidence before fault publication.",
                tick,
                command.Token));
        }

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                command.Token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "Direct-write violation test detected stale owner/domain or epoch evidence before any side effect. " +
                guardDecision.Message,
                tick,
                command.Token));
        }

        AcceleratorTokenTransition fault =
            command.Token.MarkFaulted(
                AcceleratorTokenFaultCode.DirectWriteViolation,
                currentGuardEvidence);
        AcceleratorBackendResult result = fault.Succeeded
            ? AcceleratorBackendResult.Faulted(
                command.Token,
                AcceleratorTokenFaultCode.DirectWriteViolation,
                $"Direct architectural write attempt to 0x{address:X} for {attemptedData.Length} byte(s) was detected and rejected; no memory portal exposes publication authority in Phase 07.",
                tick,
                directWriteViolationDetected: true)
            : AcceleratorBackendResult.Rejected(
                fault.FaultCode,
                fault.Message,
                tick,
                command.Token);
        return RecordResult(result);
    }

    private AcceleratorBackendResult RecordResult(AcceleratorBackendResult result)
    {
        if (_telemetry is null)
        {
            return result;
        }

        if (result.DirectWriteViolationDetected)
        {
            _telemetry.RecordDirectWriteViolationReject(
                result.Token,
                result.Message);
        }
        else if (result.IsRejected)
        {
            _telemetry.RecordBackendReject(
                result.FaultCode,
                result.Token,
                result.Message);
        }

        return result;
    }
}
