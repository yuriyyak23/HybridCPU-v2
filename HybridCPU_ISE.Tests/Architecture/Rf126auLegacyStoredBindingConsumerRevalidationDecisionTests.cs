using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6au decision-only guard for the legacy asynchronous queue's stored
/// binding consumers. This authorizes no production migration.
/// </summary>
public sealed class Rf126auLegacyStoredBindingConsumerRevalidationDecisionTests
{
    private const string ThisFile =
        "Rf126auLegacyStoredBindingConsumerRevalidationDecisionTests.cs";

    [Fact]
    public void PaperStagesStableBindingConsumptionAfterPrivateAcceptanceStorage()
    {
        string paper = Read(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("Queue lookup, arbitration,", paper,
            StringComparison.Ordinal);
        Assert.Contains("completion and cancellation use that captured binding",
            paper, StringComparison.Ordinal);
        Assert.Contains("Then cut valid stable-generation queue and", paper,
            StringComparison.Ordinal);
        Assert.Contains("cancellation consumers over to the stored binding.",
            paper, StringComparison.Ordinal);
        Assert.Contains("including whether cancellation falls", paper,
            StringComparison.Ordinal);
        Assert.Contains("separate decision and implementation slice", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyConsumerInventoryAddsOnlyCancellationAsBindingReader()
    {
        string root = Root();
        string operations = Operations(root);
        string helpers = Helpers(root);
        string subsystem = Subsystem(root);
        string cancel = Slice(operations, "public bool CancelPendingRequest(ulong requestID)",
            "#endregion");
        string execute = Slice(helpers, "private void ExecuteBankRequest(",
            "/// <summary>\n        /// Advance the explicitly bound persistent DMA agent");

        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalBankBinding\s*=\s*physicalBankBinding").Count);
        Assert.Contains("private readonly PhysicalMemoryBankBinding physicalBankBinding;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("public PhysicalMemoryBankBinding PhysicalBankBinding;",
            subsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("ComputeBankId(token.Address)", cancel,
            StringComparison.Ordinal);
        Assert.Contains("token.GetPhysicalBankBindingForOwner()", cancel,
            StringComparison.Ordinal);
        Assert.DoesNotContain("request.PhysicalBankBinding", execute,
            StringComparison.Ordinal);
        Assert.Contains("ExecuteBankRequest(targetBank, request);", helpers,
            StringComparison.Ordinal);
        Assert.Contains("ExecuteBankRequest(selectedBank, request);", helpers,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationAndServiceHaveDistinctCurrentLocationAuthorities()
    {
        string root = Root();
        string operations = Operations(root);
        string helpers = Helpers(root);
        string cancel = Slice(operations, "public bool CancelPendingRequest(ulong requestID)",
            "#endregion");
        string completion = Slice(helpers, "private void ExecuteBankRequest(",
            "// Update statistics");

        Order(cancel,
            "if (requestID == 0)",
            "lock (geometryLifecycleGate)",
            "pendingRequests.TryGetValue(requestID, out token)",
            "token.IsComplete",
            "token.GetPhysicalBankBindingForOwner();",
            "pendingRequests.Remove(requestID);",
            "RemoveQueuedBankRequest(");
        Assert.Contains("bankQueues[targetBank].Dequeue()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("bankQueues[selectedBank].Dequeue()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("pendingRequests.TryGetValue(request.RequestID",
            completion, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBankBinding", completion,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StableGenerationCancellationIsTheOnlySelectedLaterCutover()
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(
            0, 64, 1, new byte[1]);
        PhysicalMemoryBankBinding binding = (PhysicalMemoryBankBinding)Field(
            token, "physicalBankBinding")!;

        Assert.True(binding.IsWellFormed);
        Assert.Equal(memory.PublishedPhysicalBankGeometry.Generation,
            binding.Generation);
        Assert.True(memory.CancelPendingRequest(token));
        Assert.Equal(0, memory.CurrentQueuedRequests);

        string evidence = Read(Root(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF12",
            "rf12.6au-legacy-stored-binding-consumer-revalidation-decision.md");
        Assert.Contains("RF-12.6av legacy raw-ID cancellation", evidence,
            StringComparison.Ordinal);
        Assert.Contains("cancellation", evidence, StringComparison.Ordinal);
        Assert.Contains("Queue-service validation is not selected", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Production/runtime change: none", evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveSetterAndPublicTokenCompatibilityRemainOutsideSelection()
    {
        MemorySubsystem remapped = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = remapped.EnqueueRead(
            0, 64, 1, new byte[1]);
        remapped.BankWidthBytes = 128;
        Assert.True(remapped.CancelPendingRequest(token));
        Assert.Equal(0, remapped.CurrentQueuedRequests);

        string tokenSource = Slice(Subsystem(Root()),
            "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Regex.Matches(tokenSource,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(tokenSource,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", tokenSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WireReplayCertificateTelemetryAndTestSupportRemainBindingFree()
    {
        string root = Root();
        string external = ReadTrees(root,
            "HybridCPU_Compiler",
            "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge",
            "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string architecture = ReadTree(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture"), ThisFile);

        Assert.DoesNotContain("MemoryRequestToken", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("physicalBankBinding", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"SetValue\s*\([^;\r\n]*(?:physicalBankBinding|PhysicalBankBinding)",
            architecture);
        Assert.DoesNotMatch(@"\b(?:SlotId|LaneId|PinnedLaneId)\b",
            Operations(root) + Helpers(root) + Subsystem(root));
    }


    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
    }

    private static object? Field(object target, string name) =>
        target.GetType().GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance)!.GetValue(target);

    private static string Operations(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs");

    private static string Helpers(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Helpers.cs");

    private static string Subsystem(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.cs");

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first,
            $"Could not slice '{start}' through '{end}'.");
        return text[first..last];
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

    private static string ReadTrees(string root, params string[] roots) =>
        string.Join("\n", roots.Select(relative =>
            ReadTree(Path.Combine(root, relative), ThisFile)));

    private static string ReadTree(string root, params string[] excluded) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excluded.Any(name => path.EndsWith(name,
                StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

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
