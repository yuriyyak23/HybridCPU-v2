namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>External execution boundary outcomes, not machine trap decoding or release authority.</summary>
public enum ExternalExecutionTerminalKind : byte
{
    Completed = 0,
    BudgetExhausted = 1,
    KernelRejected = 2,
    ExecutionFailure = 3,
}

/// <summary>Same original Start/receipt and retained typed runner outcome. Pure lookup; null remains unknown.</summary>
public sealed record ExternalOriginalExecutionTerminalObservation(
    ExternalOriginalChildExecutionStartRequest Request,
    ExternalChildExecutionReceipt Receipt,
    ExternalExecutionTerminalKind Kind)
{
    /// <summary>Optional original Completed program result. Not success or release authority.</summary>
    public int? ProcessExitCode { get; init; }
}

public interface IHybridCpuOriginalExecutionTerminalRuntimeV1
{
    ExternalOriginalExecutionTerminalObservation? ResolveOriginalExecutionTerminal(
        ExternalOriginalChildExecutionStartRequest originalRequest);
    bool ValidateCurrentOriginalExecutionTerminal(ExternalOriginalChildExecutionStartRequest originalRequest,
        ExternalOriginalExecutionTerminalObservation originalObservation);
}
