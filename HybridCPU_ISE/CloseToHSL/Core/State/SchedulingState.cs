namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Core-local scheduling bindings and VT selection/stall state. The referenced
/// MicroOpScheduler retains ownership of all queues, legality, fairness,
/// scoreboard, FSP, replay and assist internals.
/// </summary>
internal sealed class SchedulingState
{
    internal MicroOpScheduler Scheduler = null!;
    internal bool[] VirtualThreadStalled = null!;
    internal int ActiveVirtualThreadId;
}
