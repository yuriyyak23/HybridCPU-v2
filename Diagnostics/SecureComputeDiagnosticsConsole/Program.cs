namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

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
                Console.WriteLine("SecureCompute diagnostic scenarios");
                foreach (ISecureComputeScenario scenario in ScenarioCatalog.All)
                {
                    Console.WriteLine($"{scenario.Id,-30} {scenario.SurfaceKind,-18} {scenario.Description}");
                    Console.WriteLine($"{string.Empty,-30} ceiling: {scenario.AuthorityCeiling}");
                }
                return 0;
            }

            if (options.WorkerScenarioId is not null)
                return await WorkerHost.RunAsync(options);

            IReadOnlyList<ISecureComputeScenario> scenarios =
                string.Equals(options.Command, "matrix", StringComparison.OrdinalIgnoreCase)
                    ? ScenarioCatalog.All
                    : [ScenarioCatalog.Resolve(options.Command)];
            BatchRunResult result = await new IsolatedRunController().RunAsync(scenarios, options);
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
            Console.Error.WriteLine($"Fatal SecureCompute diagnostic error: {ex}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SecureComputeDiagnosticsConsole [matrix|scenario|list] [options]");
        Console.WriteLine("  --iterations N     Iterations per scenario (default: 10)");
        Console.WriteLine("  --timeout-ms N     Per-scenario process timeout (default: 30000)");
        Console.WriteLine("  --seed N           Deterministic diagnostic seed (default: 20260810)");
        Console.WriteLine("  --artifacts PATH   Override artifact root");
        Console.WriteLine("  --fail-fast        Stop matrix after first failed child");
        Console.WriteLine("  --compact          Print one line per scenario; detailed output is the default");
    }
}
