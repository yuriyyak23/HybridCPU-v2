using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HybridCPU.Compiler.Core.IR.Telemetry;

public sealed partial class TelemetryProfileReader
{
    public const string ProfileSchemaV1 = "hybridcpu.compiler-telemetry/v1";
    public const int MaximumProfileJsonCharacters = 1_048_576;

    /// <summary>
    /// Explicit versioned ingress. Invalid/stale data disables advisory heuristics.
    /// Digest covers the compiler-owned projection, independent of JSON property order.
    /// This does not enable any scheduler flag or grant runtime authority.
    /// </summary>
    public static TelemetryProfileReader CreateVersionedJson(
        string json, string expectedProgramHash, out string? canonicalDigest)
    {
        canonicalDigest = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumProfileJsonCharacters ||
            string.IsNullOrWhiteSpace(expectedProgramHash)) return CreateEmpty();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("Schema", out var schema) || schema.GetString() != ProfileSchemaV1 ||
                !root.TryGetProperty("Profile", out var payload)) return CreateEmpty();
            // Validate raw input too: duplicate members cannot silently become last-writer-wins.
            using var raw = new MemoryStream();
            using (var writer = new Utf8JsonWriter(raw)) WriteCanonical(writer, root);
            var profile = payload.Deserialize<TypedSlotTelemetryProfile>(new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            });
            if (profile is null || profile.ProgramHash != expectedProgramHash ||
                profile.TotalInjectionsPerClass is null || profile.TotalRejectsPerClass is null ||
                profile.RejectsByReason is null || profile.PerVtInjectionCounts is null ||
                profile.TotalInjectionsPerClass.Keys.Any(k => !Enum.IsDefined(k)) ||
                profile.TotalRejectsPerClass.Keys.Any(k => !Enum.IsDefined(k)) ||
                profile.RejectsByReason.Keys.Any(k => !Enum.IsDefined(k))) return CreateEmpty();
            using var projection = JsonDocument.Parse(JsonSerializer.Serialize(profile));
            using var canonical = new MemoryStream();
            using (var writer = new Utf8JsonWriter(canonical))
            {
                writer.WriteStartObject();
                writer.WriteString("Schema", ProfileSchemaV1);
                writer.WritePropertyName("Profile");
                WriteCanonical(writer, projection.RootElement);
                writer.WriteEndObject();
            }
            canonicalDigest = Convert.ToHexString(SHA256.HashData(canonical.ToArray()));
            return new TelemetryProfileReader(profile);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return CreateEmpty();
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value, string? member = null)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate profile member.");
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value, property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                double number = value.GetDouble();
                if (!double.IsFinite(number) || number < 0 ||
                    (member is not null && (member.Contains("Density", StringComparison.Ordinal) ||
                        member.Contains("Utilization", StringComparison.Ordinal) || member.EndsWith("Rate", StringComparison.Ordinal)) && number > 1))
                    throw new JsonException("Invalid telemetry number.");
                if (value.TryGetInt64(out long integer)) writer.WriteNumberValue(integer);
                else writer.WriteNumberValue(number);
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
