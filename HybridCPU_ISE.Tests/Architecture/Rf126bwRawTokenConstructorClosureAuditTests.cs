using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bw raw-token constructor contour closure audit.</summary>
public sealed class Rf126bwRawTokenConstructorClosureAuditTests
{
    [Fact]
    public void RuntimeHasNoPublicRawConstructorAndOneNonPublicAcceptedForm()
    {
        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Empty(token.GetConstructors(BindingFlags.Instance | BindingFlags.Public));

        ConstructorInfo accepted = Assert.Single(token.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        Assert.Contains(accepted.GetParameters(), parameter =>
            parameter.ParameterType.Name == "PhysicalMemoryBankBinding");
    }

    [Fact]
    public void SourceHasOnlyOwnerAcceptedConstructionAndNoCompatibilityDeclaration()
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
    public void PaperDefinesTheClosureBoundaryAndNoOtherFamilyIsRetyped()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("public token constructor may be hardened or removed only after",
            paper, StringComparison.Ordinal);
        Assert.Contains("DMA/stream/device", paper, StringComparison.Ordinal);
    }

    private static int Count(string text, string marker) => text.Split(marker).Length - 1;
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
