using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ak closed-world canonical envelope producer/storage/consumer
/// revalidation. This decision guard changes no production, runtime,
/// invalid-input, compatibility or wire behavior.
/// </summary>
public sealed class
    Rf126akCanonicalEnvelopeAdmissionStorageServiceRevalidationTests
{
    [Fact]
    public void PaperSelectsCaptureStorageBeforeSeparateServiceConsumption()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "The timed-memory owner constructs the complete envelope",
            "under the same",
            "immutable `PhysicalMemoryBankGeometry` snapshot",
            "before request identity",
            "is published to the shared read FIFO.");
        Order(
            paper,
            "Migration order is:",
            "add a zero-caller immutable",
            "`CanonicalVectorPhysicalBankEnvelope` valid-input contract;",
            "revalidate its sole canonical admission, request-storage and service",
            "cut over valid-input envelope capture and immutable request storage;",
            "cut over valid-input service consumption separately",
            "decide malformed/envelope-mismatch public behavior separately;",
            "remove the single-source compatibility carrier");
    }

    [Fact]
    public void LaterCaptureStorageHasOneOwnerProducerAndNoExternalCallers()
    {
        string root = FindRepositoryRoot();
        string declaration = Full(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem",
            "CanonicalVectorPhysicalBankEnvelope.cs");
        string production = ReadTree(
            Path.Combine(root, "HybridCPU_ISE"),
            declaration);

        Assert.Equal(6, Regex.Matches(
            production,
            @"\bCanonicalVectorPhysicalBankEnvelope\b").Count);
        Assert.Contains(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate",
            production,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"\bCanonicalVectorPhysicalBankEnvelope\b",
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")));
        Assert.DoesNotMatch(
            @"\bCanonicalVectorPhysicalBankEnvelope\b",
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")));
        Assert.DoesNotMatch(
            @"\bCanonicalVectorPhysicalBankEnvelope\b",
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")));
        Assert.DoesNotMatch(
            @"\bCanonicalVectorPhysicalBankEnvelope\b",
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")));

        string functionalTests = ReadTree(
            Path.Combine(root, "HybridCPU_ISE.Tests"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aiCanonicalVectorPhysicalBankEnvelopeArchitectureDecisionTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126ajCanonicalVectorPhysicalBankEnvelopeCoreValidInputContractTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126akCanonicalEnvelopeAdmissionStorageServiceRevalidationTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126amCanonicalStoredEnvelopeServiceConsumptionValidInputCutoverTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126anCanonicalEnvelopeMismatchInvalidBehaviorArchitectureDecisionTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aoCanonicalSourceBaseBindingRemovalEligibilityDecisionTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126nPhysicalMemoryBankGeometryLifetimeArchitectureDecisionTests.cs"),
            Full(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126oPhysicalMemoryBankIndexCoreValidInputContractTests.cs"));
        Assert.DoesNotMatch(
            @"\bCanonicalVectorPhysicalBankEnvelope\b",
            functionalTests);
    }

    [Fact]
    public void CanonicalAdmissionHasOneProductionCallerAndCurrentDirectTests()
    {
        string root = FindRepositoryRoot();
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));
        string canonicalTests = Read(root, "HybridCPU_ISE.Tests",
            "Architecture", "Rf1010CanonicalVectorTransferMemoryCycleTests.cs");
        string bindingTests = Read(root, "HybridCPU_ISE.Tests",
            "Architecture",
            "Rf126afControllerNativeAcceptedRequestBindingStorageTests.cs");
        string removalTests = Read(root, "HybridCPU_ISE.Tests",
            "Architecture",
            "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs");

        Assert.Equal(2, Regex.Matches(production,
            @"\bTryAcceptCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(2, Regex.Matches(canonicalTests,
            @"\.TryAcceptCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(0, Regex.Matches(bindingTests,
            @"\.TryAcceptCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(2, Regex.Matches(removalTests,
            @"\.TryAcceptCanonicalVectorTransfer\s*\(").Count);
        Assert.Contains(
            "controller.TryAcceptCanonicalVectorTransfer(",
            VectorOwner(root),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AdmissionOrderHasOneCapacityGateAndOneRequestPublication()
    {
        string admission = Slice(
            Controller(FindRepositoryRoot()),
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");

        Order(
            admission,
            "elementCount == 0 || elementSize <= 0 || stride == 0",
            "ulong totalBytes = checked(elementCount * (ulong)elementSize);",
            "checked(sourceAddress + checked((elementCount - 1) * stride)",
            "lock (_gate)",
            "if (_outstandingCanonicalVectorTransfers >= CanonicalVectorTransferCapacity)",
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            "MemoryRequestId requestId = AllocateRequestId();",
            "_outstanding.Add(",
            "ControllerRequest.CreateCanonicalVectorTransfer(",
            "_outstandingCanonicalVectorTransfers++;",
            "_readQueue.Enqueue(requestId);",
            "return MemoryAdmissionResult.Accepted(requestId);");
        Assert.Equal(1, Regex.Matches(admission,
            @"_readQueue\.Enqueue\(requestId\)").Count);
    }

    [Fact]
    public void PrivateFactoryAndStorageHaveOneCanonicalContour()
    {
        string controller = Controller(FindRepositoryRoot());
        string request = Slice(
            controller,
            "private readonly record struct ControllerRequest(",
            "/// <summary>\n/// Compatibility platform-edge adapter");

        Assert.Equal(2, Regex.Matches(controller,
            @"\bCreateCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"PhysicalMemoryBankBinding PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding = new\(\);")
            .Count);
        Assert.Contains("ulong ElementCount = 0", request,
            StringComparison.Ordinal);
        Assert.Contains("int ElementSize = 0", request,
            StringComparison.Ordinal);
        Assert.Contains("ushort Stride = 0", request,
            StringComparison.Ordinal);
        Assert.Contains("CanonicalVectorPhysicalBankEnvelope PhysicalBankEnvelope = default",
            request,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFifoMakesOneCanonicalServiceDecision()
    {
        string controller = Controller(FindRepositoryRoot());
        string service = Slice(
            controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Assert.Equal(1, Regex.Matches(controller,
            @"while \(_readQueue\.Count > 0\)").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"_memorySubsystem\.ExecuteControllerVectorTransferReadStep\(")
            .Count);
        Order(
            service,
            "_readQueue.Dequeue();",
            "_outstanding.TryGetValue(requestId, out ControllerRequest request)",
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            "ExecuteControllerVectorTransferReadStep(",
            "request.ElementCount,",
            "request.ElementSize,",
            "request.Stride,",
            "_nextCompletions.Add(",
            "break;");
        Assert.Contains("request.PhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryOwnerServicePreservesPackedAndOrderedStridedReads()
    {
        string helpers = Helpers(FindRepositoryRoot());
        string service = Slice(
            helpers,
            "internal bool ExecuteControllerVectorTransferReadStep(",
            "#endregion");

        Assert.Equal(1, Regex.Matches(helpers,
            @"internal bool ExecuteControllerVectorTransferReadStep\s*\(")
            .Count);
        Order(
            service,
            "lock (geometryLifecycleGate)",
            "if (elementCount == 0 || elementSize <= 0 || stride == 0",
            "if (stride == elementSize)",
            "ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(",
            "for (ulong element = 0; element < elementCount; element++)",
            "address = checked(sourceAddress + checked(element * stride));",
            "elementBuffer.CopyTo(packedDestination, offset);",
            "return true;");
        Assert.Equal(2, Regex.Matches(helpers,
            @"return IOMMU\.ReadBurst\(deviceId, address, destination\.AsSpan\(\)\);")
            .Count);
        string elementLeaf = Slice(
            helpers,
            "private bool ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(",
            "internal bool ExecuteControllerVectorTransferReadStep(");
        Assert.Equal(1, Regex.Matches(elementLeaf,
            @"return IOMMU\.ReadBurst\(deviceId, address, destination\.AsSpan\(\)\);")
            .Count);
        Assert.Contains("CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SourceBaseBindingIsAbsentFromCanonicalFactory()
    {
        string root = FindRepositoryRoot();
        string admission = Slice(
            Controller(root),
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");
        string factory = Slice(
            Controller(root),
            "internal static ControllerRequest CreateCanonicalVectorTransfer(",
            "internal static ControllerRequest CreateScalarStore(");

        Assert.Equal(1, Regex.Matches(admission,
            @"CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate\s*\(")
            .Count);
        Assert.DoesNotContain(
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding physicalBankBinding",
            factory,
            StringComparison.Ordinal);
        Assert.Contains(
            "CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope",
            factory,
            StringComparison.Ordinal);
        Assert.DoesNotContain("physicalBankBinding,", factory,
            StringComparison.Ordinal);
        Assert.Contains("Array.Empty<byte>(),\n                default,\n                opcode,",
            factory,
            StringComparison.Ordinal);
        Assert.Contains("physicalBankEnvelope);",
            factory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipCompletionCancellationAndRetireRemainDistinct()
    {
        string controller = Controller(FindRepositoryRoot());
        string vector = VectorOwner(FindRepositoryRoot());

        Assert.Equal(2, Regex.Matches(
            ReadTree(Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE")),
            @"\bOwnsOutstandingCanonicalVectorTransfer\s*\(").Count);
        Assert.Contains("_requestController.TryTakeCompletion(",
            vector,
            StringComparison.Ordinal);
        Assert.Contains("_requestController.TryCancel(",
            vector,
            StringComparison.Ordinal);
        Assert.Contains("new VectorTransferRetireEffect(",
            vector,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalVectorPhysicalBankEnvelope",
            Slice(controller,
                "public bool TryTakeCompletion(",
                "internal bool OwnsOutstandingSingleLaneScalarLoad("),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWireReplayTelemetryAndTestSupportRemainEnvelopeFree()
    {
        string root = FindRepositoryRoot();
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs")
        });

        Assert.DoesNotContain("CanonicalVectorPhysicalBankEnvelope",
            external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalPhysicalBankEnvelope",
            external,
            StringComparison.Ordinal);
    }


    private static string Paper(string root) =>
        Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) =>
        Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.6ak-canonical-envelope-admission-storage-service-revalidation-decision.md");

    private static string Controller(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
            "MemoryCycleController.cs");

    private static string Helpers(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemorySubsystem.Helpers.cs");

    private static string VectorOwner(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector", "VectorMicroOps.Data.cs");

    private static string Slice(
        string source,
        string start,
        string end)
    {
        int startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing slice start: {start}");
        int endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing slice end: {end}");
        return source[startIndex..endIndex];
    }

    private static void Order(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = source.IndexOf(
                marker,
                previous + 1,
                StringComparison.Ordinal);
            Assert.True(current > previous,
                $"Missing or out-of-order marker: {marker}");
            previous = current;
        }
    }

    private static string ReadTree(
        string root,
        params string[] excludedPaths) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedPaths.Contains(
                Path.GetFullPath(path),
                StringComparer.OrdinalIgnoreCase))
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

    private static string Full(string root, params string[] parts) =>
        Path.GetFullPath(parts.Aggregate(root, Path.Combine));

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
