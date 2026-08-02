using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ab decision-only mapping of geometry lifecycle serialization and
/// the exact quiescence predicate onto current timed-memory owner state.
/// </summary>
public sealed class Rf126abMemoryBankGeometryLifecycleQuiescenceArchitectureDecisionTests
{
    private const string ThisFile =
        "Rf126abMemoryBankGeometryLifecycleQuiescenceArchitectureDecisionTests.cs";

    [Fact]
    public void PaperDefinesQuiescenceAndExactOutcomePrecedence()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(paper,
            "Geometry replacement is an explicit quiescent lifecycle action.",
            "no request is pending or",
            "queued, no bank or port is active, and no synchronous memory operation still",
            "uses the old snapshot");
        Order(paper,
            "Rejection precedence is `InvalidBankCount`, then",
            "`InvalidBankWidth`, then `Busy`, then `GenerationExhausted`, then",
            "`PlatformRejected`; otherwise the result is `Applied`");
        Assert.Contains("holding its geometry\nlifecycle authority", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MemorySubsystemRemainsOwnerAndControllerRemainsCoordinatorOnly()
    {
        string root = FindRepositoryRoot();
        string evidence = Evidence(root);

        Assert.Contains(
            "`MemorySubsystem` remains the geometry-publication authority",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "`MemoryCycleController` is only the outer serialization coordinator",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "controller gate -> owner geometry gate",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "No path may acquire the controller gate while holding the owner gate",
            evidence, StringComparison.Ordinal);
    }


    [Fact]
    public void LegacyPendingPredicateDistinguishesCompletedRetainedTokens()
    {
        string root = FindRepositoryRoot();
        string operations = Operations(root);
        string helpers = Helpers(root);
        string evidence = Evidence(root);

        Assert.Contains("matchingToken.IsComplete = true;", helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain("pendingRequests.Remove(request.RequestID)",
            helpers, StringComparison.Ordinal);
        Assert.Contains("pendingRequests.Remove(requestID);", operations,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingRequests.Values.All(token => token.IsComplete)",
            evidence, StringComparison.Ordinal);
        Assert.Contains("bankQueues.All(queue => queue.Count == 0)",
            evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerPredicateCoversEveryQueuedOutstandingCompletionState()
    {
        string root = FindRepositoryRoot();
        string controller = Controller(root);
        string evidence = Evidence(root);

        foreach (string marker in new[]
                 {
                     "_readQueue", "_scalarStoreQueue", "_outstanding",
                     "_nextCompletions", "_publishedCompletions"
                 })
        {
            Assert.Contains(marker, controller, StringComparison.Ordinal);
            Assert.Contains(marker, evidence, StringComparison.Ordinal);
        }

        Assert.Contains("_outstanding.Count == 0", evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrbBankAndPortPredicatesCoverRemainingLiveOwnerState()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string trb = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "StreamEngine", "BurstIO",
            "TransactionReorderBuffer.cs");
        string evidence = Evidence(root);

        Assert.Contains("public readonly int OutstandingCount => _count;",
            trb, StringComparison.Ordinal);
        Assert.Contains("private bool[] bankOccupied;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("private PortState[] _portStates;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("TRB.OutstandingCount == 0", evidence,
            StringComparison.Ordinal);
        Assert.Contains("bankOccupied.All(active => !active)", evidence,
            StringComparison.Ordinal);
        Assert.Contains("_portStates.All(port => !port.Busy)", evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SynchronousReadWriteRequireWholeOperationOwnerGateParticipation()
    {
        string subsystem = Subsystem(FindRepositoryRoot());
        string evidence = Evidence(FindRepositoryRoot());

        Assert.Equal(2, Regex.Matches(subsystem,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\)")
            .Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"IOMMU\.(?:Read|Write)Burst").Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"public bool (?:Read|Write)\([^)]*\)\s*\{\s*lock \(geometryLifecycleGate\)",
            RegexOptions.Singleline).Count);
        Assert.Contains(
            "hold the owner geometry gate for the whole geometry-dependent operation",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "exclusive owner-gate possession proves that no synchronous operation",
            evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetAndCompatibilityMutationPathsAreExplicitParticipants()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string helpers = Helpers(root);
        string evidence = Evidence(root);

        Assert.Contains("ReconfigureBankTopology();", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("private int _bankWidthBytes = 64;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("public void ResetStatistics()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("bankQueues[i].Clear();", helpers,
            StringComparison.Ordinal);
        Assert.Contains("compatibility setters", evidence,
            StringComparison.Ordinal);
        Assert.Contains("ResetStatistics", evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DmaAndDomainFamiliesAreExcludedFromGeometryQuiescence()
    {
        string root = FindRepositoryRoot();
        string dma = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "DMA", "DMAController.cs");
        string evidence = Evidence(root);

        Assert.Contains("private ChannelControl[] channels;", dma,
            StringComparison.Ordinal);
        Assert.Contains("IOMMU.ReadBurst", dma, StringComparison.Ordinal);
        Assert.Contains("IOMMU.WriteBurst", dma, StringComparison.Ordinal);
        Assert.DoesNotContain("NumBanks", dma, StringComparison.Ordinal);
        Assert.DoesNotContain("BankWidthBytes", dma, StringComparison.Ordinal);
        Assert.Contains(
            "DMA channel and domain-inflight state are excluded",
            evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicationMigrationRetainsDecisionLockAndPredicateShape()
    {
        string root = FindRepositoryRoot();
        string subsystemTree = ReadTree(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem"),
            ContractPath(root, "MemoryBankGeometryUpdateResult.cs"),
            ContractPath(root, "PhysicalMemoryBankGeometry.cs"),
            ContractPath(root, "PhysicalMemoryBankBinding.cs"),
            ContractPath(root, "PhysicalMemoryBankResolution.cs"),
            ContractPath(root, "MemoryBankGeometryGeneration.cs"));

        Assert.Contains("MemoryBankGeometryUpdateResult", subsystemTree,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankGeometry", subsystemTree,
            StringComparison.Ordinal);
        Assert.Contains("geometryLifecycleGate", subsystemTree,
            StringComparison.Ordinal);
        Assert.Contains("IsPhysicalBankGeometryOwnerQuiescent()",
            subsystemTree, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSupportAndReflectionDoNotOwnLifecycleState()
    {
        string root = FindRepositoryRoot();
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string tests = ReadTree(Path.Combine(root, "HybridCPU_ISE.Tests"),
            Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture", ThisFile));

        Assert.DoesNotContain("GeometryLifecycle", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NumBanks", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"GetField\s*\(\s*""(?:_numBanks|bankQueues|bankOccupied|bankLastAccessCycle|_portStates|_gate)""",
            tests);
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

    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root,
        "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ab-memory-bank-geometry-lifecycle-quiescence-architecture-decision.md");

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
