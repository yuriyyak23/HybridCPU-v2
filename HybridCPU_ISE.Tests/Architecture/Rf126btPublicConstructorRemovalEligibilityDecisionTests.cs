namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bt closed-world public raw-token removal eligibility guard.</summary>
public sealed class Rf126btPublicConstructorRemovalEligibilityDecisionTests
{
    [Fact]
    public void ClosedWorldHasNoRawPublicConstructorCallAndOnlyAcceptedOwnerConstruction()
    {
        string root = Root();
        string production = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Operations.cs"));
        Assert.Equal(2, Count(production, "new MemoryRequestToken("));
        Assert.Equal(2, Count(production, "physicalBankBinding: physicalBankBinding"));

        foreach (string relative in new[] { "HybridCPU_Compiler", "HybridCPU_RoslynBridge",
                     "CpuInterfaceBridge", "TestAssemblerConsoleApps" })
        {
            string tree = Tree(Path.Combine(root, relative));
            Assert.DoesNotContain("MemoryRequestToken", tree, StringComparison.Ordinal);
        }

        string[] directTestCallers = Directory.EnumerateFiles(
                Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).StartsWith("Rf126", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(
                "new MemorySubsystem.MemoryRequestToken(", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(directTestCallers);
    }

    [Fact]
    public void PaperAllowsOnlySeparateRemovalAfterClosedWorldProof()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("may be hardened or removed only after a closed-world",
            paper, StringComparison.Ordinal);
    }

    private static int Count(string text, string marker) => text.Split(marker).Length - 1;
    private static string Tree(string root) => string.Join("\n", Directory.EnumerateFiles(root,
        "*.cs", SearchOption.AllDirectories).Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase)).Select(File.ReadAllText));
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
