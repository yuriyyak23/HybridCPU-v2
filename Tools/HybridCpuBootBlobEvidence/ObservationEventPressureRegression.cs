using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using DoomSharp.HybridCpu.Windows;

internal static class ObservationEventPressureRegression
{
    private const int Publications = 260;
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
        Task Send(string operation, ObservationTransportBinding? binding = null) => ObservationFrameTransport.WriteAsync(pipe,
            ObservationTransportProtocol.Encode(new ObservationTransportRequest(++id, operation, 5000,
                binding?.SegmentId ?? default, binding?.Generation ?? default)), timeout.Token);
        var welcome = await Read();
        ObservationTransportProtocol.ValidateHandshake(JsonSerializer.SerializeToUtf8Bytes(welcome.Detail), manifest);
        Console.WriteLine("READY"); Console.Out.Flush();
        ObservationTransportBinding binding;
        while (true)
        {
            await Send("LossStatus"); var state = await Read();
            if (state.Binding is { Kind: "Main" } main) { binding = main; break; }
            await Task.Delay(1, timeout.Token);
        }
        var received = new Dictionary<Guid, int>();
        var sequences = new Dictionary<Guid, long>();
        for (int subscriber = 0; subscriber < 4; subscriber++)
        {
            await Send("Subscribe"); var response = await Read();
            if (response.Status != "Subscribed") throw new InvalidDataException("Subscription admission failed.");
            Guid subscription = ((JsonElement)response.Detail!).GetProperty("SubscriptionId").GetGuid();
            received.Add(subscription, 0); sequences.Add(subscription, 0);
        }
        await Send("Subscribe");
        if ((await Read()).Status != "Busy") throw new InvalidDataException("Fifth subscription admitted.");
        await Send("CaptureOnce", binding); long captureId = id;
        // No pipe reads until all injected publications have passed through the real bounded outgoing buffer.
        if (await Console.In.ReadLineAsync(timeout.Token) != "BURST") throw new InvalidDataException("Burst not complete.");
        await Send("LossStatus"); long lossId = id;
        ObservationTransportMessage? capture = null;
        JsonElement loss = default;
        void Event(ObservationTransportMessage message)
        {
            if (message.Kind != "Event" || message.Binding != binding || message.ConnectionId != welcome.ConnectionId ||
                !received.ContainsKey(message.SubscriptionId) || message.Sequence <= sequences[message.SubscriptionId] || message.Sequence > Publications)
                throw new InvalidDataException("Event identity/sequence invalid.");
            if (capture is null || JsonSerializer.Serialize(message.Capture) != JsonSerializer.Serialize(capture.Capture))
                throw new InvalidDataException("Burst event differs from real captured snapshot.");
            received[message.SubscriptionId]++; sequences[message.SubscriptionId] = message.Sequence;
        }
        while (loss.ValueKind == JsonValueKind.Undefined)
        {
            var row = await Read();
            if (row.Kind == "Event") Event(row);
            else if (row.RequestId == captureId && row.Status == "Observed") capture = row;
            else if (row.RequestId == lossId && row.Status == "LossStatus") loss = (JsonElement)row.Detail!;
            else throw new InvalidDataException("Unexpected pressure response.");
        }
        long dropped = loss.GetProperty("Dropped").GetInt64();
        if (loss.GetProperty("Sequence").GetInt64() != Publications || dropped <= 0 ||
            loss.GetProperty("QueuedBytes").GetInt32() > manifest.Limits.OutgoingBytes)
            throw new InvalidDataException("Saturation/loss/byte bound not demonstrated.");
        var losses = loss.GetProperty("Subscriptions").EnumerateArray().ToDictionary(
            row => row.GetProperty("SubscriptionId").GetGuid(), row => row.GetProperty("Dropped").GetInt64());
        if (losses.Count != 4 || losses.Values.Sum() != dropped ||
            loss.GetProperty("Subscriptions").EnumerateArray().Any(row => row.GetProperty("Queued").GetInt32() > 128))
            throw new InvalidDataException("Independent subscription counters/bounds invalid.");
        while (received.Values.Sum() < Publications * 4 - dropped) Event(await Read());
        foreach (var subscription in received.Keys)
            if (received[subscription] + losses[subscription] != Publications)
                throw new InvalidDataException("Delivered + lost does not equal publications for each subscriber.");
        await Send("LossStatus"); var drained = await Read();
        var terminal = (JsonElement)drained.Detail!;
        if (terminal.GetProperty("Dropped").GetInt64() != dropped || terminal.GetProperty("QueuedBytes").GetInt32() != 0)
            throw new InvalidDataException("Drained counters changed or bytes retained.");
        await Send("Disconnect");
        if ((await Read()).Status != "Disconnected") throw new InvalidDataException("Pressure disconnect failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { Publications, Received = received, Losses = losses, Full = loss, Drained = terminal, Capture = capture }));
        return 0;
    }

    public static int Run(string directory)
    {
        string image = GcEvidenceModeRegression.CreateObservationFixture(directory, 8192);
        var baseline = new DoomGuestExecutionService().Execute(image, 20000, true);
        if (baseline.ExitCode != 0) throw new InvalidOperationException(baseline.Detail);
        Process? client = null;
        int bursts = 0;
        string manifest = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(manifest, null, publish =>
        {
            Interlocked.Increment(ref bursts);
            for (int index = 0; index < Publications; index++) publish();
            client!.StandardInput.WriteLine("BURST"); client.StandardInput.Flush();
        });
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-event-pressure-client"); start.ArgumentList.Add(manifest);
            client = Process.Start(start)!;
            var stderr = client.StandardError.ReadToEndAsync();
            if (client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult() != "READY")
                throw new InvalidDataException("Pressure client handshake failed.");
            var stdout = client.StandardOutput.ReadToEndAsync();
            var observed = new DoomGuestExecutionService { ObservationAttach = host.Attach }.Execute(image, 20000, true);
            client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)).GetAwaiter().GetResult();
            string transcript = stdout.GetAwaiter().GetResult();
            using (var output = new StreamWriter(new FileStream(Path.Combine(directory, "client.log"), FileMode.CreateNew)))
            { output.Write(transcript); output.Write(stderr.GetAwaiter().GetResult()); }
            host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            using (var output = new FileStream(Path.Combine(directory, "outcomes.json"), FileMode.CreateNew))
                JsonSerializer.Serialize(output, new { Baseline = baseline.Execution, Observed = observed.Execution, Bursts = bursts, ClientExit = client.ExitCode, Transport = host.Report });
            if (client.ExitCode != 0 || bursts != 1 || observed.Execution?.ObservationDiagnostics?.FailedSegments != 0 ||
                JsonSerializer.Serialize(baseline.Execution) != JsonSerializer.Serialize(observed.Execution! with { ObservationDiagnostics = null }))
                throw new InvalidDataException("Pressure client failed or changed guest outcome.");
            using var clientReport = JsonDocument.Parse(transcript);
            using var hostReport = JsonDocument.Parse(JsonSerializer.Serialize(host.Report));
            if (hostReport.RootElement.GetProperty("DroppedEvents").GetInt64() != clientReport.RootElement.GetProperty("Drained").GetProperty("Dropped").GetInt64() ||
                hostReport.RootElement.GetProperty("HostFailure").ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Host terminal loss differs from client loss.");
            Console.WriteLine("PASS loader-backed event pressure: injected 260-publication burst, four bounded subscribers, slow reader, exact delivered/lost conservation, identical guest outcome. Not Doom qualification.");
            return 0;
        }
        finally { host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); client?.Dispose(); }
    }
}
