using System.Reflection;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6be decision-only inventory for the AV forged-token witness.</summary>
public sealed class Rf126beLegacyCancellationForgedTokenMigrationInventoryTests
{
    [Fact]
    public void AvWitnessCombinesRequestIdForwardingAndMalformedDefaultToken()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE.Tests",
            "Architecture", "Rf126avLegacyCancellationStoredBindingValidInputCutoverTests.cs"));
        Assert.DoesNotContain("new MemorySubsystem.MemoryRequestToken(", source,
            StringComparison.Ordinal);
        Assert.Contains("MemorySubsystem.MemoryRequestToken accepted", source,
            StringComparison.Ordinal);
        Assert.Contains("memory.CancelPendingRequest(accepted)", source,
            StringComparison.Ordinal);

        string operations = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Operations.cs"));
        Assert.Contains("return token != null && CancelPendingRequest(token.RequestID);",
            operations, StringComparison.Ordinal);
    }


    [Fact]
    public void PaperKeepsRawConstructorCallerPresenceSeparateFromAuthority()
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
