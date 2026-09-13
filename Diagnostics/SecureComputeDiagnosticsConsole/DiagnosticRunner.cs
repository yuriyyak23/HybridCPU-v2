using System.Diagnostics;
using System.Reflection;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class IsolatedRunController
{
    public async Task<BatchRunResult> RunAsync(
        IReadOnlyList<ISecureComputeScenario> scenarios,
        CommandOptions options)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        string repositoryRoot = RepositoryLocator.FindRoot();
        RepositorySnapshot repository = RepositorySnapshotReader.Read(repositoryRoot);
        string root = options.ArtifactRoot is null
            ? Path.Combine(repositoryRoot, "TestResults", "SecureComputeDiagnosticsConsole")
            : Path.GetFullPath(options.ArtifactRoot);
        string command = scenarios.Count == ScenarioCatalog.All.Count ? "matrix" : scenarios[0].Id;
        string directory = Path.Combine(root, $"{started:yyyyMMdd_HHmmss_fff}_{command}");
        var batchArtifacts = new ArtifactStore(directory);
        batchArtifacts.EnsureDirectory();
        var children = new List<ChildRunResult>();

        ConsoleReporter.PrintRunHeader(command, scenarios.Count, options, repositoryRoot, repository, directory);
        foreach (ISecureComputeScenario scenario in scenarios)
        {
            string childDirectory = Path.Combine(directory, scenario.Id);
            ChildRunResult child = await RunChildAsync(scenario, childDirectory, options);
            children.Add(child);
            ConsoleReporter.PrintChild(child, options.Compact);
            if (!child.Succeeded && options.FailFast)
                break;
        }

        var result = new BatchRunResult(
            "securecompute-diagnostic-batch/v2",
            command,
            children.Count == scenarios.Count && children.All(child => child.Succeeded),
            started,
            DateTimeOffset.UtcNow,
            repositoryRoot,
            repository,
            directory,
            scenarios.Count,
            children);
        batchArtifacts.WriteJson("manifest.json", result);
        ConsoleReporter.PrintSummary(result, options.Compact);
        return result;
    }

    private static async Task<ChildRunResult> RunChildAsync(
        ISecureComputeScenario scenario,
        string artifactDirectory,
        CommandOptions options)
    {
        var artifacts = new ArtifactStore(artifactDirectory);
        artifacts.EnsureDirectory();
        ProcessStartInfo startInfo = CreateWorkerStartInfo(scenario.Id, artifactDirectory, options);
        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool timedOut = false;
        using var timeout = new CancellationTokenSource(options.TimeoutMs);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        string capturedOut = await stdout;
        string capturedError = await stderr;
        File.WriteAllText(artifacts.PathFor("stdout.log"), capturedOut);
        File.WriteAllText(artifacts.PathFor("stderr.log"), capturedError);
        stopwatch.Stop();
        ScenarioResult? scenarioResult = artifacts.ReadJson<ScenarioResult>("result.json");
        bool succeeded = !timedOut && process.ExitCode == 0 && scenarioResult?.Succeeded == true;
        string? failure = timedOut
            ? $"Worker exceeded {options.TimeoutMs} ms timeout."
            : scenarioResult?.FailureMessage ?? (process.ExitCode == 0 ? null : capturedError.Trim());
        return new(
            scenario.Id,
            scenario.Description,
            scenario.SurfaceKind,
            succeeded,
            timedOut,
            process.ExitCode,
            stopwatch.Elapsed,
            artifactDirectory,
            failure,
            scenarioResult);
    }

    private static ProcessStartInfo CreateWorkerStartInfo(
        string scenarioId,
        string artifactDirectory,
        CommandOptions options)
    {
        string assemblyPath = Assembly.GetEntryAssembly()!.Location;
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException("Current process path is unavailable.");
        bool dotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (dotnetHost)
            start.ArgumentList.Add(assemblyPath);
        start.ArgumentList.Add("--worker-scenario");
        start.ArgumentList.Add(scenarioId);
        start.ArgumentList.Add("--worker-artifacts");
        start.ArgumentList.Add(artifactDirectory);
        start.ArgumentList.Add("--iterations");
        start.ArgumentList.Add(options.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--seed");
        start.ArgumentList.Add(options.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return start;
    }
}

internal static class WorkerHost
{
    public static async Task<int> RunAsync(CommandOptions options)
    {
        ISecureComputeScenario scenario = ScenarioCatalog.Resolve(options.WorkerScenarioId!);
        var artifacts = new ArtifactStore(options.WorkerArtifactDirectory!);
        artifacts.EnsureDirectory();
        var profile = new ScenarioProfile(
            "securecompute-diagnostic-profile/v1",
            scenario.Id,
            scenario.Description,
            scenario.SurfaceKind,
            options.Iterations,
            options.Seed,
            scenario.AuthorityCeiling);
        artifacts.WriteJson("profile.json", profile);
        artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
            "securecompute-diagnostic-heartbeat/v1",
            scenario.Id,
            "Starting",
            Environment.ProcessId,
            DateTimeOffset.UtcNow,
            0,
            0,
            scenario.Description));

        var stopwatch = Stopwatch.StartNew();
        int completed = 0;
        long assertions = 0;
        IReadOnlyDictionary<string, long> counters = new Dictionary<string, long>();
        IReadOnlyList<DiagnosticFinding> findings = [];
        long traceEventCount = 0;
        IReadOnlyDictionary<string, long> traceEventsByName = new Dictionary<string, long>();
        TraceObservation? lastTraceObservation = null;
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
                findings = context.Findings;
                traceEventCount = context.TraceEventCount;
                traceEventsByName = new Dictionary<string, long>(context.TraceEventsByName);
                lastTraceObservation = context.LastTraceObservation;
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
            "securecompute-diagnostic-result/v2",
            scenario.Id,
            scenario.Description,
            scenario.SurfaceKind,
            succeeded,
            options.Iterations,
            completed,
            assertions,
            scenario.AuthorityCeiling,
            failure?.Message,
            failure?.GetType().FullName,
            traceHash,
            traceEventCount,
            traceEventsByName,
            lastTraceObservation,
            stopwatch.Elapsed,
            counters,
            findings);
        artifacts.WriteJson("result.json", result);
        artifacts.WriteJson("heartbeat.json", new HeartbeatRecord(
            "securecompute-diagnostic-heartbeat/v1",
            scenario.Id,
            succeeded ? "Succeeded" : "Failed",
            Environment.ProcessId,
            DateTimeOffset.UtcNow,
            completed,
            assertions,
            failure?.Message));

        if (failure is not null)
            Console.Error.WriteLine(failure);
        return succeeded ? 0 : 1;
    }
}
