using System.IO.Pipes;

internal static class ObservationRemoteAccessRegression
{
    public static async Task Verify(string productionName)
    {
        // Positive control uses the same UNC server name; an unavailable SMB route
        // must not be mislabeled as rejection by PIPE_REJECT_REMOTE_CLIENTS.
        string controlName = "hybridcpu-remote-control-" + Guid.NewGuid().ToString("N");
        using var control = new NamedPipeServerStream(controlName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepted = control.WaitForConnectionAsync(deadline.Token);
        using (var allowed = new NamedPipeClientStream(Environment.MachineName, controlName,
            PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await allowed.ConnectAsync(deadline.Token);
            await accepted;
            byte[] probe = new byte[1];
            var reading = control.ReadExactlyAsync(probe, deadline.Token).AsTask();
            await allowed.WriteAsync(new byte[] { 0x5a }, deadline.Token);
            await reading;
            if (probe[0] != 0x5a) throw new InvalidDataException("UNC positive-control payload mismatch.");
        }
        using var rejected = new NamedPipeClientStream(Environment.MachineName, productionName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        try { await rejected.ConnectAsync(deadline.Token); }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("PASS UNC positive control connected; production pipe rejected same UNC route with access denied.");
            return;
        }
        throw new InvalidDataException("Production pipe accepted UNC access or rejection was not access-denied.");
    }
}
