using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using CpuInterfaceBridge.Diagnostics;
using DoomSharp.HybridCpu.Windows;
using HybridCPU_ISE.NonRTL.Runtime;

// Bounded test protocol only. No Doom endpoint publication or transport acceptance claim.
internal static class ObservationLoaderPipeRegression
{
    private sealed record Segment(Guid SegmentId, ulong EntryAddress, string Kind, HostObservationDescriptor Descriptor);
    private sealed record Command(Guid SegmentId);
    private sealed record Response(string Status, Guid SegmentId, HostCaptureResult? Capture);
    private sealed class Consumer : IOwnedCoreObservationConsumer
    {
        private readonly HostObservationEndpoint endpoint;
        private readonly HostObservationConnection connection;
        public readonly ObservationCaptureQueue<HostCaptureResult> Queue;
        public readonly Segment Segment;
        public Consumer(OwnedCoreObservationBinding binding)
        {
            endpoint = IseHostObservationAdapter.Create("loader-pipe-fixture", binding.Observer);
            connection = endpoint.Connect(endpoint.Descriptor.Identity);
            Queue = new(core => connection.CaptureOnce(core));
            Segment = new(binding.SegmentId, binding.EntryAddress, binding.SegmentKind, endpoint.Descriptor);
        }
        public void OnBoundary() => Queue.DrainOne();
        public void Dispose()
        {
            Queue.Dispose();
            connection.Dispose();
            endpoint.Dispose();
        }
    }
    private static readonly ObservationCapabilities Capabilities = ObservationHandshake.HandshakeOnly with { CaptureOnce = true };

