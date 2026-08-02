using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ae closed-world accepted-request physical-bank binding inventory.
/// These guards select one later controller-native storage cutover and make no
/// production, resolver, queue-consumer, cancellation, wire or invalid-input
/// migration.
/// </summary>
public sealed class Rf126aeAcceptedRequestPhysicalBankBindingInventoryTests
{
    [Fact]
    public void PaperRequiresCaptureBeforeQueueAndStoredBindingConsumers()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "Acceptance fixes lifetime identity.",
            "Every accepted asynchronous memory request",
            "captures its request identity plus the resolved physical bank index and",
            "geometry generation before it enters a bank queue.",
            "Queue lookup, arbitration,",
            "completion and cancellation use that captured binding",
            "cancellation may not",
            "re-resolve the request address against current geometry.");
        Assert.Contains(
            "The binding is",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "evidence for locating the request only and does not grant completion or store",
            paper, StringComparison.Ordinal);
        Assert.Contains("publication authority", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyAcceptanceHasTwoProducersAndCapturesOneBindingEach()
    {
        string operations = Operations(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(operations,
            @"public MemoryRequestToken EnqueueRead\(").Count);
        Assert.Equal(1, Regex.Matches(operations,
            @"public MemoryRequestToken EnqueueWrite\(").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankIndex bankIndex = ComputeBankId\(address\);")
            .Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"bankQueues\[physicalBankBinding\.BankIndex\.Value\]").Count);
        Assert.DoesNotContain("GeometryGeneration", operations,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyTokenPendingMapAndBankRequestCarryPrivateBinding()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Assert.Contains(
            "private readonly System.Collections.Generic.Dictionary<ulong, MemoryRequestToken> pendingRequests;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("private struct BankRequest", subsystem,
            StringComparison.Ordinal);
        foreach (string field in new[]
                 {
                     "public ulong RequestID;", "public ulong DeviceID;",
                     "public ulong Address;", "public int Length;",
                     "public bool IsRead;", "public long EnqueueCycle;"
                 })
        {
            Assert.Contains(field, subsystem, StringComparison.Ordinal);
        }

        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Null(token.GetProperty("PhysicalBankBinding",
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance));
        FieldInfo? binding = token.GetField("physicalBankBinding",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(binding);
        Assert.Equal(typeof(PhysicalMemoryBankBinding), binding!.FieldType);
        Assert.Null(token.GetProperty("PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance));
        Assert.Null(token.GetProperty("GeometryGeneration",
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance));
    }

    [Fact]
    public void LegacyQueueArbitrationCompletionAndCancellationUseRawLocation()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string operations = Operations(root);

        Assert.Contains("bankQueues[targetBank].Dequeue()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("bankQueues[selectedBank].Dequeue()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("ExecuteBankRequest(targetBank, request)", helpers,
            StringComparison.Ordinal);
        Assert.Contains("ExecuteBankRequest(selectedBank, request)", helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingRequests.TryGetValue(request.RequestID, out MemoryRequestToken? matchingToken)",
            helpers, StringComparison.Ordinal);
        Order(
            operations,
            "PhysicalMemoryBankBinding physicalBankBinding;",
            "token.GetPhysicalBankBindingForOwner();",
            "pendingRequests.Remove(requestID);",
            "RemoveQueuedBankRequest(");
    }

    [Fact]
    public void ControllerHasExactlySixAcceptedRequestProducerFamilies()
    {
        string controller = Controller(FindRepositoryRoot());

        foreach (string producer in new[]
                 {
                     "TryAcceptExplicitPacketScalarLoad",
                     "TryAcceptSingleLaneScalarLoad",
                     "TryAcceptVectorSegmentLoad",
                     "TryAcceptCanonicalVectorTransfer",
                     "TryAcceptExplicitPacketScalarStore",
                     "TryAcceptSingleLaneScalarStore"
                 })
        {
            Assert.Equal(1, Regex.Matches(controller,
                $@"public MemoryAdmissionResult {producer}\(").Count);
        }

        Assert.Equal(4, Regex.Matches(controller,
            @"TryAcceptRead\(").Count);
        Assert.Equal(3, Regex.Matches(controller,
            @"TryAcceptScalarStore\(").Count);
        Assert.Equal(3, Regex.Matches(controller,
            @"_outstanding\.Add\(").Count);
        Assert.Equal(2, Regex.Matches(controller,
            @"_readQueue\.Enqueue\(requestId\);").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"_scalarStoreQueue\.Enqueue\(requestId\);").Count);
    }

    [Fact]
    public void ControllerRequestIsOneImmutableCarrierWithSelectedBindingStorage()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Contains("private readonly record struct ControllerRequest(",
            controller, StringComparison.Ordinal);
        foreach (string field in new[]
                 {
                     "ReadRequestClass? ReadRequestClass",
                     "ScalarStoreRequestClass? StoreRequestClass",
                     "ulong DeviceId", "ulong Address", "int Size",
                     "byte[] Data",
                     "PhysicalMemoryBankBinding PhysicalBankBinding",
                     "uint Opcode = 0",
                     "ulong DestinationAddress = 0",
                     "ulong ElementCount = 0", "int ElementSize = 0",
                     "ushort Stride = 0"
                 })
        {
            Assert.Contains(field, controller, StringComparison.Ordinal);
        }
        Assert.Equal(5, Regex.Matches(controller,
            @"\bPhysicalMemoryBankBinding\b").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
    }

    [Fact]
    public void ControllerQueuesCompletionAndCancellationRetainRequestIdOnly()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Contains(
            "private readonly Queue<MemoryRequestId> _readQueue = new();",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "private readonly Queue<MemoryRequestId> _scalarStoreQueue = new();",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding = new();",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "private readonly Dictionary<MemoryRequestId, MemoryCompletion> _nextCompletions = new();",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "private readonly Dictionary<MemoryRequestId, MemoryCompletion> _publishedCompletions = new();",
            controller, StringComparison.Ordinal);
        Assert.Contains("_readQueue.Dequeue()", controller,
            StringComparison.Ordinal);
        Assert.Contains("_scalarStoreQueue.Dequeue()", controller,
            StringComparison.Ordinal);
        Assert.Contains("_outstanding.Remove(requestId", controller,
            StringComparison.Ordinal);
        Assert.Contains("_nextCompletions.Remove(requestId)", controller,
            StringComparison.Ordinal);
        Assert.Contains("_publishedCompletions.Remove(requestId)", controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCallerManifestIsExact()
    {
        string production = ReadTree(Path.Combine(
            FindRepositoryRoot(), "HybridCPU_ISE"));

        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["EnqueueRead"] = 1,
            ["EnqueueWrite"] = 2,
            ["TryAcceptExplicitPacketScalarLoad"] = 2,
            ["TryAcceptSingleLaneScalarLoad"] = 2,
            ["TryAcceptVectorSegmentLoad"] = 2,
            ["TryAcceptCanonicalVectorTransfer"] = 2,
            ["TryAcceptExplicitPacketScalarStore"] = 2,
            ["TryAcceptSingleLaneScalarStore"] = 2
        };
        foreach ((string method, int count) in expected)
        {
            Assert.Equal(count, Regex.Matches(production,
                $@"\b{method}\s*\(").Count);
        }
    }

    [Fact]
    public void ConsumerHandlesDoNotConflateRequestAndGeometryIdentity()
    {
        string root = FindRepositoryRoot();
        string consumerTree = string.Join("\n", new[]
        {
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Stages", "Memory", "CPU_Core.PipelineExecution.Memory.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "MicroOps", "Memory", "MicroOp.LoadStore.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "MicroOps", "Vector", "VectorMicroOps.Memory.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "MicroOps", "Vector", "VectorMicroOps.Data.cs")
        });

        Assert.Contains("MemoryRequestId?", consumerTree,
            StringComparison.Ordinal);
        Assert.Contains("MemoryRequestToken?", consumerTree,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", consumerTree,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", consumerTree,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionTestSupportAndWireMutationSeamsRemainAbsent()
    {
        string root = FindRepositoryRoot();
        string tests = string.Join("\n",
            Directory.EnumerateFiles(
                    Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !path.EndsWith(
                    "Rf126afControllerNativeAcceptedRequestBindingStorageTests.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(
                    "Rf126ahControllerOrdinaryReadStoredBindingValidInputCutoverTests.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(
                    "Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(
                    "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.DoesNotMatch(
            @"GetField\s*\(\s*""(?:pendingRequests|bankQueues|_outstanding|_readQueue|_scalarStoreQueue)""",
            tests);
        Assert.DoesNotMatch(
            @"SetValue\s*\(\s*(?:memory|controller).*(?:pendingRequests|bankQueues|_outstanding|_readQueue|_scalarStoreQueue)",
            tests);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedLaterCutoverIsControllerRequestStorageOnly()
    {
        string evidence = Evidence(FindRepositoryRoot());

        Assert.Contains(
            "Selected later cutover: **RF-12.6af controller-native accepted-request binding storage**",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "`ControllerRequest` is the only storage target",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "legacy",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "`MemoryRequestToken`, `pendingRequests`, `BankRequest` and cancellation",
            evidence, StringComparison.Ordinal);
        Assert.Contains("Production/runtime change: none", evidence,
            StringComparison.Ordinal);
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

    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root, "Documentation",
        "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ae-accepted-request-physical-bank-binding-inventory-decision.md");

    private static string ReadTree(string root) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
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

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "ResearchPaper")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1, StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Expected marker after offset {cursor}: {marker}");
            cursor = next;
        }
    }
}
