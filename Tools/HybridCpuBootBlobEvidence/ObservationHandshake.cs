using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record ObservationHostIdentity(Guid InstanceId, int ProcessId, DateTimeOffset ProcessStartedAtUtc, string Name);
internal sealed record ObservationHello(string Schema, ObservationHostIdentity Host, Guid Generation);
internal sealed record ObservationCapabilities(bool CaptureOnce, bool Subscribe, bool ExecutionControl,
    bool MemoryReads, int MaximumPayloadBytes, int MaximumPendingRequests,
    string ImageIdentity, string GuestRunIdentity, string GcEvidence);
internal sealed record ObservationWelcome(ObservationHello Hello, Guid ConnectionId, ObservationCapabilities Capabilities);

internal static class ObservationHandshake
{
    public const string Schema = "hybridcpu.doom-host-observation-transport/v1";
    // The transport test currently exposes handshake only. Enable capabilities only after wiring them.
    public static ObservationCapabilities HandshakeOnly => new(false, false, false, false,
        ObservationFrameTransport.MaximumPayloadBytes, 1, "Unavailable", "Unavailable", "Unavailable");
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static ObservationHello Validate(ReadOnlyMemory<byte> payload, ObservationHello expected)
    {
        if (payload.Length is 0 or > ObservationFrameTransport.MaximumPayloadBytes)
            throw new InvalidDataException("Handshake payload size rejected.");
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateFields(document.RootElement);
        var hello = JsonSerializer.Deserialize<ObservationHello>(payload.Span, Options)
            ?? throw new InvalidDataException("Handshake absent.");
        if (hello.Schema != Schema || expected.Schema != Schema)
            throw new InvalidDataException("Handshake version mismatch.");
        if (hello.Host is null || hello.Host.InstanceId == Guid.Empty || hello.Host.ProcessId <= 0 ||
            string.IsNullOrWhiteSpace(hello.Host.Name) || hello.Host.Name.Length > 128 ||
            hello.Host.ProcessStartedAtUtc.Offset != TimeSpan.Zero || hello.Host.ProcessStartedAtUtc == default ||
            hello.Generation == Guid.Empty)
            throw new InvalidDataException("Handshake identity incomplete.");
        if (hello != expected) throw new InvalidDataException("Host identity or endpoint generation mismatch.");
        return hello;
    }

    public static ObservationWelcome ValidateWelcome(ReadOnlyMemory<byte> payload, ObservationHello expected,
        ObservationCapabilities expectedCapabilities)
    {
        if (payload.Length is 0 or > ObservationFrameTransport.MaximumPayloadBytes)
            throw new InvalidDataException("Welcome payload size rejected.");
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateFields(document.RootElement);
        var welcome = JsonSerializer.Deserialize<ObservationWelcome>(payload.Span, Options)
            ?? throw new InvalidDataException("Welcome absent.");
        Validate(JsonSerializer.SerializeToUtf8Bytes(welcome.Hello), expected);
        if (welcome.ConnectionId == Guid.Empty || welcome.Capabilities != expectedCapabilities)
            throw new InvalidDataException("Connection identity or capabilities mismatch.");
        return welcome;
    }

    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate handshake field.");
                RejectDuplicateFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) RejectDuplicateFields(value);
    }
}
