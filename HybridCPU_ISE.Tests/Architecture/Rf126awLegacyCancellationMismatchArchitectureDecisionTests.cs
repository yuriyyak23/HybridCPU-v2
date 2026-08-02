using System.Reflection;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6aw decision-only guard for legacy cancellation mismatch authority.
/// </summary>
public sealed class Rf126awLegacyCancellationMismatchArchitectureDecisionTests
{
    [Fact]
    public void PaperSelectsFailurePrecedenceAndOneCapturedIndexRemoval()
    {
        string paper = Read(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Order(paper,
            "Request ID zero, an",
            "absent pending-map entry and a completed token",
            "malformed/default binding is the first binding failure",
            "generation\nmismatch against the published snapshot is second",
            "and an index outside that\npublished snapshot is third",
            "returns `false` without",
            "removing the pending-map entry",
            "legacy live raw\n`NumBanks` or `BankWidthBytes` setter drift",
            "Cancellation removes\nthe authoritative pending-map entry before",
            "attempting exactly one removal\nfrom that captured-index queue",
            "There is\nno address re-resolution fallback");
    }

    [Fact]
    public void InventoryConfirmsExactlyWhyWidthAndShrinkNeedDifferentResults()
    {
        string owner = Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.cs");
        string resize = Slice(owner, "private void ReconfigureBankTopology()",
            "private void ReconfigurePortStates()");

        Assert.Contains("_bankWidthBytes = value;", owner,
            StringComparison.Ordinal);
        Order(resize, "Math.Min(existingBankQueues.Length, _numBanks)",
            "new Queue<BankRequest>(existingBankQueues![i])",
            "resizedBankQueues[i] = new Queue<BankRequest>();");
        Assert.DoesNotContain("ComputeBankId", resize, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidBindingHasNoSupportedPublicInjectionAuthority()
    {
        string subsystem = Read(Root(), "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.cs");
        string tokenSource = Slice(subsystem, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Count(tokenSource, "public MemoryRequestToken("));
        Assert.Equal(1, Count(tokenSource, "internal MemoryRequestToken("));
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", tokenSource,
            StringComparison.Ordinal);

        string operations = Cancellation(Root());
        Assert.Equal(1, Count(operations, "token.GetPhysicalBankBindingForOwner()"));
        Assert.Equal(0, Count(operations, "ComputeBankId(token.Address)"));
        Assert.DoesNotContain("PhysicalMemoryBankBinding", ReadTrees(Root(),
            "HybridCPU_Compiler", "CpuInterfaceBridge", "HybridCPU_RoslynBridge",
            "TestAssemblerConsoleApps"), StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionAuthorityIsRealizedOnlyByTheLaterAxImplementation()
    {
        string cancellation = Cancellation(Root());
        Assert.Contains("token.GetPhysicalBankBindingForOwner();", cancellation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ComputeBankId(token.Address)", cancellation,
            StringComparison.Ordinal);
    }


    private static int Count(string text, string value) => text.Split(value).Length - 1;

    private static string Cancellation(string root) => Slice(Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs"),
        "public bool CancelPendingRequest(ulong requestID)", "#endregion");

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1, StringComparison.Ordinal);
            Assert.True(next > cursor, $"Expected marker after {cursor}: {marker}");
            cursor = next;
        }
    }

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }

    private static string ReadTrees(string root, params string[] roots) =>
        string.Join("\n", roots.Select(relative => string.Join("\n",
            Directory.EnumerateFiles(Path.Combine(root, relative), "*.cs",
                SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/bin/",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Replace('\\', '/').Contains("/obj/",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText))));

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
