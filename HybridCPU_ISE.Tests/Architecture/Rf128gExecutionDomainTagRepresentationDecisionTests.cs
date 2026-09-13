namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8g authority against inventing an execution DomainTag range or typed cutover.</summary>
public sealed class Rf128gExecutionDomainTagRepresentationDecisionTests
{
    [Fact]
    public void PaperRetainsRawUlongWithoutGlobalRangeOrCheckedType()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Execution domain-tag raw representation boundary", paper, StringComparison.Ordinal);
        Assert.Contains("retained raw `ulong` payload", paper, StringComparison.Ordinal);
        Assert.Contains("bounded nonzero range", paper, StringComparison.Ordinal);
        Assert.Contains("global invalid-domain value", paper, StringComparison.Ordinal);
        Assert.Contains("neither a checked execution-domain CLR type nor a", paper, StringComparison.Ordinal);
        Assert.Contains("valid-input signature migration", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingExecutionCarriersAndReplayHashRetainRawPayload()
    {
        string root = Root();
        string placement = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "SlotPlacementMetadata.cs"));
        string replay = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Certificates", "ReplayPhaseSubstrate.Implementations.cs"));
        Assert.Contains("public ulong DomainTag", placement, StringComparison.Ordinal);
        Assert.Contains("DomainTag         = 0", placement, StringComparison.Ordinal);
        Assert.Contains("opHasher.Compress(op.Placement.DomainTag)", replay, StringComparison.Ordinal);
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
