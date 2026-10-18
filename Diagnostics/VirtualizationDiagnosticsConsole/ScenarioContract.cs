using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal interface IVirtualizationScenario
{
    string Id { get; }
    string Description { get; }
    DiagnosticSurfaceKind SurfaceKind { get; }
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
    private readonly string _scenarioId;
    private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);

    public ScenarioExecutionContext(ScenarioProfile profile, ArtifactStore artifacts)
    {
        Profile = profile;
        _artifacts = artifacts;
        _scenarioId = profile.ScenarioId;
        _trace = new StreamWriter(artifacts.PathFor("trace.ndjson"), append: false, new System.Text.UTF8Encoding(false));
    }

    public ScenarioProfile Profile { get; }
    public int CompletedIterations { get; private set; }
    public long AssertionCount { get; private set; }
    public IReadOnlyDictionary<string, long> Counters => _counters;

    public void Check(bool condition, string invariant)
    {
        AssertionCount++;
        if (!condition)
            throw new ScenarioInvariantException(CompletedIterations, invariant);
    }

    public void Count(string name, long value = 1) =>
        _counters[name] = _counters.GetValueOrDefault(name) + value;

    public void Trace(string eventName, params (string Name, object? Value)[] values)
    {
        var data = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        _trace.WriteLine(JsonSerializer.Serialize(new TraceRecord(CompletedIterations, eventName, data), TraceOptions));
    }

    public void CompleteIteration(string detail)
    {
        CompletedIterations++;
        if (CompletedIterations == 1 || CompletedIterations % 10 == 0 || CompletedIterations == Profile.Iterations)
        {
            _trace.Flush();
            _artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
                "virtualization-diagnostic-heartbeat/v1",
                _scenarioId,
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
}

internal sealed class ScenarioInvariantException(int iteration, string invariant)
    : Exception($"Invariant failed at iteration {iteration}: {invariant}");
