using System.Globalization;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed record CommandOptions(
    string Command,
    int Iterations,
    int TimeoutMs,
    int Seed,
    string? ArtifactRoot,
    bool FailFast,
    bool ShowHelp,
    bool ListOnly,
    string? WorkerScenarioId,
    string? WorkerArtifactDirectory)
{
    public static CommandOptions Parse(string[] args)
    {
        string command = "matrix";
        int iterations = 150;
        int timeoutMs = 30_000;
        int seed = 20_260_808;
        string? artifactRoot = null;
        string? workerScenario = null;
        string? workerArtifacts = null;
        bool failFast = false;
        bool showHelp = false;
        bool listOnly = false;
        bool commandSeen = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "-h" or "--help": showHelp = true; break;
                case "list": listOnly = true; commandSeen = true; break;
                case "--fail-fast": failFast = true; break;
                case "--iterations": iterations = ParseInt(Next(args, ref index, argument), argument, 1, 100_000); break;
                case "--timeout-ms": timeoutMs = ParseInt(Next(args, ref index, argument), argument, 100, 3_600_000); break;
                case "--seed": seed = ParseInt(Next(args, ref index, argument), argument, int.MinValue, int.MaxValue); break;
                case "--artifacts": artifactRoot = Next(args, ref index, argument); break;
                case "--worker-scenario": workerScenario = Next(args, ref index, argument); break;
                case "--worker-artifacts": workerArtifacts = Next(args, ref index, argument); break;
                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException($"Unknown option '{argument}'.");
                    if (commandSeen)
                        throw new ArgumentException($"Unexpected argument '{argument}'.");
                    command = argument;
                    commandSeen = true;
                    break;
            }
        }

        if ((workerScenario is null) != (workerArtifacts is null))
            throw new ArgumentException("Worker invocation requires both --worker-scenario and --worker-artifacts.");

        return new(command, iterations, timeoutMs, seed, artifactRoot, failFast, showHelp, listOnly, workerScenario, workerArtifacts);
    }

    private static string Next(string[] args, ref int index, string option)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"Option '{option}' requires a value.");
        return args[index];
    }

    private static int ParseInt(string value, string option, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < minimum || parsed > maximum)
            throw new ArgumentException($"Option '{option}' has invalid value '{value}'.");
        return parsed;
    }
}
