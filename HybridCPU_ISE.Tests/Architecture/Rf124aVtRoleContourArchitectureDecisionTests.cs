using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124aVtRoleContourArchitectureDecisionTests
{
    [Fact]
    public void PaperOwnsTheExactRoleTaxonomyWithoutCreatingASecondVtType()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.16 SMT virtual-thread role contours and migration boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The existing `VtId` remains the sole checked representation",
            paper, StringComparison.Ordinal);
        Assert.Contains("Transport hint and compiler sideband", paper,
            StringComparison.Ordinal);
        Assert.Contains("Carrier/source VT", paper, StringComparison.Ordinal);
        Assert.Contains("Architectural/effect owner VT", paper,
            StringComparison.Ordinal);
        Assert.Contains("Foreground owner and FSP roles", paper,
            StringComparison.Ordinal);
        Assert.Contains("Active, request and state-index roles", paper,
            StringComparison.Ordinal);
        Assert.Contains("Replay and diagnostic roles", paper,
            StringComparison.Ordinal);
        Assert.Contains("not an identifier invariant", paper,
            StringComparison.Ordinal);

        string activeSources = JoinSources(root, "HybridCPU_ISE",
            "HybridCPU_Compiler", "TestAssemblerConsoleApps");
        Assert.Empty(Regex.Matches(activeSources,
            @"\b(?:record\s+struct|readonly\s+record\s+struct|struct|class)\s+VirtualThreadId\b"));
    }

    [Fact]
    public void PaperOwnsZeroAbsenceAndFrozenCompatibilityBehavior()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("VT0 is a present identity", paper,
            StringComparison.Ordinal);
        Assert.Contains("Absence is always outside a checked `VtId`", paper,
            StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.VirtualThreadId == 0", paper,
            StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.OwnerThreadId == 0", paper,
            StringComparison.Ordinal);
        Assert.Contains("These four clamps are\n  compatibility behavior, not validation",
            paper, StringComparison.Ordinal);
        Assert.Contains("separate invalid/absence-behavior decision", paper,
            StringComparison.Ordinal);
        Assert.Contains("diagnostic zero result into VT0 state", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesEveryAuthorityOwnerAndDefinesReversibleOrder()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("Add a distinctly named checked projection for the raw",
            paper, StringComparison.Ordinal);
        Assert.Contains("MicroOp.OwnerThreadId", paper, StringComparison.Ordinal);
        Assert.Contains("without migrating a caller or changing invalid behavior",
            paper, StringComparison.Ordinal);
        Assert.Contains("carrier/source `MicroOp.VirtualThreadId`", paper,
            StringComparison.Ordinal);
        Assert.Contains("do not combine it with owner VT", paper,
            StringComparison.Ordinal);
        Assert.Contains("dedicated bridge slice", paper, StringComparison.Ordinal);
        Assert.Contains("Decide trap zero substitution, the four clamps", paper,
            StringComparison.Ordinal);
        Assert.Contains("Stage A/B, scheduler topology", paper,
            StringComparison.Ordinal);
        Assert.Contains("FSP owner/donor policy and VT0..VT3 order", paper,
            StringComparison.Ordinal);
        Assert.Contains("bounded-retire subset/order and publication boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("timed-memory ownership and all RF-11 state owners",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRawSurfaceAndInvalidBehaviorRemainUnchanged()
    {
        PropertyInfo owner = typeof(MicroOp).GetProperty(
            nameof(MicroOp.OwnerThreadId))!;
        PropertyInfo carrier = typeof(MicroOp).GetProperty(
            nameof(MicroOp.VirtualThreadId))!;
        Assert.Equal(typeof(int), owner.PropertyType);
        Assert.Equal(typeof(int), carrier.PropertyType);
        Assert.True(owner.SetMethod!.IsPublic);
        Assert.True(carrier.SetMethod!.IsPublic);

        string root = FindRepositoryRoot();
        string production = JoinSources(root, "HybridCPU_ISE");
        Assert.Equal(3, Regex.Matches(production,
            @"Math\.Clamp\(OwnerThreadId,\s*0,\s*Processor\.CPU_Core\.SmtWays\s*-\s*1\)")
            .Count);
        Assert.Equal(1, Regex.Matches(production,
            @"Math\.Clamp\(seed\.OwnerThreadId,\s*0,\s*Processor\.CPU_Core\.SmtWays\s*-\s*1\)")
            .Count);
        Assert.Contains("trapMicroOp.VirtualThreadId != 0", production,
            StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.OwnerThreadId != 0", production,
            StringComparison.Ordinal);
        Assert.Contains("NormalizeExecutionVtId", production,
            StringComparison.Ordinal);
        Assert.Contains("NormalizePipelineStateVtId", production,
            StringComparison.Ordinal);
    }


    private static string JoinSources(string root, params string[] sourceRoots) =>
        string.Join("\n", sourceRoots.SelectMany(sourceRoot =>
            Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !HasPathSegment(path, "bin") &&
                               !HasPathSegment(path, "obj") &&
                               !HasPathSegment(path, "Legacy"))
                .OrderBy(path => path, StringComparer.Ordinal))
            .Select(File.ReadAllText));

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
