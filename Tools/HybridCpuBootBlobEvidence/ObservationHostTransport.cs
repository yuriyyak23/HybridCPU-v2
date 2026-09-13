using System.IO.Pipes;
using System.Security.Principal;
using CpuInterfaceBridge.Diagnostics;
using HybridCPU_ISE.NonRTL.Runtime;

/// <summary>Explicit local read-only IPC owner. CPU work remains in the existing execution loop.</summary>
internal sealed class ObservationHostTransport : IDisposable
{
    private sealed class Segment : IOwnedCoreObservationConsumer
    {
        private readonly ObservationHostTransport host;
        private readonly HostObservationEndpoint endpoint;
        private readonly HostObservationConnection bridge;
        public readonly ObservationCaptureQueue<HostCaptureResult> Queue;
        public readonly ObservationTransportBinding Binding;
        public Segment(ObservationHostTransport host, OwnedCoreObservationBinding binding)
        {
            this.host = host;
            endpoint = IseHostObservationAdapter.Create(host.Manifest.Host.Name, binding.Observer);
            bridge = endpoint.Connect(endpoint.Descriptor.Identity);
            Queue = new(core =>
            {
                var result = bridge.CaptureOnce(core);
                host.afterOwnerReadForTest?.Invoke(binding, result);
                return result;
            });
            Binding = new(binding.SegmentId, Guid.NewGuid(), binding.EntryAddress, binding.SegmentKind,
                endpoint.Descriptor, binding.RunIdentity?.RunId.ToString() ?? "Unavailable",
                binding.RunIdentity?.PackageSha256 ?? "Unavailable", binding.RunIdentity?.LoaderStatus ?? "Unavailable");
        }
        public void OnBoundary() { if (!host.stop.IsCancellationRequested) Queue.DrainOne(); }
        public void Dispose()
        {
            Queue.Dispose();
            bridge.Dispose();
            endpoint.Dispose();
            lock (host.gate) if (ReferenceEquals(host.segment, this)) host.segment = null;
        }
    }
    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private Segment? segment;
    private readonly Task[] workers;
    private int pending, connections;
    private long connected, rejected, captures;
    private long droppedEvents;
    private string? lastConnectionFailure, hostFailure;
    private readonly Action<OwnedCoreObservationBinding, HostCaptureResult>? afterOwnerReadForTest;
    private readonly Action<Action>? publishForTest;
    public ObservationTransportManifest Manifest { get; }
    public Task Completion => Task.WhenAll(workers);

    public ObservationHostTransport(string manifestPath) : this(manifestPath, null) { }

    // Test-only bounded provider-delay injection. Never published in the wire protocol or run-gc CLI.
    internal ObservationHostTransport(string manifestPath, Action<OwnedCoreObservationBinding, HostCaptureResult>? afterOwnerReadForTest,
        Action<Action>? publishForTest = null)
    {
        this.afterOwnerReadForTest = afterOwnerReadForTest;
        this.publishForTest = publishForTest;
        Manifest = ObservationTransportProtocol.CreateManifest("hybridcpu-doom-observe-" + Guid.NewGuid().ToString("N"));
        var first = ObservationLocalAccess.CreateServer(Manifest.PipeName, Manifest.Host.UserSid, first: true);
        try
        {
            ObservationLocalAccess.PublishManifest(manifestPath, ObservationTransportProtocol.Encode(Manifest), Manifest.Host.UserSid);
            workers = Enumerable.Range(0, 8).Select(index => Task.Run(() => WorkerAsync(index == 0 ? first : null))).ToArray();
        }
        catch { first.Dispose(); stop.Dispose(); throw; }
    }

