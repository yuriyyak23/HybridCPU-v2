using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using CpuInterfaceBridge.Diagnostics;

internal sealed record ObservationTransportIdentity(Guid InstanceId, int ProcessId, DateTimeOffset ProcessStartUtc,
    string Name, string UserSid, string Machine, string AssemblyVersion, string AssemblySha256,
    string RuntimeVersion, string Architecture);
internal sealed record ObservationTransportLimits(int Connections, int PendingPerConnection, int PendingGlobal,
    int CapturesPerBoundary, int Subscriptions, int EventsPerSubscription, int FrameBytes, int OutgoingBytes,
    int DeadlineMilliseconds, int IoTimeoutMilliseconds);
internal sealed record ObservationTransportManifest(string Schema, string SnapshotSchema, string PipeName,
    Guid EndpointGeneration, ObservationTransportIdentity Host, ObservationTransportLimits Limits, string[] Capabilities);
internal sealed record ObservationTransportBinding(Guid SegmentId, Guid Generation, ulong EntryAddress, string Kind,
    HostObservationDescriptor Bridge, string RunId, string ImageSha256, string LoaderStatus)
{
    public string GcEvidence => "Unavailable";
    public string Qualification => "Unavailable";
}
internal sealed record ObservationTransportRequest(long RequestId, string Operation, int DeadlineMilliseconds,
    Guid SegmentId = default, Guid Generation = default, long TargetRequestId = 0, Guid SubscriptionId = default);
internal sealed record ObservationTransportMessage(string Kind, Guid ConnectionId, long RequestId, string Status,
    ObservationTransportBinding? Binding = null, HostCaptureResult? Capture = null, object? Detail = null,
    Guid SubscriptionId = default, long Sequence = 0, long Dropped = 0);

internal static class ObservationTransportProtocol
{
    public const string Schema = "hybridcpu.doom-host-readonly-pipe/v1";
    public static readonly ObservationTransportLimits Limits = new(8, 16, 64, 1, 4, 128, 1048576, 4194304, 5000, 5000);
    private static readonly string[] Operations = ["Handshake", "CaptureOnce", "Subscribe", "Unsubscribe", "CancelRequest", "LossStatus", "Disconnect"];
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static ObservationTransportManifest CreateManifest(string pipeName)
    {
        using var process = Process.GetCurrentProcess();
        using var user = WindowsIdentity.GetCurrent();
        var assembly = Assembly.GetExecutingAssembly();
        return new(Schema, HostObservationDescriptor.CurrentSchema, pipeName, Guid.NewGuid(),
            new(Guid.NewGuid(), Environment.ProcessId, new DateTimeOffset(process.StartTime.ToUniversalTime()),
                "HybridCpuBootBlobEvidence", user.User!.Value, Environment.MachineName,
                assembly.GetName().Version!.ToString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(),
                RuntimeInformation.FrameworkDescription, RuntimeInformation.ProcessArchitecture.ToString()), Limits, Operations.ToArray());
    }

    public static byte[] Encode<T>(T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length is 0 or > 1048576) throw new InvalidDataException("FrameTooLarge");
        return bytes;
    }

    public static T Decode<T>(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > 1048576) throw new InvalidDataException("FrameTooLarge");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        ValidateTree(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes.Span, Options) ?? throw new InvalidDataException("EmptyJson");
    }

    private static void ValidateTree(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Length > 128 || !fields.Add(property.Name)) throw new InvalidDataException("DuplicateOrLongField");
                ValidateTree(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() > 4096) throw new InvalidDataException("ArrayTooLarge");
            foreach (var item in element.EnumerateArray()) ValidateTree(item);
        }
        else if (element.ValueKind == JsonValueKind.String && element.GetString()!.Length > 4096)
            throw new InvalidDataException("StringTooLong");
    }

    public static void ValidateManifest(ObservationTransportManifest manifest)
    {
        if (manifest.Schema != Schema || manifest.SnapshotSchema != HostObservationDescriptor.CurrentSchema ||
            manifest.EndpointGeneration == Guid.Empty || manifest.Host is not { InstanceId: var id, ProcessId: > 0 } || id == Guid.Empty ||
            manifest.Host.ProcessStartUtc == default || manifest.Host.ProcessStartUtc.Offset != TimeSpan.Zero ||
            !manifest.PipeName.StartsWith("hybridcpu-doom-observe-", StringComparison.Ordinal) ||
            manifest.PipeName.Length > 100 || manifest.PipeName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
            manifest.Limits != Limits || manifest.Capabilities is null || !manifest.Capabilities.SequenceEqual(Operations) ||
            manifest.Host.AssemblySha256.Length != 64 || manifest.Host.AssemblySha256.Any(c => !char.IsAsciiHexDigit(c)) ||
            string.IsNullOrWhiteSpace(manifest.Host.Name) || string.IsNullOrWhiteSpace(manifest.Host.AssemblyVersion) ||
            string.IsNullOrWhiteSpace(manifest.Host.RuntimeVersion) || string.IsNullOrWhiteSpace(manifest.Host.Architecture) ||
            string.IsNullOrWhiteSpace(manifest.Host.Machine) || string.IsNullOrWhiteSpace(manifest.Host.UserSid))
            throw new InvalidDataException("ManifestRejected");
        _ = new SecurityIdentifier(manifest.Host.UserSid);
    }

    public static void ValidateHandshake(ReadOnlyMemory<byte> payload, ObservationTransportManifest expected)
    {
        var manifest = Decode<ObservationTransportManifest>(payload);
        ValidateManifest(manifest);
        ValidateManifest(expected);
        using var left = JsonDocument.Parse(Encode(manifest));
        using var right = JsonDocument.Parse(Encode(expected));
        if (!JsonElement.DeepEquals(left.RootElement, right.RootElement)) throw new InvalidDataException("IdentityOrCapabilityMismatch");
    }

    public static ObservationTransportRequest Request(ReadOnlyMemory<byte> payload, long previousId)
    {
        var request = Decode<ObservationTransportRequest>(payload);
        if (request.RequestId <= previousId || request.RequestId <= 0 ||
            request.DeadlineMilliseconds is < 1 or > 5000 || !Operations.Contains(request.Operation) || request.Operation == "Handshake")
            throw new InvalidDataException("RequestRejected");
        return request;
    }
}
