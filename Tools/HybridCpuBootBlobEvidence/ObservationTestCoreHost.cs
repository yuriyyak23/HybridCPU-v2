using CpuInterfaceBridge.Diagnostics;
using HybridCPU_ISE;
using HybridCPU_ISE.NonRTL.Runtime;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU;
using ObservationCaptureQueue = ObservationCaptureQueue<CpuInterfaceBridge.CoreStateSnapshot>;

/// <summary>Separate test owner. No guest instructions are executed.</summary>
internal sealed class ObservationTestCoreHost : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread thread;
    private readonly TaskCompletionSource<ObservationCaptureQueue> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ObservationTestCoreHost()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "observation-test-core-owner" };
        thread.Start();
    }

    public async Task<CpuInterfaceBridge.CoreStateSnapshot> CaptureAsync(CancellationToken token)
    {
        var queue = await ready.Task.WaitAsync(token);
        var capture = queue.Enqueue(0, token);
        wake.Set();
        return await capture;
    }

    private void Run()
    {
        try
        {
            var core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(
                new HybridCpuIseSparseMainMemoryAreaV1(), ProcessorMode.Compiler));
            core.InitializePipeline();
            core.PrepareExecutionStart(0x10000, 0);
            core.WriteCommittedArch(0, 18, 0x12345678);
            using var source = new OwnedCoreObservationSource(core);
            using var endpoint = IseHostObservationAdapter.Create("pipe-test-core", new IseObservationService(source, new object()));
            using var connection = endpoint.Connect(endpoint.Descriptor.Identity);
            using var queue = new ObservationCaptureQueue(id =>
            {
                var result = connection.CaptureOnce(id, stop.Token);
                if (result.Status != HostCaptureStatus.Observed) throw new InvalidOperationException(result.Reason);
                return result.Frame!.Snapshot;
            });
            ready.TrySetResult(queue);
            while (!stop.IsCancellationRequested)
            {
                wake.WaitOne();
                if (!stop.IsCancellationRequested) queue.DrainOne();
            }
            done.TrySetResult();
        }
        catch (Exception error) { ready.TrySetException(error); done.TrySetException(error); }
    }

    public void Dispose()
    {
        stop.Cancel();
        wake.Set();
        if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test owner did not close.");
        done.Task.GetAwaiter().GetResult();
        wake.Dispose();
        stop.Dispose();
    }
}
