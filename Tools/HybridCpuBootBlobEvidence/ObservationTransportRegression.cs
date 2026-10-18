using System.Text.Json;

internal static class ObservationTransportRegression
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); checks++; }
        using (var abandonedEvents = new ObservationOutgoingBuffer())
        {
            var removed = abandonedEvents.Subscribe();
            _ = abandonedEvents.Subscribe();
            var captured = new ObservationTransportMessage("Response", Guid.NewGuid(), 1, "Observed");
            abandonedEvents.Publish(captured, default);
            abandonedEvents.Publish(captured, default);
            Check(abandonedEvents.Unsubscribe(removed), "Queued subscription could not be removed.");
            Check(abandonedEvents.Dropped == 2, "Unsubscribe discarded events without loss accounting.");
            abandonedEvents.Dispose();
            Check(abandonedEvents.Dropped == 4, "Connection close discarded events without loss accounting.");
            using var terminal = JsonDocument.Parse(JsonSerializer.Serialize(abandonedEvents.LossStatus()));
            Check(terminal.RootElement.GetProperty("QueuedBytes").GetInt32() == 0, "Close retained queued bytes.");
            abandonedEvents.Dispose();
            Check(abandonedEvents.Dropped == 4, "Repeated close double-counted loss.");
        }
        using (var lateEvents = new ObservationOutgoingBuffer())
        {
            _ = lateEvents.Subscribe();
            lateEvents.Publish(new ObservationTransportMessage("Response", Guid.NewGuid(), 1, "Observed"), default, 0);
            Check(lateEvents.TryTake(out var lateEvent) && lateEvent!.CanceledOrExpired, "Expired event not available to discard.");
            lateEvents.Written(lateEvent!, delivered: false);
            Check(lateEvents.Dropped == 1, "Writer-discarded expired event missing from loss accounting.");
        }
        using var buffer = new ObservationOutgoingBuffer();
        Guid first = buffer.Subscribe(), second = buffer.Subscribe();
        var message = new ObservationTransportMessage("Response", Guid.NewGuid(), 1, "Observed");
        for (int index = 0; index < 130; index++) buffer.Publish(message, default);
        using var loss = JsonDocument.Parse(JsonSerializer.Serialize(buffer.LossStatus()));
        Check(loss.RootElement.GetProperty("Dropped").GetInt64() == 4, "Independent DropNewest count lost.");
        Check(loss.RootElement.GetProperty("Sequence").GetInt64() == 130, "Event sequence lost.");
        Check(buffer.Response(message with { Status = "LossStatus" }), "Control response blocked by events.");
        Check(buffer.TryTake(out var control), "Control response missing.");
        Check(JsonDocument.Parse(control!.Bytes).RootElement.GetProperty("Status").GetString() == "LossStatus", "Control priority lost.");
        buffer.Written(control);
        var counts = new Dictionary<Guid, int>();
        while (buffer.TryTake(out var item))
        {
            using var value = JsonDocument.Parse(item!.Bytes);
            Guid id = value.RootElement.GetProperty("SubscriptionId").GetGuid();
            counts[id] = counts.GetValueOrDefault(id) + 1;
            buffer.Written(item);
        }
        Check(counts[first] == 128 && counts[second] == 128, "Subscribers stole events.");
        using var empty = JsonDocument.Parse(JsonSerializer.Serialize(buffer.LossStatus()));
        Check(empty.RootElement.GetProperty("QueuedBytes").GetInt32() == 0, "Bytes not released.");
        Check(buffer.Unsubscribe(first) && buffer.Unsubscribe(second), "Unsubscribe failed.");
        using var cancel = new CancellationTokenSource();
        int completed = 0;
        buffer.Response(message, cancel.Token, completed: () => completed++);
        cancel.Cancel();
        Check(buffer.TryTake(out var canceled) && canceled!.CanceledOrExpired, "Canceled result remains publishable.");
        buffer.Written(canceled!);
        Check(completed == 1, "Completion lost.");
        buffer.Response(message, expiresAt: 0);
        Check(buffer.TryTake(out var expired) && expired!.CanceledOrExpired, "Late result remains publishable.");
        buffer.Written(expired!);
        void Reject(byte[] bytes)
        {
            try { ObservationTransportProtocol.Request(bytes, 0); }
            catch (Exception error) when (error is InvalidDataException or JsonException) { checks++; return; }
            throw new InvalidOperationException("Invalid request admitted.");
        }
        Reject("{\"RequestId\":1,\"RequestId\":2,\"Operation\":\"LossStatus\",\"DeadlineMilliseconds\":1}"u8.ToArray());
        Reject(ObservationTransportProtocol.Encode(new ObservationTransportRequest(1, "Stop", 100)));
        Reject(ObservationTransportProtocol.Encode(new ObservationTransportRequest(1, "CaptureOnce", 5001)));
        Console.WriteLine($"PASS transport buffer/protocol: {checks} checks; component evidence only.");
        return 0;
    }
}
