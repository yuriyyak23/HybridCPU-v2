using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;

public sealed record AcceleratorLane7PressureResult
{
    private AcceleratorLane7PressureResult(
        bool accepted,
        SystemDeviceCommandKind commandKind,
        int submitPollCount,
        int throttleRejectCount,
        AcceleratorTokenFaultCode faultCode,
        bool branchOrSystemProgressPreserved,
        string message)
    {
        Accepted = accepted;
        CommandKind = commandKind;
        SubmitPollCount = submitPollCount;
        ThrottleRejectCount = throttleRejectCount;
        FaultCode = faultCode;
        BranchOrSystemProgressPreserved = branchOrSystemProgressPreserved;
        Message = message;
    }

    public bool Accepted { get; }

    public bool Rejected => !Accepted;

    public SystemDeviceCommandKind CommandKind { get; }

    public int SubmitPollCount { get; }

    public int ThrottleRejectCount { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public bool BranchOrSystemProgressPreserved { get; }

    public string Message { get; }

    public static AcceleratorLane7PressureResult Allow(
        SystemDeviceCommandKind commandKind,
        int submitPollCount,
        int throttleRejectCount,
        bool branchOrSystemProgressPreserved,
        string message) =>
        new(
            accepted: true,
            commandKind,
            submitPollCount,
            throttleRejectCount,
            AcceleratorTokenFaultCode.None,
            branchOrSystemProgressPreserved,
            message);

    public static AcceleratorLane7PressureResult Reject(
        SystemDeviceCommandKind commandKind,
        int submitPollCount,
        int throttleRejectCount,
        string message) =>
        new(
            accepted: false,
            commandKind,
            submitPollCount,
            throttleRejectCount,
            AcceleratorTokenFaultCode.Lane7PressureRejected,
            branchOrSystemProgressPreserved: true,
            message);
}

public sealed class AcceleratorLane7PressureThrottle
{
    private readonly AcceleratorTelemetry? _telemetry;

    public AcceleratorLane7PressureThrottle(
        int maxSubmitPollPerWindow,
        AcceleratorTelemetry? telemetry = null)
    {
        if (maxSubmitPollPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSubmitPollPerWindow),
                "L7-SDC lane7 pressure budget must be positive.");
        }

        MaxSubmitPollPerWindow = maxSubmitPollPerWindow;
        _telemetry = telemetry;
    }

    public int MaxSubmitPollPerWindow { get; }

    public int SubmitPollCount { get; private set; }

    public int ThrottleRejectCount { get; private set; }

    public AcceleratorLane7PressureResult TryAdmit(
        SystemDeviceCommandKind commandKind)
    {
        if (commandKind is not (SystemDeviceCommandKind.Submit or SystemDeviceCommandKind.Poll))
        {
            return AcceleratorLane7PressureResult.Allow(
                commandKind,
                SubmitPollCount,
                ThrottleRejectCount,
                branchOrSystemProgressPreserved: true,
                "Lane7 serializing/control progress is not consumed by the submit/poll storm budget.");
        }

        if (SubmitPollCount >= MaxSubmitPollPerWindow)
        {
            ThrottleRejectCount++;
            AcceleratorLane7PressureResult result = AcceleratorLane7PressureResult.Reject(
                commandKind,
                SubmitPollCount,
                ThrottleRejectCount,
                "L7-SDC submit/poll storm exceeded the bounded lane7 pressure budget; command was rejected before authority-sensitive state changed.");
            _telemetry?.RecordLane7SubmitPollThrottleReject(result.Message);
            return result;
        }

        SubmitPollCount++;
        return AcceleratorLane7PressureResult.Allow(
            commandKind,
            SubmitPollCount,
            ThrottleRejectCount,
            branchOrSystemProgressPreserved: true,
            "L7-SDC submit/poll command admitted within bounded lane7 pressure budget.");
    }

    public AcceleratorLane7PressureResult RecordBranchOrSystemProgress()
    {
        return AcceleratorLane7PressureResult.Allow(
            SystemDeviceCommandKind.Fence,
            SubmitPollCount,
            ThrottleRejectCount,
            branchOrSystemProgressPreserved: true,
            "Lane7 branch/system progress evidence bypassed L7-SDC submit/poll storm throttling.");
    }

    public void ResetWindow()
    {
        SubmitPollCount = 0;
    }
}
