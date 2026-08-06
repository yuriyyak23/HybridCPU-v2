namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-13.1 path-only relocation guard. It deliberately proves location and
/// caller closure without authorizing deletion of any retained contour.
/// </summary>
public sealed class Rf131LegacyRelocationInventoryTests
{
    [Fact]
    public void LegacyNamedSourceIsConsolidatedUnderTheLegacyRootWithRelativePathsPreserved()
    {
        string[] legacyNamedSources = SourceFiles("HybridCPU_ISE")
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Legacy{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains("Legacy", StringComparison.OrdinalIgnoreCase))
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/DecodedBundleTransportProjector.cs",
            "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/Rf06ScalarLegacyProjection.cs",
            "HybridCPU_ISE/Legacy/CloseToHSL/Core/State/Compat/LegacyCpuStateAdapter.cs",
            "HybridCPU_ISE/Legacy/CloseToHSL/Core/State/LegacyCompatibilityState.cs",
            "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyMachineStateReadException.cs",
            "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyObservationServiceFactory.cs",
            "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyProcessorMachineStateSource.cs",
            "HybridCPU_ISE/Legacy/Obsolete/CPU_Core.StateData.Obsolete.cs",
            "HybridCPU_ISE/Legacy/Obsolete/Processor.Initialization.Obsolete.cs",
        ], legacyNamedSources);
    }

    [Fact]
    public void RelocatedLiveContoursRetainTheirRuntimeAndCompatibilityCallers()
    {
        Assert.Equal(
        [
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/CPU_Core.PipelineExecution.Fsp.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06ScalarSchedulerRouting.cs",
        ], Callers("Rf06ScalarLegacyProjection."));

        Assert.Equal(
        ["HybridCPU_ISE/CloseToHSL/Core/State/LiveCpuStateAdapter.cs"],
            Callers("new LegacyCpuStateAdapter("));

        Assert.Equal(
        ["HybridCPU_ISE/CloseToHSL/Core/State/CoreRuntimeState.cs"],
            Callers("new LegacyCompatibilityState("));

        Assert.Equal(
        ["TestAssemblerConsoleApps/SimpleAsmApp.Progress.cs"],
            Callers("LegacyObservationServiceFactory.CreateLegacyGlobalCompat("));
    }

    [Fact]
    public void RelocationDoesNotExcludeMovedSourcesOrAuthorizeDeletion()
    {
        string project = File.ReadAllText(Path.Combine(Root, "HybridCPU_ISE", "HybridCPU_ISE.csproj"));
        Assert.DoesNotContain("Compile Remove=\"Legacy\\CloseToHSL", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove=\"Legacy\\NonRTL", project, StringComparison.Ordinal);

        foreach (string oldPath in new[]
        {
            "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ScalarLegacyProjection.cs",
            "HybridCPU_ISE/CloseToHSL/Core/State/Compat/LegacyCpuStateAdapter.cs",
            "HybridCPU_ISE/CloseToHSL/Core/State/LegacyCompatibilityState.cs",
            "HybridCPU_ISE/NonRTL/Legacy/LegacyMachineStateReadException.cs",
            "HybridCPU_ISE/NonRTL/Legacy/LegacyObservationServiceFactory.cs",
            "HybridCPU_ISE/NonRTL/Legacy/LegacyProcessorMachineStateSource.cs",
        })
        {
            Assert.False(File.Exists(Path.Combine(Root, oldPath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string ledger = File.ReadAllText(Path.Combine(Root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "12_RF13", "00_CURRENT_STATUS_AND_LEDGER.md"));
        string evidence = File.ReadAllText(Path.Combine(Root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF13", "rf13.1-legacy-consolidation-relocation.md"));
        Assert.Contains("RF-13.2", ledger, StringComparison.Ordinal);
        Assert.Contains("path-only move", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valid-input parity", evidence, StringComparison.Ordinal);
        Assert.Contains("invalid behavior", evidence, StringComparison.Ordinal);
        Assert.Contains("deletion proof", evidence, StringComparison.Ordinal);
    }

    private static string[] Callers(string token) => new[] { "HybridCPU_ISE", "TestAssemblerConsoleApps" }
        .SelectMany(SourceFiles)
        .Where(path => !path.EndsWith("Rf06ScalarLegacyProjection.cs", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.EndsWith("LegacyCpuStateAdapter.cs", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.EndsWith("LegacyCompatibilityState.cs", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.EndsWith("LegacyObservationServiceFactory.cs", StringComparison.OrdinalIgnoreCase))
        .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
        .Select(Relative)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(Root, root), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string Root { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the HybridCPU repository root.");
    }
}
