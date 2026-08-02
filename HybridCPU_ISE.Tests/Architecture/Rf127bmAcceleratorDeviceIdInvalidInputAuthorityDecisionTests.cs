namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bm Paper authority for undefined AcceleratorDeviceId admission.</summary>
public sealed class Rf127bmAcceleratorDeviceIdInvalidInputAuthorityDecisionTests
{
    [Fact]
    public void PaperDefinesUndefinedNonzeroAsInvalidAtLane7HandleAdmission()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("#### Accelerator-device enum admission boundary", paper,
            StringComparison.Ordinal);
        Assert.Contains("exactly the declared non-zero enum members", paper,
            StringComparison.Ordinal);
        Assert.Contains("not an accelerator-device identity and cannot become", paper,
            StringComparison.Ordinal);
        Assert.Contains("Lane-7 virtual", paper, StringComparison.Ordinal);
        Assert.Contains("handle namespace key", paper,
            StringComparison.Ordinal);
        Assert.Contains("rejects both forms", paper, StringComparison.Ordinal);
        Assert.Contains("before allocating a handle", paper,
            StringComparison.Ordinal);
        Assert.Contains("does not normalize, clamp, remap, or synthesize", paper,
            StringComparison.Ordinal);
        Assert.Contains("narrower v1 `ReferenceMatMul` capability acceptance separately", paper,
            StringComparison.Ordinal);
        Assert.Contains("existing capability-denied invalid admission", paper,
            StringComparison.Ordinal);
        Assert.Contains("form; no new fault winner", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Lane7AndParserBoundariesMatchTheSelectedAuthority()
    {
        string root = Root();
        string lane7 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.cs");
        string parser = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "ExternalAccelerators", "Descriptors", "AcceleratorDescriptorParser.cs");

        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), acceleratorId)", lane7,
            StringComparison.Ordinal);
        Assert.Contains("Lane7FaultKind.CapabilityDenied", lane7, StringComparison.Ordinal);
        Assert.Contains("acceleratorId != AcceleratorDeviceId.ReferenceMatMul", parser,
            StringComparison.Ordinal);
        Assert.Contains("AcceleratorDescriptorFault.UnsupportedAcceleratorId", parser,
            StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts]));

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
