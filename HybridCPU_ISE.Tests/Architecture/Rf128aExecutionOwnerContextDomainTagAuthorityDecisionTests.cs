namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8a Paper authority for execution OwnerContextId and DomainTag.</summary>
public sealed class Rf128aExecutionOwnerContextDomainTagAuthorityDecisionTests
{
    [Fact]
    public void PaperDefinesSeparateBaselineAndOuterAbsenceBoundaries()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Execution owner-context and domain-tag boundary", paper, StringComparison.Ordinal);
        Assert.Contains("separate baseline-scoped values", paper, StringComparison.Ordinal);
        Assert.Contains("Raw zero is the valid baseline value", paper, StringComparison.Ordinal);
        Assert.Contains("alias for VT0", paper, StringComparison.Ordinal);
        Assert.Contains("Missing owner-context or execution-domain metadata remains an outer", paper,
            StringComparison.Ordinal);
        Assert.Contains("only that guard decides admission", paper, StringComparison.Ordinal);
        Assert.Contains("checked universal domain type", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingCarriersRetainSeparateOwnerFields()
    {
        string root = Root();
        string writeBack = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "WriteBack", "CPU_Core.Pipeline.Stages.WriteBackStage.cs"));
        string ordering = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "Ordering", "GlobalMemoryConflictService.cs"));
        Assert.Contains("public ulong DomainTag", writeBack, StringComparison.Ordinal);
        Assert.Contains("public int OwnerContextId", writeBack, StringComparison.Ordinal);
        Assert.Contains("OwnerContextId = 0", ordering, StringComparison.Ordinal);
        Assert.Contains("MemoryDomainTag = memoryDomainTag", ordering, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
