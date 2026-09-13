using System.IO.Pipes;

internal static class ObservationPipeRegression
{
    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows pipe validation required.");
        string name = "hybridcpu-observation-test-" + Guid.NewGuid().ToString("N");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            4096, 4096);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Task accepted = server.WaitForConnectionAsync(deadline.Token);
        await client.ConnectAsync(deadline.Token);
        await accepted;
        byte[] request = [10, 20, 30];
        await ObservationFrameTransport.WriteAsync(client, request, deadline.Token);
        byte[] received = await ObservationFrameTransport.ReadAsync(server, deadline.Token);
        if (!received.SequenceEqual(request)) throw new InvalidOperationException("Pipe request mismatch.");
        await ObservationFrameTransport.WriteAsync(server, new byte[] { 40 }, deadline.Token);
        byte[] response = await ObservationFrameTransport.ReadAsync(client, deadline.Token);
        if (!response.SequenceEqual(new byte[] { 40 })) throw new InvalidOperationException("Pipe response mismatch.");
        client.Dispose();
        try
        {
            await ObservationFrameTransport.ReadAsync(server, deadline.Token);
            throw new InvalidOperationException("Disconnected pipe produced a frame.");
        }
        catch (EndOfStreamException) { }
        catch (IOException) { }
        server.Disconnect();
        using var reconnect = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        accepted = server.WaitForConnectionAsync(deadline.Token);
        await reconnect.ConnectAsync(deadline.Token);
        await accepted;
        using var canceled = new CancellationTokenSource();
        Task<byte[]> pending = ObservationFrameTransport.ReadAsync(server, canceled.Token);
        canceled.Cancel();
        try
        {
            await pending;
            throw new InvalidOperationException("Pending pipe read ignored cancellation.");
        }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS Windows named pipe: request/response, disconnect, reconnect, pending-read cancellation. Same-process transport test only; peer authentication and external host integration remain unverified.");
        return 0;
    }
}
