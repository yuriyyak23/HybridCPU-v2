namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bi inventory for raw-token reflection/signature callers.</summary>
public sealed class Rf126biRawTokenReflectionSignatureMigrationInventoryTests
{

    [Fact]
    public void CurrentSignatureDoesNotExposeLocationAuthority()
    {
        string subsystem = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs"));
        string token = Slice(subsystem, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Count(token, "public MemoryRequestToken("));
        Assert.Equal(1, Count(token, "internal MemoryRequestToken("));
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", token,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRequiresReflectionCallersInRemovalProof()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("reflection/signature consumer", paper, StringComparison.Ordinal);
        Assert.Contains("zero external production callers alone", paper,
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

    private static int Count(string text, string marker) => text.Split(marker).Length - 1;

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }
}
