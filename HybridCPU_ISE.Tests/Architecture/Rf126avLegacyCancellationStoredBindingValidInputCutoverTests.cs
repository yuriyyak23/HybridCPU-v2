using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6av valid-input guard for legacy raw-ID cancellation's owner-captured
/// physical-bank binding. RF-12.6ax owns the later invalid-behavior cutover.
/// </summary>
public sealed class Rf126avLegacyCancellationStoredBindingValidInputCutoverTests
{
    private const string ThisFile =
        "Rf126avLegacyCancellationStoredBindingValidInputCutoverTests.cs";

    [Fact]
    public void RawIdCancellationHadStoredBindingValidInputCutoverBeforeAx()
    {
        string cancel = Cancellation(Root());

        Assert.Contains("token.GetPhysicalBankBindingForOwner();", cancel,
            StringComparison.Ordinal);
        Assert.Contains("RemoveQueuedBankRequest(", cancel,
            StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(cancel,
            @"token\.GetPhysicalBankBindingForOwner\(\)").Count);
    }

    [Fact]
    public void StableReadAndWriteCancellationRemoveTheirCapturedQueueEntries()
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken read = memory.EnqueueRead(
            0, 64, 1, new byte[1]);
        MemorySubsystem.MemoryRequestToken write = memory.EnqueueWrite(
            0, 3UL * 64UL, 1, new byte[1]);

        Assert.True(Binding(read).IsWellFormed);
        Assert.True(Binding(write).IsWellFormed);
        Assert.Equal(memory.PublishedPhysicalBankGeometry.Generation,
            Binding(read).Generation);
        Assert.Equal(2, memory.CurrentQueuedRequests);
        Assert.True(memory.CancelPendingRequest(read.RequestID));
        Assert.True(memory.CancelPendingRequest(write));
        Assert.Equal(0, memory.CurrentQueuedRequests);
    }

    [Fact]
    public void AvEvidenceRecordsThePreAxRawFallbackBehavior()
    {
        string evidence = Read(Root(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.6av-legacy-cancellation-stored-binding-valid-input-cutover.md");
        Assert.Contains("ComputeBankId` path remains the exact fallback",
            evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicTokenCannotInjectBindingAndAuthoritativeMapStillWins()
    {
        string subsystem = Read(Root(), "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.cs");
        string tokenSource = Slice(subsystem, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Regex.Matches(tokenSource,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(tokenSource,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", tokenSource,
            StringComparison.Ordinal);

        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken accepted = memory.EnqueueRead(
            0, 64, 1, new byte[1]);
        Assert.Contains(
            "return token != null && CancelPendingRequest(token.RequestID);",
            Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.Operations.cs"),
            StringComparison.Ordinal);
        Assert.True(memory.CancelPendingRequest(accepted));
        Assert.Equal(0, memory.CurrentQueuedRequests);
    }

    [Fact]
    public void QueueServiceCompletionWiresAndTestSupportRemainOutsideCutover()
    {
        string root = Root();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string external = ReadTrees(root, "HybridCPU_Compiler", "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.DoesNotContain("request.PhysicalBankBinding", helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestToken", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("physicalBankBinding", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:SlotId|LaneId|PinnedLaneId)\b",
            Cancellation(root) + helpers);
    }


    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
    }

    private static PhysicalMemoryBankBinding Binding(
        MemorySubsystem.MemoryRequestToken token) =>
        (PhysicalMemoryBankBinding)token.GetType().GetField("physicalBankBinding",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(token)!;

    private static string Cancellation(string root)
    {
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");
        return Slice(operations, "public bool CancelPendingRequest(ulong requestID)",
            "#endregion");
    }

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1,
                StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Expected marker after offset {cursor}: {marker}");
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
        string.Join("\n", roots.Select(relative => ReadTree(
            Path.Combine(root, relative), ThisFile)));

    private static string ReadTree(string root, params string[] excluded) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/bin/",
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/",
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => !excluded.Any(name => path.EndsWith(name,
                StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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
