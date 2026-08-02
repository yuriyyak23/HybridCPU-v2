using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6as decision-only freeze for the legacy asynchronous physical-bank
/// queue and its cancellation address re-resolution contour.
/// </summary>
public sealed class Rf126asLegacyAsyncCancellationBindingCarrierDecisionTests
{
    private const string ThisFile =
        "Rf126asLegacyAsyncCancellationBindingCarrierDecisionTests.cs";

    [Fact]
    public void PaperSelectsOneOwnerCapturedBindingAndStagedMigration()
    {
        string paper = Paper(Root());

        Order(paper,
            "For the legacy asynchronous physical-bank queue",
            "`EnqueueRead` and",
            "`EnqueueWrite`",
            "must eventually capture",
            "`PhysicalMemoryBankBinding`",
            "under the geometry lifecycle gate",
            "before",
            "publishing either `pendingRequests` state or a `BankRequest`",
            "same immutable",
            "binding belongs",
            "public poll token",
            "caller-supplied location authority",
            "Migration of that contour is staged",
            "preserving public signatures",
            "cancellation re-resolution",
            "generation mismatch",
            "separate decision and implementation slice");
    }

    [Fact]
    public void ExactlyTwoAcceptedProducersCaptureBeforePublishingAndQueueing()
    {
        string operations = Operations(Root());

        Assert.Equal(1, Regex.Matches(operations,
            @"public\s+MemoryRequestToken\s+EnqueueRead\s*\(").Count);
        Assert.Equal(1, Regex.Matches(operations,
            @"public\s+MemoryRequestToken\s+EnqueueWrite\s*\(").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"ulong\s+requestID\s*=\s*nextRequestID\+\+;").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"pendingRequests\[requestID\]\s*=\s*token;").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\);").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"bankQueues\[physicalBankBinding\.BankIndex\.Value\]").Count);

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
                "new MemoryRequestToken(",
                "pendingRequests[requestID] = token;",
                "new BankRequest",
                "PhysicalBankBinding = physicalBankBinding",
                "bankQueues[physicalBankBinding.BankIndex.Value]");
        }
    }

    [Fact]
    public void ProductionCallerTopologyRemainsZeroReadsOneWriteAndOneTokenCancel()
    {
        string production = ReadTree(Path.Combine(Root(), "HybridCPU_ISE"),
            ThisFile);

        Assert.Equal(1, Regex.Matches(production,
            @"\bEnqueueRead\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bEnqueueWrite\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bCancelPendingRequest\s*\(\s*MemoryRequestToken\?").Count +
            Regex.Matches(production,
                @"\.CancelPendingRequest\s*\(\s*lane\.PendingMemoryRequest\s*\)")
                .Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bCancelPendingRequest\s*\(\s*ulong\s+requestID\s*\)").Count +
            Regex.Matches(production,
                @"CancelPendingRequest\(token\.RequestID\)").Count);
    }

    [Fact]
    public void PublicTokenIsUncheckedPollCarrierWithNoLocationAuthority()
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
        Assert.DoesNotMatch(
            @"public\s+(?:PhysicalMemoryBankBinding|PhysicalBankBinding)\s+",
            token);

        string production = ReadTree(Path.Combine(Root(), "HybridCPU_ISE"),
            ThisFile);
        Assert.Equal(2, Regex.Matches(production,
            @"new\s+MemoryRequestToken\s*\(").Count);
        string externalConstruction = ReadTrees(Root(),
            "HybridCPU_Compiler", "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps");
        Assert.DoesNotContain("new MemoryRequestToken(",
            externalConstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateBankRequestHasExactlyTwoInitializersAndStoredBinding()
    {
        string root = Root();
        string subsystem = Subsystem(root);
        string operations = Operations(root);
        string bankRequest = Slice(subsystem,
            "private struct BankRequest",
            "private sealed class PhysicalBankTopologyCandidate");

        Assert.Contains("public PhysicalMemoryBankBinding PhysicalBankBinding;",
            bankRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", bankRequest,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(operations,
            @"new\s+BankRequest\s*\{").Count);
    }

    [Fact]
    public void CancellationUsesAuthoritativeTokenButRemovesBeforeReresolution()
    {
        string operations = Operations(Root());
        string cancel = Slice(operations,
            "public bool CancelPendingRequest(MemoryRequestToken? token)",
            "#endregion");

        Order(cancel,
            "token != null && CancelPendingRequest(token.RequestID)",
            "if (requestID == 0)",
            "pendingRequests.TryGetValue(requestID, out token)",
            "token.IsComplete",
            "token.GetPhysicalBankBindingForOwner();",
            "pendingRequests.Remove(requestID);",
            "RemoveQueuedBankRequest(");
    }

    [Fact]
    public void LiveWidthRemapAndBankShrinkInvalidBehaviorRemainsExact()
    {
        MemorySubsystem remapped = CreateMemory(4, 64);
        MemorySubsystem.MemoryRequestToken remappedToken =
            remapped.EnqueueRead(0, 64, 1, new byte[1]);
        remapped.BankWidthBytes = 128;
        Assert.True(remapped.CancelPendingRequest(remappedToken));
        Assert.Equal(0, remapped.CurrentQueuedRequests);
        Assert.False(remapped.CancelPendingRequest(remappedToken));

        MemorySubsystem shrunk = CreateMemory(4, 64);
        MemorySubsystem.MemoryRequestToken droppedToken =
            shrunk.EnqueueRead(0, 3UL * 64UL, 1, new byte[1]);
        shrunk.NumBanks = 2;
        Assert.Equal(0, shrunk.CurrentQueuedRequests);
        Assert.False(shrunk.CancelPendingRequest(droppedToken));
    }

    [Fact]
    public void CompletionPublishesOnlyThroughMatchingPendingIdentity()
    {
        string helpers = Helpers(Root());
        string execute = Slice(helpers,
            "private void ExecuteBankRequest(",
            "internal void AdvanceBoundDmaAgentOneCycle()");

        Order(execute,
            "IOMMU.ReadBurst",
            "IOMMU.WriteBurst",
            "pendingRequests.TryGetValue(request.RequestID",
            "!matchingToken.IsComplete",
            "matchingToken.IsComplete = true;",
            "matchingToken.Succeeded = success;",
            "matchingToken.CompleteCycle = currentCycle;",
            "matchingToken.FailureReason =");
    }

    [Fact]
    public void GeometryReplacementRequiresLegacyTokenAndQueueQuiescence()
    {
        string subsystem = Subsystem(Root());
        string quiescence = Slice(subsystem,
            "private bool IsPhysicalBankGeometryOwnerQuiescent()",
            "private bool TryPreparePhysicalBankTopologyCandidate(");

        Order(quiescence,
            "foreach (MemoryRequestToken token in pendingRequests.Values)",
            "if (!token.IsComplete)",
            "return false;",
            "foreach (Queue<BankRequest> queue in bankQueues)",
            "queue.Count != 0",
            "return false;");
    }

    [Fact]
    public void ExternalWireReflectionAndTestSupportHaveNoLocationMutationSeam()
    {
        string root = Root();
        string external = ReadTrees(root,
            "HybridCPU_Compiler", "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string architecture = ReadTree(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture"), ThisFile);

        Assert.DoesNotContain("MemoryRequestToken", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("pendingRequests", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"SetValue\s*\([^;\r\n]*(?:pendingRequests|bankQueues|MemoryRequestToken)",
            architecture);
        Assert.DoesNotMatch(
            @"\b(?:SlotId|LaneId|PinnedLaneId)\b",
            Operations(root) + Subsystem(root));
    }


    private static MemorySubsystem CreateMemory(
        int numBanks,
        int bankWidthBytes)
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor)
        {
            NumBanks = numBanks,
            BankWidthBytes = bankWidthBytes
        };
    }

    private static string Operations(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs");

    private static string Helpers(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Helpers.cs");

    private static string Subsystem(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
