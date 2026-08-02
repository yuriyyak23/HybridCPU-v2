using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-13.12 executable completion audit for the user-authorized legacy pool.
/// It inventories physical source, not every use of "legacy" in protected code.
/// </summary>
public sealed class Rf1312LegacyCompletionAuditTests
{
    [Fact]
    public void ActualLegacySourceUniverseIsConsolidatedAndEveryRetainedContourHasEvidence()
    {
        string[] actual = SourceFiles("HybridCPU_ISE")
            .Where(path => Relative(path).Contains("/Legacy/", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains("Legacy", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains("Obsolete", StringComparison.OrdinalIgnoreCase))
            .Select(Relative).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(RetainedLegacySources, actual);

        string[] misplaced = SourceFiles("HybridCPU_ISE")
            .Where(path => !Relative(path).Contains("/Legacy/", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetFileName(path).Contains("Legacy", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains("Obsolete", StringComparison.OrdinalIgnoreCase))
            .Select(Relative).ToArray();
        Assert.Empty(misplaced);

        foreach (string name in RetentionEvidence)
        {
            Assert.True(File.Exists(Path.Combine(Root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF13", name)), name);
        }
    }

    [Fact]
    public void SoleDeletionIsArchivedWithIntegrityAndRelocatedOldPathsAreAbsent()
    {
        string live = Path.Combine(Root, "HybridCPU_ISE", "Legacy", "Obsolete", "MainMemoryAtomicMemoryUnit.Obsolete.cs");
        string archived = Path.Combine(Root, "LagecySave", "HybridCPU_ISE", "Legacy", "Obsolete", "MainMemoryAtomicMemoryUnit.Obsolete.cs");
        Assert.False(File.Exists(live));
        Assert.True(File.Exists(archived));
        Assert.Equal("6fef7f6f4d14324e1fb38dfb948a9df9a3a4103c365d0b7204ec5924de8f4963",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archived))).ToLowerInvariant());
        foreach (string oldPath in RelocatedOldPaths)
        {
            Assert.False(File.Exists(Path.Combine(Root, oldPath.Replace('/', Path.DirectorySeparatorChar))), oldPath);
        }
    }

    [Fact]
    public void RawCompatibilityReflectionTestSupportAndExcludedTestsRemainExplicitlyClassified()
    {
        string[] productionRoots = ["HybridCPU_ISE", "HybridCPU_Compiler", "CpuInterfaceBridge", "TestAssemblerConsoleApps", "tools"];
        string[] projectorCallers = productionRoots.SelectMany(SourceFiles)
            .Where(path => !path.EndsWith("DecodedBundleTransportProjector.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("DecodedBundleTransportProjector.", StringComparison.Ordinal))
            .Select(Relative).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(
        [
            "HybridCPU_ISE/CloseToHSL/Core/Frontend/Decode/BundleParser/CPU_Core.Decoder.cs",
            "TestAssemblerConsoleApps/MatrixTileSpecSuite.cs",
            "TestAssemblerConsoleApps/StreamVectorSpecSuite.cs",
        ], projectorCallers);

        string observationReflection = File.ReadAllText(Path.Combine(Root, "HybridCPU_ISE.Tests", "tests", "PhaseAuditLegacyObservationBridgeIsolationTests.cs"));
        Assert.Contains("typeof(LegacyProcessorMachineStateSource)", observationReflection, StringComparison.Ordinal);
        Assert.Contains("GetConstructors", observationReflection, StringComparison.Ordinal);

        string project = File.ReadAllText(Path.Combine(Root, "HybridCPU_ISE.Tests", "HybridCPU_ISE.Tests.csproj"));
        string[] excluded = Regex.Matches(project, "<Compile Remove=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Where(path => !path.StartsWith("obj_r6fresh", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal(41, excluded.Length);
        Assert.Equal(37, excluded.Count(path => File.Exists(Path.Combine(Root, "HybridCPU_ISE.Tests", path))));
        Assert.Equal(4, excluded.Count(path => !File.Exists(Path.Combine(Root, "HybridCPU_ISE.Tests", path))));

        string ledger = File.ReadAllText(Path.Combine(Root, "Documentation", "ArchitectureAuthorityRefactor", "12_RF13", "00_CURRENT_STATUS_AND_LEDGER.md"));
        Assert.Contains("RF-13.12", ledger, StringComparison.Ordinal);
        Assert.Contains("excluded-test", ledger, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] RetainedLegacySources =
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
    ];

    private static readonly string[] RelocatedOldPaths =
    [
        "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ScalarLegacyProjection.cs",
        "HybridCPU_ISE/CloseToHSL/Core/State/Compat/LegacyCpuStateAdapter.cs",
        "HybridCPU_ISE/CloseToHSL/Core/State/LegacyCompatibilityState.cs",
        "HybridCPU_ISE/NonRTL/Legacy/LegacyMachineStateReadException.cs",
        "HybridCPU_ISE/NonRTL/Legacy/LegacyObservationServiceFactory.cs",
        "HybridCPU_ISE/NonRTL/Legacy/LegacyProcessorMachineStateSource.cs",
    ];

    private static readonly string[] RetentionEvidence =
    [
        "rf13.3-cpu-core-constructor-retention.md",
        "rf13.4-legacy-observation-exception-retention.md",
        "rf13.5-legacy-observation-factory-retention.md",
        "rf13.6-legacy-observation-source-retention.md",
        "rf13.7-legacy-cpu-state-adapter-retention.md",
        "rf13.8-legacy-compatibility-state-retention.md",
        "rf13.9-rf06-scalar-projection-retention.md",
        "rf13.10-decoded-bundle-transport-projector-retention.md",
        "rf13.11-processor-initialization-retention.md",
    ];

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
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the HybridCPU repository root.");
    }
}
