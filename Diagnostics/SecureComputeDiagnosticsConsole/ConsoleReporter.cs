namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal static class ConsoleReporter
{
    private const int RuleWidth = 96;

    public static void PrintRunHeader(
        string command,
        int scenarioCount,
        CommandOptions options,
        string repositoryRoot,
        RepositorySnapshot repository,
        string artifactDirectory)
    {
        Console.WriteLine("SecureCompute HybridCPU-v2 diagnostics");
        Console.WriteLine(new string('=', RuleWidth));
        Console.WriteLine($"Command          : {command}");
        Console.WriteLine($"Scenarios        : {scenarioCount}");
        Console.WriteLine($"Iterations       : {options.Iterations} per scenario");
        Console.WriteLine($"Timeout          : {options.TimeoutMs} ms per process");
        Console.WriteLine($"Seed             : {options.Seed}");
        Console.WriteLine($"Output mode      : {(options.Compact ? "compact" : "detailed")}");
        Console.WriteLine($"SDK / runtime    : {repository.DotnetSdk} / {repository.Runtime}");
        Console.WriteLine($"Operating system : {repository.OperatingSystem}");
        Console.WriteLine($"Repository       : {repositoryRoot}");
        Console.WriteLine($"Git commit       : {repository.CommitSha}");
        Console.WriteLine($"Working tree     : {(repository.IsDirty ? "dirty" : "clean")}");
        Console.WriteLine($"Artifact root    : {artifactDirectory}");
        Console.WriteLine("Authority ceiling: diagnostics/policy evidence only; no execution or release authority");
        Console.WriteLine();
    }

    public static void PrintChild(ChildRunResult child, bool compact)
    {
        string status = child.Succeeded ? "PASS" : child.TimedOut ? "TIMEOUT" : "FAIL";
        Console.WriteLine($"[{status}] {child.ScenarioId} ({child.SurfaceKind}) - {child.Elapsed.TotalMilliseconds:F0} ms");
        if (compact)
            return;

        Console.WriteLine($"  Purpose          : {child.Description}");
        Console.WriteLine($"  Worker process   : exit={DisplayExitCode(child.ExitCode)}, timeout={YesNo(child.TimedOut)}, " +
                          $"wall={child.Elapsed.TotalMilliseconds:F0} ms");

        ScenarioResult? result = child.Result;
        if (result is null)
        {
            Console.WriteLine("  Result artifact  : <missing or unreadable>");
        }
        else
        {
            double completion = Percentage(result.CompletedIterations, result.RequestedIterations);
            double assertionsPerIteration = result.CompletedIterations == 0
                ? 0
                : (double)result.AssertionCount / result.CompletedIterations;
            int blockers = CountFindings(result, DiagnosticSeverity.Blocker);
            int warnings = CountFindings(result, DiagnosticSeverity.Warning);
            int information = CountFindings(result, DiagnosticSeverity.Information);
            int zeroCounters = result.Counters.Count(pair => pair.Value == 0);

            Console.WriteLine($"  Scenario result  : {(result.Succeeded ? "PASS" : "FAIL")} ({completion:F1}% complete)");
            Console.WriteLine($"  Iterations       : {result.CompletedIterations}/{result.RequestedIterations}");
            Console.WriteLine($"  Assertions       : {result.AssertionCount} total; {assertionsPerIteration:F2} per completed iteration");
            Console.WriteLine($"  Worker elapsed   : {result.Elapsed.TotalMilliseconds:F0} ms");
            Console.WriteLine($"  Findings         : {blockers} blocker(s), {warnings} warning(s), {information} info");
            Console.WriteLine($"  Trace            : {result.TraceEventCount} event(s); SHA-256 {DisplayHash(result.TraceSha256)}");
            Console.WriteLine($"  Authority ceiling: {result.AuthorityCeiling}");

            if (result.TraceEventsByName.Count != 0)
            {
                Console.WriteLine("  Trace event distribution:");
                foreach ((string name, long value) in result.TraceEventsByName.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    Console.WriteLine($"    {name,-52} {value,10}");
            }

            if (result.LastTraceObservation is not null)
            {
                Console.WriteLine($"  Last observation : iteration={result.LastTraceObservation.Iteration}, " +
                                  $"event={result.LastTraceObservation.Event}");
                foreach ((string name, string value) in result.LastTraceObservation.Data.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    Console.WriteLine($"    {name,-24} = {value}");
            }

            if (result.Counters.Count != 0)
            {
                Console.WriteLine($"  Counters         : {result.Counters.Count} total; " +
                                  $"{result.Counters.Count - zeroCounters} non-zero; {zeroCounters} zero");
                foreach ((string name, long value) in result.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    Console.WriteLine($"    {name,-52} {value,10}");
            }

            if (result.Findings.Count != 0)
            {
                Console.WriteLine("  Findings detail:");
                foreach (DiagnosticFinding finding in result.Findings)
                {
                    Console.WriteLine($"    [{Severity(finding.Severity),-7}] {finding.Code} - {finding.Title}");
                    Console.WriteLine($"              {finding.Detail}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(child.FailureMessage))
            Console.WriteLine($"  Failure          : {child.FailureMessage}");
        Console.WriteLine($"  Result JSON      : {Path.Combine(child.ArtifactDirectory, "result.json")}");
        Console.WriteLine($"  Trace NDJSON     : {Path.Combine(child.ArtifactDirectory, "trace.ndjson")}");
        Console.WriteLine($"  Worker logs      : {Path.Combine(child.ArtifactDirectory, "stdout.log")} | " +
                          Path.Combine(child.ArtifactDirectory, "stderr.log"));
        Console.WriteLine();
    }

    public static void PrintSummary(BatchRunResult result, bool compact)
    {
        long assertions = result.Children.Sum(child => child.Result?.AssertionCount ?? 0);
        long traceEvents = result.Children.Sum(child => child.Result?.TraceEventCount ?? 0);
        int blockers = result.Children.Sum(child => CountFindings(child.Result, DiagnosticSeverity.Blocker));
        int warnings = result.Children.Sum(child => CountFindings(child.Result, DiagnosticSeverity.Warning));
        int information = result.Children.Sum(child => CountFindings(child.Result, DiagnosticSeverity.Information));
        int passed = result.Children.Count(child => child.Succeeded);
        int failed = result.Children.Count(child => !child.Succeeded);
        int timedOut = result.Children.Count(child => child.TimedOut);
        long completedIterations = result.Children.Sum(child => child.Result?.CompletedIterations ?? 0);
        long requestedIterations = result.Children.Sum(child => child.Result?.RequestedIterations ?? 0);
        IReadOnlyDictionary<string, long> aggregateCounters = AggregateCounters(result.Children);

        Console.WriteLine(new string('=', RuleWidth));
        Console.WriteLine("SecureCompute diagnostic summary");
        Console.WriteLine($"Harness result    : {(result.Succeeded ? "PASS" : "FAIL")}");
        Console.WriteLine($"Architecture gate : {(blockers == 0 ? "NO BLOCKER REPORTED (not a release approval)" : $"BLOCKED by {blockers} finding(s)")}");
        Console.WriteLine($"Scenarios         : {result.Children.Count}/{result.RequestedScenarioCount} executed; " +
                          $"{passed} passed; {failed} failed; {timedOut} timed out");
        Console.WriteLine($"Iterations        : {completedIterations}/{requestedIterations} " +
                          $"({Percentage(completedIterations, requestedIterations):F1}% complete)");
        Console.WriteLine($"Assertions        : {assertions}");
        Console.WriteLine($"Trace events      : {traceEvents}");
        Console.WriteLine($"Findings          : {blockers} blocker(s), {warnings} warning(s), {information} info");
        Console.WriteLine($"Elapsed           : {(result.FinishedUtc - result.StartedUtc).TotalMilliseconds:F0} ms");

        if (!compact)
        {
            PrintScenarioTable(result.Children);
            PrintAggregateCounters(aggregateCounters);
            PrintOpenFindings(result.Children);
        }

        Console.WriteLine("Boundary verdict  : positive SecureCompute execution, completion/retire publication,");
        Console.WriteLine("                    compiler secure emission and limited/production release remain denied.");
        Console.WriteLine($"JSON manifest     : {Path.Combine(result.ArtifactDirectory, "manifest.json")}");
        Console.WriteLine($"Artifact directory: {result.ArtifactDirectory}");
    }

    private static void PrintScenarioTable(IReadOnlyList<ChildRunResult> children)
    {
        Console.WriteLine();
        Console.WriteLine("Scenario matrix:");
        Console.WriteLine($"  {"Scenario",-30} {"State",-8} {"Iter",9} {"Checks",8} {"Trace",7} {"B/W/I",11} {"ms",8}");
        Console.WriteLine("  " + new string('-', 86));
        foreach (ChildRunResult child in children)
        {
            ScenarioResult? scenario = child.Result;
            string state = child.Succeeded ? "PASS" : child.TimedOut ? "TIMEOUT" : "FAIL";
            string iterations = scenario is null ? "-" : $"{scenario.CompletedIterations}/{scenario.RequestedIterations}";
            string findings = scenario is null
                ? "-"
                : $"{CountFindings(scenario, DiagnosticSeverity.Blocker)}/" +
                  $"{CountFindings(scenario, DiagnosticSeverity.Warning)}/" +
                  $"{CountFindings(scenario, DiagnosticSeverity.Information)}";
            Console.WriteLine($"  {child.ScenarioId,-30} {state,-8} {iterations,9} " +
                              $"{scenario?.AssertionCount ?? 0,8} {scenario?.TraceEventCount ?? 0,7} " +
                              $"{findings,11} {child.Elapsed.TotalMilliseconds,8:F0}");
        }
    }

    private static void PrintAggregateCounters(IReadOnlyDictionary<string, long> counters)
    {
        if (counters.Count == 0)
            return;
        Console.WriteLine();
        Console.WriteLine("Aggregate counters across executed scenarios:");
        foreach ((string name, long value) in counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {name,-58} {value,12}");
    }

    private static void PrintOpenFindings(IReadOnlyList<ChildRunResult> children)
    {
        DiagnosticFinding[] actionable = children
            .SelectMany(child => (child.Result?.Findings ?? [])
                .Where(finding => finding.Severity != DiagnosticSeverity.Information)
                .Select(finding => finding with { Code = $"{child.ScenarioId}/{finding.Code}" }))
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ToArray();
        if (actionable.Length == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("Open blocker/warning register:");
        foreach (DiagnosticFinding finding in actionable)
            Console.WriteLine($"  [{Severity(finding.Severity),-7}] {finding.Code} - {finding.Title}");
    }

    private static IReadOnlyDictionary<string, long> AggregateCounters(IReadOnlyList<ChildRunResult> children)
    {
        var aggregate = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (ChildRunResult child in children)
        {
            if (child.Result is null)
                continue;
            foreach ((string name, long value) in child.Result.Counters)
                aggregate[name] = aggregate.GetValueOrDefault(name) + value;
        }
        return aggregate;
    }

    private static int CountFindings(ScenarioResult? result, DiagnosticSeverity severity) =>
        result?.Findings.Count(finding => finding.Severity == severity) ?? 0;

    private static double Percentage(long completed, long requested) =>
        requested == 0 ? 0 : completed * 100.0 / requested;

    private static string DisplayHash(string hash) =>
        string.IsNullOrEmpty(hash) ? "<missing>" : hash;

    private static string DisplayExitCode(int? exitCode) =>
        exitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<unavailable>";

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Severity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Blocker => "BLOCKER",
        DiagnosticSeverity.Warning => "WARNING",
        _ => "INFO",
    };
}
