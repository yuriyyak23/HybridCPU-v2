using System;

namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct VmxDebugTraceCounters(
    ulong VmxEventCount,
    ulong VmExitCount,
    ulong VmFailCount,
    ulong VmAbortCount,
    ulong InvalidationCount,
    ulong DroppedPostedEventCount,
    ulong LastExitReason)
{
    public bool ContainsHostEvidence => false;
}

internal sealed class VmxDebugTracePlane : IVmxEventSink
{
    public void RecordVmxEvent(
        VmxEventKind kind,
        ushort vtId,
        VmExitReason exitReason = VmExitReason.None)
    {
        RecordEvent(kind, exitReason);
    }

    public void RecordEvent(
        VmxEventKind kind,
        VmExitReason exitReason = VmExitReason.None)
    {
        VmxEventCount++;
        if (kind == VmxEventKind.VmExit)
        {
            RecordExit(exitReason);
        }
    }

    public ulong VmxEventCount { get; private set; }

    public ulong VmExitCount { get; private set; }

    public ulong VmFailCount { get; private set; }

    public ulong VmAbortCount { get; private set; }

    public ulong InvalidationCount { get; private set; }

    public ulong DroppedPostedEventCount { get; private set; }

    public VmExitReason LastExitReason { get; private set; }

    public void RecordExit(VmExitReason reason)
    {
        VmExitCount++;
        LastExitReason = reason;
    }

    public void RecordFail()
    {
        VmFailCount++;
    }

    public void RecordAbort()
    {
        VmAbortCount++;
    }

    public void RecordInvalidation()
    {
        InvalidationCount++;
    }

    public void RecordDroppedPostedEvents(ulong count)
    {
        DroppedPostedEventCount += count;
    }

    public VmxDebugTraceCounters Snapshot() =>
        new(
            VmxEventCount,
            VmExitCount,
            VmFailCount,
            VmAbortCount,
            InvalidationCount,
            DroppedPostedEventCount,
            unchecked((ulong)LastExitReason));

    public void Reset()
    {
        VmxEventCount = 0;
        VmExitCount = 0;
        VmFailCount = 0;
        VmAbortCount = 0;
        InvalidationCount = 0;
        DroppedPostedEventCount = 0;
        LastExitReason = VmExitReason.None;
    }

    public void DiscardTraceHandles()
    {
        Reset();
    }
}
