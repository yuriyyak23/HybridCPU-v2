using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6aa decision-only closed-world inventory of the authoritative
/// physical-memory geometry publication producer, lifecycle state and consumers.
/// </summary>
public sealed class Rf126aaMemoryBankGeometryPublicationProducerConsumerInventoryTests
{
    private const string ThisFile =
        "Rf126aaMemoryBankGeometryPublicationProducerConsumerInventoryTests.cs";

    [Fact]
    public void PaperDefinesExactQuiescentAtomicPublicationLifecycle()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(paper,
            "Geometry replacement is an explicit quiescent lifecycle action.",
            "no request is pending or",
            "queued, no bank or port is active, and no synchronous memory operation still",
            "uses the old snapshot",
            "holding its geometry",
            "lifecycle authority");
        Order(paper,
            "Rejection precedence is `InvalidBankCount`, then",
            "`InvalidBankWidth`, then `Busy`, then `GenerationExhausted`, then",
            "`PlatformRejected`; otherwise the result is `Applied`");
        Assert.Contains("fresh non-zero generation is issued only for the atomic publish",
            paper, StringComparison.Ordinal);
        Assert.Contains("Every\nrejection leaves the old geometry, generation, queues and owner state",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativePublicationProducerAndOwnerStorageAreNowUnique()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string production = ReadTree(
            Path.Combine(root, "HybridCPU_ISE"),
            ContractPath(root, "MemoryBankGeometryUpdateResult.cs"),
            ContractPath(root, "PhysicalMemoryBankGeometry.cs"),
            ContractPath(root, "PhysicalMemoryBankBinding.cs"),
            ContractPath(root, "PhysicalMemoryBankResolution.cs"),
            ContractPath(root, "MemoryBankGeometryGeneration.cs"));

        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            subsystem, StringComparison.Ordinal);
        Assert.Contains(
            "private PhysicalMemoryBankGeometry _publishedPhysicalBankGeometry;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("geometryLifecycleGate", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankGeometryUpdateResult", production,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IndependentCompatibilitySettersRemainTheOnlyMutationProducers()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Order(subsystem,
            "private int _numBanks = 8;",
            "public int NumBanks",
            "int sanitized = Math.Max(1, value);",
            "_numBanks = sanitized;",
            "ReconfigureBankTopology();",
            "private int _bankWidthBytes = 64;",
            "public int BankWidthBytes",
            "_bankWidthBytes = value;");
        Assert.Equal(1, Regex.Matches(subsystem,
            @"\bReconfigureBankTopology\s*\(\s*\)").Count - 1);
    }

