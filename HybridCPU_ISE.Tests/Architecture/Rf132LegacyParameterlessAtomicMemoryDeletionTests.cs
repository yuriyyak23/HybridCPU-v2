using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-13.2 deletion guard for the archived parameterless atomic-memory shim.
/// It proves removal, archive integrity, and explicit-binding continuity.
/// </summary>
public sealed class Rf132LegacyParameterlessAtomicMemoryDeletionTests
{
    [Fact]
    public void ParameterlessObsoleteShimIsArchivedOutsideTheProjectAndAbsentFromLiveSource()
    {
        string archived = Path.Combine(Root, "LagecySave", "HybridCPU_ISE", "Legacy", "Obsolete",
            "MainMemoryAtomicMemoryUnit.Obsolete.cs");
        string live = Path.Combine(Root, "HybridCPU_ISE", "Legacy", "Obsolete",
            "MainMemoryAtomicMemoryUnit.Obsolete.cs");

        Assert.True(File.Exists(archived));
        Assert.False(File.Exists(live));
        Assert.Equal(
            "6fef7f6f4d14324e1fb38dfb948a9df9a3a4103c365d0b7204ec5924de8f4963",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archived))).ToLowerInvariant());
    }

    [Fact]
    public void ParameterlessConstructionHasZeroReachableCallersAndExplicitConstructionRemains()
    {
        string[] roots = ["HybridCPU_ISE", "HybridCPU_Compiler", "CpuInterfaceBridge", "TestAssemblerConsoleApps", "tools", "HybridCPU_ISE.Tests"];
        string[] directCallers = roots
            .SelectMany(SourceFiles)
            .Where(path => !path.EndsWith(nameof(Rf132LegacyParameterlessAtomicMemoryDeletionTests) + ".cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("new MainMemoryAtomicMemoryUnit()", StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(directCallers);

        ConstructorInfo[] constructors = typeof(YAKSys_Hybrid_CPU.Core.Memory.MainMemoryAtomicMemoryUnit)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        ConstructorInfo explicitConstructor = Assert.Single(constructors);
        Assert.Single(explicitConstructor.GetParameters());
        Assert.Equal("mainMemory", explicitConstructor.GetParameters()[0].Name);
        Assert.Null(typeof(YAKSys_Hybrid_CPU.Core.Memory.MainMemoryAtomicMemoryUnit)
            .GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void CurrentLedgerAndEvidenceDeclareTheIntentionalInvalidBehaviorChange()
    {
        string ledger = File.ReadAllText(Path.Combine(Root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "12_RF13",
            "00_CURRENT_STATUS_AND_LEDGER.md"));
        string evidence = File.ReadAllText(Path.Combine(Root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF13",
            "rf13.2-parameterless-atomic-memory-legacy-deletion.md"));

        Assert.Contains("RF-13.2", ledger, StringComparison.Ordinal);
        Assert.Contains("LagecySave", ledger, StringComparison.Ordinal);
        Assert.Contains("compile-time absence", evidence, StringComparison.Ordinal);
        Assert.Contains("zero-reachable-caller", evidence, StringComparison.Ordinal);
        Assert.Contains("rollback", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(RootPath, root), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string Relative(string path) => Path.GetRelativePath(RootPath, path).Replace('\\', '/');

    private static string RootPath { get; } = FindRepositoryRoot();

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

    private static string Root => RootPath;
}