    public IOwnedCoreObservationConsumer Attach(OwnedCoreObservationBinding binding)
    {
        lock (gate)
        {
            if (stop.IsCancellationRequested) throw new InvalidOperationException(hostFailure ?? "TransportClosed");
            if (segment is not null) throw new InvalidOperationException("ConcurrentCoreBindingRejected");
            return segment = new Segment(this, binding);
        }
    }
    public ObservationTransportBinding? CurrentBinding { get { lock (gate) return segment?.Binding; } }
    public object Report
    {
        get { lock (gate) return new { Schema = ObservationTransportProtocol.Schema, Manifest.Host, Manifest.EndpointGeneration,
            Connections = connections, ConnectionsObserved = connected, RejectedConnections = rejected, Captures = captures,
            PendingRequests = pending, DroppedEvents = droppedEvents, HostFailure = hostFailure, LastConnectionFailure = lastConnectionFailure,
            GcEvidence = "Unavailable", Qualification = "Unavailable" }; }
    }
    private async Task WorkerAsync(NamedPipeServerStream? initial)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var pipe = initial ?? ObservationLocalAccess.CreateServer(Manifest.PipeName, Manifest.Host.UserSid, false);
                initial = null;
                await pipe.WaitForConnectionAsync(stop.Token);
                lock (gate) { connections++; connected++; }
                try
                {
                    using var connection = new Connection(this, pipe);
                    await connection.RunAsync();
                }
                catch (Exception error) when (!stop.IsCancellationRequested)
                { lock (gate) { rejected++; lastConnectionFailure = Bound(error.Message); } }
                finally { lock (gate) connections--; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (gate) hostFailure ??= Bound(error.Message);
            stop.Cancel();
        }
        finally { initial?.Dispose(); }
    }
    private (Task<HostCaptureResult> Result, ObservationTransportBinding Binding) Enqueue(ObservationTransportRequest request, CancellationToken token)
    {
        lock (gate)
        {
            if (segment is null) throw new InvalidOperationException("SegmentUnavailable");
            if (segment.Binding.SegmentId != request.SegmentId || segment.Binding.Generation != request.Generation)
                throw new InvalidOperationException("StaleBinding");
            if (pending >= 64) throw new InvalidOperationException("Busy");
            Task<HostCaptureResult> task;
            try { task = segment.Queue.Enqueue(0, token); }
            catch (InvalidOperationException) { throw new InvalidOperationException("Busy"); }
            pending++;
            return (CompleteAsync(task), segment.Binding);
        }
    }
    private async Task<HostCaptureResult> CompleteAsync(Task<HostCaptureResult> task)
    {
        try { var result = await task; lock (gate) captures++; return result; }
        finally { lock (gate) pending--; }
    }
    public void Dispose() => stop.Cancel(); // Never stops the guest or disposes the owner-thread source.
    private static string Bound(string value) => value[..Math.Min(value.Length, 2048)];

    private sealed class Connection : IDisposable
    {
        private readonly ObservationHostTransport host;
        private readonly NamedPipeServerStream pipe;
        private readonly CancellationTokenSource closed;
        private readonly ObservationOutgoingBuffer outgoing = new();
        private readonly object gate = new();
        private readonly Dictionary<long, CancellationTokenSource> pending = new();
        private readonly HashSet<Task> operations = new();
        private readonly Guid id = Guid.NewGuid();
        private volatile bool detach;
        public Connection(ObservationHostTransport host, NamedPipeServerStream pipe)
        { this.host = host; this.pipe = pipe; closed = CancellationTokenSource.CreateLinkedTokenSource(host.stop.Token); }
        private ObservationTransportMessage Reply(long request, string status, object? detail = null) =>
            new("Response", id, request, status, host.CurrentBinding, Detail: detail);

        public async Task RunAsync()
        {
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(closed.Token))
            {
                handshake.CancelAfter(5000);
                byte[] bytes = await ObservationFrameTransport.ReadAsync(pipe, handshake.Token);
                ObservationPipePeer.RequireClientSid(pipe, new SecurityIdentifier(host.Manifest.Host.UserSid));
                ObservationTransportProtocol.ValidateHandshake(bytes, host.Manifest);
                await ObservationFrameTransport.WriteAsync(pipe,
                    ObservationTransportProtocol.Encode(Reply(0, "Welcome", host.Manifest)), handshake.Token);
            }
            var writer = WriteAsync();
            try
            {
                long previousId = 0;
                while (!closed.IsCancellationRequested && !detach)
                {
                    using var read = CancellationTokenSource.CreateLinkedTokenSource(closed.Token);
                    read.CancelAfter(5000);
                    var request = ObservationTransportProtocol.Request(await ObservationFrameTransport.ReadAsync(pipe, read.Token), previousId);
                    previousId = request.RequestId;
                    if (request.Operation == "CaptureOnce")
                    {
                        lock (gate)
                        {
                            if (pending.Count == 16) { outgoing.Response(Reply(request.RequestId, "Busy")); continue; }
                            var deadline = CancellationTokenSource.CreateLinkedTokenSource(closed.Token);
                            deadline.CancelAfter(request.DeadlineMilliseconds);
                            pending.Add(request.RequestId, deadline);
                            long expiresAt = System.Diagnostics.Stopwatch.GetTimestamp() +
                                System.Diagnostics.Stopwatch.Frequency * request.DeadlineMilliseconds / 1000;
                            var operation = CaptureAsync(request, deadline, expiresAt);
                            operations.Add(operation);
                            _ = operation.ContinueWith(task => { lock (gate) operations.Remove(task); }, TaskScheduler.Default);
                        }
                    }
                    else Control(request);
                }
                if (detach) await writer.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                closed.Cancel();
                try { await writer; } catch (Exception error)
                { lock (host.gate) host.lastConnectionFailure = Bound(error.Message); }
                Task[] remaining;
                lock (gate) remaining = operations.ToArray();
                await Task.WhenAll(remaining);
            }
        }
        private void Control(ObservationTransportRequest request)
        {
            switch (request.Operation)
            {
                case "Subscribe":
                    try { outgoing.Response(Reply(request.RequestId, "Subscribed", new { SubscriptionId = outgoing.Subscribe() })); }
                    catch (InvalidOperationException) { outgoing.Response(Reply(request.RequestId, "Busy")); }
                    break;
                case "Unsubscribe": outgoing.Response(Reply(request.RequestId,
                    outgoing.Unsubscribe(request.SubscriptionId) ? "Unsubscribed" : "UnknownSubscription")); break;
                case "LossStatus": outgoing.Response(Reply(request.RequestId, "LossStatus", outgoing.LossStatus())); break;
                case "CancelRequest":
                    bool canceled;
                    lock (gate) { canceled = pending.TryGetValue(request.TargetRequestId, out var target); target?.Cancel(); }
                    outgoing.Response(Reply(request.RequestId, canceled ? "CancelRequested" : "UnknownRequest")); break;
                case "Disconnect":
                    lock (gate) foreach (var deadline in pending.Values.ToArray()) deadline.Cancel();
                    outgoing.Response(Reply(request.RequestId, "Disconnected"));
                    detach = true;
                    outgoing.Wake();
                    break;
                default: throw new InvalidDataException("RequestRejected");
            }
        }
        private async Task CaptureAsync(ObservationTransportRequest request, CancellationTokenSource deadline, long expiresAt)
        {
            CancellationToken token = deadline.Token;
            int finished = 0;
            void Finish()
            {
                if (Interlocked.Exchange(ref finished, 1) != 0) return;
                lock (gate) pending.Remove(request.RequestId);
                deadline.Dispose();
            }
            try
            {
                var queued = host.Enqueue(request, token);
                var result = await queued.Result;
                token.ThrowIfCancellationRequested();
                var message = new ObservationTransportMessage("Response", id, request.RequestId, result.Status.ToString(), queued.Binding, result);
                if (!outgoing.Response(message, token, expiresAt, Finish)) throw new OperationCanceledException();
                if (result.Status == HostCaptureStatus.Observed)
                {
                    if (host.publishForTest is { } stressPublish) stressPublish(() => outgoing.Publish(message, token, expiresAt));
                    else outgoing.Publish(message, token, expiresAt);
                }
            }
            catch (Exception error)
            {
                string status = error is OperationCanceledException ? "CanceledOrDeadline" :
                    error is ObjectDisposedException ? "SegmentEnded" : Bound(error.Message);
                try { if (!outgoing.Response(Reply(request.RequestId, status), completed: Finish)) Finish(); }
                catch { Finish(); closed.Cancel(); }
            }
        }
        private async Task WriteAsync()
        {
            try
            {
                while (!closed.IsCancellationRequested)
                {
                    while (outgoing.TryTake(out var item))
                    {
                        bool delivered = false;
                        try
                        {
                            if (item!.CanceledOrExpired) continue;
                            using var write = CancellationTokenSource.CreateLinkedTokenSource(closed.Token);
                            write.CancelAfter(5000);
                            await ObservationFrameTransport.WriteAsync(pipe, item.Bytes, write.Token);
                            delivered = true;
                        }
                        finally { outgoing.Written(item!, delivered); }
                    }
                    if (detach) return;
                    await outgoing.WaitAsync(closed.Token);
                }
            }
            catch { closed.Cancel(); throw; }
        }
        public void Dispose()
        {
            closed.Cancel();
            outgoing.Dispose();
            lock (host.gate) host.droppedEvents += outgoing.Dropped;
            closed.Dispose();
        }
    }
}
