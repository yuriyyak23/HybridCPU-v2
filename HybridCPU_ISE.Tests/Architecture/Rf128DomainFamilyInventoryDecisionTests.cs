using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8 closed-world inventory of separate domain-related families.</summary>
public sealed class Rf128DomainFamilyInventoryDecisionTests
{
    [Fact]
    public void PaperSeparatesExecutionAndIoDomainFamiliesAndTheirAbsenceRules()
    {
        string paper = Read(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("`OwnerContextId` and execution `DomainTag` are distinct families", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero denotes the baseline owner context or baseline execution domain", paper,
            StringComparison.Ordinal);
        Assert.Contains("IOMMU domain identity/tag/epoch, address-space identity, translation tags", paper,
            StringComparison.Ordinal);
        Assert.Contains("no universal `DomainId` is introduced", paper, StringComparison.Ordinal);
        Assert.Contains("Zero/default composite is unbound or absent", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionKeepsSeparateAddressTranslationAndCertificateSurfaces()
    {
        string root = Root();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] files = Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("AddressSpaceId", StringComparison.Ordinal) ||
                File.ReadAllText(path).Contains("IotlbTag", StringComparison.Ordinal) ||
                File.ReadAllText(path).Contains("NestedTlbTag", StringComparison.Ordinal) ||
                File.ReadAllText(path).Contains("BundleResourceCertificate", StringComparison.Ordinal))
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()!;

        Assert.Contains("AddressSpaceId.cs", files);
        Assert.Contains("IotlbTag.cs", files);
        Assert.Contains("NestedTlbTag.cs", files);
        Assert.Contains("BundleResourceCertificate.cs", files);
        string all = string.Join("\n", Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        Assert.DoesNotMatch(new Regex(@"\b(?:struct|class|record struct)\s+DomainId\b"), all);
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));
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
