using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

/// <summary>
/// Runtime-owned reference backend for the Phase 08 current L7-SDC contour.
/// It performs queue admission, guarded source reads, backend-private staging,
/// and DeviceComplete promotion without granting direct memory publication.
/// </summary>
public sealed class ReferenceExternalAcceleratorBackend : IExternalAcceleratorBackend
{
    private readonly IAcceleratorBackendClock _clock;
    private readonly AcceleratorTelemetry? _telemetry;
    private readonly ExternalAcceleratorFeatureSwitch _featureSwitch;

    public ReferenceExternalAcceleratorBackend(
        IAcceleratorBackendClock? clock = null,
        AcceleratorTelemetry? telemetry = null,
        ExternalAcceleratorFeatureSwitch? featureSwitch = null)
    {
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
                "L7-SDC reference backend execution is disabled by rollback feature switch; queue admission and staged writes were not reached.",
                tick,
                request.TokenAdmission.Token));
        }

        AcceleratorQueueAdmissionResult queueResult =
            queue.TryEnqueue(request, currentGuardEvidence);
        return RecordResult(queueResult.IsAccepted
            ? AcceleratorBackendResult.Submitted(queueResult, tick)
            : AcceleratorBackendResult.Rejected(
                queueResult.FaultCode,
                queueResult.Message,
                tick,
                queueResult.Command?.Token,
                queueResult));
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
                "L7-SDC reference backend tick is disabled by rollback feature switch; queued commands remain unexecuted and no staged write occurred.",
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
                "L7-SDC reference backend tick requires fresh guard evidence before Running transition.",
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
            memoryPortal.ReadSourceRanges(
                token,
                command.Descriptor,
                currentGuardEvidence);
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
        IReadOnlyList<AcceleratorMemoryRange> destinations =
            command.Descriptor.DestinationRanges;
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
                    "L7-SDC reference backend cannot materialize destination staging range larger than Int32.MaxValue bytes.",
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

        return RecordResult(AcceleratorBackendResult.DeviceCompleted(
            token,
            reads.BytesRead,
            bytesStaged,
            stagedWriteCount,
            tick,
            "L7-SDC reference backend completed guarded execution and staged bytes only; token is DeviceComplete, not Committed."));
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
