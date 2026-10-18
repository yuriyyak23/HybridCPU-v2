using System.Threading.Channels;

internal sealed class ObservationOutgoingBuffer : IDisposable
{
    internal sealed record Item(byte[] Bytes, CancellationToken Token, long ExpiresAt = long.MaxValue, Action? Completed = null,
        Guid SubscriptionId = default)
    {
        public bool CanceledOrExpired => Token.IsCancellationRequested || System.Diagnostics.Stopwatch.GetTimestamp() >= ExpiresAt;
    }
    private sealed class Subscription
    {
        public readonly Queue<Item> Events = new();
        public long Dropped;
    }
    private readonly object gate = new();
    private readonly Queue<Item> responses = new();
    private readonly Dictionary<Guid, Subscription> subscriptions = new();
    private readonly Channel<bool> signal = Channel.CreateBounded<bool>(1);
    private long sequence, dropped;
    private int bytes, eventBytes, cursor;
    private bool closed;
    private const int EventBytesLimit = 3 * 1024 * 1024; // Reserve control capacity even when every event queue is full.

    public Guid Subscribe()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (subscriptions.Count == 4) throw new InvalidOperationException("SubscriptionBusy");
            var id = Guid.NewGuid();
            subscriptions.Add(id, new());
            return id;
        }
    }
    public bool Unsubscribe(Guid id)
    {
        lock (gate)
        {
            if (!subscriptions.Remove(id, out var subscription)) return false;
            while (subscription.Events.TryDequeue(out var item))
            { bytes -= item.Bytes.Length; eventBytes -= item.Bytes.Length; dropped++; }
            return true;
        }
    }
    public bool Response(ObservationTransportMessage response, CancellationToken token = default,
        long expiresAt = long.MaxValue, Action? completed = null)
    {
        byte[] payload = ObservationTransportProtocol.Encode(response);
        lock (gate)
        {
            if (closed || token.IsCancellationRequested) return false;
            if (responses.Count >= 64 || bytes + payload.Length > 4 * 1024 * 1024) throw new InvalidOperationException("OutgoingBusy");
            responses.Enqueue(new(payload, token, expiresAt, completed));
            bytes += payload.Length;
            signal.Writer.TryWrite(true);
            return true;
        }
    }
    public void Publish(ObservationTransportMessage captured, CancellationToken token, long expiresAt = long.MaxValue)
    {
        lock (gate)
        {
            if (closed || token.IsCancellationRequested) return;
            long next = ++sequence;
            foreach (var entry in subscriptions)
            {
                byte[] payload = ObservationTransportProtocol.Encode(captured with
                {
                    Kind = "Event", RequestId = 0, SubscriptionId = entry.Key, Sequence = next, Dropped = entry.Value.Dropped
                });
                if (entry.Value.Events.Count == 128 || eventBytes + payload.Length > EventBytesLimit || bytes + payload.Length > 4 * 1024 * 1024)
                { entry.Value.Dropped++; dropped++; continue; }
                entry.Value.Events.Enqueue(new(payload, token, expiresAt, SubscriptionId: entry.Key));
                bytes += payload.Length;
                eventBytes += payload.Length;
            }
            signal.Writer.TryWrite(true);
        }
    }
    public object LossStatus()
    {
        lock (gate) return new { Sequence = sequence, Dropped = dropped, QueuedBytes = bytes,
            MaximumBytes = 4 * 1024 * 1024, OverflowPolicy = "DropNewest", Replay = false,
            Subscriptions = subscriptions.Select(pair => new { SubscriptionId = pair.Key,
                Queued = pair.Value.Events.Count, pair.Value.Dropped }).ToArray() };
    }
    public bool TryTake(out Item? item)
    {
        lock (gate)
        {
            if (responses.TryDequeue(out item)) return true;
            var entries = subscriptions.Values.ToArray();
            for (int scan = 0; scan < entries.Length; scan++)
            {
                cursor %= entries.Length;
                if (entries[cursor++].Events.TryDequeue(out item))
                { eventBytes -= item.Bytes.Length; return true; }
            }
            item = null;
            return false;
        }
    }
    public void Written(Item item, bool delivered = true)
    {
        lock (gate)
        {
            bytes -= item.Bytes.Length;
            if (!delivered && item.SubscriptionId != Guid.Empty)
            {
                dropped++;
                if (subscriptions.TryGetValue(item.SubscriptionId, out var subscription)) subscription.Dropped++;
            }
        }
        item.Completed?.Invoke();
    }
    public long Dropped { get { lock (gate) return dropped; } }
    public void Wake() => signal.Writer.TryWrite(true);
    public async Task WaitAsync(CancellationToken token) => _ = await signal.Reader.ReadAsync(token);
    public void Dispose()
    {
        var completions = new List<Action>();
        lock (gate)
        {
            closed = true;
            while (responses.TryDequeue(out var item))
            { bytes -= item.Bytes.Length; if (item.Completed is not null) completions.Add(item.Completed); }
            foreach (var subscription in subscriptions.Values)
                while (subscription.Events.TryDequeue(out var item)) { bytes -= item.Bytes.Length; dropped++; }
            eventBytes = 0;
            subscriptions.Clear();
            signal.Writer.TryComplete();
        }
        foreach (var completion in completions) completion();
    }
}
