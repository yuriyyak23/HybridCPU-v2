using HybridCPU_ISE;

/// <summary>Host-owned queue. The transport cannot supply code to execute or control the CPU.</summary>
internal sealed class ObservationCaptureQueue<TSnapshot> : IDisposable
{
    private sealed record Request(int CoreId, CancellationToken Token, TaskCompletionSource<TSnapshot> Result);
    private readonly object gate = new();
    private readonly Queue<Request> requests = new();
    private readonly Thread owner = Thread.CurrentThread;
    private readonly Func<int, TSnapshot> capture;
    private readonly int capacity;
    private bool closed;
    private Request? active;

    public ObservationCaptureQueue(Func<int, TSnapshot> capture, int capacity = 64)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (capacity is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capture = capture;
        this.capacity = capacity;
    }

    public Task<TSnapshot> Enqueue(int coreId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (coreId != 0) throw new ArgumentOutOfRangeException(nameof(coreId));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            // A disconnected/deadline-expired client must not monopolize admission
            // while the CPU owner has no available boundary. Preserve live FIFO order.
            int waiting = requests.Count;
            for (int index = 0; index < waiting; index++)
            {
                var prior = requests.Dequeue();
                if (prior.Token.IsCancellationRequested) prior.Result.TrySetCanceled(prior.Token);
                else requests.Enqueue(prior);
            }
            if (requests.Count >= capacity) throw new InvalidOperationException("Capture queue Busy.");
            var completion = new TaskCompletionSource<TSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            requests.Enqueue(new(coreId, token, completion));
            return AwaitRequest(completion.Task, token);
        }
    }

    private static async Task<TSnapshot> AwaitRequest(Task<TSnapshot> task, CancellationToken token)
    {
        // Observe any later fault after the client's deadline/disconnect wins.
        _ = task.ContinueWith(static t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return await task.WaitAsync(token).ConfigureAwait(false);
    }

    // Called once at an existing CPU-owner boundary. No stream I/O or CPU cycles here.
    public bool DrainOne()
    {
        if (!ReferenceEquals(owner, Thread.CurrentThread))
            throw new InvalidOperationException("Capture queue must drain on its owner thread.");
        Request request;
        lock (gate)
        {
            if (closed || requests.Count == 0) return false;
            if (active is not null) throw new InvalidOperationException("Reentrant capture is unsupported.");
            request = requests.Dequeue();
            if (request.Token.IsCancellationRequested)
            {
                request.Result.TrySetCanceled(request.Token);
                return true;
            }
            active = request;
        }
        try
        {
            var frame = capture(request.CoreId);
            lock (gate)
            {
                if (request.Token.IsCancellationRequested) request.Result.TrySetCanceled(request.Token);
                else if (closed) request.Result.TrySetException(new ObjectDisposedException(nameof(ObservationCaptureQueue<TSnapshot>)));
                else request.Result.TrySetResult(frame);
            }
        }
        catch (Exception error) { request.Result.TrySetException(error); }
        finally { lock (gate) active = null; }
        return true;
    }

    public void Dispose()
    {
        lock (gate)
        {
            closed = true;
            active?.Result.TrySetException(new ObjectDisposedException(nameof(ObservationCaptureQueue<TSnapshot>)));
            while (requests.TryDequeue(out var request))
                request.Result.TrySetException(new ObjectDisposedException(nameof(ObservationCaptureQueue<TSnapshot>)));
        }
    }
}
