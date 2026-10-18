using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

namespace HybridCPU_ISE.NonRTL.Runtime;

/// <summary>Opt-in test evidence only. No core, memory, scheduler or execution control surface.</summary>
public sealed record TestOnlyInitializerEvent(
    long Sequence, Guid RunId, DateTimeOffset Utc, string Kind,
    HybridCpuIseManagedInitializerBindingV1? Initializer, ulong ImageBase,
    string? BeginState, string? CompletionState, bool BeginAccepted, bool CompletionAccepted,
    int? PipelineCycleDelta, int GcSafepointsObserved, ulong ProcessExitCount,
    ulong FinalPc, ulong LastRetiredPc, ulong RetireSequence, bool? Success, string Reason);

public sealed class TestOnlyInitializerTrace
{
    private const int Capacity = 4096;
    private readonly List<TestOnlyInitializerEvent> events = new();
    public Guid RunId { get; } = Guid.NewGuid();
    public long Dropped { get; private set; }
    public HybridCpuIseManagedInitializerBindingV1? Active { get; private set; }
    public IReadOnlyList<TestOnlyInitializerEvent> Snapshot() => Array.AsReadOnly(events.ToArray());

    internal void Record(string kind, HybridCpuIseManagedInitializerBindingV1? initializer,
        ulong imageBase, string? beginState, string? completionState, bool begun, bool completed,
        HybridCpuIseManagedGuestExecutionResultV1? result = null)
    {
        if (kind == "Begin") Active = initializer;
        if (kind is "Finalizer" or "Terminal") Active = null;
        if (events.Count == Capacity) { Dropped++; return; }
        // ExecutionFault catch paths in the existing runner can return zero after executing cycles.
        // Do not turn that legacy result into an exact measured delta.
        int? cycles = result is null ? 0 :
            result.Status == HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault &&
            result.RetiredPipelineCycles == 0 ? null : result.RetiredPipelineCycles;
        events.Add(new(events.Count + 1L, RunId, DateTimeOffset.UtcNow, kind, initializer, imageBase,
            beginState, completionState, begun, completed, cycles, result?.GcSafepointsObserved ?? 0,
            result?.ProcessExitEcallsObserved ?? 0, result?.FinalProgramCounter ?? 0,
            result?.LastRetiredBundlePc ?? 0, result?.LastRetireSequence ?? 0, result?.IsSuccess,
            result?.Reason ?? (kind == "BeginRejected" ? "Initializer admission rejected before ExecuteCore." : "")));
    }
}
