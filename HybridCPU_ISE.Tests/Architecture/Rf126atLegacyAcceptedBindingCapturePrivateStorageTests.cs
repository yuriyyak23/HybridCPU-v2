using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6at valid-input guard for legacy asynchronous accepted-binding
/// capture, owner-only token storage and private bank-request storage.
/// </summary>
public sealed class Rf126atLegacyAcceptedBindingCapturePrivateStorageTests
{
    private const string ThisFile =
        "Rf126atLegacyAcceptedBindingCapturePrivateStorageTests.cs";

    [Fact]
    public void BothProducersCaptureBeforePublicationAndEnqueueByCapturedIndex()
    {
        string operations = Operations(Root());

        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\);").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=\s*\r?\n\s*PhysicalMemoryBankBinding\.Create").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalBankBinding\s*=\s*physicalBankBinding").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"bankQueues\[physicalBankBinding\.BankIndex\.Value\]\s*\r?\n\s*\.Enqueue\(bankRequest\);").Count);

        foreach (string start in new[]
                 {
                     "public MemoryRequestToken EnqueueRead(",
                     "public MemoryRequestToken EnqueueWrite("
                 })
        {
            string producer = Slice(operations, start,
                start.Contains("Read", StringComparison.Ordinal)
                    ? "public MemoryRequestToken EnqueueWrite("
                    : "public bool CancelPendingRequest(");
            Order(producer,
                "lock (geometryLifecycleGate)",
                "ulong requestID = nextRequestID++;",
                "ComputeBankId(address)",
                "PhysicalMemoryBankBinding.Create(",
                "_publishedPhysicalBankGeometry.Generation",
                "new MemoryRequestToken(",
                "physicalBankBinding: physicalBankBinding",
                "pendingRequests[requestID] = token;",
                "new BankRequest",
                "PhysicalBankBinding = physicalBankBinding",
                "bankQueues[physicalBankBinding.BankIndex.Value]",
                ".Enqueue(bankRequest);");
        }
    }

    [Fact]
    public void AuthoritativeTokenAndQueuedRequestStoreTheSameImmutableValue()
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(
            0,
            64,
            1,
            new byte[1]);

        PhysicalMemoryBankBinding tokenBinding =
            (PhysicalMemoryBankBinding)Field(
                token,
                "physicalBankBinding")!;
        Assert.True(tokenBinding.IsWellFormed);
        Assert.Equal(1, tokenBinding.BankIndex.Value);
        Assert.Equal(
            memory.PublishedPhysicalBankGeometry.Generation,
            tokenBinding.Generation);

        Array bankQueues = (Array)Field(memory, "bankQueues")!;
        object queue = bankQueues.GetValue(tokenBinding.BankIndex.Value)!;
        object queuedRequest = queue.GetType().GetMethod("Peek")!
            .Invoke(queue, null)!;
        PhysicalMemoryBankBinding requestBinding =
            (PhysicalMemoryBankBinding)Field(
                queuedRequest,
                "PhysicalBankBinding")!;

        Assert.Equal(tokenBinding, requestBinding);
    }

    [Fact]
    public void PublicCompatibilityConstructorCannotAcceptLocationAuthority()
    {
        string token = Slice(Subsystem(Root()),
            "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Regex.Matches(token,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(token,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", token,
            StringComparison.Ordinal);
        Assert.Contains(
            "private readonly PhysicalMemoryBankBinding physicalBankBinding;",
            token, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateBankRequestStoresBindingAndCancellationIsItsFirstConsumer()
    {
        string subsystem = Subsystem(Root());
        string bankRequest = Slice(subsystem,
            "private struct BankRequest",
            "private sealed class PhysicalBankTopologyCandidate");
        string helpers = Helpers(Root());
        string operations = Operations(Root());

        Assert.Contains(
            "public PhysicalMemoryBankBinding PhysicalBankBinding;",
            bankRequest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "request.PhysicalBankBinding",
            helpers,
            StringComparison.Ordinal);
        Assert.Contains("token.GetPhysicalBankBindingForOwner();", operations,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ": ComputeBankId(token.Address);",
            operations,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationAndLiveSetterInvalidBehaviorRemainExact()
    {
        MemorySubsystem remapped = CreateMemory();
        MemorySubsystem.MemoryRequestToken remappedToken =
            remapped.EnqueueRead(0, 64, 1, new byte[1]);
        remapped.BankWidthBytes = 128;
        Assert.True(remapped.CancelPendingRequest(remappedToken));
        Assert.Equal(0, remapped.CurrentQueuedRequests);
        Assert.False(remapped.CancelPendingRequest(remappedToken));

        MemorySubsystem shrunk = CreateMemory();
        MemorySubsystem.MemoryRequestToken droppedToken =
            shrunk.EnqueueRead(0, 3UL * 64UL, 1, new byte[1]);
        shrunk.NumBanks = 2;
        Assert.Equal(0, shrunk.CurrentQueuedRequests);
        Assert.False(shrunk.CancelPendingRequest(droppedToken));
    }

    [Fact]
    public void ExternalWireReplayCertificateTelemetryAndTestSupportStayAbsent()
    {
        string root = Root();
        string external = ReadTrees(root,
            "HybridCPU_Compiler",
            "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge",
            "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
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
        Assert.DoesNotMatch(
            @"\b(?:SlotId|LaneId|PinnedLaneId)\b",
            Operations(root) + Subsystem(root));
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
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "ResearchPaper")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
