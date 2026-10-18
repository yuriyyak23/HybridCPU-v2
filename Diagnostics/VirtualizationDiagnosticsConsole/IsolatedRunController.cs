using System.Diagnostics;
using System.Reflection;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class IsolatedRunController
{
    public async Task<BatchRunResult> RunAsync(IReadOnlyList<IVirtualizationScenario> scenarios, CommandOptions options)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        string root = options.ArtifactRoot is null
            ? Path.Combine(RepositoryLocator.FindRoot(), "TestResults", "VirtualizationDiagnosticsConsole")
            : Path.GetFullPath(options.ArtifactRoot);
        string command = scenarios.Count == ScenarioCatalog.All.Count ? "matrix" : scenarios[0].Id;
        string directory = Path.Combine(root, $"{started:yyyyMMdd_HHmmss_fff}_{command}");
        var batchArtifacts = new ArtifactStore(directory);
        batchArtifacts.EnsureDirectory();
        var children = new List<ChildRunResult>();

        foreach (IVirtualizationScenario scenario in scenarios)
        {
            string childDirectory = Path.Combine(directory, scenario.Id);
            ChildRunResult child = await RunChildAsync(scenario, childDirectory, options);
            children.Add(child);
            Console.WriteLine($"{(child.Succeeded ? "PASS" : "FAIL")} {scenario.Id} -> {childDirectory}");
            if (!child.Succeeded && options.FailFast)
                break;
        }

        var result = new BatchRunResult(
            "virtualization-diagnostic-batch/v1", command, children.Count == scenarios.Count && children.All(child => child.Succeeded),
            started, DateTimeOffset.UtcNow, directory, children);
        batchArtifacts.WriteJson("manifest.json", result);
        return result;
    }

    private static async Task<ChildRunResult> RunChildAsync(
        IVirtualizationScenario scenario,
        string artifactDirectory,
        CommandOptions options)
    {
        var artifacts = new ArtifactStore(artifactDirectory);
        artifacts.EnsureDirectory();
        string stdoutPath = artifacts.PathFor("stdout.log");
        string stderrPath = artifacts.PathFor("stderr.log");
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
        File.WriteAllText(stdoutPath, capturedOut);
        File.WriteAllText(stderrPath, capturedError);
        stopwatch.Stop();
        ScenarioResult? scenarioResult = artifacts.ReadJson<ScenarioResult>("result.json");
        bool succeeded = !timedOut && process.ExitCode == 0 && scenarioResult?.Succeeded == true;
        string? failure = timedOut
            ? $"Worker exceeded {options.TimeoutMs} ms timeout."
            : scenarioResult?.FailureMessage ?? (process.ExitCode == 0 ? null : capturedError.Trim());
        return new(scenario.Id, scenario.SurfaceKind, succeeded, timedOut, process.ExitCode,
            stopwatch.Elapsed, artifactDirectory, failure);
    }

    private static ProcessStartInfo CreateWorkerStartInfo(string scenarioId, string artifactDirectory, CommandOptions options)
    {
        string assemblyPath = Assembly.GetEntryAssembly()!.Location;
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Current process path is unavailable.");
        bool dotnetHost = string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);
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
