using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6af valid-input cutover for controller-native accepted-request
/// physical-bank binding storage. The stored binding is not yet consumed by
/// service, completion or cancellation.
/// </summary>
public sealed class
    Rf126afControllerNativeAcceptedRequestBindingStorageTests
{
    [Fact]
    public void PaperRequiresOwnerSnapshotBindingBeforeQueuePublication()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "Every accepted asynchronous memory request",
            "captures its request identity plus the resolved physical bank index and",
            "geometry generation before it enters a bank queue.",
            "Queue lookup, arbitration,",
            "completion and cancellation use that captured binding");
        Assert.Contains(
            "cancellation may not",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "re-resolve the request address against current geometry",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FiveNonCanonicalAdmissionFamiliesStoreBindingFromPublishedGeometry()
    {
        const ulong address = 192;

        AssertBinding(
            Accept(memory => memory.CycleController
                .TryAcceptExplicitPacketScalarLoad(0, address, 8)),
            3, 1);
        AssertBinding(
            Accept(memory => memory.CycleController
                .TryAcceptSingleLaneScalarLoad(0, address, 8)),
            3, 1);
        AssertBinding(
            Accept(memory => memory.CycleController
                .TryAcceptVectorSegmentLoad(0, address, 8)),
            3, 1);
        AssertBinding(
            Accept(memory => memory.CycleController
                .TryAcceptExplicitPacketScalarStore(
                    0, address, 8, new byte[8])),
            3, 1);
        AssertBinding(
            Accept(memory => memory.CycleController
                .TryAcceptSingleLaneScalarStore(
                    0, address, 8, new byte[8])),
            3, 1);
    }

    [Fact]
    public void AppliedGeometryReplacementChangesNewRequestBindingGeneration()
    {
        MemorySubsystem memory = CreateMemory();
        Assert.True(
            memory.TryReplacePhysicalMemoryBankGeometry(4, 128).IsApplied);

        MemoryAdmissionResult admission = memory.CycleController
            .TryAcceptExplicitPacketScalarLoad(0, 384, 8);

        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        AssertBinding(memory.CycleController, admission.RequestId, 3, 2);
    }

    [Fact]
    public void RawCompatibilityGeometryCannotOverridePublishedSnapshotBinding()
    {
        MemorySubsystem memory = CreateMemory();
        memory.NumBanks = 2;
        memory.BankWidthBytes = 32;

        MemoryAdmissionResult admission = memory.CycleController
            .TryAcceptSingleLaneScalarLoad(0, 192, 8);

        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        AssertBinding(memory.CycleController, admission.RequestId, 3, 1);
        Assert.Equal(8, memory.PublishedPhysicalBankGeometry.BankCount);
        Assert.Equal(64,
            memory.PublishedPhysicalBankGeometry.BankWidthBytes);
    }

    [Fact]
    public void InvalidAndBackpressuredAdmissionsDoNotCreateStoredBindings()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryCycleController controller = memory.CycleController;

        MemoryAdmissionResult invalid =
            controller.TryAcceptExplicitPacketScalarLoad(0, 0, 3);
        Assert.Equal(MemoryAdmissionStatus.Rejected, invalid.Status);
        Assert.Equal(0, Outstanding(controller).Count);

        for (int index = 0;
             index < MemoryCycleController.ExplicitPacketScalarLoadCapacity;
             index++)
        {
            Assert.Equal(
                MemoryAdmissionStatus.Accepted,
                controller.TryAcceptExplicitPacketScalarLoad(
                    0, (ulong)(index * 64), 8).Status);
        }

        int before = Outstanding(controller).Count;
        MemoryAdmissionResult backpressured =
            controller.TryAcceptExplicitPacketScalarLoad(0, 1024, 8);
        Assert.Equal(MemoryAdmissionStatus.Backpressured,
            backpressured.Status);
        Assert.Equal(before, Outstanding(controller).Count);
    }

    [Fact]
    public void CaptureOccursAfterChecksAndBeforeOutstandingAndFifoPublication()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(2, Regex.Matches(
            controller,
            @"CapturePublishedPhysicalMemoryBankBindingUnderControllerGate\(")
            .Count);
        Order(
            controller,
            "if (_outstandingCanonicalVectorTransfers >= CanonicalVectorTransferCapacity)",
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            "MemoryRequestId requestId = AllocateRequestId();",
            "_outstanding.Add(",
            "_readQueue.Enqueue(requestId);");
        Order(
            controller,
            "if (outstandingForClass >= capacity)",
            "MemoryRequestId requestId = AllocateRequestId();",
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            "_outstanding.Add(",
            "_scalarStoreQueue.Enqueue(requestId);");
    }

    [Fact]
    public void ControllerRequestHasExactlyOneImmutableBindingField()
    {
        Type requestType = typeof(MemoryCycleController)
            .GetNestedType("ControllerRequest",
                BindingFlags.NonPublic)!;
        PropertyInfo[] bindings = requestType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)
            .Where(property =>
                property.PropertyType == typeof(PhysicalMemoryBankBinding))
            .ToArray();

        PropertyInfo binding = Assert.Single(bindings);
        Assert.Equal("PhysicalBankBinding", binding.Name);
        Assert.Contains(
            typeof(IsExternalInit),
            binding.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        FieldInfo backingField = requestType.GetField(
            "<PhysicalBankBinding>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(backingField.IsInitOnly);
        Assert.True(requestType.IsValueType);
        Assert.True(requestType.IsDefined(
            typeof(IsReadOnlyAttribute), inherit: false));
    }

    [Fact]
    public void StoredBindingHasOneOrdinaryServiceConsumerOnly()
    {
        string controller = Controller(FindRepositoryRoot());

        Assert.Equal(1, Regex.Matches(
            controller,
            @"PhysicalMemoryBankBinding PhysicalBankBinding").Count);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.DoesNotContain(
            "request.PhysicalBankBinding",
            Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL",
                "Memory", "Subsystem", "MemorySubsystem.Operations.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyRequestTokenPendingAndBankRequestStorePrivateBinding()
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
        Type token = typeof(MemorySubsystem.MemoryRequestToken);

        Assert.Contains("PhysicalMemoryBankBinding", legacy,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalBankBinding = physicalBankBinding", legacy,
            StringComparison.Ordinal);
        Assert.Null(token.GetProperty("PhysicalBankBinding",
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic));
        FieldInfo? binding = token.GetField("physicalBankBinding",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(binding);
        Assert.True(binding!.IsInitOnly);
    }

    [Fact]
    public void BindingRemainsAbsentFromWireCompilerRuntimeAndTestSupport()
    {
        string root = FindRepositoryRoot();
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps")),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs")
        });

        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
    }


    private static (MemoryCycleController Controller, MemoryRequestId RequestId)
        Accept(Func<MemorySubsystem, MemoryAdmissionResult> accept)
    {
        MemorySubsystem memory = CreateMemory();
        MemoryAdmissionResult admission = accept(memory);
        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        return (memory.CycleController, admission.RequestId);
    }

    private static void AssertBinding(
        (MemoryCycleController Controller, MemoryRequestId RequestId) accepted,
        int bankIndex,
        ulong generation) =>
        AssertBinding(
            accepted.Controller,
            accepted.RequestId,
            bankIndex,
            generation);

    private static void AssertBinding(
        MemoryCycleController controller,
        MemoryRequestId requestId,
        int bankIndex,
        ulong generation)
    {
        object request = Outstanding(controller)[requestId]!;
        PropertyInfo property = request.GetType().GetProperty(
            "PhysicalBankBinding",
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic)!;
        var binding =
            (PhysicalMemoryBankBinding)property.GetValue(request)!;

        Assert.Equal(bankIndex, binding.BankIndex.Value);
        Assert.Equal(generation, binding.Generation.Value);
        Assert.True(binding.IsWellFormed);
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

    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

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
