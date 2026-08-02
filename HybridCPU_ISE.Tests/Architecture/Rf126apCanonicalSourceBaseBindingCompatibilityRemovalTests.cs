using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ap canonical-only compatibility removal for the redundant
/// source-base physical-bank binding projection and private factory ingress.
/// </summary>
public sealed class
    Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests
{
    [Fact]
    public void PaperAuthorizesOnlyCanonicalProjectionAndFactoryRemoval()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "The canonical projection is therefore eligible",
            "remove its admission-local construction",
            "canonical factory parameter",
            "initialize the shared binding slot",
            "default/absent binding for canonical requests",
            "shared",
            "`ControllerRequest.PhysicalBankBinding` declaration itself is not eligible",
            "ordinary-read service branch remains");
    }

    [Fact]
    public void CanonicalAdmissionAndFactoryContainNoBindingProjection()
    {
        string controller = Controller(FindRepositoryRoot());
        string admission = CanonicalAdmission(controller);
        string factory = CanonicalFactory(controller);

        Assert.Equal(2, Regex.Matches(
            controller,
            @"\bCreateCanonicalVectorTransfer\s*\(").Count);
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetSourceBankIndex(0)",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding",
            factory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "physicalBankBinding",
            factory,
            StringComparison.Ordinal);
        Assert.Contains(
            "Array.Empty<byte>(),\n                default,\n                opcode,",
            factory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedCanonicalRequestStoresDefaultBindingAndExactEnvelope()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryAdmissionResult admission = memory.CycleController
            .TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                0,
                192,
                1024,
                2,
                8,
                64);

        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        PhysicalMemoryBankBinding binding = Stored<
            PhysicalMemoryBankBinding>(
            memory.CycleController,
            admission.RequestId,
            "PhysicalBankBinding");
        CanonicalVectorPhysicalBankEnvelope envelope = Stored<
            CanonicalVectorPhysicalBankEnvelope>(
            memory.CycleController,
            admission.RequestId,
            "PhysicalBankEnvelope");

        Assert.Equal(default, binding);
        Assert.False(binding.IsWellFormed);
        Assert.False(binding.Generation.IsIssued);
        Assert.True(envelope.IsWellFormed);
        Assert.Equal(1UL, envelope.Generation.Value);
        Assert.Equal(new[] { 3, 4 },
            envelope.CopySourceBankIndexes()
                .Select(index => index.Value)
                .ToArray());
    }

    [Fact]
    public void DefaultCanonicalSlotIsAbsenceAndNotBankZeroFallback()
    {
        PhysicalMemoryBankBinding absent = default;
        PhysicalMemoryBankBinding validBankZero =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Zero,
                MemoryBankGeometryGeneration.Create(1));

        Assert.Equal(0, absent.BankIndex.Value);
        Assert.False(absent.Generation.IsIssued);
        Assert.False(absent.IsWellFormed);
        Assert.True(validBankZero.IsWellFormed);
        Assert.NotEqual(validBankZero, absent);
    }

    [Fact]
    public void NonCanonicalReadAndStoreBindingsRemainWellFormed()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryAdmissionResult read = memory.CycleController
            .TryAcceptExplicitPacketScalarLoad(0, 192, 8);
        MemoryAdmissionResult store = memory.CycleController
            .TryAcceptSingleLaneScalarStore(0, 256, 8, new byte[8]);

        Assert.Equal(MemoryAdmissionStatus.Accepted, read.Status);
        Assert.Equal(MemoryAdmissionStatus.Accepted, store.Status);
        PhysicalMemoryBankBinding readBinding = Stored<
            PhysicalMemoryBankBinding>(
            memory.CycleController,
            read.RequestId,
            "PhysicalBankBinding");
        PhysicalMemoryBankBinding storeBinding = Stored<
            PhysicalMemoryBankBinding>(
            memory.CycleController,
            store.RequestId,
            "PhysicalBankBinding");

        Assert.True(readBinding.IsWellFormed);
        Assert.Equal(3, readBinding.BankIndex.Value);
        Assert.Equal(1UL, readBinding.Generation.Value);
        Assert.True(storeBinding.IsWellFormed);
        Assert.Equal(4, storeBinding.BankIndex.Value);
        Assert.Equal(1UL, storeBinding.Generation.Value);
    }

    [Fact]
    public void InvalidAdmissionStillRejectsWithoutPublishingRequest()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryAdmissionResult rejected = memory.CycleController
            .TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                0,
                0,
                0,
                0,
                8,
                8);

        Assert.Equal(MemoryAdmissionStatus.Rejected, rejected.Status);
        Assert.Empty(Outstanding(memory.CycleController));
    }

    [Fact]
    public void PublicAdmissionAndCompletionSignaturesRemainExact()
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

        MethodInfo completion = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryTakeCompletion))!;
        Assert.Equal(typeof(bool), completion.ReturnType);
        Assert.Equal(
            new[] { typeof(MemoryRequestId), typeof(MemoryCompletion).MakeByRefType() },
            completion.GetParameters()
                .Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void SharedFieldOrdinaryReaderAndCanonicalEnvelopeReaderRemainExact()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(
            controller,
            @"PhysicalMemoryBankBinding\s+PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankEnvelope").Count);
        Assert.Equal(2, Regex.Matches(
            controller,
            @"CapturePublishedPhysicalMemoryBankBindingUnderControllerGate\s*\(")
            .Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate\s*\(")
            .Count);
    }

    [Fact]
    public void ExternalWireReplayTelemetryAndTestSupportRemainAbsent()
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


    private static T Stored<T>(
        MemoryCycleController controller,
        MemoryRequestId requestId,
        string propertyName)
    {
        object request = Outstanding(controller)[requestId]!;
        return (T)request.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)!
            .GetValue(request)!;
    }

    private static IDictionary Outstanding(MemoryCycleController controller) =>
        (IDictionary)typeof(MemoryCycleController)
            .GetField("_outstanding",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
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
