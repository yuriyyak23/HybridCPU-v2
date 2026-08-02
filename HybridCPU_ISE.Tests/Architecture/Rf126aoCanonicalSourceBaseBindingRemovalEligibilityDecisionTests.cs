using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ao decision-only closed-world guard for removal eligibility of the
/// redundant canonical source-base physical-bank binding projection.
/// </summary>
public sealed class
    Rf126aoCanonicalSourceBaseBindingRemovalEligibilityDecisionTests
{
    [Fact]
    public void PaperSeparatesCanonicalProjectionFromSharedRequestLayout()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "The closed-world removal-eligibility decision for step 6",
            paper,
            StringComparison.Ordinal);
        Order(
            paper,
            "admission currently derives one",
            "`PhysicalMemoryBankBinding` from envelope",
            "canonical dispatch branch",
            "wire have zero readers of that",
            "eligible for a later, independently",
            "reversible removal slice",
            "initialize the shared binding slot to the",
            "`ControllerRequest.PhysicalBankBinding` declaration itself is not eligible",
            "ordinary-read service branch remains",
            "its authority consumer");
    }

    [Fact]
    public void CanonicalFactoryRemovalMatchesClosedEligibilityDecision()
    {
        string controller = Controller(FindRepositoryRoot());
        string admission = CanonicalAdmission(controller);
        string factory = CanonicalFactory(controller);

        Assert.Equal(2, Regex.Matches(
            controller,
            @"\bCreateCanonicalVectorTransfer\s*\(").Count);
        Assert.Equal(0, Regex.Matches(
            admission,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=")
            .Count);
        Order(
            admission,
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            "MemoryRequestId requestId = AllocateRequestId();",
            "ControllerRequest.CreateCanonicalVectorTransfer(",
            "physicalBankEnvelope));");
        Assert.Equal(0, Regex.Matches(
            factory,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding").Count);
        Assert.DoesNotContain("physicalBankBinding,", factory,
            StringComparison.Ordinal);
        Assert.Contains(
            "Array.Empty<byte>(),\n                default,\n                opcode,",
            factory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalDispatchNeverEvaluatesSharedBindingReader()
    {
        string controller = Controller(FindRepositoryRoot());
        string dispatch = Slice(
            controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Order(
            dispatch,
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            "ExecuteControllerVectorTransferReadStep(",
            "request.PhysicalBankEnvelope,",
            ": _memorySubsystem.ExecuteControllerReadStep(",
            "request.PhysicalBankBinding,");
    }

    [Fact]
    public void CanonicalServiceHasZeroBindingInputsOrReaders()
    {
        string service = CanonicalService(FindRepositoryRoot());

        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalBankBinding",
            service,
            StringComparison.Ordinal);
        Assert.Contains(
            "CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope",
            service,
            StringComparison.Ordinal);
        Assert.Contains(
            "GetSourceBankIndex(elementIndex)",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipCompletionCancellationAndDiagnosticsHaveZeroReaders()
    {
        string controller = Controller(FindRepositoryRoot());
        string ownership = Slice(
            controller,
            "internal bool OwnsOutstandingCanonicalVectorTransfer(",
            "internal bool OwnsOutstandingExplicitPacketScalarStore(");
        string takeAndCancel = Slice(
            controller,
            "public bool TryTakeCompletion(",
            "internal bool OwnsOutstandingSingleLaneScalarLoad(");
        string counters = Slice(
            controller,
            "private void DecrementOutstandingClass(",
            "private static string RenderRequestClass(");
        string diagnostics = Slice(
            controller,
            "private static string RenderRequestClass(",
            "private enum ReadRequestClass");

        foreach (string contour in new[]
                 {
                     ownership, takeAndCancel, counters, diagnostics
                 })
        {
            Assert.DoesNotContain(
                "PhysicalBankBinding",
                contour,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedBindingFieldRemainsRequiredByOrdinaryReadService()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(
            controller,
            @"PhysicalMemoryBankBinding\s+PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.Contains(
            ": _memorySubsystem.ExecuteControllerReadStep(",
            controller,
            StringComparison.Ordinal);
        Assert.Contains(
            "request.PhysicalBankBinding,",
            controller,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWireReplayTelemetryAndTestSupportHaveZeroSeams()
    {
        string root = FindRepositoryRoot();
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL",
                "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"))
        });

        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding",
            external,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalBankBinding",
            external,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CreateCanonicalVectorTransfer",
            external,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FormerReflectionCallerNoLongerObservesCanonicalBinding()
    {
        string root = FindRepositoryRoot();
        string reflectionTest = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE.Tests", "Architecture",
            "Rf126afControllerNativeAcceptedRequestBindingStorageTests.cs"));

        Assert.DoesNotContain(
            "TryAcceptCanonicalVectorTransfer(",
            reflectionTest,
            StringComparison.Ordinal);
        Assert.Contains(
            ".GetProperty(\n            \"PhysicalBankBinding\"",
            reflectionTest,
            StringComparison.Ordinal);
        Assert.Contains(
            ".GetValue(request)!",
            reflectionTest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".SetValue(",
            reflectionTest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Activator.CreateInstance",
            reflectionTest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicAdmissionAndCompletionSignaturesRemainUnchanged()
    {
        MethodInfo admission = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryAcceptCanonicalVectorTransfer))!;
        Assert.Equal(
            new[]
            {
                typeof(uint), typeof(ulong), typeof(ulong), typeof(ulong),
                typeof(ulong), typeof(int), typeof(ushort)
            },
            admission.GetParameters()
                .Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(
            admission.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(PhysicalMemoryBankBinding));

        MethodInfo completion = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryTakeCompletion))!;
        Assert.Equal(typeof(bool), completion.ReturnType);
    }


    private static string CanonicalAdmission(string controller) =>
        Slice(
            controller,
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");

    private static string CanonicalFactory(string controller) =>
        Slice(
            controller,
            "internal static ControllerRequest CreateCanonicalVectorTransfer(",
            "internal static ControllerRequest CreateScalarStore(");

    private static string CanonicalService(string root) =>
        Slice(
            File.ReadAllText(Path.Combine(
                root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
                "MemorySubsystem.Helpers.cs")),
            "internal bool ExecuteControllerVectorTransferReadStep(",
            "#endregion");

    private static string Paper(string root) =>
        File.ReadAllText(Path.Combine(
            root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));

    private static string Controller(string root) =>
        File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
            "MemoryCycleController.cs"));

    private static string ReadTree(string path) =>
        string.Join("\n", Directory.EnumerateFiles(
                path, "*.cs", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(File.ReadAllText));

    private static string Slice(
        string text,
        string startMarker,
        string endMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        int end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return text[start..end];
    }

    private static void Order(string text, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(
                marker,
                previous + 1,
                StringComparison.Ordinal);
            Assert.True(current > previous,
                $"Missing or out-of-order marker: {marker}");
            previous = current;
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(
                    directory.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName, "Documentation")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU_ISE repository root.");
    }
}