    [Fact]
    public void CompatibilityTopologyReplacementIsNotAtomicOrRollbackSafe()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Order(subsystem,
            "private void ReconfigureBankTopology()",
            "new bool[_numBanks]",
            "new long[_numBanks]",
            "new Queue<BankRequest>[_numBanks]",
            "Math.Min(existingBankQueues.Length, _numBanks)",
            "bankOccupied = resizedBankOccupied;",
            "bankLastAccessCycle = resizedBankLastAccessCycle;",
            "bankQueues = resizedBankQueues;",
            "roundRobinIndex = _numBanks == 0 ? 0 : roundRobinIndex % _numBanks;");
        Assert.Contains("MemoryBankGeometryUpdateResult",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("PlatformRejected", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("private void ReconfigureBankTopology()", subsystem,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QueueBankPortAndTrbStateAreQuiescenceConsumers()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string helpers = Helpers(root);
        string trb = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "StreamEngine", "BurstIO",
            "TransactionReorderBuffer.cs");

        foreach (string marker in new[]
                 {
                     "private bool[] bankOccupied;",
                     "private PortState[] _portStates;",
                     "private Queue<BankRequest>[] bankQueues;",
                     "public TransactionReorderBuffer TRB;",
                     "private int roundRobinIndex;",
                     "private long currentCycle;"
                 })
        {
            Assert.Contains(marker, subsystem, StringComparison.Ordinal);
        }

        Assert.Contains("bankOccupied[bankId] = true;", helpers,
            StringComparison.Ordinal);
        Assert.Contains("_portStates[i].Busy = true;", helpers,
            StringComparison.Ordinal);
        Assert.Contains("public readonly int OutstandingCount => _count;",
            trb, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyPendingDictionaryCannotServeAsPendingQuiescenceCount()
    {
        string root = FindRepositoryRoot();
        string operations = Operations(root);
        string helpers = Helpers(root);

        Assert.Equal(2, Regex.Matches(operations,
            @"pendingRequests\[requestID\]\s*=\s*token").Count);
        Assert.Contains("matchingToken.IsComplete = true;", helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain("pendingRequests.Remove(request.RequestID)",
            helpers, StringComparison.Ordinal);
        Assert.Contains("pendingRequests.Remove(requestID);", operations,
            StringComparison.Ordinal);
        Assert.Contains("token.IsComplete", operations,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedLegacyRequestsCarryPrivateCapturedGeometryBinding()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string operations = Operations(root);
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Types.cs");

        Assert.Contains("private struct BankRequest", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankIndex bankIndex = ComputeBankId(address);",
            operations, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"bankQueues\[physicalBankBinding\.BankIndex\.Value\]").Count);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", types,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", types,
            StringComparison.Ordinal);
        Match request = Regex.Match(subsystem,
            @"(?s)private struct BankRequest\s*\{.*?\n\s*\}");
        Assert.True(request.Success);
        Assert.Contains(
            "public PhysicalMemoryBankBinding PhysicalBankBinding;",
            request.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", request.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerOwnsAdditionalOutstandingAndQueuedLifecycleState()
    {
        string controller = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");

        foreach (string marker in new[]
                 {
                     "private readonly object _gate = new();",
                     "private readonly Queue<MemoryRequestId> _readQueue = new();",
                     "private readonly Queue<MemoryRequestId> _scalarStoreQueue = new();",
                     "private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding = new();",
                     "private readonly Dictionary<MemoryRequestId, MemoryCompletion> _nextCompletions = new();",
                     "private readonly Dictionary<MemoryRequestId, MemoryCompletion> _publishedCompletions = new();"
                 })
        {
            Assert.Contains(marker, controller, StringComparison.Ordinal);
        }

        Assert.Contains("TryReplacePhysicalMemoryBankGeometry", controller,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_publishedPhysicalBankGeometry", controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SynchronousOperationsUseOwnerGateWithoutActiveOperationLatch()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Assert.Equal(2, Regex.Matches(subsystem,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\)")
            .Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"bankOccupied\[bankIndex\.Value\]\s*=\s*true").Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"bankOccupied\[bankIndex\.Value\]\s*=\s*false").Count);
        Assert.DoesNotMatch(
            @"(?:active|inflight|synchronous)\w*(?:Operation|Access)\w*",
            subsystem);
        Assert.Contains("lock (geometryLifecycleGate)", subsystem,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WiresReplayCompilerAndExternalRootsCarryNoPublicationIdentity()
    {
        string root = FindRepositoryRoot();
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });
        string observation = Read(root, "HybridCPU_ISE", "Machine",
            "IseObservationService.cs");

        Assert.DoesNotContain("PhysicalMemoryBankGeometry", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryUpdateResult", external,
            StringComparison.Ordinal);
        Assert.Contains("NumBanks = mem.NumBanks", observation,
            StringComparison.Ordinal);
        Assert.Contains("BankWidthBytes = mem.BankWidthBytes", observation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", observation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TestsUsePublicCompatibilitySettersWithoutReflectionMutation()
    {
        string root = FindRepositoryRoot();
        string tests = ReadTree(
            Path.Combine(root, "HybridCPU_ISE.Tests"),
            Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture", ThisFile),
            Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126adMemoryBankGeometryAuthoritativeReplacementValidInputCutoverTests.cs"));
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("memory.NumBanks = 4;", tests,
            StringComparison.Ordinal);
        Assert.Contains("memory.BankWidthBytes = 64;", tests,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"GetField\s*\(\s*""(?:_numBanks|bankQueues|bankOccupied|bankLastAccessCycle|_portStates)""",
            tests);
        Assert.DoesNotContain("NumBanks", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BankWidthBytes", testSupport,
            StringComparison.Ordinal);
    }


    private static string ContractPath(string root, string file) =>
        Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", file));

    private static string Subsystem(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.cs");

    private static string Helpers(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Helpers.cs");

    private static string Operations(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1,
                StringComparison.Ordinal);
            Assert.True(next > cursor, $"Missing or out-of-order marker: {marker}");
            cursor = next;
        }
    }

    private static string ReadTree(string sourceRoot, params string[] excluded)
    {
        var excludedPaths = excluded.Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !excludedPaths.Contains(Path.GetFullPath(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "Documentation")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
