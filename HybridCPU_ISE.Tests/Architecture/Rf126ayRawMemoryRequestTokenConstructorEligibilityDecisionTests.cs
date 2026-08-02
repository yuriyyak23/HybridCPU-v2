using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6ay closed-world public raw-token removal eligibility guard.</summary>
public sealed class Rf126ayRawMemoryRequestTokenConstructorEligibilityDecisionTests
{

    [Fact]
    public void PaperRequiresAllCallerClassesForZeroCallerProof()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("zero external production callers alone\nis insufficient", paper,
            StringComparison.Ordinal);
        Assert.Contains("reflection/signature consumer,\ntest and", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCompatibilityConstructorRemainsDistinctFromOwnerConstruction()
    {
        string subsystem = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs"));
        string token = Slice(subsystem, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        string internalConstructor = Slice(token, "internal MemoryRequestToken(",
            "public byte[] GetBuffer(");
        Assert.Equal(0, Count(token, "public MemoryRequestToken("));
        Assert.Equal(1, Count(token, "internal MemoryRequestToken("));
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding",
            internalConstructor, StringComparison.Ordinal);
    }

    private static int Count(string text, string marker) => text.Split(marker).Length - 1;
    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }
    private static string Tree(string root, params string[] exclude) => string.Join("\n",
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !exclude.Any(name => path.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText));
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
