using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6an decision-only guard for canonical stored-envelope mismatch
/// reachability, public failure projection and winner order.
/// </summary>
public sealed class
    Rf126anCanonicalEnvelopeMismatchInvalidBehaviorArchitectureDecisionTests
{
    [Fact]
    public void PaperSelectsExistingFailedReadProjectionAndExactWinnerOrder()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "The canonical mismatch decision selects the existing failed-read completion",
            paper,
            StringComparison.Ordinal);
        Order(
            paper,
            "After the existing raw service-shape guard",
            "malformed/default carrier",
            "generation mismatch second",
            "accepted element-count mismatch third",
            "first out-of-membership index in ascending logical-element order fourth",
            "packed-destination length or overflow",
            "source-address arithmetic",
            "translation and functional read failure");
        Assert.Contains(
            "source-read `PageFaultException`",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "No destination byte or architectural retire state is published.",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SupportedPublicAdmissionCannotInjectOrReplaceEnvelope()
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
                parameter.ParameterType ==
                typeof(CanonicalVectorPhysicalBankEnvelope));

        string admissionSource = CanonicalAdmission(FindRepositoryRoot());
        Order(
            admissionSource,
            "elementCount == 0 || elementSize <= 0 || stride == 0",
            "catch (OverflowException)",
            "lock (_gate)",
            "_outstandingCanonicalVectorTransfers >=",
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            "AllocateRequestId()",
            "_outstanding.Add(",
            "_readQueue.Enqueue(requestId)",
            "MemoryAdmissionResult.Accepted(requestId)");
    }

    [Fact]
    public void GeometryReplacementCannotCreateAStaleAcceptedEnvelope()
    {
        string controller = Controller(FindRepositoryRoot());
        string replacement = Slice(
            controller,
            "internal MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarLoad(");
        string owner = Owner(FindRepositoryRoot());
        string ownerReplacement = Slice(
            owner,
            "TryReplacePhysicalMemoryBankGeometryUnderControllerGate(",
            "private bool IsPhysicalBankGeometryOwnerQuiescent(");

        Order(
            replacement,
            "_readQueue.Count == 0",
            "_scalarStoreQueue.Count == 0",
            "_outstanding.Count == 0",
            "_nextCompletions.Count == 0",
            "_publishedCompletions.Count == 0",
            "TryReplacePhysicalMemoryBankGeometryUnderControllerGate(");
        Assert.Contains(
            "!controllerIsQuiescent || !IsPhysicalBankGeometryOwnerQuiescent()",
            ownerReplacement,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryUpdateRejectReason.Busy",
            ownerReplacement,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MismatchStatesHaveOnlyInternalTestReachability()
    {
        string root = FindRepositoryRoot();
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));

        Assert.Equal(1, Regex.Matches(
            production,
            @"request\.PhysicalBankEnvelope").Count);
        Assert.Equal(2, Regex.Matches(
            production,
            @"CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate\s*\(")
            .Count);
        Assert.DoesNotContain(
            "public CanonicalVectorPhysicalBankEnvelope PhysicalBankEnvelope",
            production,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EnvelopeMismatch",
            production,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryOwnerShortCircuitsEveryMismatchBeforeRead()
    {
        string service = CanonicalService(FindRepositoryRoot());

        Order(
            service,
            "elementCount == 0 || elementSize <= 0 || stride == 0",
            "!physicalBankEnvelope.IsWellFormed",
            "physicalBankEnvelope.Generation != geometry.Generation",
            "physicalBankEnvelope.ElementCount != elementCount",
            "for (int elementIndex = 0;",
            ".GetSourceBankIndex(elementIndex).Value >=",
            "geometry.BankCount",
            "(ulong)packedDestination.Length !=",
            "if (stride == elementSize)",
            "ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(");
        Assert.DoesNotContain(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ComputeBankId", service,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerProjectsOneFailedNextLatchCompletion()
    {
        string controller = Controller(FindRepositoryRoot());
        string dispatch = Slice(
            controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Order(
            dispatch,
            "byte[] data = new byte[request.Size];",
            "ExecuteControllerVectorTransferReadStep(",
            "request.PhysicalBankEnvelope,",
            "string? failureReason = succeeded",
            "MemoryCycleController {RenderRequestClass",
            "_nextCompletions.Add(",
            "new MemoryCompletion(requestId, succeeded, data, failureReason",
            "break;");
        Assert.DoesNotContain("MemoryAdmissionResult.Rejected", dispatch,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryAdmissionResult.Backpressured", dispatch,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TryCancel", dispatch,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VectorOwnerKeepsExistingPageFaultWinnerAndNoRetireEffect()
    {
        string vector = VectorOwner(FindRepositoryRoot());
        string completion = Slice(
            vector,
            "if (!_requestController.TryTakeCompletion(",
            "return _state == ExecutionState.Complete;");

        Order(
            completion,
            "TryTakeCompletion(",
            "ClearControllerRequestState(resetExecutionState: false);",
            "completion == null || !completion.Succeeded",
            "throw new PageFaultException(",
            "_acceptedSourceAddress,",
            "isWrite: false",
            "completion.Data.Span.CopyTo(_transferBuffer);",
            "_retireEffect = new VectorTransferRetireEffect(",
            "_state = ExecutionState.Complete;");
    }

    [Fact]
    public void NoWireReplayTelemetryOrTestSupportMismatchSurfaceExists()
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
            "CanonicalVectorPhysicalBankEnvelope",
            external,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalBankEnvelope",
            external,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EnvelopeMismatch",
            external,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionChangesNoProductionSignatureOrFailureCarrier()
    {
        MethodInfo service = typeof(MemorySubsystem).GetMethod(
            "ExecuteControllerVectorTransferReadStep",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(bool), service.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(ulong), typeof(ulong), typeof(ulong), typeof(int),
                typeof(ushort), typeof(CanonicalVectorPhysicalBankEnvelope),
                typeof(byte[])
            },
            service.GetParameters()
                .Select(parameter => parameter.ParameterType));

        MethodInfo completion = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryTakeCompletion))!;
        Assert.Equal(typeof(bool), completion.ReturnType);
    }


    private static string CanonicalAdmission(string root) =>
        Slice(
            Controller(root),
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");

    private static string CanonicalService(string root) =>
        Slice(
            Helpers(root),
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

    private static string Helpers(string root) =>
        File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemorySubsystem.Helpers.cs"));

    private static string Owner(string root) =>
        File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemorySubsystem.cs"));

    private static string VectorOwner(string root) =>
        File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector", "VectorMicroOps.Data.cs"));

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
