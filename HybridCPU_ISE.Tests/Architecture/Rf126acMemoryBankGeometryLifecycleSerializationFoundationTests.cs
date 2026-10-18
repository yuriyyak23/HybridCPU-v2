using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ac valid-input serialization foundation for the timed-memory
/// owner's split physical-bank geometry and positional state. These guards
/// authorize no geometry publication or invalid-input behavior change.
/// </summary>
public sealed class Rf126acMemoryBankGeometryLifecycleSerializationFoundationTests
{
    [Fact]
    public void PaperAndDecisionFixOwnerAndControllerOuterLockOrder()
    {
        string root = FindRepositoryRoot();
        string paper = Paper(root);
        string decision = Decision(root);

        Assert.Contains(
            "The timed-memory owner selects exactly one outcome while holding its geometry",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "`MemorySubsystem` remains the geometry-publication authority",
            decision, StringComparison.Ordinal);
        Assert.Contains("controller gate -> owner geometry gate",
            decision, StringComparison.Ordinal);
        Assert.Contains(
            "No path may acquire the controller gate while holding the owner gate",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void MemorySubsystemDeclaresExactlyOneOwnerLifecycleGate()
    {
        string tree = SubsystemTree(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(tree,
            @"private readonly object geometryLifecycleGate = new\(\);")
            .Count);
        Assert.True(Regex.Matches(tree,
            @"\block\s*\(\s*geometryLifecycleGate\s*\)").Count >= 20);
        Assert.DoesNotMatch(
            @"(?:static|public|protected)\s+(?:readonly\s+)?object\s+geometryLifecycleGate",
            tree);
    }

    [Fact]
    public void SplitCompatibilityPropertiesRetainSignaturesAndInvalidBehavior()
    {
        string source = Subsystem(FindRepositoryRoot());

        Assert.Contains("public int NumBanks", source,
            StringComparison.Ordinal);
        Assert.Contains("int sanitized = Math.Max(1, value);", source,
            StringComparison.Ordinal);
        Assert.Contains("public int BankWidthBytes", source,
            StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", source,
            StringComparison.Ordinal);
        Assert.Equal(typeof(int), typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.NumBanks))!.PropertyType);
        Assert.Equal(typeof(int), typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.BankWidthBytes))!.PropertyType);

        MemorySubsystem memory = CreateMemory();
        memory.NumBanks = 0;
        Assert.Equal(1, memory.NumBanks);
        memory.NumBanks = -17;
        Assert.Equal(1, memory.NumBanks);
        memory.BankWidthBytes = 0;
        Assert.Equal(0, memory.BankWidthBytes);
        memory.BankWidthBytes = -17;
        Assert.Equal(-17, memory.BankWidthBytes);
    }

    [Fact]
    public void LegacyAdmissionAndCancellationShareOneOwnerCriticalSection()
    {
        string operations = Operations(FindRepositoryRoot());

        Assert.Equal(3, Regex.Matches(operations,
            @"\block\s*\(\s*geometryLifecycleGate\s*\)").Count);
        Assert.Contains("pendingRequests[requestID] = token;", operations,
            StringComparison.Ordinal);
        Assert.Contains("bankQueues[physicalBankBinding.BankIndex.Value]",
            operations, StringComparison.Ordinal);
        Assert.Contains("pendingRequests.Remove(requestID);", operations,
            StringComparison.Ordinal);
        Assert.Contains("RemoveQueuedBankRequest(",
            operations, StringComparison.Ordinal);

        MemorySubsystem memory = CreateMemory(numBanks: 4, bankWidthBytes: 64);
        byte[] buffer = new byte[8];
        MemorySubsystem.MemoryRequestToken token =
            memory.EnqueueRead(0, 128, buffer.Length, buffer);
        Assert.Equal(1, memory.CurrentQueuedRequests);
        Assert.True(memory.CancelPendingRequest(token));
        Assert.Equal(0, memory.CurrentQueuedRequests);
        Assert.False(memory.CancelPendingRequest(token));
    }

    [Fact]
    public void ObservationResetAndLegacyProgressRetainTheirOwnerGates()
    {
        string helpers = Helpers(FindRepositoryRoot());

        Assert.Matches(
            @"(?s)public void ResetStatistics\(\)\s*\{.*?CycleController\.ResetTelemetry\(\);\s*lock \(geometryLifecycleGate\)",
            helpers);
        Assert.Matches(
            @"internal void AdvanceLegacyAgentOneCycle\(\)\s*\{\s*lock \(geometryLifecycleGate\)",
            helpers);
        Assert.Contains("TRB.FindNextIssuable(bankBusySpan)", helpers,
            StringComparison.Ordinal);
        Assert.Contains("ProcessBankQueues();", helpers,
            StringComparison.Ordinal);
        Assert.Contains("Telemetry reset is observation-only", helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain("bankQueues[i].Clear();", helpers,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerCycleKeepsControllerOuterAndOwnerInnerOrder()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Matches(
            @"(?s)internal bool AdvancePlatformEdge\(ulong platformCycle\)\s*\{\s*lock \(_gate\)",
            controller);
        Assert.Matches(
            @"(?s)internal void AdvanceCompatibilityCycles\(long cycles\).*?lock \(_gate\).*?TickOneCycle\(\);",
            controller);
        Order(controller,
            "private void TickOneCycle()",
            "_memorySubsystem.AdvanceLegacyAgentOneCycle();",
            "_memorySubsystem.ExecuteController");

        string helpers = Helpers(FindRepositoryRoot());
        Assert.Matches(
            @"(?s)internal bool ExecuteControllerReadStep\(.*?lock \(geometryLifecycleGate\)",
            helpers);
        Assert.Matches(
            @"(?s)internal bool ExecuteControllerVectorTransferReadStep\(.*?lock \(geometryLifecycleGate\)",
            helpers);
    }

    [Fact]
    public void OwnerNeverAcquiresControllerWhileHoldingLifecycleGate()
    {
        string subsystem = Subsystem(FindRepositoryRoot());
        string helpers = Helpers(FindRepositoryRoot());
        string operations = Operations(FindRepositoryRoot());

        Assert.Matches(
            @"public void AdvanceCycles\(long cycles\)\s*\{\s*CycleController\.AdvanceCompatibilityCycles\(cycles\);\s*\}",
            helpers);
        Assert.Equal(1, Regex.Matches(subsystem,
            @"\bCycleController\.TryReplacePhysicalMemoryBankGeometry\(")
            .Count);
        Assert.Matches(
            @"(?s)public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry\(.*?\)\s*=>\s*CycleController\.TryReplacePhysicalMemoryBankGeometry",
            subsystem);
        Assert.DoesNotMatch(@"\bCycleController\.\w+\(", operations);
    }

    [Fact]
    public void SynchronousReadWriteHoldOwnerGateForWholeOperation()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Assert.Equal(2, Regex.Matches(subsystem,
            @"public bool (?:Read|Write)\([^)]*\)\s*\{\s*lock \(geometryLifecycleGate\)",
            RegexOptions.Singleline).Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\)")
            .Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"IOMMU\.(?:Read|Write)Burst").Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"OnBurstCompleted\(address,\s*length,\s*(?:true|false),\s*bankIndex\.Value\)")
            .Count);
    }

    [Fact]
    public void OwnerGateActuallySerializesDirectCompatibilityMutation()
    {
        MemorySubsystem memory = CreateMemory();
        FieldInfo gateField = typeof(MemorySubsystem).GetField(
            "geometryLifecycleGate",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object gate = gateField.GetValue(memory)!;
        using ManualResetEventSlim started = new();
        Exception? mutationFailure = null;

        Monitor.Enter(gate);
        Thread mutation = new(
            () =>
            {
                try
                {
                    started.Set();
                    memory.NumBanks = 4;
                }
                catch (Exception exception)
                {
                    mutationFailure = exception;
                }
            });
        mutation.IsBackground = true;
        mutation.Start();
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(mutation.Join(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(8, memory.NumBanks);
        }
        finally
        {
            Monitor.Exit(gate);
        }

        Assert.True(mutation.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(mutationFailure);
        Assert.Equal(4, memory.NumBanks);
    }

    [Fact]
    public void DiagnosticsObserveOwnerConsistentGeometrySnapshots()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        foreach (string marker in new[]
                 {
                     "public int CurrentQueuedRequests",
                     "internal int[] GetBankQueueDepthsSnapshot()",
                     "internal long CurrentCycle",
                     "public bool IsChannelOverloaded",
                     "public HardwareOccupancySnapshot128 GetHardwareOccupancySnapshot128()",
                     "public void AdvanceSamplingEpoch()"
                 })
        {
            int markerIndex = subsystem.IndexOf(marker,
                StringComparison.Ordinal);
            Assert.True(markerIndex >= 0, $"Missing diagnostic marker: {marker}");
            int nextLock = subsystem.IndexOf("lock (geometryLifecycleGate)",
                markerIndex, StringComparison.Ordinal);
            Assert.InRange(nextLock - markerIndex, 0, 420);
        }
    }

    [Fact]
    public void FoundationGateNowHostsPublicationAndOwnerBindingCapture()
    {
        string tree = SubsystemTree(FindRepositoryRoot(),
            "MemoryBankGeometryUpdateResult.cs",
            "PhysicalMemoryBankGeometry.cs",
            "PhysicalMemoryBankBinding.cs",
            "PhysicalMemoryBankResolution.cs",
            "MemoryBankGeometryGeneration.cs");

        Assert.Contains("MemoryBankGeometryUpdateResult", tree,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankGeometry", tree,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankGeometryGeneration", tree,
            StringComparison.Ordinal);
        Assert.Contains("pendingRequests.Values", tree,
            StringComparison.Ordinal);
        Assert.Contains("TryReplacePhysicalMemoryBankGeometry(",
            tree, StringComparison.Ordinal);
        Assert.Contains(
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            tree, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankResolution", tree,
            StringComparison.Ordinal);
    }


    private static MemorySubsystem CreateMemory(
        int numBanks = 8,
        int bankWidthBytes = 64)
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor)
        {
            NumBanks = numBanks,
            BankWidthBytes = bankWidthBytes
        };
    }

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

    private static string Decision(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ab-memory-bank-geometry-lifecycle-quiescence-architecture-decision.md");

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ac-memory-bank-geometry-lifecycle-serialization-foundation.md");

    private static string SubsystemTree(
        string root,
        params string[] excludedFileNames)
    {
        string sourceRoot = Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem");
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !excludedFileNames.Contains(
                    Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1,
                StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Missing or out-of-order marker: {marker}");
            cursor = next;
        }
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
