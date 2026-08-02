using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6s authority-only decision for physical resolution and geometry
/// update rejection carriers plus their migration dependency order.
/// </summary>
public sealed class Rf126sPhysicalMemoryBankRejectionCarrierArchitectureDecisionTests
{
    [Fact]
    public void PaperDefinesDistinctPhysicalResolutionAndBinding()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains("PhysicalMemoryBankBinding =", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "(PhysicalMemoryBankIndex, MemoryBankGeometryGeneration)",
            paper, StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankResolution =", paper,
            StringComparison.Ordinal);
        Assert.Contains("Resolved(PhysicalMemoryBankBinding)", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "distinct from scheduler-visible `MemoryBankResolution`",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalUnavailableHasNoZeroOrDefaultAlias()
    {
        string paper = Paper(FindRepositoryRoot());

        foreach (string reason in new[]
                 {
                     "NoPublishedGeometry",
                     "InvalidBankCount",
                     "InvalidBankWidth",
                     "GenerationUnavailable"
                 })
        {
            Assert.Contains(reason, paper, StringComparison.Ordinal);
        }

        Assert.Contains("`Unavailable` contains neither", paper,
            StringComparison.Ordinal);
        Assert.Contains("may not encode absence as index zero or generation zero",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void GeometryUpdateCarrierAndReasonsAreExact()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains("MemoryBankGeometryUpdateResult =", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Rejected(MemoryBankGeometryUpdateRejectReason)",
            paper, StringComparison.Ordinal);
        foreach (string reason in new[]
                 {
                     "InvalidBankCount",
                     "InvalidBankWidth",
                     "Busy",
                     "GenerationExhausted",
                     "PlatformRejected"
                 })
        {
            Assert.Contains(reason, paper, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FaultPrecedenceAndAtomicityAreAuthoritative()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "Rejection precedence is `InvalidBankCount`, then",
            paper, StringComparison.Ordinal);
        Assert.Contains("`InvalidBankWidth`, then `Busy`, then",
            paper, StringComparison.Ordinal);
        Assert.Contains("`GenerationExhausted`, then", paper,
            StringComparison.Ordinal);
        Assert.Contains("`PlatformRejected`; otherwise the result is `Applied`",
            paper, StringComparison.Ordinal);
        Assert.Contains("issued only for the atomic publish", paper,
            StringComparison.Ordinal);
        Assert.Contains("consumes no generation", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilitySettersRemainNonAuthoritativeAndUnchanged()
    {
        string root = FindRepositoryRoot();
        string paper = Paper(root);
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.cs");

        Assert.Contains(
            "are not the authoritative mutation operation because they cannot return this",
            paper, StringComparison.Ordinal);
        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("private int _bankWidthBytes = 64;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            subsystem, StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyOrderKeepsGenerationAndBehaviorSlicesSeparate()
    {
        string paper = Paper(FindRepositoryRoot());

        int generation = paper.IndexOf(
            "add a zero-caller valid-input contract for",
            StringComparison.Ordinal);
        int snapshot = paper.IndexOf(
            "add the zero-caller immutable geometry-snapshot contract",
            StringComparison.Ordinal);
        int carriers = paper.IndexOf(
            "add zero-caller physical binding, resolution and geometry-update result",
            StringComparison.Ordinal);
        int publication = paper.IndexOf(
            "cut over valid-input geometry publication and physical resolution",
            StringComparison.Ordinal);
        int request = paper.IndexOf(
            "store the captured binding on accepted requests",
            StringComparison.Ordinal);
        int invalid = paper.IndexOf(
            "change each invalid-input behavior separately",
            StringComparison.Ordinal);
        int removal = paper.IndexOf(
            "remove a compatibility API only after a closed-world zero-caller proof",
            StringComparison.Ordinal);

        Assert.True(generation >= 0 && snapshot > generation &&
                    carriers > snapshot && publication > carriers &&
                    request > publication && invalid > request &&
                    removal > invalid);
        Assert.Contains("No step may reuse", paper, StringComparison.Ordinal);
        Assert.Contains(
            "`MemoryBankGeometryGeneration` as a request, replay, domain, token or",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void LaterCutoverAddsOnlyTheAuthoritativeOwnerCarrierUse()
    {
        string root = FindRepositoryRoot();
        string generationContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryGeneration.cs"));
        string bindingContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankBinding.cs"));
        string resolutionContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankResolution.cs"));
        string updateResultContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryUpdateResult.cs"));
        string production = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_ISE"),
                generationContract, bindingContract, resolutionContract,
                updateResultContract),
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankBinding\b",
            File.ReadAllText(bindingContract));
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankResolution\b",
            File.ReadAllText(resolutionContract));
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+" +
            @"MemoryBankGeometryUpdateResult\b",
            File.ReadAllText(updateResultContract));
        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryUpdateResult",
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            StringComparison.Ordinal);
    }


    [Fact]
    public void PaperDecisionDoesNotChangeRuntimeSources()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "This authority decision changes no production declaration",
            paper, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(paper,
            @"PhysicalMemoryBankResolution\s*=").Count);
        Assert.Equal(1, Regex.Matches(paper,
            @"MemoryBankGeometryUpdateResult\s*=").Count);
    }

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string ReadTree(string path, params string[] excludedPaths) =>
        string.Join("\n", Directory.EnumerateFiles(path, "*.cs",
                SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !excludedPaths.Contains(
                Path.GetFullPath(file), StringComparer.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
