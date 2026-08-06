using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class
    Rf126arControllerCompletionCancellationBindingAuthorityRevalidationTests
{
    [Fact]
    public void PaperDefinesIdentityOnlyTerminalLocationBoundary()
    {
        string paper = Paper(Root());

        Order(paper,
            "The completion-and-cancellation location rule applies",
            "A controller-native",
            "request-ID FIFO has no physical-bank queue entry",
            "completion publication,",
            "take and cancellation only close the accepted request identity",
            "must not re-resolve the address",
            "If a",
            "request family later acquires a physical-bank queue",
            "accepted stored",
            "binding or complete accepted envelope");
    }

    [Fact]
    public void ControllerDeclaresExactlySixAdmissionFamilies()
    {
        string controller = Controller(Root());

        Assert.Equal(6, Regex.Matches(controller,
            @"public MemoryAdmissionResult TryAccept").Count);
        foreach (string family in new[]
                 {
                     "ExplicitPacketScalarLoad",
                     "SingleLaneScalarLoad",
                     "VectorSegmentLoad",
                     "CanonicalVectorTransfer",
                     "ExplicitPacketScalarStore",
                     "SingleLaneScalarStore"
                 })
        {
            Assert.Equal(1, Regex.Matches(controller,
                $@"public MemoryAdmissionResult TryAccept{family}\(").Count);
        }
    }

    [Fact]
    public void ReadServiceConsumesLocationBeforeCreatingCompletion()
    {
        string controller = Controller(Root());
        string readService = Slice(controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Order(readService,
            "request.PhysicalBankEnvelope",
            "request.PhysicalBankBinding",
            "_nextCompletions.Add(",
            "new MemoryCompletion(");
        Assert.Equal(1, Regex.Matches(readService,
            @"request\.PhysicalBankEnvelope").Count);
        Assert.Equal(1, Regex.Matches(readService,
            @"request\.PhysicalBankBinding").Count);
    }

    [Fact]
    public void StoreReadinessCreatesCompletionWithoutLocationOperation()
    {
        string controller = Controller(Root());
        string storeService = Slice(controller,
            "while (_scalarStoreQueue.Count > 0)",
            "private MemoryRequestId AllocateRequestId()");

        Assert.Contains("succeeded: true", storeService,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBank", storeService,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteController", storeService,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicationTakeAndCancelRemainRequestIdOnly()
    {
        string controller = Controller(Root());
        string publication = Slice(controller,
            "foreach ((MemoryRequestId requestId, MemoryCompletion completion) in _nextCompletions)",
            "// All non-migrated queues");
        string take = Slice(controller, "public bool TryTakeCompletion(",
            "public bool TryCancel(");
        string cancel = Slice(controller, "public bool TryCancel(",
            "internal bool OwnsOutstandingSingleLaneScalarLoad(");

        Order(publication, "_outstanding.TryGetValue(requestId, out ControllerRequest request)",
            "_publishedCompletions.Add(", "new MemoryCompletion(",
            "_nextCompletions.Clear();");
        Order(take, "_publishedCompletions.Remove(requestId, out completion)",
            "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);");
        Order(cancel, "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);",
            "_nextCompletions.Remove(requestId);",
            "_publishedCompletions.Remove(requestId);");
        Assert.DoesNotContain("PhysicalBank", publication + take + cancel,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Address", take + cancel,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionCarrierContainsFunctionalResultButNoLocationCarrier()
    {
        string controller = Controller(Root());
        string completion = controller[..controller.IndexOf(
            "public sealed class MemoryCycleController",
            StringComparison.Ordinal)];

        foreach (string property in new[]
                 {
                     "MemoryRequestId RequestId",
                     "bool Succeeded",
                     "string? FailureReason",
                     "ulong PublishedCycle",
                     "ReadOnlyMemory<byte> Data"
                 })
            Assert.Contains(property, completion, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBank", completion,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Geometry", completion,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionAndCancellationCallerTopologyIsFrozen()
    {
        string root = Root();
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));
        string terminalConsumers = string.Join("\n", new[]
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

        Assert.Equal(7, Regex.Matches(production,
            @"\bTryTakeCompletion\s*\(").Count);
        Assert.Equal(10, Regex.Matches(terminalConsumers,
            @"\.TryCancel\s*\(").Count);
        Assert.Equal(6, Regex.Matches(terminalConsumers,
            @"\.TryTakeCompletion\s*\(").Count);
    }

    [Fact]
    public void CancellationLeavesOnlyStaleFifoIdentityToDrain()
    {
        string controller = Controller(Root());
        string cancel = Slice(controller, "public bool TryCancel(",
            "internal bool OwnsOutstandingSingleLaneScalarLoad(");

        Assert.DoesNotContain("_readQueue", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("_scalarStoreQueue", cancel,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(controller,
            @"!_outstanding\.TryGetValue\(requestId, out ControllerRequest request\)")
            .Count);
    }

    [Fact]
    public void GeometryReplacementRequiresAllTerminalStateToBeEmpty()
    {
        string controller = Controller(Root());

        Order(controller, "bool controllerIsQuiescent =",
            "_readQueue.Count == 0",
            "_scalarStoreQueue.Count == 0",
            "_outstanding.Count == 0",
            "_nextCompletions.Count == 0",
            "_publishedCompletions.Count == 0",
            "TryReplacePhysicalMemoryBankGeometryUnderControllerGate(");
    }

    [Fact]
    public void LegacyAddressReresolutionAndExternalAbsenceRemainSeparate()
    {
        string root = Root();
        string legacy = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs")
        });

        Order(legacy, "public bool CancelPendingRequest(",
            "token.GetPhysicalBankBindingForOwner();",
            "pendingRequests.Remove(requestID);",
            "RemoveQueuedBankRequest(");
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
    }


    private static string Controller(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ar-controller-completion-cancellation-binding-authority-revalidation.md");

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }

    private static string ReadTree(string root) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
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
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "ResearchPaper")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
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
}
