using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

public sealed class FakeMatMulExternalAcceleratorBackend : IExternalAcceleratorBackend
{
    private readonly IAcceleratorBackendClock _clock;
    private readonly Dictionary<ulong, MatMulDescriptor> _descriptorsByIdentity = new();
    private readonly MatMulDescriptorValidator _validator = new();
    private readonly AcceleratorTelemetry? _telemetry;
    private readonly ExternalAcceleratorFeatureSwitch _featureSwitch;

    public FakeMatMulExternalAcceleratorBackend(
        IAcceleratorBackendClock? clock = null,
        AcceleratorTelemetry? telemetry = null,
        ExternalAcceleratorFeatureSwitch? featureSwitch = null)
    {
        _clock = clock ?? new ManualAcceleratorBackendClock();
        _telemetry = telemetry;
        _featureSwitch = featureSwitch ?? ExternalAcceleratorFeatureSwitch.Enabled;
    }

    public bool IsTestOnly => true;

    public void RegisterDescriptor(
        AcceleratorCommandDescriptor commandDescriptor,
        MatMulDescriptor matMulDescriptor)
    {
        ArgumentNullException.ThrowIfNull(commandDescriptor);
        ArgumentNullException.ThrowIfNull(matMulDescriptor);

        MatMulDescriptorValidationResult validation =
            _validator.Validate(
                matMulDescriptor,
                commandDescriptor);
        if (validation.IsRejected)
        {
            throw new ArgumentException(
                validation.Message,
                nameof(matMulDescriptor));
        }

        _descriptorsByIdentity[commandDescriptor.Identity.DescriptorIdentityHash] =
            matMulDescriptor;
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
                "Fake MatMul backend execution is disabled by rollback feature switch; queue admission and staged writes were not reached.",
                tick,
                request.TokenAdmission.Token));
        }

        if (request.CapabilityAcceptance.Descriptor?.AcceleratorId !=
            MatMulCapabilityProvider.AcceleratorId ||
            !MatMulCapabilityProvider.Matches(request.Descriptor))
        {
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.BackendRejected,
                "Fake MatMul backend requires ReferenceMatMul command descriptor metadata; capability metadata still does not grant execution authority.",
                tick,
                request.TokenAdmission.Token));
        }

        AcceleratorQueueAdmissionResult queueResult =
            queue.TryEnqueue(
                request,
                currentGuardEvidence);
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
                "Fake MatMul backend tick is disabled by rollback feature switch; queued commands remain unexecuted and no staged write occurred.",
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
                "Fake MatMul backend tick requires fresh guard evidence before Running transition.",
                tick,
                token));
        }

        if (!_descriptorsByIdentity.TryGetValue(
                command.Descriptor.Identity.DescriptorIdentityHash,
                out MatMulDescriptor? matMulDescriptor))
        {
            return RecordResult(FaultToken(
                token,
                AcceleratorTokenFaultCode.BackendRejected,
                "Fake MatMul backend requires a typed MatMulDescriptor bound by descriptor identity before staged execution.",
                currentGuardEvidence,
                tick));
        }

        MatMulDescriptorValidationResult validation =
            _validator.Validate(
                matMulDescriptor,
                command.Descriptor);
        if (validation.IsRejected)
        {
            return RecordResult(FaultToken(
                token,
                AcceleratorTokenFaultCode.BackendRejected,
                validation.Message,
                currentGuardEvidence,
                tick));
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

        if (!TryBuildStagedMatMulResultForTest(
                matMulDescriptor,
                validation.Footprint!,
                reads.Reads,
                out byte[] stagedBytes,
                out string buildMessage))
        {
            token.MarkFaulted(
                AcceleratorTokenFaultCode.StagingRejected,
                currentGuardEvidence);
            return RecordResult(AcceleratorBackendResult.Rejected(
                AcceleratorTokenFaultCode.StagingRejected,
                buildMessage,
                tick,
                token));
        }

        AcceleratorMemoryRange destination =
            validation.Footprint!.CRange;
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
            (ulong)stagedBytes.Length,
            stagedWriteCount: 1,
            tick,
            "Fake MatMul backend staged descriptor-bound result bytes only; DeviceComplete is not Committed."));
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

    public static bool TryBuildStagedMatMulResultForTest(
        MatMulDescriptor descriptor,
        MatMulFootprint footprint,
        IReadOnlyList<AcceleratorMemoryRead> reads,
        out byte[] stagedBytes,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(footprint);
        ArgumentNullException.ThrowIfNull(reads);

        stagedBytes = Array.Empty<byte>();
        if (footprint.CRange.Length > int.MaxValue)
        {
            message = "Fake MatMul backend cannot stage destination ranges larger than Int32.MaxValue bytes.";
            return false;
        }

        if (!TryFindRead(
                reads,
                footprint.ARange,
                out ReadOnlyMemory<byte> aData) ||
            !TryFindRead(
                reads,
                footprint.BRange,
                out ReadOnlyMemory<byte> bData))
        {
            message = "Fake MatMul backend requires exact A and B source reads from the guarded memory portal.";
            return false;
        }

        if ((ulong)aData.Length != footprint.ARange.Length ||
            (ulong)bData.Length != footprint.BRange.Length)
        {
            message = "Fake MatMul backend requires exact source bytes for A and B from the guarded memory portal.";
            return false;
        }

        stagedBytes = new byte[(int)footprint.CRange.Length];
        try
        {
            switch (descriptor.Datatypes.OutputDatatype)
            {
                case AcceleratorDatatype.Float32:
                    ComputeFloat32(descriptor, aData.Span, bData.Span, stagedBytes);
                    break;
                case AcceleratorDatatype.Float64:
                    ComputeFloat64(descriptor, aData.Span, bData.Span, stagedBytes);
                    break;
                case AcceleratorDatatype.Int32:
                    ComputeInt32(descriptor, aData.Span, bData.Span, stagedBytes);
                    break;
                default:
                    message = "Fake MatMul backend received an unsupported datatype after validation.";
                    return false;
            }
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            stagedBytes = Array.Empty<byte>();
            message = "Fake MatMul backend rejected malformed or overflowing descriptor-backed source data.";
            return false;
        }

        message = "Fake MatMul backend generated staged result bytes without legacy custom-accelerator execute.";
        return true;
    }

    private static AcceleratorBackendResult FaultToken(
        AcceleratorToken token,
        AcceleratorTokenFaultCode faultCode,
        string message,
        AcceleratorGuardEvidence currentGuardEvidence,
        ulong tick)
    {
        AcceleratorTokenTransition fault =
            token.MarkFaulted(
                faultCode,
                currentGuardEvidence);
        return fault.Succeeded
            ? AcceleratorBackendResult.Faulted(
                token,
                faultCode,
                message,
                tick)
            : AcceleratorBackendResult.Rejected(
                fault.FaultCode,
                fault.Message,
                tick,
                token);
    }

    private static bool TryFindRead(
        IReadOnlyList<AcceleratorMemoryRead> reads,
        AcceleratorMemoryRange range,
        out ReadOnlyMemory<byte> data)
    {
        for (int index = 0; index < reads.Count; index++)
        {
            AcceleratorMemoryRead read = reads[index];
            if (read.Range.Address == range.Address &&
                read.Range.Length == range.Length)
            {
                data = read.Data;
                return true;
            }
        }

        data = default;
        return false;
    }

    private static void ComputeFloat32(
        MatMulDescriptor descriptor,
        ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b,
        Span<byte> c)
    {
        const int size = sizeof(float);
        for (uint row = 0; row < descriptor.M; row++)
        {
            for (uint col = 0; col < descriptor.N; col++)
            {
                float sum = 0;
                for (uint k = 0; k < descriptor.K; k++)
                {
                    ulong aOffset = (((ulong)row * descriptor.Lda) + k) * size;
                    ulong bOffset = (((ulong)k * descriptor.Ldb) + col) * size;
                    sum += ReadSingle(a, aOffset) *
                           ReadSingle(b, bOffset);
                }

                ulong cOffset = (((ulong)row * descriptor.Ldc) + col) * size;
                BitConverter.TryWriteBytes(
                    c.Slice(checked((int)cOffset), size),
                    sum);
            }
        }
    }

    private static void ComputeFloat64(
        MatMulDescriptor descriptor,
        ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b,
        Span<byte> c)
    {
        const int size = sizeof(double);
        for (uint row = 0; row < descriptor.M; row++)
        {
            for (uint col = 0; col < descriptor.N; col++)
            {
                double sum = 0;
                for (uint k = 0; k < descriptor.K; k++)
                {
                    ulong aOffset = (((ulong)row * descriptor.Lda) + k) * size;
                    ulong bOffset = (((ulong)k * descriptor.Ldb) + col) * size;
                    sum += ReadDouble(a, aOffset) *
                           ReadDouble(b, bOffset);
                }

                ulong cOffset = (((ulong)row * descriptor.Ldc) + col) * size;
                BitConverter.TryWriteBytes(
                    c.Slice(checked((int)cOffset), size),
                    sum);
            }
        }
    }

    private static void ComputeInt32(
        MatMulDescriptor descriptor,
        ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b,
        Span<byte> c)
    {
        const int size = sizeof(int);
        for (uint row = 0; row < descriptor.M; row++)
        {
            for (uint col = 0; col < descriptor.N; col++)
            {
                int sum = 0;
                for (uint k = 0; k < descriptor.K; k++)
                {
                    ulong aOffset = (((ulong)row * descriptor.Lda) + k) * size;
                    ulong bOffset = (((ulong)k * descriptor.Ldb) + col) * size;
                    sum = checked(sum + (ReadInt32(a, aOffset) *
                                          ReadInt32(b, bOffset)));
                }

                ulong cOffset = (((ulong)row * descriptor.Ldc) + col) * size;
                BitConverter.TryWriteBytes(
                    c.Slice(checked((int)cOffset), size),
                    sum);
            }
        }
    }

    private static float ReadSingle(ReadOnlySpan<byte> data, ulong byteOffset) =>
        BitConverter.ToSingle(data.Slice(checked((int)byteOffset), sizeof(float)));

    private static double ReadDouble(ReadOnlySpan<byte> data, ulong byteOffset) =>
        BitConverter.ToDouble(data.Slice(checked((int)byteOffset), sizeof(double)));

    private static int ReadInt32(ReadOnlySpan<byte> data, ulong byteOffset) =>
        BitConverter.ToInt32(data.Slice(checked((int)byteOffset), sizeof(int)));
}
