using System.Diagnostics;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal static class WorkerHost
{
    public static async Task<int> RunAsync(CommandOptions options)
    {
        IVirtualizationScenario scenario = ScenarioCatalog.Resolve(options.WorkerScenarioId!);
        var artifacts = new ArtifactStore(options.WorkerArtifactDirectory!);
        artifacts.EnsureDirectory();
        var profile = new ScenarioProfile(
            "virtualization-diagnostic-profile/v1",
            scenario.Id,
            scenario.SurfaceKind,
            options.Iterations,
            options.Seed);
        artifacts.WriteJson("profile.json", profile);
        artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
            "virtualization-diagnostic-heartbeat/v1", scenario.Id, "Starting", Environment.ProcessId,
            DateTimeOffset.UtcNow, 0, 0, scenario.Description));

        var stopwatch = Stopwatch.StartNew();
        int completed = 0;
        long assertions = 0;
        IReadOnlyDictionary<string, long> counters = new Dictionary<string, long>();
        Exception? failure = null;

        try
        {
            using var context = new ScenarioExecutionContext(profile, artifacts);
            try
            {
                await scenario.ExecuteAsync(context, CancellationToken.None);
            }
            finally
            {
                completed = context.CompletedIterations;
                assertions = context.AssertionCount;
                counters = new Dictionary<string, long>(context.Counters);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        stopwatch.Stop();
        string tracePath = artifacts.PathFor("trace.ndjson");
        string traceHash = File.Exists(tracePath) ? ArtifactStore.Sha256(tracePath) : string.Empty;
        bool succeeded = failure is null && completed == options.Iterations;
        var result = new ScenarioResult(
            "virtualization-diagnostic-result/v1", scenario.Id, scenario.SurfaceKind, succeeded,
            options.Iterations, completed, assertions, failure?.Message, failure?.GetType().FullName,
            traceHash, stopwatch.Elapsed, counters);
        artifacts.WriteJson("result.json", result);
        artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
            "virtualization-diagnostic-heartbeat/v1", scenario.Id, succeeded ? "Succeeded" : "Failed",
            Environment.ProcessId, DateTimeOffset.UtcNow, completed, assertions, failure?.Message));

        if (failure is not null)
            Console.Error.WriteLine(failure);
        else
            Console.WriteLine($"{scenario.Id}: {completed} iterations, {assertions} assertions, trace {traceHash}");
        return succeeded ? 0 : 1;
    }
}
