namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bq Paper authority for Lane-7 checkpoint accelerator-device re-entry.</summary>
public sealed class Rf127bqAcceleratorDeviceIdCheckpointAuthorityDecisionTests
{
    [Fact]
    public void PaperMakesCheckpointReentryASeparateFailClosedOwnerBoundary()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("#### Accelerator-device checkpoint re-entry boundary", paper, StringComparison.Ordinal);
        Assert.Contains("owner-local restore carrier", paper, StringComparison.Ordinal);
        Assert.Contains("Before a restored", paper, StringComparison.Ordinal);
        Assert.Contains("inserted into either Lane-7 owner dictionary", paper, StringComparison.Ordinal);
        Assert.Contains("declared non-zero accelerator-taxonomy enum", paper, StringComparison.Ordinal);
        Assert.Contains("rejected from re-entry", paper, StringComparison.Ordinal);
        Assert.Contains("same fail-closed omission shape", paper, StringComparison.Ordinal);
        Assert.Contains("defined enum member keeps", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryShowsThePreCutoverDictionaryInsertionAndExistingStructuralFilter()
    {
        string checkpoint = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime",
            "Lanes", "Lane7", "Lane7StateBlock.Checkpoint.partial.cs"));

        Assert.Contains("!handle.IsValid", checkpoint, StringComparison.Ordinal);
        Assert.Contains("Lane7VirtualHandle handle = checkpoint.VirtualHandles[index]", checkpoint, StringComparison.Ordinal);
        Assert.Contains("_handleByOwner[(handle.ExecutionDomainTag, handle.OwnerVirtualThreadId, handle.AcceleratorId)]",
            checkpoint, StringComparison.Ordinal);
        Assert.Contains("HostEvidence.PrepareForRestore(EvidencePolicyDescriptor.FailClosed)", checkpoint,
            StringComparison.Ordinal);
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
