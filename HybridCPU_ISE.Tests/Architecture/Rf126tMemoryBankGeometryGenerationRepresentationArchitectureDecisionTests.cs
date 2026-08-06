using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6t authority-only decision for the raw width, checked domain,
/// absence and allocation boundary of memory-bank geometry generation.
/// </summary>
public sealed class Rf126tMemoryBankGeometryGenerationRepresentationArchitectureDecisionTests
{
    [Fact]
    public void PaperSelectsExactUInt64Representation()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "retained raw form of `MemoryBankGeometryGeneration` is `UInt64`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Checked", paper, StringComparison.Ordinal);
        Assert.Contains("values are exactly `1..UInt64.MaxValue`", paper,
            StringComparison.Ordinal);
        Assert.Contains("The first issued", paper, StringComparison.Ordinal);
        Assert.Contains("generation is one", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroIsAbsentAndCannotNormalizeToOne()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "raw/default zero is the unissued or",
            paper, StringComparison.Ordinal);
        Assert.Contains("absent outer representation and is never a checked value",
            paper, StringComparison.Ordinal);
        Assert.Contains("rejects zero without normalizing it to one",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "may not encode absence as index zero or generation zero",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerOnlySuccessorAndExhaustionAreExact()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "Only the timed-memory geometry owner advances allocation state",
            paper, StringComparison.Ordinal);
        Assert.Contains("from an unissued state it may issue one", paper,
            StringComparison.Ordinal);
        Assert.Contains("it may issue the exact successor", paper,
            StringComparison.Ordinal);
        Assert.Contains("`UInt64.MaxValue` it reports `GenerationExhausted`",
            paper, StringComparison.Ordinal);
        Assert.Contains("without mutation", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedGenerationForbidsArithmeticAndImplicitRawConversion()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "no arithmetic, increment, decrement, modulo, clamp or implicit raw-integer",
            paper, StringComparison.Ordinal);
        Assert.Contains("conversion", paper, StringComparison.Ordinal);
        Assert.Contains(
            "of raw generation numbers across different timed-memory subsystem instances",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "does not establish common ownership or binding identity",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void LaterCutoverAddsOnlyOwnerLocalGenerationStorage()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryGeneration.cs"));
        string geometryContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankGeometry.cs"));
        string bindingContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankBinding.cs"));
        string production = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_ISE"),
                contractPath, geometryContractPath, bindingContractPath),
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryGeneration.Create(nextGenerationRaw)",
            production, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(File.ReadAllText(contractPath),
            @"public\s+readonly\s+record\s+struct\s+MemoryBankGeometryGeneration\b")
            .Count);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(geometryContractPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(bindingContractPath),
            StringComparison.Ordinal);

        string memory = ReadTree(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem"),
            contractPath, geometryContractPath, bindingContractPath);
        Assert.Contains("_lastIssuedPhysicalBankGeometryGeneration", memory,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BankGeneration", memory,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GenerationId", memory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CrossFamilyEpochsRemainRawAndDistinct()
    {
        string root = FindRepositoryRoot();
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string lane6 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Lanes", "Lane6", "Lane6QueueRuntime.cs");

        Assert.Contains("public ulong EpochId", replay,
            StringComparison.Ordinal);
        Assert.Contains("private static bool TryAdvanceEpoch(ref ulong epoch",
            lane6, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", replay,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", lane6,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceRecordsAuthorityGapAndNoProductionChange()
    {
        string root = FindRepositoryRoot();
        string evidence = Evidence(root);

        Assert.Contains(
            "did not select the raw width or exact representational domain",
            evidence, StringComparison.Ordinal);
        Assert.Contains("A production", evidence, StringComparison.Ordinal);
        Assert.Contains(
            "type at that point would have invented architecture authority",
            evidence, StringComparison.Ordinal);
        Assert.Contains("Production/runtime change: none", evidence,
            StringComparison.Ordinal);
        Assert.Contains("no unchecked public constructor", evidence,
            StringComparison.Ordinal);
        Assert.Contains("no reflection or TestSupport mutation seam", evidence,
            StringComparison.Ordinal);
    }


    [Fact]
    public void PaperHasOneExactRawFormDecision()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(paper,
            @"retained raw form of `MemoryBankGeometryGeneration` is `UInt64`")
            .Count);
    }

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6t-memory-bank-geometry-generation-representation-architecture-decision.md");

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
