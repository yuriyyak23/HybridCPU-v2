using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal interface ISecureComputeScenario
{
    string Id { get; }
    string Description { get; }
    DiagnosticSurfaceKind SurfaceKind { get; }
    string AuthorityCeiling { get; }
    Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken);
}

internal sealed class ScenarioExecutionContext : IDisposable
{
    private static readonly JsonSerializerOptions TraceOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly StreamWriter _trace;
    private readonly ArtifactStore _artifacts;
    private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DiagnosticFinding> _findings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _traceEventsByName = new(StringComparer.Ordinal);

    public ScenarioExecutionContext(ScenarioProfile profile, ArtifactStore artifacts)
    {
        Profile = profile;
        _artifacts = artifacts;
        _trace = new StreamWriter(artifacts.PathFor("trace.ndjson"), append: false, new System.Text.UTF8Encoding(false));
    }

    public ScenarioProfile Profile { get; }
    public int CompletedIterations { get; private set; }
    public long AssertionCount { get; private set; }
    public long TraceEventCount { get; private set; }
    public IReadOnlyDictionary<string, long> Counters => _counters;
    public IReadOnlyDictionary<string, long> TraceEventsByName => _traceEventsByName;
    public TraceObservation? LastTraceObservation { get; private set; }
    public IReadOnlyList<DiagnosticFinding> Findings => _findings.Values
        .OrderByDescending(finding => finding.Severity)
        .ThenBy(finding => finding.Code, StringComparer.Ordinal)
        .ToArray();

    public void Check(bool condition, string invariant)
    {
        AssertionCount++;
        if (!condition)
            throw new ScenarioInvariantException(CompletedIterations, invariant);
    }

    public void Count(string name, long value = 1) =>
        _counters[name] = _counters.GetValueOrDefault(name) + value;

    public void Finding(string code, DiagnosticSeverity severity, string title, string detail) =>
        _findings.TryAdd(code, new DiagnosticFinding(code, severity, title, detail));

    public void Trace(string eventName, params (string Name, object? Value)[] values)
    {
        var data = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        _trace.WriteLine(JsonSerializer.Serialize(new TraceRecord(CompletedIterations, eventName, data), TraceOptions));
        TraceEventCount++;
        _traceEventsByName[eventName] = _traceEventsByName.GetValueOrDefault(eventName) + 1;
        LastTraceObservation = new TraceObservation(
            CompletedIterations,
            eventName,
            values.ToDictionary(
                pair => pair.Name,
                pair => FormatTraceValue(pair.Value),
                StringComparer.Ordinal));
    }

    public void CompleteIteration(string detail)
    {
        CompletedIterations++;
        if (CompletedIterations == 1 || CompletedIterations % 10 == 0 || CompletedIterations == Profile.Iterations)
        {
            _trace.Flush();
            _artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
                "securecompute-diagnostic-heartbeat/v1",
                Profile.ScenarioId,
                "Running",
                Environment.ProcessId,
                DateTimeOffset.UtcNow,
                CompletedIterations,
                AssertionCount,
                detail));
        }
    }

    public void Dispose()
    {
        _trace.Flush();
        _trace.Dispose();
    }

    private static string FormatTraceValue(object? value) => value switch
    {
        null => "<null>",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}

internal sealed class ScenarioInvariantException(int iteration, string invariant)
    : Exception($"Invariant failed at iteration {iteration}: {invariant}");
