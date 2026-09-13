using System.Security.Cryptography;
using System.Text.Json;

// Offline comparison of explicit captures. Never polls, attaches or controls execution.
internal static class ObservationProgressComparison
{
    public static int Run(string before, string after)
    {
        try
        {
            byte[] first = File.ReadAllBytes(before), second = File.ReadAllBytes(after);
            using var a = JsonDocument.Parse(first);
            using var b = JsonDocument.Parse(second);
            var result = Compare(a.RootElement, b.RootElement);
            Console.WriteLine(JsonSerializer.Serialize(new { Result = result,
                BeforeSha256 = Convert.ToHexString(SHA256.HashData(first)),
                AfterSha256 = Convert.ToHexString(SHA256.HashData(second)), Qualification = "Unavailable",
                Meaning = "Observed cycles and finalized write-back lanes only; no CIL instruction count, loop diagnosis or guest completion inferred." }));
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException)
        { Console.Error.WriteLine("Comparison unavailable: " + error.Message); return 4; }
    }

    internal static object Compare(JsonElement a, JsonElement b)
    {
        static JsonElement Observation(JsonElement value)
        {
            if (value.GetProperty("Disconnect").GetString() != "Disconnected") throw new InvalidDataException("Incomplete capture.");
            var observation = value.GetProperty("Observation");
            if (observation.GetProperty("Status").GetString() != "Observed") throw new InvalidDataException("Capture unavailable.");
            return observation;
        }
        var x = Observation(a); var y = Observation(b);
        var xb = x.GetProperty("Binding"); var yb = y.GetProperty("Binding");
        foreach (string field in new[] { "RunId", "ImageSha256", "SegmentId", "Generation", "Kind", "LoaderStatus" })
        {
            string? left = xb.GetProperty(field).GetString(), right = yb.GetProperty(field).GetString();
            if (string.IsNullOrWhiteSpace(left) || left != right) throw new InvalidDataException("Binding mismatch: " + field);
        }
        if (xb.GetProperty("LoaderStatus").GetString() != "Success") throw new InvalidDataException("Loader not successful.");
        if (!JsonElement.DeepEquals(a.GetProperty("Manifest"), b.GetProperty("Manifest")))
            throw new InvalidDataException("Host endpoint identity mismatch.");
        var xf = x.GetProperty("Capture").GetProperty("Frame"); var yf = y.GetProperty("Capture").GetProperty("Frame");
        foreach (var pair in new[] { (Frame: xf, Binding: xb), (Frame: yf, Binding: yb) })
            if (!JsonElement.DeepEquals(pair.Frame.GetProperty("Host"), pair.Binding.GetProperty("Bridge").GetProperty("Identity")))
                throw new InvalidDataException("Frame host mismatch.");
        DateTimeOffset start = xf.GetProperty("CapturedAtUtc").GetDateTimeOffset(), end = yf.GetProperty("CapturedAtUtc").GetDateTimeOffset();
        if (end <= start) throw new InvalidDataException("Stale or reversed timestamps.");
        var xs = xf.GetProperty("Snapshot"); var ys = yf.GetProperty("Snapshot");
        if (xs.GetProperty("CoreId").GetInt32() != ys.GetProperty("CoreId").GetInt32() ||
            xs.GetProperty("ActiveVirtualThreadId").GetInt32() != ys.GetProperty("ActiveVirtualThreadId").GetInt32())
            throw new InvalidDataException("Core/VT mismatch.");
        ulong first = xs.GetProperty("CycleCount").GetUInt64(), last = ys.GetProperty("CycleCount").GetUInt64();
        if (last < first) throw new InvalidDataException("Cycle counter regressed.");
        static ulong? Retired(JsonElement snapshot) =>
            !snapshot.TryGetProperty("PipelineInstructionsRetired", out var value) || value.ValueKind == JsonValueKind.Null
                ? null : value.GetUInt64();
        ulong? retiredBefore = Retired(xs), retiredAfter = Retired(ys);
        if (retiredBefore.HasValue && retiredAfter.HasValue && retiredAfter.Value < retiredBefore.Value)
            throw new InvalidDataException("Retirement counter regressed/reset; delta unavailable.");
        ulong? retiredDelta = retiredBefore.HasValue && retiredAfter.HasValue ? retiredAfter.Value - retiredBefore.Value : null;
        return new { SegmentId = xb.GetProperty("SegmentId").GetString(), CycleDelta = last - first,
            RetiredLaneDelta = retiredDelta, RetirementEvidence = retiredDelta.HasValue ? "Available" : "Unavailable",
            ElapsedSeconds = (end - start).TotalSeconds, BeforePc = xs.GetProperty("LiveInstructionPointer").GetUInt64(),
            AfterPc = ys.GetProperty("LiveInstructionPointer").GetUInt64(),
            Status = last > first ? "CyclesAdvanced" : "NoCycleDelta" };
    }
}
