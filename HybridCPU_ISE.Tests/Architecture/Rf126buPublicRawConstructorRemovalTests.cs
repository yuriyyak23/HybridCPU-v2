using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bu public raw-token constructor removal contract.</summary>
public sealed class Rf126buPublicRawConstructorRemovalTests
{
    [Fact]
    public void OnlyInternalAcceptedConstructorRemains()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs"));
        string token = Slice(source, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");

        Assert.Equal(0, Regex.Matches(token,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(token,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", token,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerAcceptedConstructionAndCancellationRemainOperational()
    {
        Processor processor = default;
        MemorySubsystem memory = new(ref processor) { NumBanks = 4, BankWidthBytes = 64 };
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(0, 64, 1, new byte[1]);
        Assert.True(memory.CancelPendingRequest(token));
        Assert.Equal(0, memory.CurrentQueuedRequests);
    }

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
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
