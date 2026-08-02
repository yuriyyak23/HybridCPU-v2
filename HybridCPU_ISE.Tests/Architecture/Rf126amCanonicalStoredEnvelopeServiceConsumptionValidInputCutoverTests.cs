using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6am valid-input cutover for canonical stored-envelope transport and
/// memory-owner service consumption. Public invalid-input, compatibility and
/// wire behavior remain unchanged.
/// </summary>
public sealed class
    Rf126amCanonicalStoredEnvelopeServiceConsumptionValidInputCutoverTests
{
    [Fact]
    public void PaperRequiresStoredIndexesAndGenerationWithoutReresolution()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "The service owner consumes the captured ordered indexes and generation",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "may not divide, modulo, clamp, normalize or re-resolve",
            paper,
            StringComparison.Ordinal);
        Order(
            paper,
            "cut over valid-input envelope capture and immutable request storage;",
            "cut over valid-input service consumption separately",
            "decide malformed/envelope-mismatch public behavior separately;",
            "remove the single-source compatibility carrier");
    }

    [Fact]
    public void ReadFifoPassesExactlyTheStoredEnvelope()
    {
        string controller = Controller(FindRepositoryRoot());
        string service = Slice(
            controller,
            "while (_readQueue.Count > 0)",
            "while (_scalarStoreQueue.Count > 0)");

        Order(
            service,
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            "ExecuteControllerVectorTransferReadStep(",
            "request.DeviceId,",
            "request.Address,",
            "request.ElementCount,",
            "request.ElementSize,",
            "request.Stride,",
            "request.PhysicalBankEnvelope,",
            "data)",
            "_nextCompletions.Add(",
            "break;");
        Assert.Equal(1, Regex.Matches(controller,
            @"request\.PhysicalBankEnvelope").Count);
        Assert.Equal(1, Regex.Matches(controller,
            @"_memorySubsystem\.ExecuteControllerVectorTransferReadStep\(")
            .Count);
    }

    [Fact]
    public void OwnerValidatesGenerationShapeAndMembershipBeforeAnyRead()
    {
        string service = CanonicalService(FindRepositoryRoot());

        Order(
            service,
            "lock (geometryLifecycleGate)",
            "elementCount == 0 || elementSize <= 0 || stride == 0",
            "PhysicalMemoryBankGeometry geometry =",
            "_publishedPhysicalBankGeometry;",
            "!physicalBankEnvelope.IsWellFormed",
            "physicalBankEnvelope.Generation != geometry.Generation",
            "physicalBankEnvelope.ElementCount != elementCount",
            "for (int elementIndex = 0;",
            ".GetSourceBankIndex(elementIndex).Value >=",
            "geometry.BankCount",
            "(ulong)packedDestination.Length !=",
            "if (stride == elementSize)",
            "ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(");
    }

    [Fact]
    public void ServiceDoesNotResolveIndexesFromAddresses()
    {
        string service = CanonicalService(FindRepositoryRoot());

        Assert.DoesNotContain(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelope",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"(?:address|sourceAddress)\s*/",
                RegexOptions.CultureInvariant),
            service);
        Assert.DoesNotMatch(
            new Regex(@"(?:address|sourceAddress).{0,80}%",
                RegexOptions.CultureInvariant |
                RegexOptions.Singleline),
            service);
        Assert.DoesNotContain("ComputeBankId", service,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PackedServiceStillUsesOneContiguousRead()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong source = 0x100;
            byte[] sourceBytes = Enumerable.Range(1, 8)
                .Select(value => (byte)value)
                .ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(
                source,
                sourceBytes));
            CanonicalVectorPhysicalBankEnvelope envelope = Envelope(
                memory,
                source,
                elementCount: 2,
                stride: 4);
            byte[] destination = new byte[8];

            Assert.True(memory.ExecuteControllerVectorTransferReadStep(
                0, source, 2, 4, 4, envelope, destination));
            Assert.Equal(sourceBytes, destination);
        });
    }

    [Fact]
    public void StridedServiceRetainsExactLogicalElementOrder()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong source = 0x180;
            byte[] sourceBytes = Enumerable.Range(1, 12)
                .Select(value => (byte)value)
                .ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(
                source,
                sourceBytes));
            CanonicalVectorPhysicalBankEnvelope envelope = Envelope(
                memory,
                source,
                elementCount: 3,
                stride: 4);
            byte[] destination = new byte[6];

            Assert.True(memory.ExecuteControllerVectorTransferReadStep(
                0, source, 3, 2, 4, envelope, destination));
            Assert.Equal(
                new byte[] { 1, 2, 5, 6, 9, 10 },
                destination);
        });
    }

    [Fact]
    public void MalformedEnvelopeFailsBeforeIommuMutation()
    {
        MemorySubsystem memory = CreateMemory();
        byte[] destination = Enumerable.Repeat((byte)0xA5, 4).ToArray();

        Assert.False(memory.ExecuteControllerVectorTransferReadStep(
            0, 0, 1, 4, 4, default, destination));
        Assert.Equal(
            Enumerable.Repeat((byte)0xA5, 4),
            destination);
    }

    [Fact]
    public void GenerationMismatchFailsBeforeIommuMutation()
    {
        MemorySubsystem memory = CreateMemory();
        CanonicalVectorPhysicalBankEnvelope stale = new(
            MemoryBankGeometryGeneration.Create(1),
            new[] { PhysicalMemoryBankIndex.Zero });
        Assert.True(
            memory.TryReplacePhysicalMemoryBankGeometry(8, 64).IsApplied);
        byte[] destination = Enumerable.Repeat((byte)0xA5, 4).ToArray();

        Assert.False(memory.ExecuteControllerVectorTransferReadStep(
            0, 0, 1, 4, 4, stale, destination));
        Assert.Equal(
            Enumerable.Repeat((byte)0xA5, 4),
            destination);
    }

    [Fact]
    public void CountAndMembershipMismatchFailBeforeIommuMutation()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryBankGeometryGeneration generation =
            memory.PublishedPhysicalBankGeometry.Generation;
        CanonicalVectorPhysicalBankEnvelope countMismatch = new(
            generation,
            new[] { PhysicalMemoryBankIndex.Zero });
        CanonicalVectorPhysicalBankEnvelope membershipMismatch = new(
            generation,
            new[] { PhysicalMemoryBankIndex.Create(8) });
        byte[] destination = Enumerable.Repeat((byte)0xA5, 8).ToArray();

        Assert.False(memory.ExecuteControllerVectorTransferReadStep(
            0, 0, 2, 4, 4, countMismatch, destination));
        Assert.False(memory.ExecuteControllerVectorTransferReadStep(
            0, 0, 1, 8, 8, membershipMismatch, destination));
        Assert.Equal(
            Enumerable.Repeat((byte)0xA5, 8),
            destination);
    }

    [Fact]
    public void PublicAdmissionAndTerminalSignaturesRemainUnchanged()
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
        Assert.Equal(typeof(MemoryAdmissionResult), admission.ReturnType);

        string controller = Controller(FindRepositoryRoot());
        Assert.DoesNotContain("PhysicalBankEnvelope",
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
        Assert.DoesNotContain("PhysicalBankEnvelope",
            external,
            StringComparison.Ordinal);
    }


    private static CanonicalVectorPhysicalBankEnvelope Envelope(
        MemorySubsystem memory,
        ulong sourceAddress,
        ulong elementCount,
        ushort stride)
    {
        PhysicalMemoryBankGeometry geometry =
            memory.PublishedPhysicalBankGeometry;
        var indexes =
            new PhysicalMemoryBankIndex[checked((int)elementCount)];
        for (int index = 0; index < indexes.Length; index++)
        {
            ulong address = checked(
                sourceAddress + checked((ulong)index * stride));
            indexes[index] = PhysicalMemoryBankIndex.Create(
                (int)((address / (ulong)geometry.BankWidthBytes) %
                      (ulong)geometry.BankCount));
        }

        return new CanonicalVectorPhysicalBankEnvelope(
            geometry.Generation,
            indexes);
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
                0, 0, 0, 0x2000UL,
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

    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
    }

    private static string Paper(string root) =>
        Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Controller(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
            "MemoryCycleController.cs");

    private static string CanonicalService(string root) =>
        Slice(
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.Helpers.cs"),
            "internal bool ExecuteControllerVectorTransferReadStep(",
            "#endregion");

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
