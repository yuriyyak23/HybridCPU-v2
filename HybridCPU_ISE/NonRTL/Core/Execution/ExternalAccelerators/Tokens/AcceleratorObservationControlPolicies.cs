namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public readonly record struct AcceleratorWaitPolicy(
    bool TimeoutExpired,
    bool MarkTokenTimedOutOnTimeout,
    ulong TimeoutTicks)
{
    public static AcceleratorWaitPolicy ObserveOnly { get; } =
        new(
            TimeoutExpired: false,
            MarkTokenTimedOutOnTimeout: false,
            TimeoutTicks: 0);

    public static AcceleratorWaitPolicy TimedOutNoStateChange(
        ulong timeoutTicks = 0) =>
        new(
            TimeoutExpired: true,
            MarkTokenTimedOutOnTimeout: false,
            TimeoutTicks: timeoutTicks);

    public static AcceleratorWaitPolicy TimedOutAndMarkToken(
        ulong timeoutTicks = 0) =>
        new(
            TimeoutExpired: true,
            MarkTokenTimedOutOnTimeout: true,
            TimeoutTicks: timeoutTicks);
}

public enum AcceleratorRunningCancelDisposition : byte
{
    CooperativeCancel = 0,
    Reject = 1,
    Fault = 2
}

public readonly record struct AcceleratorCancelPolicy(
    AcceleratorRunningCancelDisposition RunningDisposition)
{
    public static AcceleratorCancelPolicy Cooperative { get; } =
        new(AcceleratorRunningCancelDisposition.CooperativeCancel);

    public static AcceleratorCancelPolicy RejectRunning { get; } =
        new(AcceleratorRunningCancelDisposition.Reject);

    public static AcceleratorCancelPolicy FaultRunning { get; } =
        new(AcceleratorRunningCancelDisposition.Fault);
}
