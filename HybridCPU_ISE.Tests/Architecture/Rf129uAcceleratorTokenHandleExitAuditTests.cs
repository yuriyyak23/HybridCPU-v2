namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129uAcceleratorTokenHandleExitAuditTests
{

    [Fact]
    public void NoGenericTokenOrVirtualFallbackWasIntroduced()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("not a VM-local virtual-token, DMA-stream token, memory request, replay token", paper, StringComparison.Ordinal);
        Assert.Contains("No generic token conversion", paper, StringComparison.Ordinal);
        Assert.Contains("no shared invalid result", paper, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) && Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
