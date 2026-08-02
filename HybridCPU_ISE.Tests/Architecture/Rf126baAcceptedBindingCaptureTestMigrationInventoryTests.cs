using System.Reflection;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ba decision-only inventory for the one RF-12.6at direct public
/// raw-token construction. It selects no constructor or test migration.
/// </summary>
public sealed class Rf126baAcceptedBindingCaptureTestMigrationInventoryTests
{


    [Fact]
    public void PaperMakesTheDirectCallerARemovalBlockerWithoutGrantingAuthority()
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
