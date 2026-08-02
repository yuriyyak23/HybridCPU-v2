using System.Reflection;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bc decision-only inventory for the AU raw-token test seam.</summary>
public sealed class Rf126bcStoredBindingConsumerTestMigrationInventoryTests
{


    [Fact]
    public void PaperKeepsCallerPresenceSeparateFromTokenAuthority()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("test and TestSupport seam", paper, StringComparison.Ordinal);
        Assert.Contains("never pending-map, location, admission, completion or", paper,
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