    public static async Task<int> ClientAsync(string name, string expectedJson)
    {
        var expected = JsonSerializer.Deserialize<ObservationHello>(expectedJson)!;
        ObservationHandshake.Validate(JsonSerializer.SerializeToUtf8Bytes(expected), expected);
        if (!name.StartsWith("hybridcpu-loader-test-", StringComparison.Ordinal))
            throw new InvalidDataException("Explicit test endpoint name required.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(timeout.Token);
        if (ObservationPipePeer.ServerProcessId(pipe) != expected.Host.ProcessId)
            throw new InvalidDataException("Server PID mismatch.");
        using var host = Process.GetProcessById(expected.Host.ProcessId);
        if (host.StartTime.ToUniversalTime() != expected.Host.ProcessStartedAtUtc.UtcDateTime)
            throw new InvalidDataException("Server start mismatch.");
        await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(expected), timeout.Token);
        var welcome = ObservationHandshake.ValidateWelcome(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token), expected, Capabilities);
        Guid previous = Guid.Empty;
        var seen = new HashSet<Guid>();
        for (int index = 0; index < 3; index++)
        {
            var segment = JsonSerializer.Deserialize<Segment>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token))!;
            if (!seen.Add(segment.SegmentId) || segment.SegmentId == Guid.Empty ||
                segment.Kind != (index < 2 ? "Initializer" : "Main") ||
                segment.Descriptor.Identity.ProcessId != expected.Host.ProcessId ||
                segment.Descriptor.Identity.ProcessStartedAtUtc != expected.Host.ProcessStartedAtUtc ||
                segment.Descriptor.SourceKind != HostObservationSourceKind.LiveCore ||
                segment.Descriptor.SupportsRemoteAttach || segment.Descriptor.SupportsExecutionControl)
                throw new InvalidDataException("Segment/core/host binding mismatch.");
            // The previous binding must not read the next core. This request is sent by the independent client.
            await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(new Command(previous)), timeout.Token);
            var stale = JsonSerializer.Deserialize<Response>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token))!;
            if (stale.Status != "StaleBinding" || stale.Capture is not null)
                throw new InvalidDataException("Stale capture was accepted.");
            await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(new Command(segment.SegmentId)), timeout.Token);
            byte[] bytes = await ObservationFrameTransport.ReadAsync(pipe, timeout.Token);
            var response = JsonSerializer.Deserialize<Response>(bytes)!;
            if (response.Status != "Observed" || response.SegmentId != segment.SegmentId ||
                response.Capture is not { Status: HostCaptureStatus.Observed, Frame: not null } capture ||
                capture.Frame.Host != segment.Descriptor.Identity || capture.Frame.SourceKind != HostObservationSourceKind.LiveCore ||
                capture.Frame.Snapshot.CycleCount == 0 || capture.Frame.Snapshot.LiveInstructionPointer < segment.EntryAddress)
                throw new InvalidDataException("No exact running loader-backed segment capture.");
            await ObservationFrameTransport.WriteAsync(pipe, SHA256.HashData(bytes), timeout.Token);
            previous = segment.SegmentId;
        }
        Console.WriteLine($"Independent loader client: 3 segment captures, 3 stale rejections; connection {welcome.ConnectionId}; exit 0.");
        return 0;
    }

    public static int Run(string directory, string? frozenImage = null)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = frozenImage ?? GcEvidenceModeRegression.CreateObservationFixture(directory);
            var baseline = new DoomGuestExecutionService().Execute(path, 20000, true);
            if (baseline.ExitCode != 0) throw new InvalidOperationException("Fixture baseline failed: " + baseline.Detail);
            if (baseline.Execution!.LastRetireSequence == 0 || baseline.Execution.LastRetiredBundlePc == 0)
                throw new InvalidOperationException("Fixture terminal retired identity unavailable; cannot prove parity.");
            string name = "hybridcpu-loader-test-" + Guid.NewGuid().ToString("N");
            using var current = Process.GetCurrentProcess();
            var hello = new ObservationHello(ObservationHandshake.Schema,
                new(Guid.NewGuid(), Environment.ProcessId, current.StartTime.ToUniversalTime(), "loader-pipe-fixture"), Guid.NewGuid());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var bindings = Channel.CreateBounded<Consumer>(3);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var transcript = new List<object>();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-loader-client");
            start.ArgumentList.Add(name);
            start.ArgumentList.Add(JsonSerializer.Serialize(hello));
            using var client = Process.Start(start) ?? throw new InvalidOperationException("Independent client did not start.");
            Task<string> stdout = client.StandardOutput.ReadToEndAsync();
            Task<string> stderr = client.StandardError.ReadToEndAsync();
            var server = Task.Run(async () =>
            {
                try
                {
                    await pipe.WaitForConnectionAsync(timeout.Token);
                    if (ObservationPipePeer.ClientProcessId(pipe) != client.Id)
                        throw new InvalidDataException("Client PID mismatch.");
                    using var owner = WindowsIdentity.GetCurrent();
                    ObservationPipePeer.RequireClientSid(pipe, owner.User!);
                    ObservationHandshake.Validate(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token), hello);
                    var welcome = new ObservationWelcome(hello, Guid.NewGuid(), Capabilities);
                    await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(welcome), timeout.Token);
                    transcript.Add(welcome);
                    ready.TrySetResult();
                    for (int index = 0; index < 3; index++)
                    {
                        var consumer = await bindings.Reader.ReadAsync(timeout.Token);
                        await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(consumer.Segment), timeout.Token);
                        var stale = JsonSerializer.Deserialize<Command>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token))!;
                        if (stale.SegmentId == consumer.Segment.SegmentId) throw new InvalidDataException("Expected stale request.");
                        var rejected = new Response("StaleBinding", stale.SegmentId, null);
                        await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(rejected), timeout.Token);
                        var command = JsonSerializer.Deserialize<Command>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token))!;
                        if (command.SegmentId != consumer.Segment.SegmentId) throw new InvalidDataException("Wrong capture binding.");
                        using var captureTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                        captureTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                        var capture = await consumer.Queue.Enqueue(0, captureTimeout.Token);
                        var response = new Response(capture.Status.ToString(), consumer.Segment.SegmentId, capture);
                        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(response);
                        await ObservationFrameTransport.WriteAsync(pipe, bytes, timeout.Token);
                        byte[] ack = await ObservationFrameTransport.ReadAsync(pipe, timeout.Token);
                        if (!CryptographicOperations.FixedTimeEquals(ack, SHA256.HashData(bytes)))
                            throw new InvalidDataException("Independent client snapshot digest mismatch.");
                        transcript.Add(new { consumer.Segment, Rejected = rejected, Response = response,
                            SnapshotSha256 = Convert.ToHexString(ack) });
                    }
                }
                catch (Exception error) { ready.TrySetException(error); throw; }
            });
            // Host setup may wait before execution. Neither OnBoundary nor the CPU loop waits on IPC.
            ready.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            var observed = new DoomGuestExecutionService
            {
                ObservationAttach = binding =>
                {
                    var consumer = new Consumer(binding);
                    if (!bindings.Writer.TryWrite(consumer)) { consumer.Dispose(); throw new InvalidOperationException("Binding queue full."); }
                    return consumer;
                }
            }.Execute(path, 20000, true);
            bindings.Writer.TryComplete();
            server.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            client.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
            WriteNew(Path.Combine(directory, "client.stdout.log"), stdout.GetAwaiter().GetResult());
            WriteNew(Path.Combine(directory, "client.stderr.log"), stderr.GetAwaiter().GetResult());
            if (client.ExitCode != 0) throw new InvalidOperationException("Independent client exit " + client.ExitCode);
            if (observed.ExitCode != 0 || observed.Execution!.ObservationDiagnostics is not
                { SegmentsObserved: 3, FailedSegments: 0, DroppedSegments: 0 } ||
                JsonSerializer.Serialize(observed.Execution with { ObservationDiagnostics = null }) != JsonSerializer.Serialize(baseline.Execution))
                throw new InvalidOperationException("Transport changed guest execution or diagnostic lifecycle failed.");
            WriteNew(Path.Combine(directory, "loader-transport-evidence.json"), JsonSerializer.Serialize(new
            {
                Image = path, ImageSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                Host = hello, ClientPid = client.Id, ClientExitCode = client.ExitCode,
                Baseline = baseline.Execution, Observed = observed.Execution, Transcript = transcript,
                Qualification = "Unavailable", LiveGcEvidence = "Unavailable", RuntimeRunIdentity = "Unavailable"
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS loader-backed external pipe: 2 initializers + main; 3 live captures + stale rejections; unchanged outcome/retire/GC, independent client exit 0. Test protocol only, not Doom or transport acceptance.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.Write(text);
    }
}
