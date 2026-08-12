namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            CommandOptions options = CommandOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (options.ListOnly)
            {
                foreach (IVirtualizationScenario scenario in ScenarioCatalog.All)
                    Console.WriteLine($"{scenario.Id,-28} {scenario.SurfaceKind,-16} {scenario.Description}");
                return 0;
            }

            if (options.WorkerScenarioId is not null)
                return await WorkerHost.RunAsync(options);

            IReadOnlyList<IVirtualizationScenario> scenarios =
                string.Equals(options.Command, "matrix", StringComparison.OrdinalIgnoreCase)
                    ? ScenarioCatalog.All
                    : [ScenarioCatalog.Resolve(options.Command)];

            var controller = new IsolatedRunController();
            BatchRunResult result = await controller.RunAsync(scenarios, options);
            PrintSummary(result);
            return result.Succeeded ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal diagnostic error: {ex}");
            return 1;
        }
    }

    private static void PrintSummary(BatchRunResult result)
    {
        Console.WriteLine();
        Console.WriteLine("Virtualization diagnostic summary");
        foreach (ChildRunResult child in result.Children)
        {
            string status = child.Succeeded ? "PASS" : child.TimedOut ? "TIMEOUT" : "FAIL";
            Console.WriteLine($"  {status,-7} {child.ScenarioId,-28} {child.Elapsed.TotalMilliseconds,8:F0} ms");
            if (!string.IsNullOrWhiteSpace(child.FailureMessage))
                Console.WriteLine($"          {child.FailureMessage}");
        }

        Console.WriteLine($"Artifacts: {result.ArtifactDirectory}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("VirtualizationDiagnosticsConsole [matrix|scenario|list] [options]");
        Console.WriteLine("  --iterations N     Iterations per scenario (default: 150)");
        Console.WriteLine("  --timeout-ms N     Per-scenario process timeout (default: 30000)");
        Console.WriteLine("  --seed N           Deterministic model seed (default: 20260808)");
        Console.WriteLine("  --artifacts PATH   Override artifact root");
        Console.WriteLine("  --fail-fast        Stop matrix after first failed child");
    }
}
