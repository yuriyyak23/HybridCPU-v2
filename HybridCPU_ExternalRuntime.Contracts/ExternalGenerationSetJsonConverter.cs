using System.Text.Json;
using System.Text.Json.Serialization;

namespace HybridCPU.ExternalRuntime.Contracts;

// Wire encoding does not expose a provider component model to CPU consumers.
internal sealed class ExternalGenerationSetJsonConverter : JsonConverter<ExternalGenerationSet>
{
    public ExternalGenerationSetJsonConverter() { }
    public override ExternalGenerationSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Snapshot object required.");
        var names = root.EnumerateObject().Select(x => x.Name).ToArray();
        if (names.Length != 2 || names.Distinct().Count() != 2 || !names.Contains("version") || !names.Contains("tokens"))
            throw new JsonException("Exact snapshot schema required.");
        try
        {
            var version = root.GetProperty("version");
            if (version.ValueKind != JsonValueKind.Array || version.GetArrayLength() != 3) throw new JsonException("Version triple required.");
            var values = version.EnumerateArray().Select(x => x.GetUInt16()).ToArray();
            var tokens = root.GetProperty("tokens").EnumerateArray().Select(x => x.GetGuid()).ToArray();
            return new(new(values[0], values[1], values[2]), tokens);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new JsonException("Invalid snapshot.", exception);
        }
    }
    public override void Write(Utf8JsonWriter writer, ExternalGenerationSet value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WritePropertyName("version"); writer.WriteStartArray();
        writer.WriteNumberValue(value.ContractVersion.Major); writer.WriteNumberValue(value.ContractVersion.Minor);
        writer.WriteNumberValue(value.ContractVersion.Patch); writer.WriteEndArray();
        writer.WritePropertyName("tokens"); writer.WriteStartArray();
        foreach (var token in value.CopyTokens()) writer.WriteStringValue(token);
        writer.WriteEndArray(); writer.WriteEndObject();
    }
}
