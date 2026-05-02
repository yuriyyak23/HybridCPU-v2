using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.Diagnostics;

public sealed class TestAssemblerWhiteBookHarnessTests
{
    [Fact]
    public void ProfileCatalogRegistersWhiteBookCommandsAndPreservesMatrixSmokeShape()
    {
        string catalog = ReadRepoFile("TestAssemblerConsoleApps/DiagnosticProfileCatalog.cs");
        string program = ReadRepoFile("TestAssemblerConsoleApps/Program.cs");
        string workloadKind = ReadRepoFile("TestAssemblerConsoleApps/DiagnosticWorkloadKind.cs");

        Assert.Contains("WhiteBookContractDiagnostics", workloadKind, StringComparison.Ordinal);
        Assert.Contains("\"whitebook-contract\"", catalog, StringComparison.Ordinal);
        Assert.Contains("WhiteBookSmoke", catalog, StringComparison.Ordinal);
        Assert.Contains("WhiteBookFull", catalog, StringComparison.Ordinal);
        Assert.Contains("IsWhiteBookSmokeCommand", program, StringComparison.Ordinal);
        Assert.Contains("IsWhiteBookFullCommand", program, StringComparison.Ordinal);

        int smokeStart = catalog.IndexOf("public static IReadOnlyList<DiagnosticRunProfile> MatrixSmoke", StringComparison.Ordinal);
        int runtimeStart = catalog.IndexOf("public static IReadOnlyList<DiagnosticRunProfile> MatrixRuntime", StringComparison.Ordinal);
        Assert.True(smokeStart >= 0 && runtimeStart > smokeStart, "Could not isolate MatrixSmoke definition.");

        string matrixSmoke = catalog[smokeStart..runtimeStart];
        Assert.Contains("GetRequired(\"safety\")", matrixSmoke, StringComparison.Ordinal);
        Assert.Contains("GetRequired(\"replay-reuse\")", matrixSmoke, StringComparison.Ordinal);
        Assert.Contains("GetRequired(\"assistant\")", matrixSmoke, StringComparison.Ordinal);
        Assert.DoesNotContain("whitebook", matrixSmoke, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, Regex.Matches(matrixSmoke, "GetRequired\\(").Count);
    }

    [Fact]
    public void CliHelpListsWhiteBookCommands()
    {
        ProcessResult result = RunDotnet(
            "run",
            "--project",
            "TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj",
            "--",
            "help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("whitebook-contract", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("whitebook-smoke", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("whitebook-full", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void WhiteBookContractProfileProducesExpectedArtifactAndProbeIds()
    {
        ProcessResult result = RunDotnet(
            "run",
            "--project",
            "TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj",
            "--",
            "whitebook-contract",
            "--iterations",
            "1",
            "--telemetry-logs",
            "minimal");

        Assert.Equal(0, result.ExitCode);

        string artifactDirectory = ExtractArtifactDirectory(result.Stdout);
        string reportPath = Path.Combine(artifactDirectory, "whitebook_contract_report.json");
        Assert.True(File.Exists(reportPath), $"Missing report artifact: {reportPath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(0, root.GetProperty("Summary").GetProperty("FailedProbeCount").GetInt32());

        string[] probeIds = root
            .GetProperty("Probes")
            .EnumerateArray()
            .Select(static probe => probe.GetProperty("Id").GetString())
            .Where(static id => id is not null)
            .Select(static id => id!)
            .ToArray();

        string[] expectedProbeIds =
        [
            "lane6.dsc.fail-closed",
            "dsc1.strict.current-only",
            "dsc2.parser-only",
            "dsc.token-progress-fault.model-only",
            "dsc.conflict-addressing-cache.no-current-execution",
            "l7.accel.fail-closed-no-rd",
            "l7.model-fake-backend.boundary",
            "compiler.production-lowering.prohibited",
            "phase12.migration-gate",
            "phase13.dependency-non-inversion"
        ];

        foreach (string expectedProbeId in expectedProbeIds)
        {
            Assert.Contains(expectedProbeId, probeIds);
        }
    }

    private static string ReadRepoFile(string relativePath)
    {
        string path = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Missing repository file: {relativePath}");
        return File.ReadAllText(path);
    }

    private static ProcessResult RunDotnet(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = CompatFreezeScanner.FindRepoRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument.Replace('/', Path.DirectorySeparatorChar));
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(milliseconds: 120_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException(
                $"dotnet {string.Join(' ', arguments)} timed out.");
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string ExtractArtifactDirectory(string stdout)
    {
        string? artifactLine = stdout
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .LastOrDefault(static line => line.StartsWith("Artifacts: ", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(artifactLine), $"No artifact directory in stdout: {stdout}");
        return artifactLine["Artifacts: ".Length..].Trim();
    }

    private readonly record struct ProcessResult(
        int ExitCode,
        string Stdout,
        string Stderr);
}
