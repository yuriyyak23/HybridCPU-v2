using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bv post-removal closed-world raw-token constructor audit.</summary>
public sealed class Rf126bvPostRemovalRawTokenConstructorAuditTests
{
    [Fact]
    public void PublicRawConstructorIsAbsentAndAcceptedConstructionIsOwnerOnly()
    {
        string root = Root();
        string token = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs"));
        string operations = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Operations.cs"));

        Assert.Equal(0, Regex.Matches(token,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(token,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(2, Count(operations, "new MemoryRequestToken("));
        Assert.Equal(2, Count(operations, "physicalBankBinding: physicalBankBinding"));
    }

    [Fact]
    public void NonInventoryConsumersHaveNoRawConstructionOrReflectionBypass()
    {
        string root = Root();
        foreach (string relative in new[] { "HybridCPU_Compiler", "HybridCPU_RoslynBridge",
                     "CpuInterfaceBridge", "TestAssemblerConsoleApps" })
        {
            Assert.DoesNotContain("MemoryRequestToken", Tree(Path.Combine(root, relative)),
                StringComparison.Ordinal);
        }

        string[] nonInventoryTestCallers = Directory.EnumerateFiles(
                Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("Rf126", StringComparison.Ordinal))
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                "new MemorySubsystem.MemoryRequestToken(", StringComparison.Ordinal) ||
                File.ReadAllText(path).Contains(
                    "typeof(MemorySubsystem.MemoryRequestToken).Get" + "Constructors(",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(nonInventoryTestCallers);
    }

    [Fact]
    public void PaperRequiresClosedWorldProofAndPermitsDistinctNonPublicOwnerForm()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("closed-world zero-caller proof", paper, StringComparison.Ordinal);
        Assert.Contains("owner may continue using a distinct non-public accepted",
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
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
