namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Storage-only containment for the retained legacy FSM counter/flag surface.
/// This state is distinct from PipelineControl and modern telemetry counters;
/// RF-11 containment does not authorize RF-13 removal or reinterpretation.
/// </summary>
internal sealed class LegacyCompatibilityState
{
    internal ulong CycleCounter;
    internal int StageCycleCounter;
    internal bool Stalled;
}
