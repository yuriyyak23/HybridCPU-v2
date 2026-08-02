using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ah valid-input cutover for owner-local consumption of the binding
/// captured by the three controller-native ordinary-read families.
/// </summary>
public sealed class
    Rf126ahControllerOrdinaryReadStoredBindingValidInputCutoverTests
{
    [Fact]
    public void PaperRequiresCapturedBindingForQueueService()
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
            "evidence for locating the request only",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not grant completion or store",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReadServicePassesStoredBindingExactlyOnce()
    {
        string root = FindRepositoryRoot();
        string controller = Controller(root);
        string helpers = Helpers(root);

        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Order(
            controller,
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            "ExecuteControllerVectorTransferReadStep(",
            ": _memorySubsystem.ExecuteControllerReadStep(",
            "request.PhysicalBankBinding,",
            "data);");
        Assert.Contains(
            "PhysicalMemoryBankBinding physicalBankBinding",
            helpers,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerConsumesGenerationAndMembershipWithoutAddressReresolution()
    {
        string helpers = Helpers(FindRepositoryRoot());
        string method = Slice(
            helpers,
            "internal bool ExecuteControllerReadStep(",
            "private bool ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(");

        Order(
            method,
            "ArgumentNullException.ThrowIfNull(destination);",
            "lock (geometryLifecycleGate)",
            "PhysicalMemoryBankGeometry geometry =",
            "_publishedPhysicalBankGeometry;",
            "physicalBankBinding.Generation != geometry.Generation",
            "physicalBankBinding.BankIndex.Value >= geometry.BankCount",
            "IOMMU.ReadBurst(deviceId, address, destination.AsSpan())");
        Assert.DoesNotContain("address /", method, StringComparison.Ordinal);
        Assert.DoesNotContain("%", method, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CapturePublishedPhysicalMemoryBankBinding",
            method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AllThreeOrdinaryReadFamiliesPreserveDataAndCycleOrder()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            Assert.True(
                memory.TryReplacePhysicalMemoryBankGeometry(4, 128).IsApplied);

            byte[] explicitBytes = BitConverter.GetBytes(
                0x1122_3344_5566_7788UL);
            byte[] singleLaneBytes = BitConverter.GetBytes(
                0x8877_6655_4433_2211UL);
            byte[] vectorBytes = Enumerable.Range(1, 16)
                .Select(value => (byte)value)
                .ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(0x80, explicitBytes));
            Assert.True(mainMemory.TryWritePhysicalRange(
                0x100,
                singleLaneBytes));
            Assert.True(mainMemory.TryWritePhysicalRange(0x180, vectorBytes));

            MemoryAdmissionResult explicitLoad = memory.CycleController
                .TryAcceptExplicitPacketScalarLoad(
                    0,
                    0x80,
                    explicitBytes.Length);
            MemoryAdmissionResult singleLaneLoad = memory.CycleController
                .TryAcceptSingleLaneScalarLoad(
                    0,
                    0x100,
                    singleLaneBytes.Length);
            MemoryAdmissionResult vectorLoad = memory.CycleController
                .TryAcceptVectorSegmentLoad(
                    0,
                    0x180,
                    vectorBytes.Length);

            Assert.Equal(MemoryAdmissionStatus.Accepted, explicitLoad.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted,
                singleLaneLoad.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, vectorLoad.Status);
            AssertBinding(memory.CycleController, explicitLoad.RequestId, 1, 2);
            AssertBinding(memory.CycleController,
                singleLaneLoad.RequestId,
                2,
                2);
            AssertBinding(memory.CycleController, vectorLoad.RequestId, 3, 2);
            Assert.Equal(
                MemoryBankGeometryUpdateRejectReason.Busy,
                memory.TryReplacePhysicalMemoryBankGeometry(8, 64)
                    .RejectReason);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(
                explicitLoad.RequestId,
                out _));

            memory.AdvanceCycles(1);
            AssertCompletion(
                memory.CycleController,
                explicitLoad.RequestId,
                explicitBytes,
                2);
            Assert.False(memory.CycleController.TryTakeCompletion(
                singleLaneLoad.RequestId,
                out _));

            memory.AdvanceCycles(1);
            AssertCompletion(
                memory.CycleController,
                singleLaneLoad.RequestId,
                singleLaneBytes,
                3);
            Assert.False(memory.CycleController.TryTakeCompletion(
                vectorLoad.RequestId,
                out _));

            memory.AdvanceCycles(1);
            AssertCompletion(
                memory.CycleController,
                vectorLoad.RequestId,
                vectorBytes,
                4);
        });
    }

    [Fact]
    public void CanonicalVectorStoreTerminalAndWireSurfacesRemainExcluded()
    {
        string root = FindRepositoryRoot();
        string controller = Controller(root);
        string helpers = Helpers(root);
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.Contains(
            "ExecuteControllerVectorTransferReadStep(",
            controller,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(",
            helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding physicalBankBinding",
            Slice(
                helpers,
                "internal bool ExecuteControllerVectorTransferReadStep(",
                "#endregion"),
            StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(
            controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.DoesNotContain(
            "PhysicalMemoryBankBinding",
            external,
            StringComparison.Ordinal);
        Assert.Empty(typeof(MemoryCompletion).GetProperties()
            .Where(property =>
                property.PropertyType == typeof(PhysicalMemoryBankBinding)));
    }

    [Fact]
    public void PublicAdmissionAndInvalidEnvelopeRemainUnchanged()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            MemoryAdmissionResult invalid = memory.CycleController
                .TryAcceptExplicitPacketScalarLoad(0, 0, 3);

            Assert.Equal(MemoryAdmissionStatus.Rejected, invalid.Status);
            Assert.False(invalid.RequestId.IsValid);
            Assert.Contains(
                "1/2/4/8-byte envelope",
                invalid.Reason,
                StringComparison.Ordinal);
        });

        MethodInfo method = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryAcceptExplicitPacketScalarLoad))!;
        Assert.DoesNotContain(method.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(PhysicalMemoryBankBinding));
    }


    private static void AssertCompletion(
        MemoryCycleController controller,
        MemoryRequestId requestId,
        byte[] expected,
        ulong publishedCycle)
    {
        Assert.True(controller.TryTakeCompletion(
            requestId,
            out MemoryCompletion? completion));
        Assert.NotNull(completion);
        Assert.True(completion.Succeeded);
        Assert.Equal(expected, completion.Data.ToArray());
        Assert.Equal(publishedCycle, completion.PublishedCycle);
    }

    private static void AssertBinding(
        MemoryCycleController controller,
        MemoryRequestId requestId,
        int bankIndex,
        ulong generation)
    {
        var outstanding = (IDictionary)typeof(MemoryCycleController)
            .GetField("_outstanding",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;
        object request = outstanding[requestId]!;
        var binding = (PhysicalMemoryBankBinding)request.GetType()
            .GetProperty(
                "PhysicalBankBinding",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)!
            .GetValue(request)!;

        Assert.Equal(bankIndex, binding.BankIndex.Value);
        Assert.Equal(generation, binding.Generation.Value);
    }

    private static void WithMappedMemory(
        Action<Processor.MainMemoryArea, MemorySubsystem> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            var mainMemory =
                new Processor.MultiBankMemoryArea(4, 0x2000UL);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                0,
                0,
                0,
                0x2000UL,
                IOMMUAccessPermissions.ReadWrite));
            Processor processor = default;
            var memory = new MemorySubsystem(ref processor);
            Processor.Memory = memory;
            body(mainMemory, memory);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static string Slice(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing start marker: {start}");
        int endIndex = text.IndexOf(
            end,
            startIndex + start.Length,
            StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing end marker: {end}");
        return text[startIndex..endIndex];
    }

    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Helpers(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Helpers.cs");

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
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(
                    current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(
                    current.FullName,
                    "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(
                marker,
                cursor + 1,
                StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Expected marker after offset {cursor}: {marker}");
            cursor = next;
        }
    }
}
