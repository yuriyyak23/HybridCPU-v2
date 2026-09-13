using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using HybridCPU_ISE;
using HybridCPU_ISE.NonRTL.Runtime;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU;

internal static class ObservationPendingRegression
{
    public static async Task<int> Client(string path)
    {
        var manifest = ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(path));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await CheckGlobalLimit(manifest, timeout.Token);
        Guid abandonedConnection;
        using (var abandoned = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
        {
            await abandoned.ConnectAsync(timeout.Token);
            ObservationLocalAccess.VerifyServer(abandoned, manifest.Host);
            await ObservationFrameTransport.WriteAsync(abandoned, ObservationTransportProtocol.Encode(manifest), timeout.Token);
            var hello = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(abandoned, timeout.Token));
            abandonedConnection = hello.ConnectionId;
            var bound = hello.Binding!;
            await ObservationFrameTransport.WriteAsync(abandoned, ObservationTransportProtocol.Encode(new ObservationTransportRequest(
                1, "CaptureOnce", 5000, bound.SegmentId, bound.Generation)), timeout.Token);
            // A control response proves the preceding capture was accepted into the connection reader.
            await ObservationFrameTransport.WriteAsync(abandoned, ObservationTransportProtocol.Encode(new ObservationTransportRequest(2, "LossStatus", 5000)), timeout.Token);
            var barrier = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(abandoned, timeout.Token));
            if (barrier.RequestId != 2 || barrier.Status != "LossStatus") throw new InvalidDataException("Pending disconnect barrier failed.");
        }
        using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(timeout.Token);
        ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(manifest), timeout.Token);
        async Task<ObservationTransportMessage> Read() => ObservationTransportProtocol.Decode<ObservationTransportMessage>(
            await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        var welcome = await Read();
        if (welcome.ConnectionId == abandonedConnection) throw new InvalidDataException("Raw reconnect reused connection identity.");
        var binding = welcome.Binding ?? throw new InvalidOperationException("Test core missing.");
        Task Send(long id, string operation, int deadline = 5000, long target = 0) =>
            ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(
                id, operation, deadline, binding.SegmentId, binding.Generation, target)), timeout.Token);
        await Send(1, "CaptureOnce");
        await Send(2, "CancelRequest", target: 1);
        var cancellation = new[] { await Read(), await Read() };
        if (!cancellation.Any(row => row.RequestId == 1 && row.Status == "CanceledOrDeadline") ||
            !cancellation.Any(row => row.RequestId == 2 && row.Status == "CancelRequested"))
            throw new InvalidDataException("Pending cancel responses inconsistent.");
        await Send(3, "CaptureOnce", 50);
        var expired = await Read();
        if (expired.RequestId != 3 || expired.Status != "CanceledOrDeadline") throw new InvalidDataException("Deadline failed.");
        for (long id = 4; id <= 20; id++) await Send(id, "CaptureOnce");
        var busy = await Read();
        if (busy.RequestId != 20 || busy.Status != "Busy") throw new InvalidDataException("Pending limit did not reject seventeenth request.");
        await Send(21, "Disconnect");
        bool disconnected = false;
        for (int response = 0; response < 17; response++)
        {
            var row = await Read();
            if (row.Status == "Disconnected" && row.RequestId == 21) { disconnected = true; break; }
            if (row.Status != "CanceledOrDeadline") throw new InvalidDataException("Disconnect published unexpected capture.");
        }
        if (!disconnected) throw new InvalidDataException("Disconnect response missing.");
        Console.WriteLine("PASS independent pending client: cancel, deadline, 16-request limit/Busy, disconnect; no owner boundary.");
        return 0;
    }

    private static async Task CheckGlobalLimit(ObservationTransportManifest manifest, CancellationToken token)
    {
        var clients = new List<NamedPipeClientStream>();
        var identities = new HashSet<Guid>();
        async Task<ObservationTransportMessage> Read(NamedPipeClientStream pipe) =>
            ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, token));
        Task Send(NamedPipeClientStream pipe, ObservationTransportRequest request) =>
            ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(request), token);
        try
        {
            ObservationTransportBinding? binding = null;
            for (int connection = 0; connection < 5; connection++)
            {
                var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                clients.Add(pipe);
                await pipe.ConnectAsync(token);
                ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
                await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(manifest), token);
                var welcome = await Read(pipe);
                if (welcome.Status != "Welcome" || !identities.Add(welcome.ConnectionId))
                    throw new InvalidDataException("Global-limit connections lack distinct identity.");
                binding = welcome.Binding ?? throw new InvalidDataException("Global-limit test core unavailable.");
                if (connection == 4) break;
                for (long id = 1; id <= 16; id++)
                    await Send(pipe, new(id, "CaptureOnce", 5000, binding.SegmentId, binding.Generation));
                await Send(pipe, new(17, "LossStatus", 5000));
                var barrier = await Read(pipe);
                if (barrier.RequestId != 17 || barrier.Status != "LossStatus")
                    throw new InvalidDataException("Global-limit fill rejected before 64 pending requests.");
            }
            var probe = clients[4];
            await Send(probe, new(1, "CaptureOnce", 5000, binding!.SegmentId, binding.Generation));
            var busy = await Read(probe);
            if (busy.RequestId != 1 || busy.Status != "Busy")
                throw new InvalidDataException("Global-limit 65th pending request was not Busy.");
            // Raw close must release a full connection while the listener and other clients stay live.
            clients[0].Dispose();
            var release = Stopwatch.StartNew();
            long requestId = 1;
            while (true)
            {
                await Send(probe, new(++requestId, "CaptureOnce", 50, binding.SegmentId, binding.Generation));
                var result = await Read(probe);
                if (result.RequestId != requestId) throw new InvalidDataException("Global-limit response identity mismatch.");
                if (result.Status == "CanceledOrDeadline") break; // Admitted; there is deliberately no owner boundary.
                if (result.Status != "Busy" || release.Elapsed > TimeSpan.FromSeconds(2))
                    throw new InvalidDataException("Global-limit capacity was not recovered after raw disconnect.");
                await Task.Delay(5, token);
            }
            Console.WriteLine("PASS global pending: 4 x 16 admitted, connection 5 request 65 Busy, capacity recovered after raw close; five distinct identities.");
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(path);
        var core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(new HybridCpuIseSparseMainMemoryAreaV1(), ProcessorMode.Compiler));
        core.InitializePipeline(); core.PrepareExecutionStart(0x10000, 0);
        using var source = new OwnedCoreObservationSession(core, 0x10000, host.Attach);
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-pending-client"); start.ArgumentList.Add(path);
            using var client = Process.Start(start)!;
            var stdout = client.StandardOutput.ReadToEndAsync(); var stderr = client.StandardError.ReadToEndAsync();
            client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25)).GetAwaiter().GetResult();
            Console.Write(stdout.GetAwaiter().GetResult()); Console.Error.Write(stderr.GetAwaiter().GetResult());
            var releaseDeadline = Stopwatch.StartNew();
            while (true)
            {
                using var live = JsonDocument.Parse(JsonSerializer.Serialize(host.Report));
                if (host.Completion.IsCompleted) throw new InvalidDataException("Host stopped before resource-release assertion.");
                if (live.RootElement.GetProperty("Connections").GetInt32() == 0 &&
                    live.RootElement.GetProperty("PendingRequests").GetInt32() == 0)
                {
                    Console.WriteLine("LIVE BEFORE SHUTDOWN: " + live.RootElement);
                    break;
                }
                if (releaseDeadline.Elapsed > TimeSpan.FromSeconds(2)) throw new InvalidDataException("Disconnected resources remained live before shutdown.");
                Thread.Sleep(10);
            }
            host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            using var report = JsonDocument.Parse(JsonSerializer.Serialize(host.Report));
            Console.WriteLine(report.RootElement.ToString());
            if (client.ExitCode != 0 || report.RootElement.GetProperty("PendingRequests").GetInt32() != 0 ||
                report.RootElement.GetProperty("Captures").GetInt64() != 0 || report.RootElement.GetProperty("HostFailure").ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Pending resources leaked or host faulted.");
            Console.WriteLine("PASS pending host: zero captures, zero pending, no CPU loop; component IPC evidence only.");
            return 0;
        }
        finally { host.Dispose(); }
    }
}
