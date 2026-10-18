using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using CpuInterfaceBridge.Diagnostics;
using DoomSharp.HybridCpu.Windows;

internal static class ObservationLateCancelRegression
{
    public static async Task<int> Client(string path)
    {
        var manifest = ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(path));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(timeout.Token);
        ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(manifest), timeout.Token);
        async Task<ObservationTransportMessage> Read() => ObservationTransportProtocol.Decode<ObservationTransportMessage>(
            await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        long id = 0;
        Task Send(string operation, ObservationTransportBinding? binding = null, int deadline = 5000, long target = 0) =>
            ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(
                ++id, operation, deadline, binding?.SegmentId ?? default, binding?.Generation ?? default, target)), timeout.Token);
        var welcome = await Read();
        ObservationTransportProtocol.ValidateHandshake(JsonSerializer.SerializeToUtf8Bytes(welcome.Detail), manifest);
        Console.WriteLine("READY"); Console.Out.Flush();
        ObservationTransportBinding binding;
        while (true)
        {
            await Send("LossStatus");
            var state = await Read();
            if (state.Binding is { Kind: "Main" } main) { binding = main; break; }
            await Task.Delay(1, timeout.Token);
        }
        await Send("Subscribe");
        if ((await Read()).Status != "Subscribed") throw new InvalidDataException("Late-cancel subscribe failed.");
        for (int scenario = 1; scenario <= 2; scenario++)
        {
            await Send("CaptureOnce", binding, deadline: scenario == 1 ? 5000 : 500);
            long captureId = id;
            if (await Console.In.ReadLineAsync(timeout.Token) != "READ" + scenario)
                throw new InvalidDataException("Owner read not confirmed before cancellation/deadline.");
            if (scenario == 1)
            {
                await Send("CancelRequest", target: captureId);
                var rows = new[] { await Read(), await Read() };
                if (!rows.Any(row => row.RequestId == captureId && row.Status == "CanceledOrDeadline") ||
                    !rows.Any(row => row.RequestId == id && row.Status == "CancelRequested"))
                    throw new InvalidDataException("Cancel-after-read did not complete while provider was withheld.");
            }
            else
            {
                var expired = await Read();
                if (expired.RequestId != captureId || expired.Status != "CanceledOrDeadline")
                    throw new InvalidDataException("Deadline-after-read published a result.");
            }
            Console.WriteLine("RELEASE" + scenario); Console.Out.Flush();
        }
        await Send("CaptureOnce", binding);
        var observed = await Read();
        if (observed.RequestId != id || observed.Status != "Observed" || observed.Binding != binding ||
            observed.Capture?.Frame?.Host != binding.Bridge.Identity)
            throw new InvalidDataException("Fresh capture after late cancellation failed.");
        var ev = await Read();
        if (ev.Kind != "Event" || ev.Sequence != 1 || ev.Dropped != 0 ||
            JsonSerializer.Serialize(ev.Capture) != JsonSerializer.Serialize(observed.Capture))
            throw new InvalidDataException("Canceled result leaked into events or fresh capture differed.");
        await Send("LossStatus");
        var loss = await Read();
        if (((JsonElement)loss.Detail!).GetProperty("Sequence").GetInt64() != 1)
            throw new InvalidDataException("Canceled capture consumed event sequence.");
        await Send("Disconnect");
        if ((await Read()).Status != "Disconnected") throw new InvalidDataException("Late-cancel disconnect failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { Observed = observed, Event = ev, Loss = loss }));
        return 0;
    }

    public static int Run(string directory)
    {
        string image = GcEvidenceModeRegression.CreateObservationFixture(directory, 8192);
        var baseline = new DoomGuestExecutionService().Execute(image, 20000, true);
        if (baseline.ExitCode != 0) throw new InvalidOperationException(baseline.Detail);
        Process? client = null;
        int reads = 0;
        var transcript = new List<string>();
        string manifest = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(manifest, (binding, result) =>
        {
            if (binding.SegmentKind != "Main" || result.Status != HostCaptureStatus.Observed)
                throw new InvalidDataException("Late-cancel injection did not follow real Main read.");
            int read = ++reads;
            if (read > 2) return;
            // Only this synthetic test provider is delayed. Existing CPU loop and production protocol are unchanged.
            client!.StandardInput.WriteLine("READ" + read); client.StandardInput.Flush();
            string? release = client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            transcript.Add("READ" + read + " -> " + release);
            if (release != "RELEASE" + read) throw new InvalidDataException("Independent client failed to cancel withheld provider.");
        });
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-late-cancel-client"); start.ArgumentList.Add(manifest);
            client = Process.Start(start)!;
            var stderr = client.StandardError.ReadToEndAsync();
            if (client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult() != "READY")
                throw new InvalidDataException("Late-cancel handshake failed.");
            var observed = new DoomGuestExecutionService { ObservationAttach = host.Attach }.Execute(image, 20000, true);
            var stdout = client.StandardOutput.ReadToEndAsync();
            client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)).GetAwaiter().GetResult();
            using (var output = new StreamWriter(new FileStream(Path.Combine(directory, "client.log"), FileMode.CreateNew)))
            { foreach (var line in transcript) output.WriteLine(line); output.Write(stdout.GetAwaiter().GetResult()); output.Write(stderr.GetAwaiter().GetResult()); }
            using (var output = new FileStream(Path.Combine(directory, "outcomes.json"), FileMode.CreateNew))
                JsonSerializer.Serialize(output, new { Baseline = baseline.Execution, Observed = observed.Execution, Reads = reads, ClientExit = client.ExitCode, Transport = host.Report });
            if (client.ExitCode != 0 || reads != 3 || observed.Execution?.ObservationDiagnostics?.FailedSegments != 0 ||
                JsonSerializer.Serialize(baseline.Execution) != JsonSerializer.Serialize(observed.Execution! with { ObservationDiagnostics = null }))
                throw new InvalidDataException("Late-cancel client failed, provider failed, or guest outcome changed.");
            Console.WriteLine("PASS loader-backed late cancel/deadline: real owner reads withheld until independent client receives cancellation; fresh capture/event only, identical guest outcome. Not Doom qualification.");
            return 0;
        }
        finally { host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); client?.Dispose(); }
    }
}
