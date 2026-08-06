using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ag closed-world controller stored-binding consumer revalidation.
/// This decision guard selects one later ordinary-read service cutover and
/// makes no production, completion, cancellation, legacy, invalid-input or
/// wire migration.
/// </summary>
public sealed class
    Rf126agControllerStoredBindingConsumerRevalidationTests
{
    [Fact]
    public void PaperRequiresEveryLocationConsumerToUseCapturedBinding()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "Every accepted asynchronous memory request",
            "captures its request identity plus the resolved physical bank index and",
            "geometry generation before it enters a bank queue.",
            "Queue lookup, arbitration,",
            "completion and cancellation use that captured binding",
            "cancellation may not",
            "re-resolve the request address against current geometry.");
        Assert.Contains(
            "evidence for locating the request only and does not grant completion or store",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StoredBindingHasExactlyOneOrdinaryReadServiceConsumer()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(
            controller,
            @"PhysicalMemoryBankBinding PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.Equal(2, Regex.Matches(
            controller,
            @"PhysicalMemoryBankBinding physicalBankBinding =").Count);
        Assert.Contains(
            "private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding = new();",
            controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceGraphHasOneReadFifoAndOneReadinessOnlyStoreFifo()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(controller,
            @"while \(_readQueue\.Count > 0\)").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"while \(_scalarStoreQueue\.Count > 0\)").Count);
        Assert.Equal(8, Regex.Matches(controller,
            @"_outstanding\.TryGetValue\(requestId, out ControllerRequest request\)")
            .Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"_memorySubsystem\.ExecuteControllerReadStep\(").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"_memorySubsystem\.ExecuteControllerVectorTransferReadStep\(")
            .Count);
        Assert.Contains(
            "physical publication remains selected-retire-owned",
            controller,
            StringComparison.Ordinal);
        Assert.Contains("succeeded: true", controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryAndCanonicalReadOwnerAdaptersRemainDistinct()
    {
        string root = FindRepositoryRoot();
        string controller = Controller(root);
        string helpers = Helpers(root);

        Assert.Contains(
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            controller,
            StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(helpers,
            @"ExecuteControllerReadStep\(").Count);
        Assert.Equal(1, Regex.Matches(helpers,
            @"internal bool ExecuteControllerVectorTransferReadStep\(").Count);
        Assert.Contains("if (stride == elementSize)", helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "for (ulong element = 0; element < elementCount; element++)",
            helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "PhysicalMemoryBankBinding physicalBankBinding",
            helpers,
            StringComparison.Ordinal);
        string canonical = helpers[helpers.IndexOf(
            "internal bool ExecuteControllerVectorTransferReadStep(",
            StringComparison.Ordinal)..];
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding physicalBankBinding",
            canonical,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionPublicationAndTerminalTakeRemainRequestIdOnly()
    {
        string controller = Controller(FindRepositoryRoot());

        Order(
            controller,
            "foreach ((MemoryRequestId requestId, MemoryCompletion completion) in _nextCompletions)",
            "if (_outstanding.TryGetValue(requestId, out ControllerRequest request))",
            "_publishedCompletions.Add(",
            "new MemoryCompletion(",
            "_nextCompletions.Clear();");
        Order(
            controller,
            "public bool TryTakeCompletion(",
            "_publishedCompletions.Remove(requestId, out completion)",
            "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);");
        Assert.DoesNotContain("PhysicalMemoryBankBinding",
            Completion(FindRepositoryRoot()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationRemovesIdentityStoresButLeavesStaleFifoIdToDrain()
    {
        string controller = Controller(FindRepositoryRoot());

        Order(
            controller,
            "public bool TryCancel(MemoryRequestId requestId)",
            "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);",
            "_nextCompletions.Remove(requestId);",
            "_publishedCompletions.Remove(requestId);",
            "return true;");
        Assert.DoesNotMatch(
            @"TryCancel[\s\S]*?_readQueue\.Remove",
            controller);
        Assert.DoesNotMatch(
            @"TryCancel[\s\S]*?_scalarStoreQueue\.Remove",
            controller);
        Assert.Contains(
            "!_outstanding.TryGetValue(requestId, out ControllerRequest request)",
            controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerTerminalAndOwnershipCallerManifestIsExact()
    {
        string root = FindRepositoryRoot();
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));

        Assert.Equal(7, Regex.Matches(production,
            @"\bTryTakeCompletion\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bOwnsOutstandingSingleLaneScalarLoad\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bOwnsOutstandingVectorSegmentLoad\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bOwnsOutstandingCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(1, Regex.Matches(production,
            @"\bOwnsOutstandingExplicitPacketScalarStore\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bOwnsOutstandingSingleLaneScalarStore\s*\(").Count);

        string cancellationCallers = string.Join("\n", new[]
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
        Assert.Equal(10, Regex.Matches(cancellationCallers,
            @"\.TryCancel\s*\(").Count);
    }

    [Fact]
    public void QuiescentReplacementPreventsPublicGenerationMismatch()
    {
        string controller = Controller(FindRepositoryRoot());

        Order(
            controller,
            "bool controllerIsQuiescent =",
            "_readQueue.Count == 0",
            "_scalarStoreQueue.Count == 0",
            "_outstanding.Count == 0",
            "_nextCompletions.Count == 0",
            "_publishedCompletions.Count == 0",
            "TryReplacePhysicalMemoryBankGeometryUnderControllerGate(");
        Assert.Contains(
            "controllerIsQuiescent",
            controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyExternalWireReplayTelemetryAndTestSupportRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string legacy = string.Join("\n", new[]
        {
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
                "MemorySubsystem.Operations.cs"),
            helpers[..helpers.IndexOf(
                "Functional backing adapter for the RF-10 controller-native exact",
                StringComparison.Ordinal)]
        });
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs")
        });

        Assert.DoesNotContain("ComputeBankId(token.Address)", legacy,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankBinding", legacy,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSelectsOnlyOrdinaryReadServiceLaterCutover()
    {
        string evidence = Evidence(FindRepositoryRoot());

        Assert.Contains(
            "Selected later cutover: **RF-12.6ah controller ordinary-read stored-binding",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains(
            "service valid-input cutover**",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains("explicit-packet scalar load", evidence,
            StringComparison.Ordinal);
        Assert.Contains("single-lane scalar load", evidence,
            StringComparison.Ordinal);
        Assert.Contains("vector-segment load", evidence,
            StringComparison.Ordinal);
        Assert.Contains(
            "canonical vector transfer",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains("store readiness", evidence, StringComparison.Ordinal);
        Assert.Contains("completion and", evidence, StringComparison.Ordinal);
        Assert.Contains("cancellation remain separate", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Production/runtime change: none", evidence,
            StringComparison.Ordinal);
    }


    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Helpers(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Helpers.cs");

    private static string Completion(string root) => Controller(root)[..Controller(root)
        .IndexOf("public sealed class MemoryCycleController",
            StringComparison.Ordinal)];

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ag-controller-stored-binding-consumer-revalidation-decision.md");

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
            int next = text.IndexOf(marker, cursor + 1,
                StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Expected marker after offset {cursor}: {marker}");
            cursor = next;
        }
    }
}
