using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class
    Rf126aqScalarStoreStoredBindingRetentionValidationBoundaryDecisionTests
{
    [Fact]
    public void PaperDefinesReadinessOnlyStoreBindingBoundary()
    {
        string paper = Paper(Root());

        Order(paper,
            "The controller-native scalar-store classes are a readiness-only specialization",
            "retains the captured binding",
            "performs no physical-bank",
            "Readiness service therefore does",
            "read or validate the binding",
            "Completion take and cancellation remain terminal request-identity operations",
            "Controller quiescence prevents geometry replacement",
            "Selected retirement remains the sole",
            "not the retained binding");
    }

    [Fact]
    public void ExactlyTwoPublicStoreFamiliesShareOneCapturedBindingFactory()
    {
        string controller = Controller(Root());

        Assert.Equal(1, Regex.Matches(controller,
            @"public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore\(").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"public MemoryAdmissionResult TryAcceptSingleLaneScalarStore\(").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"private MemoryAdmissionResult TryAcceptScalarStore\(").Count);
        string factory = Slice(controller,
            "private MemoryAdmissionResult TryAcceptScalarStore(",
            "private MemoryAdmissionResult TryAcceptRead(");
        Assert.Equal(1, Regex.Matches(factory,
            @"CapturePublishedPhysicalMemoryBankBindingUnderControllerGate\(").Count);
        Order(controller,
            "private MemoryAdmissionResult TryAcceptScalarStore(",
            "MemoryRequestId requestId = AllocateRequestId();",
            "PhysicalMemoryBankBinding physicalBankBinding",
            "ControllerRequest.CreateScalarStore(",
            "physicalBankBinding));",
            "_scalarStoreQueue.Enqueue(requestId);");
    }

    [Fact]
    public void ProductionAdmissionsHaveExactlyOneCallerPerStoreFamily()
    {
        string production = ReadTree(Path.Combine(Root(), "HybridCPU_ISE"));

        Assert.Equal(2, Regex.Matches(production,
            @"\bTryAcceptExplicitPacketScalarStore\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bTryAcceptSingleLaneScalarStore\s*\(").Count);
        Assert.Contains("CPU_Core.PipelineExecution.Memory.cs",
            string.Join("\n", FilesContaining(Root(),
                "TryAcceptExplicitPacketScalarStore")), StringComparison.Ordinal);
        Assert.Contains("MicroOp.LoadStore.cs",
            string.Join("\n", FilesContaining(Root(),
                "TryAcceptSingleLaneScalarStore")), StringComparison.Ordinal);
    }

    [Fact]
    public void StoreServicePublishesReadinessWithoutReadingBinding()
    {
        string controller = Controller(Root());
        string service = Slice(controller,
            "while (_scalarStoreQueue.Count > 0)",
            "private void RecordAcceptedRequest(");

        Assert.Contains("physical publication remains selected-retire-owned",
            service, StringComparison.Ordinal);
        Assert.Contains("succeeded: true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBankBinding", service,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteControllerReadStep", service,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteControllerWriteStep", service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionAndCancellationAreRequestIdentityTerminalOperations()
    {
        string controller = Controller(Root());
        string take = Slice(controller, "public bool TryTakeCompletion(",
            "public bool TryCancel(");
        string cancel = Slice(controller, "public bool TryCancel(",
            "internal bool OwnsOutstandingSingleLaneScalarLoad(");

        Order(take, "_publishedCompletions.Remove(requestId, out completion)",
            "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);");
        Order(cancel, "_outstanding.Remove(requestId, out ControllerRequest request)",
            "DecrementOutstandingClass(request);",
            "_nextCompletions.Remove(requestId);",
            "_publishedCompletions.Remove(requestId);");
        Assert.DoesNotContain("PhysicalBankBinding", take + cancel,
            StringComparison.Ordinal);
        Assert.DoesNotContain("address", cancel,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoreOwnershipPredicatesRemainPayloadEvidenceNotBankAuthority()
    {
        string root = Root();
        string controller = Controller(root);
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));

        Assert.Equal(1, Regex.Matches(production,
            @"\bOwnsOutstandingExplicitPacketScalarStore\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bOwnsOutstandingSingleLaneScalarStore\s*\(").Count);
        string predicates = Slice(controller,
            "internal bool OwnsOutstandingExplicitPacketScalarStore(",
            "internal bool AdvancePlatformEdge(");
        Assert.Contains("request.Data.AsSpan().SequenceEqual", predicates,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBankBinding", predicates,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitPacketReadinessDefersMutationToSelectedRetire()
    {
        string root = Root();
        string memory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Stages", "Memory",
            "CPU_Core.PipelineExecution.Memory.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");

        Order(memory,
            "TryAcceptExplicitPacketScalarStore(",
            "TryTakeCompletion(",
            "lane.DefersStoreCommitToWriteBack = true");
        Order(retire,
            "if (lane.DefersStoreCommitToWriteBack)",
            "retireBatch.AppendDeferredStoreLane(laneIndex);");
        Assert.Contains("ApplyRetiredScalarStoreCommit(", retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SingleLaneReadinessAndRetirePublicationRemainSeparate()
    {
        string root = Root();
        string store = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch",
            "ExecutionDispatcherV4.MemoryAndControl.cs");

        Order(store, "controller.TryAcceptSingleLaneScalarStore(",
            "controller.TryTakeCompletion(", "return true;");
        Assert.Contains("_requestController?.TryCancel(_controllerRequestId.Value)",
            store, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowScalarMemoryStore(",
            dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", dispatcher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeometryReplacementCannotCrossAnyLiveStoreState()
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
    public void NoWireFallbackReflectionMutationOrCrossFamilySeamWasAdded()
    {
        string root = Root();
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs")
        });
        string evidence = Evidence(root);

        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
        Assert.Contains("invalid-to-zero alias: absent", evidence,
            StringComparison.Ordinal);
        Assert.Contains("reflection/TestSupport mutation seam: absent", evidence,
            StringComparison.Ordinal);
        Assert.Contains("SlotId/physical LaneId/pinning mixing: absent", evidence,
            StringComparison.Ordinal);
    }


    private static string Controller(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root, "Documentation",
        "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6aq-scalar-store-stored-binding-retention-validation-boundary-decision.md");

    private static IEnumerable<string> FilesContaining(string root, string marker) =>
        Directory.EnumerateFiles(Path.Combine(root, "HybridCPU_ISE"), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path) &&
                           File.ReadAllText(path).Contains(marker,
                               StringComparison.Ordinal));

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
