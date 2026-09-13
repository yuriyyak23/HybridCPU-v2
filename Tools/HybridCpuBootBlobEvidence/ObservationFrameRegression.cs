using System.Buffers.Binary;

internal static class ObservationFrameRegression
{
    public static async Task<int> RunAsync()
    {
        int checks = 0;
        async Task Reject<T>(Func<Task> action) where T : Exception
        {
            try { await action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException($"Expected {typeof(T).Name}");
        }
        using var wire = new MemoryStream();
        byte[] payload = [1, 2, 3, 4, 5];
        await ObservationFrameTransport.WriteAsync(wire, payload, default);
        await ObservationFrameTransport.WriteAsync(wire, new byte[] { 9 }, default);
        wire.Position = 0;
        var first = await ObservationFrameTransport.ReadAsync(wire, default);
        var second = await ObservationFrameTransport.ReadAsync(wire, default);
        if (!first.SequenceEqual(payload) || !second.SequenceEqual(new byte[] { 9 }) || wire.Position != wire.Length)
            throw new InvalidOperationException("Frame boundaries were not preserved.");
        checks++;
        foreach (uint length in new uint[] { 0, ObservationFrameTransport.MaximumPayloadBytes + 1, uint.MaxValue })
        {
            byte[] prefix = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(prefix, length);
            using var input = new MemoryStream(prefix);
            await Reject<InvalidDataException>(() => ObservationFrameTransport.ReadAsync(input, default));
        }
        using var shortPrefix = new MemoryStream(new byte[] { 1, 0 });
        await Reject<EndOfStreamException>(() => ObservationFrameTransport.ReadAsync(shortPrefix, default));
        using var shortPayload = new MemoryStream(new byte[] { 3, 0, 0, 0, 1 });
        await Reject<EndOfStreamException>(() => ObservationFrameTransport.ReadAsync(shortPayload, default));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        wire.Position = 0;
        await Reject<OperationCanceledException>(() => ObservationFrameTransport.ReadAsync(wire, canceled.Token));
        if (wire.Position != 0) throw new InvalidOperationException("Canceled read consumed bytes.");
        checks++;
        using var output = new MemoryStream();
        await Reject<InvalidDataException>(() => ObservationFrameTransport.WriteAsync(output, ReadOnlyMemory<byte>.Empty, default));
        await Reject<InvalidDataException>(() => ObservationFrameTransport.WriteAsync(output,
            new byte[ObservationFrameTransport.MaximumPayloadBytes + 1], default));
        if (output.Length != 0) throw new InvalidOperationException("Invalid frame wrote bytes.");
        checks++;
        Console.WriteLine($"PASS observation framing: {checks} checks; no endpoint or guest qualification claimed.");
        return 0;
    }
}
