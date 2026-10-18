using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using System.Security.Principal;
using System.Security.Cryptography;

internal static class ObservationExternalPipeRegression
{
    private sealed record CaptureResponse(Guid ConnectionId, CpuInterfaceBridge.CoreStateSnapshot Snapshot,
        string SourceKind, string ImageIdentity, string GuestRunIdentity, string GcEvidence);
    private static ObservationCapabilities TestCapabilities => ObservationHandshake.HandshakeOnly with { CaptureOnce = true };
    public static async Task<int> ClientAsync(string name, string expectedJson)
    {
        var expected = JsonSerializer.Deserialize<ObservationHello>(expectedJson)
            ?? throw new InvalidDataException("Expected host identity absent.");
        ObservationHandshake.Validate(JsonSerializer.SerializeToUtf8Bytes(expected), expected);
        if (!name.StartsWith("hybridcpu-external-test-", StringComparison.Ordinal))
            throw new ArgumentException("Expected explicit test pipe name.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(deadline.Token);
        if (ObservationPipePeer.ServerProcessId(pipe) != expected.Host.ProcessId)
            throw new InvalidDataException("OS pipe server PID mismatch.");
        using var hostProcess = Process.GetProcessById(expected.Host.ProcessId);
        if (hostProcess.StartTime.ToUniversalTime() != expected.Host.ProcessStartedAtUtc.UtcDateTime)
            throw new InvalidDataException("OS server process start mismatch.");
        await ObservationFrameTransport.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(expected), deadline.Token);
        byte[] response = await ObservationFrameTransport.ReadAsync(pipe, deadline.Token);
        var welcome = ObservationHandshake.ValidateWelcome(response, expected, TestCapabilities);
        await ObservationFrameTransport.WriteAsync(pipe, new byte[] { 1 }, deadline.Token);
        byte[] captureBytes = await ObservationFrameTransport.ReadAsync(pipe, deadline.Token);
        var capture = JsonSerializer.Deserialize<CaptureResponse>(captureBytes)
            ?? throw new InvalidDataException("Capture absent.");
        if (capture.ConnectionId != welcome.ConnectionId || capture.Snapshot is null ||
            capture.Snapshot.LiveInstructionPointer != 0x10000 ||
            capture.Snapshot.ActiveVirtualThreadRegisters.Count != 32 ||
            capture.Snapshot.ActiveVirtualThreadRegisters[18] != 0x12345678 || capture.SourceKind != "LiveCore" ||
            capture.Snapshot.VirtualThreadLivePcs.Count == 0 || capture.Snapshot.VirtualThreadCommittedPcs.Count == 0 ||
            capture.Snapshot.DecodePublicationCertificate is null || capture.Snapshot.ExecuteCompletionCertificate is null ||
            capture.Snapshot.RetireVisibilityCertificate is null || capture.ImageIdentity != "Unavailable" ||
            capture.GuestRunIdentity != "Unavailable" || capture.GcEvidence != "Unavailable")
            throw new InvalidDataException("Wrong test core capture.");
        // Round trip all fields, including certificate contents; defaults cannot silently replace supplied values.
        using var original = JsonDocument.Parse(captureBytes);
        using var roundTrip = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(capture));
        if (!JsonElement.DeepEquals(original.RootElement, roundTrip.RootElement))
            throw new InvalidDataException("Snapshot fields changed during deserialization.");
        await ObservationFrameTransport.WriteAsync(pipe, SHA256.HashData(captureBytes), deadline.Token);
        return 0;
    }

    public static async Task<int> RunAsync()
    {
        using var coreHost = new ObservationTestCoreHost();
        string name = "hybridcpu-external-test-" + Guid.NewGuid().ToString("N");
        using var hostProcess = Process.GetCurrentProcess();
        var expected = new ObservationHello(ObservationHandshake.Schema,
            new ObservationHostIdentity(Guid.NewGuid(), Environment.ProcessId,
                hostProcess.StartTime.ToUniversalTime(), "external-pipe-regression"), Guid.NewGuid());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("test-observation-pipe-client");
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(JsonSerializer.Serialize(expected));
        using var client = Process.Start(start) ?? throw new InvalidOperationException("Test client did not start.");
        await server.WaitForConnectionAsync(deadline.Token);
        if (ObservationPipePeer.ClientProcessId(server) != client.Id)
            throw new InvalidDataException("OS pipe client PID mismatch.");
        byte[] request = await ObservationFrameTransport.ReadAsync(server, deadline.Token);
        using var owner = WindowsIdentity.GetCurrent();
        ObservationPipePeer.RequireClientSid(server, owner.User ?? throw new InvalidOperationException("Host SID absent."));
        bool wrongSidRejected = false;
        try { ObservationPipePeer.RequireClientSid(server, new SecurityIdentifier("S-1-0-0")); }
        catch (UnauthorizedAccessException) { wrongSidRejected = true; }
        if (!wrongSidRejected) throw new InvalidOperationException("Incorrect expected SID accepted.");
        ObservationHandshake.Validate(request, expected);
        var welcome = new ObservationWelcome(expected, Guid.NewGuid(), TestCapabilities);
        await ObservationFrameTransport.WriteAsync(server, JsonSerializer.SerializeToUtf8Bytes(welcome), deadline.Token);
        byte[] command = await ObservationFrameTransport.ReadAsync(server, deadline.Token);
        if (!command.SequenceEqual(new byte[] { 1 })) throw new InvalidDataException("Unsupported test command.");
        var snapshot = await coreHost.CaptureAsync(deadline.Token);
        var capture = new CaptureResponse(welcome.ConnectionId, snapshot, "LiveCore", "Unavailable", "Unavailable", "Unavailable");
        byte[] captureBytes = JsonSerializer.SerializeToUtf8Bytes(capture);
        await ObservationFrameTransport.WriteAsync(server, captureBytes, deadline.Token);
        byte[] acknowledged = await ObservationFrameTransport.ReadAsync(server, deadline.Token);
        if (!CryptographicOperations.FixedTimeEquals(acknowledged, SHA256.HashData(captureBytes)))
            throw new InvalidDataException("Client did not acknowledge the exact snapshot bytes.");
        await client.WaitForExitAsync(deadline.Token);
        if (client.ExitCode != 0) throw new InvalidOperationException($"Client exit {client.ExitCode}");
        Console.WriteLine($"PASS external core capture: host PID {Environment.ProcessId}, client PID {client.Id}, client exit 0. Handshake/SID checked; capture serviced by separate core-owner thread through bridge. Test protocol only; no guest execution or qualification.");
        return 0;
    }
}
