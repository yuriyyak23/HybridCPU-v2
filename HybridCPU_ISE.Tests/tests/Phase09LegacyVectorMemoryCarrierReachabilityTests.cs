using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests;

public sealed class Phase09LegacyVectorMemoryCarrierReachabilityTests
{
    private static readonly string[] LegacyCarrierTypeNames =
    {
        "LoadSegmentMicroOp",
        "Load2DMicroOp",
        "GatherMicroOp",
        "StoreSegmentMicroOp",
        "Store2DMicroOp",
        "StoreScatterMicroOp"
    };

    [Fact]
    public void LegacyVectorMemoryCarrierClasses_AreNotInstantiatedInNonTestProductionSource()
    {
        string repositoryRoot = FindRepositoryRoot();
        string productionRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");

        var offenders = new List<string>();
        foreach (string sourceFile in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (sourceFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                sourceFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string sourceText = File.ReadAllText(sourceFile);
            foreach (string carrierTypeName in LegacyCarrierTypeNames)
            {
                if (Regex.IsMatch(sourceText, $@"\bnew\s+{Regex.Escape(carrierTypeName)}\b"))
                {
                    offenders.Add($"{Path.GetRelativePath(repositoryRoot, sourceFile)} -> {carrierTypeName}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Legacy vector-memory carrier instantiation reappeared in production source: " + string.Join(", ", offenders));
    }

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "Documentation")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE.Tests")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for legacy vector-memory reachability proof.");
    }
}
