using System.Text;
using System.Text.Json;

internal static class ObservationHandshakeRegression
{
    public static int Run()
    {
        var identity = new ObservationHostIdentity(Guid.NewGuid(), Environment.ProcessId,
            DateTimeOffset.UtcNow, "test-host");
        var expected = new ObservationHello(ObservationHandshake.Schema, identity, Guid.NewGuid());
        int checks = 0;
        void Reject(byte[] bytes)
        {
            try { ObservationHandshake.Validate(bytes, expected); }
            catch (Exception ex) when (ex is InvalidDataException or JsonException) { checks++; return; }
            throw new InvalidOperationException("Invalid handshake accepted.");
        }
        if (ObservationHandshake.Validate(JsonSerializer.SerializeToUtf8Bytes(expected), expected) != expected)
            throw new InvalidOperationException("Valid handshake rejected.");
        checks++;
        foreach (var wrong in new[]
        {
            expected with { Schema = "future/v2" },
            expected with { Generation = Guid.NewGuid() },
            expected with { Host = identity with { InstanceId = Guid.NewGuid() } },
            expected with { Host = identity with { ProcessStartedAtUtc = identity.ProcessStartedAtUtc.AddSeconds(1) } },
            expected with { Host = identity with { ProcessId = identity.ProcessId + 1 } },
            expected with { Host = identity with { Name = "other" } }
        }) Reject(JsonSerializer.SerializeToUtf8Bytes(wrong));
        string json = JsonSerializer.Serialize(expected);
        Reject(Encoding.UTF8.GetBytes(json.Insert(1, "\"Schema\":\"duplicate\",")));
        Reject(Encoding.UTF8.GetBytes(json.Insert(1, "\"Step\":true,")));
        Reject(Encoding.UTF8.GetBytes("{}"));
        Reject(Encoding.UTF8.GetBytes("null"));
        var welcome = new ObservationWelcome(expected, Guid.NewGuid(), ObservationHandshake.HandshakeOnly);
        _ = ObservationHandshake.ValidateWelcome(JsonSerializer.SerializeToUtf8Bytes(welcome), expected, ObservationHandshake.HandshakeOnly);
        checks++;
        foreach (var invalid in new[] { welcome with { ConnectionId = Guid.Empty },
            welcome with { Capabilities = welcome.Capabilities with { ExecutionControl = true } },
            welcome with { Capabilities = welcome.Capabilities with { MaximumPayloadBytes = int.MaxValue } } })
        {
            try { ObservationHandshake.ValidateWelcome(JsonSerializer.SerializeToUtf8Bytes(invalid), expected, ObservationHandshake.HandshakeOnly); }
            catch (InvalidDataException) { checks++; continue; }
            throw new InvalidOperationException("Invalid welcome accepted.");
        }
        Console.WriteLine($"PASS handshake validation: {checks} checks; identity is not authentication.");
        return 0;
    }
}
