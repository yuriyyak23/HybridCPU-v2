using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6al valid-input cutover for owner-local canonical physical-bank
/// envelope capture and immutable private accepted-request storage. Service,
/// invalid-input, compatibility and wire behavior remain unchanged.
/// </summary>
public sealed class
    Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests
{
    [Fact]
    public void PaperRequiresOneSnapshotCaptureBeforeIdentityPublication()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "The timed-memory owner constructs the complete envelope",
            "under the same",
            "immutable `PhysicalMemoryBankGeometry` snapshot",
            "before request identity",
            "is published to the shared read FIFO.");
        Assert.Contains(
            "Duplicate\nand alternating indexes are retained in element order",
            paper,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0UL, 6UL, (ushort)64, new[] { 0, 1, 2, 3, 0, 1 })]
    [InlineData(192UL, 4UL, (ushort)4, new[] { 3, 3, 3, 3 })]
    public void AcceptedRequestStoresExactOrderedEnvelope(
        ulong sourceAddress,
        ulong elementCount,
        ushort stride,
        int[] expectedIndexes)
    {
        MemorySubsystem memory = CreateMemory();
        Assert.True(
            memory.TryReplacePhysicalMemoryBankGeometry(4, 64).IsApplied);

        MemoryAdmissionResult admission = memory.CycleController
            .TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                0,
                sourceAddress,
                4096,
                elementCount,
                4,
                stride);

        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        CanonicalVectorPhysicalBankEnvelope envelope =
            StoredEnvelope(memory.CycleController, admission.RequestId);
        Assert.True(envelope.IsWellFormed);
        Assert.Equal(2UL, envelope.Generation.Value);
        Assert.Equal(elementCount, envelope.ElementCount);
        Assert.Equal(
            expectedIndexes,
            envelope.CopySourceBankIndexes()
                .Select(index => index.Value)
                .ToArray());
    }

    [Fact]
    public void CompatibilityGeometrySettersCannotChangeCapturedSnapshot()
    {
        MemorySubsystem memory = CreateMemory();
        memory.NumBanks = 2;
        memory.BankWidthBytes = 16;

        MemoryAdmissionResult admission = memory.CycleController
            .TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VSTORE,
                0,
                0,
                1024,
                3,
                4,
                64);

        CanonicalVectorPhysicalBankEnvelope envelope =
            StoredEnvelope(memory.CycleController, admission.RequestId);
        Assert.Equal(1UL, envelope.Generation.Value);
        Assert.Equal(
            new[] { 0, 1, 2 },
            envelope.CopySourceBankIndexes()
                .Select(index => index.Value)
                .ToArray());
        Assert.Equal(8, memory.PublishedPhysicalBankGeometry.BankCount);
        Assert.Equal(64,
            memory.PublishedPhysicalBankGeometry.BankWidthBytes);
    }

    [Fact]
    public void RejectedAndBackpressuredAdmissionsPublishNoRequestOrEnvelope()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryCycleController controller = memory.CycleController;

        MemoryAdmissionResult rejected =
            controller.TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                0, 0, 0, 0, 4, 4);
        Assert.Equal(MemoryAdmissionStatus.Rejected, rejected.Status);
        Assert.Empty(Outstanding(controller));

        for (int index = 0;
             index < MemoryCycleController.CanonicalVectorTransferCapacity;
             index++)
        {
            Assert.Equal(
                MemoryAdmissionStatus.Accepted,
                controller.TryAcceptCanonicalVectorTransfer(
                    Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                    0,
                    (ulong)(index * 64),
                    (ulong)(2048 + index * 64),
                    1,
                    4,
                    4).Status);
        }

        int before = Outstanding(controller).Count;
        MemoryAdmissionResult backpressured =
            controller.TryAcceptCanonicalVectorTransfer(
                Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                0, 1024, 4096, 1, 4, 4);
        Assert.Equal(MemoryAdmissionStatus.Backpressured,
            backpressured.Status);
        Assert.Equal(before, Outstanding(controller).Count);
    }

    [Fact]
    public void CaptureUsesOneLocalPublishedGeometryAndCheckedAddressOrder()
    {
        string owner = Owner(FindRepositoryRoot());
        string capture = Slice(
            owner,
            "internal CanonicalVectorPhysicalBankEnvelope",
            "/// <summary>\n        /// Retained compatibility threshold setting.");

        Order(
            capture,
            "lock (geometryLifecycleGate)",
            "PhysicalMemoryBankGeometry geometry =",
            "_publishedPhysicalBankGeometry;",
            "new PhysicalMemoryBankIndex[capturedElementCount]",
            "for (int elementIndex = 0;",
            "ulong elementAddress = checked(",
            "sourceAddress +",
            "checked((ulong)elementIndex * stride)",
            "(elementAddress / (ulong)geometry.BankWidthBytes) %",
            "(ulong)geometry.BankCount;",
            "CanonicalVectorPhysicalBankEnvelope.Create(",
            "geometry.Generation,",
            "sourceBankIndexes);");
        Assert.Equal(1, Regex.Matches(capture,
            @"_publishedPhysicalBankGeometry").Count);
        Assert.DoesNotContain("NumBanks", capture,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BankWidthBytes\n", capture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AdmissionCapturesAfterCapacityAndBeforeIdentityAndFifo()
    {
        string admission = Slice(
            Controller(FindRepositoryRoot()),
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");

        Order(
            admission,
            "elementCount == 0 || elementSize <= 0 || stride == 0",
            "ulong totalBytes = checked(elementCount * (ulong)elementSize);",
            "if (_outstandingCanonicalVectorTransfers >= CanonicalVectorTransferCapacity)",
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            "MemoryRequestId requestId = AllocateRequestId();",
            "ControllerRequest.CreateCanonicalVectorTransfer(",
            "physicalBankEnvelope));",
            "_outstandingCanonicalVectorTransfers++;",
            "_readQueue.Enqueue(requestId);",
            "return MemoryAdmissionResult.Accepted(requestId);");
        Assert.Equal(1, Regex.Matches(admission,
            @"CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate\s*\(")
            .Count);
    }

    [Fact]
    public void PrivateCanonicalRequestStoresOneImmutableEnvelope()
    {
        Type requestType = typeof(MemoryCycleController)
            .GetNestedType("ControllerRequest",
                BindingFlags.NonPublic)!;
        PropertyInfo envelope = Assert.Single(
            requestType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(property =>
                    property.PropertyType ==
                    typeof(CanonicalVectorPhysicalBankEnvelope)));

        Assert.Equal("PhysicalBankEnvelope", envelope.Name);
        Assert.Contains(
            typeof(IsExternalInit),
            envelope.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        FieldInfo backingField = requestType.GetField(
            "<PhysicalBankEnvelope>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(backingField.IsInitOnly);
        Assert.True(requestType.IsValueType);
        Assert.True(requestType.IsDefined(
            typeof(IsReadOnlyAttribute), inherit: false));
    }

    [Fact]
    public void RemovedSourceBaseBindingIsNotDerivedFromCapturedEnvelope()
    {
        string admission = Slice(
            Controller(FindRepositoryRoot()),
            "public MemoryAdmissionResult TryAcceptCanonicalVectorTransfer(",
            "public MemoryAdmissionResult TryAcceptExplicitPacketScalarStore(");

        Assert.DoesNotContain(
            "physicalBankEnvelope.GetSourceBankIndex(0)",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "physicalBankEnvelope.Generation",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            admission,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LaterServiceCutoverConsumesStoredEnvelopeWithoutReresolution()
    {
        string controller = Controller(FindRepositoryRoot());
        string service = Slice(
            controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Assert.Contains("request.PhysicalBankEnvelope", service,
            StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(controller,
            @"request\.PhysicalBankEnvelope").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"ExecuteControllerVectorTransferReadStep\s*\(").Count);
        Assert.DoesNotContain(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicWireReplayTelemetryAndTestSupportRemainEnvelopeFree()
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
    }


    private static CanonicalVectorPhysicalBankEnvelope StoredEnvelope(
        MemoryCycleController controller,
        MemoryRequestId requestId)
    {
        object request = Outstanding(controller)[requestId]!;
        PropertyInfo property = request.GetType().GetProperty(
            "PhysicalBankEnvelope",
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic)!;
        return (CanonicalVectorPhysicalBankEnvelope)
            property.GetValue(request)!;
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

    private static string Paper(string root) =>
        Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Owner(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemorySubsystem.cs");

    private static string Controller(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
            "MemoryCycleController.cs");

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
