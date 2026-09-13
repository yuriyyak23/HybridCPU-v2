using HybridCPU_ISE;
using ObservationCaptureQueue = ObservationCaptureQueue<HybridCPU_ISE.CoreStateSnapshot>;

internal static class ObservationQueueRegression
{
    public static int Run()
    {
        int checks = 0, reads = 0;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Queue invariant failed."); checks++; }
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException($"Expected {typeof(T).Name}");
        }
        using (var isolated = new ObservationCaptureQueue(_ => throw new InvalidOperationException("No provider read permitted"), 1))
        using (var disconnectedClient = new CancellationTokenSource())
        {
            var canceledRequest = isolated.Enqueue(0, disconnectedClient.Token);
            disconnectedClient.Cancel();
            Reject<OperationCanceledException>(() => canceledRequest.GetAwaiter().GetResult());
            var replacement = isolated.Enqueue(0, default);
            Check(!replacement.IsCompleted);
            isolated.Dispose();
            Reject<ObjectDisposedException>(() => replacement.GetAwaiter().GetResult());
        }
        using var queue = new ObservationCaptureQueue(_ => { reads++; return new CoreStateSnapshot(); }, 1);
        using var cancel = new CancellationTokenSource();
        var pending = queue.Enqueue(0, cancel.Token);
        Check(reads == 0);
        Reject<InvalidOperationException>(() => queue.Enqueue(0, default));
        cancel.Cancel();
        Reject<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
        Check(queue.DrainOne() && reads == 0);
        var success = queue.Enqueue(0, default);
        Exception? failure = null;
        var thread = new Thread(() => { try { queue.DrainOne(); } catch (Exception ex) { failure = ex; } });
        thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(5)));
        Check(failure is InvalidOperationException && reads == 0);
        Check(queue.DrainOne());
        _ = success.GetAwaiter().GetResult();
        Check(reads == 1);
        using var late = new CancellationTokenSource();
        using var lateQueue = new ObservationCaptureQueue(_ => { late.Cancel(); return new CoreStateSnapshot(); });
        var lateResult = lateQueue.Enqueue(0, late.Token);
        Check(lateQueue.DrainOne());
        Reject<OperationCanceledException>(() => lateResult.GetAwaiter().GetResult());
        var abandoned = queue.Enqueue(0, default);
        queue.Dispose();
        Reject<ObjectDisposedException>(() => abandoned.GetAwaiter().GetResult());
        Reject<ObjectDisposedException>(() => queue.Enqueue(0, default));
        Check(!queue.DrainOne() && reads == 1);
        using var entered = new ManualResetEventSlim();
        using var disconnected = new ManualResetEventSlim();
        bool disposedDuringCapture = false;
        using var slowQueue = new ObservationCaptureQueue(_ =>
        {
            entered.Set();
            disposedDuringCapture = disconnected.Wait(TimeSpan.FromSeconds(2));
            return new CoreStateSnapshot();
        });
        var slowResult = slowQueue.Enqueue(0, default);
        var disconnect = new Thread(() =>
        {
            if (!entered.Wait(TimeSpan.FromSeconds(5))) return;
            slowQueue.Dispose();
            disconnected.Set();
        });
        disconnect.Start();
        slowQueue.DrainOne();
        Check(disconnect.Join(TimeSpan.FromSeconds(5)));
        if (!disposedDuringCapture) throw new InvalidOperationException("Disconnect waited for the capture provider.");
        checks++;
        Reject<ObjectDisposedException>(() => slowResult.GetAwaiter().GetResult());
        Console.WriteLine($"PASS capture queue: {checks} checks; no CPU execution or endpoint claimed.");
        return 0;
    }
}
