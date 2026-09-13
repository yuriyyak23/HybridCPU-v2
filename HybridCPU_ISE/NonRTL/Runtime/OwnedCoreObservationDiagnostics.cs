using System;
using System.Collections.Generic;

namespace HybridCPU_ISE.NonRTL.Runtime;

public sealed record OwnedCoreObservationSegmentReport(Guid SegmentId, ulong EntryAddress,
    string SegmentKind, long BoundariesObserved, bool Closed, string? Failure, bool FailureTruncated);

/// <summary>Terminal host diagnostic evidence only; never GC or guest completion evidence.</summary>
public sealed record OwnedCoreObservationDiagnostics(IReadOnlyList<OwnedCoreObservationSegmentReport> Segments,
    long SegmentsObserved, long FailedSegments, long DroppedSegments)
{
    public string Schema => "hybridcpu.owned-core-observation-terminal/v1";
}

internal sealed class OwnedCoreObservationLedger
{
    internal const int Capacity = 256;
    private readonly List<OwnedCoreObservationSegmentReport> segments = new();
    private long observed;
    private long failed;

    public void Record(OwnedCoreObservationSegmentReport report)
    {
        observed++;
        if (report.Failure is not null) failed++;
        if (segments.Count < Capacity) segments.Add(report);
    }

    public OwnedCoreObservationDiagnostics Snapshot() => new(
        Array.AsReadOnly(segments.ToArray()), observed, failed, observed - segments.Count);
}
