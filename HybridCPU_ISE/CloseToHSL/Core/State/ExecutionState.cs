namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Execute-stage and execution-local pipeline latches. This container does not
/// own micro-op semantics, hazard decisions, fault selection or publication.
/// </summary>
internal sealed class ExecutionState
{
    internal Processor.CPU_Core.ExecuteStage Execute;
    internal Processor.CPU_Core.PipelineControl Control;
    internal Processor.CPU_Core.ForwardingPath ExecuteForwarding;
    internal Processor.CPU_Core.ForwardingPath MemoryForwarding;
    internal Processor.CPU_Core.ForwardingPath WriteBackForwarding;
    internal readonly Decoder.OperationAttemptIssuer OperationAttemptIssuer = new();
}
