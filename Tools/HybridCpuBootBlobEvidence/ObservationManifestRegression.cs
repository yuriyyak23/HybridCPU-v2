using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;

internal static class ObservationManifestRegression
{
    public static async Task<int> Client(string path)
    {
        var manifest = ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(path));
        ObservationTransportProtocol.ValidateManifest(manifest);
        ObservationAnonymousAccessRegression.Verify(manifest.PipeName);
        await ObservationRemoteAccessRegression.Verify(manifest.PipeName);
        int negativeChecks = 0;
        using (var stalled = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            await stalled.ConnectAsync(timeout.Token);
            ObservationLocalAccess.VerifyServer(stalled, manifest.Host);
            await ObservationFrameTransport.WriteAsync(stalled, ObservationTransportProtocol.Encode(manifest), timeout.Token);
            _ = await ObservationFrameTransport.ReadAsync(stalled, timeout.Token);
            // Deliberately never consume control responses. A bounded queue or write timeout
            // must terminate only this connection, while the client writes more requests.
            bool boundedClose = false;
            try
            {
                for (long request = 1; request <= 20000; request++)
                    await ObservationFrameTransport.WriteAsync(stalled, ObservationTransportProtocol.Encode(
                        new ObservationTransportRequest(request, "LossStatus", 5000)), timeout.Token);
            }
            catch (IOException) { boundedClose = true; }
            if (!boundedClose) throw new InvalidDataException("Slow-reader connection did not close within bounded request flood.");
            Console.WriteLine("PASS non-reading client isolated by bounded output admission/write timeout; subsequent connections must remain healthy.");
        }
        async Task RejectConnection(ObservationTransportManifest hello, byte[]? command = null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(timeout.Token);
            ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
            void RejectServer(ObservationTransportIdentity identity)
            {
                try { ObservationLocalAccess.VerifyServer(pipe, identity); }
                catch (UnauthorizedAccessException) { negativeChecks++; return; }
                throw new InvalidDataException("Spoofed server identity accepted.");
            }
            RejectServer(manifest.Host with { ProcessId = int.MaxValue });
            RejectServer(manifest.Host with { ProcessStartUtc = manifest.Host.ProcessStartUtc.AddTicks(1) });
            RejectServer(manifest.Host with { UserSid = "S-1-0-0" });
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(hello), timeout.Token);
            if (command is not null)
            {
                _ = await ObservationFrameTransport.ReadAsync(pipe, timeout.Token);
                await ObservationFrameTransport.WriteAsync(pipe, command, timeout.Token);
            }
            try { _ = await ObservationFrameTransport.ReadAsync(pipe, timeout.Token); }
            catch (IOException) { negativeChecks++; return; }
            throw new InvalidDataException("Invalid connection received a response instead of disconnect.");
        }
        await RejectConnection(manifest with { EndpointGeneration = Guid.NewGuid() });
        await RejectConnection(manifest with { Host = manifest.Host with { InstanceId = Guid.NewGuid() } });
        await RejectConnection(manifest with { Schema = "unsupported/v99" });
        await RejectConnection(manifest with { Capabilities = ["Stop"] });
        await RejectConnection(manifest, ObservationTransportProtocol.Encode(new ObservationTransportRequest(1, "Stop", 1000)));
        await RejectConnection(manifest, "{\"RequestId\":1,\"RequestId\":2,\"Operation\":\"LossStatus\",\"DeadlineMilliseconds\":1000}"u8.ToArray());
        Guid previous = Guid.Empty;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(timeout.Token);
            ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(manifest), timeout.Token);
            var welcome = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (welcome.Status != "Welcome" || welcome.ConnectionId == Guid.Empty || welcome.ConnectionId == previous)
                throw new InvalidDataException("Reconnect identity rejected.");
            ObservationTransportProtocol.ValidateHandshake(JsonSerializer.SerializeToUtf8Bytes(welcome.Detail), manifest);
            previous = welcome.ConnectionId;
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(1, "LossStatus", 5000)), timeout.Token);
            var loss = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (loss.Status != "LossStatus" || loss.ConnectionId != previous || loss.RequestId != 1)
                throw new InvalidDataException("Control response identity rejected.");
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(2, "Disconnect", 5000)), timeout.Token);
            var detached = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
            if (detached.Status != "Disconnected" || detached.ConnectionId != previous)
                throw new InvalidDataException("Disconnect response rejected.");
        }
        Console.WriteLine($"PASS independent manifest client: {negativeChecks} negative assertions; ACL, server PID/start/SID, exact handshake, control response, healthy disconnect/reconnect after rejection.");
        return 0;
    }

    public static async Task<int> Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(path);
        try
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("test-observation-manifest-client");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            Console.Write(await stdout);
            Console.Error.Write(await stderr);
            Console.WriteLine(JsonSerializer.Serialize(host.Report));
            if (process.ExitCode != 0) return 1;
            Console.WriteLine("PASS manifest integration; no CPU execution or Doom qualification.");
            return 0;
        }
        finally { host.Dispose(); await host.Completion.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
}
