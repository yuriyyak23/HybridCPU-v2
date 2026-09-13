using System;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.NonRTL.Runtime;

/// <summary>Optional host diagnostics. All methods execute on the existing CPU-owner thread.</summary>
public interface IOwnedCoreObservationConsumer : IDisposable
{
    void OnBoundary();
}

public sealed record OwnedCoreObservationBinding(Guid SegmentId, ulong EntryAddress, IseObservationService Observer)
{
    public string SegmentKind { get; init; } = "Unavailable";
    public OwnedCoreObservationRunIdentity? RunIdentity { get; init; }
}

/// <summary>Supplied by the package/loader owner, never inferred from an observation snapshot.</summary>
public sealed record OwnedCoreObservationRunIdentity(Guid RunId, string PackageSha256, string LoaderStatus);

/// <summary>Contains diagnostic failures so they cannot replace the guest execution outcome.</summary>
public sealed class OwnedCoreObservationSession : IDisposable
{
    private readonly OwnedCoreObservationSource source;
    private IOwnedCoreObservationConsumer? consumer;
    private bool closed;
    public string? Failure { get; private set; }
    private readonly Guid segmentId = Guid.NewGuid();
    private readonly ulong entryAddress;
    private readonly string segmentKind;
    private long boundaries;
    private bool failureTruncated;
    private const int MaximumFailureCharacters = 2048;

    public OwnedCoreObservationSession(Processor.CPU_Core core, ulong entryAddress,
        Func<OwnedCoreObservationBinding, IOwnedCoreObservationConsumer> attach,
        string segmentKind = "Unavailable")
    {
        ArgumentNullException.ThrowIfNull(attach);
        this.entryAddress = entryAddress;
        this.segmentKind = segmentKind;
        source = new OwnedCoreObservationSource(core);
        try
        {
            consumer = attach(new(segmentId, entryAddress, new IseObservationService(source, new object()))
                { SegmentKind = segmentKind })
                ?? throw new InvalidOperationException("Observation attach returned no consumer.");
        }
        catch (Exception error)
        {
            RecordFailure(error.Message);
            source.Dispose();
            closed = true;
        }
    }

    public void OnBoundary()
    {
        if (closed || consumer is null) return;
        try { boundaries++; consumer.OnBoundary(); }
        catch (Exception error)
        {
            RecordFailure(error.Message);
            CloseConsumer();
            source.Dispose();
            closed = true;
        }
    }

    private void CloseConsumer()
    {
        var prior = consumer;
        consumer = null;
        try { prior?.Dispose(); }
        catch (Exception error) { RecordFailure(error.Message, closing: true); }
    }

    private void RecordFailure(string message, bool closing = false)
    {
        string prefix = closing && Failure is not null ? " | close: " : string.Empty;
        int available = MaximumFailureCharacters - (Failure?.Length ?? 0);
        string bounded = prefix + message[..Math.Min(message.Length, MaximumFailureCharacters)];
        failureTruncated |= message.Length > MaximumFailureCharacters || bounded.Length > available;
        Failure = (Failure ?? string.Empty) + bounded[..Math.Min(available, bounded.Length)];
    }

    public OwnedCoreObservationSegmentReport Report => new(segmentId, entryAddress, segmentKind,
        boundaries, closed, Failure, failureTruncated);

    public void Dispose()
    {
        if (closed) return;
        CloseConsumer();
        source.Dispose();
        closed = true;
    }
}
