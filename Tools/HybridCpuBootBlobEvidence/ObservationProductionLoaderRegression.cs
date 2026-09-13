using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using DoomSharp.HybridCpu.Windows;

internal static class ObservationProductionLoaderRegression
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
        var welcome = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        ObservationTransportProtocol.ValidateHandshake(JsonSerializer.SerializeToUtf8Bytes(welcome.Detail), manifest);
        Console.WriteLine("READY"); Console.Out.Flush();
        long id = 0;
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, "Subscribe", 5000)), timeout.Token);
        var subscribed = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        if (subscribed.Status != "Subscribed") throw new InvalidDataException("Subscription rejected.");
        Guid subscriptionId = ((JsonElement)subscribed.Detail!).GetProperty("SubscriptionId").GetGuid();
        long sequence = 0;
        var captured = new Dictionary<Guid, string>();
        string? runId = null, imageSha = null;
        while (captured.Count < 3)
        {
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, "LossStatus", 5000)), timeout.Token);
            var state = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (state.Binding is not { } binding || captured.ContainsKey(binding.SegmentId)) { await Task.Delay(1, timeout.Token); continue; }
            if (!Guid.TryParse(binding.RunId, out var parsedRun) || parsedRun == Guid.Empty ||
                binding.ImageSha256.Length != 64 || binding.LoaderStatus != "Success" ||
                (runId is not null && (runId != binding.RunId || imageSha != binding.ImageSha256)))
                throw new InvalidDataException("Runtime-owned identity missing or changed across segments.");
            runId = binding.RunId; imageSha = binding.ImageSha256;
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id,
                "CaptureOnce", 5000, binding.SegmentId, binding.Generation)), timeout.Token);
            var result = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (result.Status != "Observed") continue;
            if (result.ConnectionId != welcome.ConnectionId || result.RequestId != id || result.Binding != binding ||
                result.Capture?.Frame is not { } frame || frame.Host != binding.Bridge.Identity || frame.Snapshot.CycleCount == 0)
                throw new InvalidDataException("Production capture binding inconsistent.");
            captured.Add(binding.SegmentId, binding.Kind);
            Console.WriteLine(JsonSerializer.Serialize(result));
            var ev = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (ev.Kind != "Event" || ev.ConnectionId != welcome.ConnectionId || ev.SubscriptionId != subscriptionId ||
                ev.Sequence != ++sequence || ev.Dropped != 0 || ev.Binding != result.Binding ||
                JsonSerializer.Serialize(ev.Capture) != JsonSerializer.Serialize(result.Capture))
                throw new InvalidDataException("Subscription event differs from exact capture or sequence/loss identity.");
        }
        if (!captured.Values.SequenceEqual(new[] { "Initializer", "Initializer", "Main" })) throw new InvalidDataException("Lifecycle mismatch.");
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, "LossStatus", 5000)), timeout.Token);
        var loss = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        var counters = (JsonElement)loss.Detail!;
        if (counters.GetProperty("Sequence").GetInt64() != 3 || counters.GetProperty("Dropped").GetInt64() != 0)
            throw new InvalidDataException("Transport loss control response inconsistent.");
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, "Unsubscribe", 5000, SubscriptionId: subscriptionId)), timeout.Token);
        var unsubscribed = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        if (unsubscribed.Status != "Unsubscribed") throw new InvalidDataException("Unsubscribe failed.");
        await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, "Disconnect", 5000)), timeout.Token);
        var close = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
        if (close.Status != "Disconnected") throw new InvalidDataException("Disconnect failed.");
        return 0;
    }
    public static int Run(string directory)
    {
        string image = GcEvidenceModeRegression.CreateObservationFixture(directory, 8192);
        var baseline = new DoomGuestExecutionService().Execute(image, 20000, true);
        if (baseline.ExitCode != 0) throw new InvalidOperationException(baseline.Detail);
        using var host = new ObservationHostTransport(Path.Combine(directory, "manifest.json"));
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-production-client");
            start.ArgumentList.Add(Path.Combine(directory, "manifest.json"));
            using var client = Process.Start(start)!;
            var stderr = client.StandardError.ReadToEndAsync();
            if (client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult() != "READY")
                throw new InvalidOperationException("Client handshake failed.");
            var stdout = client.StandardOutput.ReadToEndAsync();
            var observed = new DoomGuestExecutionService { ObservationAttach = host.Attach }.Execute(image, 20000, true);
            client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)).GetAwaiter().GetResult();
            string transcript = stdout.GetAwaiter().GetResult();
            using (var output = new StreamWriter(new FileStream(Path.Combine(directory, "client.log"), FileMode.CreateNew)))
            { output.Write(transcript); output.Write(stderr.GetAwaiter().GetResult()); }
            string expectedSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(image)));
            var messages = transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => ObservationTransportProtocol.Decode<ObservationTransportMessage>(System.Text.Encoding.UTF8.GetBytes(line))).ToArray();
            if (messages.Length != 3 || messages.Any(message => !string.Equals(message.Binding!.ImageSha256, expectedSha, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Runtime SHA does not match exact supplied fixture bytes.");
            using (var output = new FileStream(Path.Combine(directory, "outcomes.json"), FileMode.CreateNew))
                JsonSerializer.Serialize(output, new { Baseline = baseline.Execution, Observed = observed.Execution, Transport = host.Report });
            if (client.ExitCode != 0 || JsonSerializer.Serialize(baseline.Execution) !=
                JsonSerializer.Serialize(observed.Execution! with { ObservationDiagnostics = null }))
                throw new InvalidOperationException("Client failed or transport changed guest outcome.");
            Console.WriteLine("PASS production transport loader lifecycle: independent client, two initializers/main, identical guest outcome/GC/retired identity; not Doom qualification.");
            return 0;
        }
        finally { host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
    }
}
