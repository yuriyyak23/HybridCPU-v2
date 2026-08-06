using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6v decision-only closed-world revalidation of the immutable
/// physical-memory geometry snapshot and current count/width consumers.
/// </summary>
public sealed class Rf126vPhysicalMemoryBankGeometrySnapshotRevalidationTests
{
    private const string ThisFile =
        "Rf126vPhysicalMemoryBankGeometrySnapshotRevalidationTests.cs";

    [Fact]
    public void PaperDefinesExactImmutablePublishedTuple()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(paper,
            "A published physical geometry is the immutable tuple:",
            "(positive BankCount, positive BankWidthBytes,",
            "non-zero MemoryBankGeometryGeneration)",
            "`BankCount` and `BankWidthBytes` are positive `Int32` values",
            "Address resolution consumes one immutable geometry snapshot");
        Assert.Contains("raw/default zero is the unissued or",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "absent outer representation and is never a checked value",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperSeparatesRepresentationFromOwnerAndAllocationAuthority()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains("values. Allocation", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "feasibility is a platform-owner decision rather than identifier validity",
            paper, StringComparison.Ordinal);
        Assert.Contains("identifier validity", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Only the timed-memory geometry owner advances allocation state",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "Checked representation alone grants no admission",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRequiresOneSnapshotForResolutionAndBinding()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(paper,
            "Address resolution consumes one immutable geometry snapshot",
            "(address / BankWidthBytes) % BankCount",
            "binds the result to that snapshot's generation");
        Assert.Contains("synthetic `4096/16` substitution is not validation",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "An invalid or unavailable geometry produces an outer result with no physical",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "index and no generation; it never produces physical bank zero",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeGeometrySnapshotAndPublishedGenerationStorageAreUnique()
    {
        string root = FindRepositoryRoot();
        string production = ReadProduction(root);
        string geometryContract = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "PhysicalMemoryBankGeometry.cs");
        string bindingContract = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "PhysicalMemoryBankBinding.cs");

        Assert.DoesNotMatch(
            @"\b(?:record\s+struct|readonly\s+struct|struct|class)\s+(?:PhysicalMemoryBankGeometry|MemoryBankGeometrySnapshot)\b",
            production);
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankGeometry\b",
            geometryContract);
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankBinding\b",
            bindingContract);

        string generationContract = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemoryBankGeometryGeneration.cs");
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+MemoryBankGeometryGeneration\b",
            generationContract);

        string memoryOwner = ReadTree(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem"),
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemoryBankGeometryGeneration.cs"),
            GeometryContractPath(root),
            BindingContractPath(root));
        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            memoryOwner, StringComparison.Ordinal);
        Assert.Contains(
            "private PhysicalMemoryBankGeometry _publishedPhysicalBankGeometry;",
            memoryOwner, StringComparison.Ordinal);
        Assert.Contains(
            "public PhysicalMemoryBankGeometry PublishedPhysicalBankGeometry",
            memoryOwner, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(memoryOwner,
            @"private\s+ulong\s+_lastIssuedPhysicalBankGeometryGeneration\s*;")
            .Count);
        Assert.Equal(1, Regex.Matches(memoryOwner,
            @"private\s+PhysicalMemoryBankGeometry\s+" +
            @"_publishedPhysicalBankGeometry\s*;").Count);
    }

    [Fact]
    public void CurrentGeometryStorageIsSplitAndMutable()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Order(subsystem,
            "private int _numBanks = 8;",
            "public int NumBanks",
            "int sanitized = Math.Max(1, value);",
            "_numBanks = sanitized;",
            "ReconfigureBankTopology();",
            "private int _bankWidthBytes = 64;",
            "public int BankWidthBytes",
            "_bankWidthBytes = value;");
        Assert.Equal(typeof(int), typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.NumBanks))!.PropertyType);
        Assert.True(typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.NumBanks))!.CanWrite);
        Assert.Equal(typeof(int), typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.BankWidthBytes))!.PropertyType);
        Assert.True(typeof(MemorySubsystem)
            .GetProperty(nameof(MemorySubsystem.BankWidthBytes))!.CanWrite);
    }

    [Fact]
    public void CountWidthProductionManifestRemainsExact()
    {
        string root = FindRepositoryRoot();

        AssertCounts(root, "NumBanks", new Dictionary<string, int>
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Execution/Memory/LoadStore/MemoryBankRouting.cs"] = 2,
            ["HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.cs"] = 11,
            ["HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.Helpers.cs"] = 11,
            ["HybridCPU_ISE/Machine/IseObservationService.cs"] = 8,
            ["HybridCPU_ISE/NonRTL/Processor/Performance/Processor.Performance.cs"] = 1
        });
        AssertCounts(root, "BankWidthBytes", new Dictionary<string, int>
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Execution/Memory/LoadStore/MemoryBankRouting.cs"] = 2,
            ["HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.cs"] = 5,
            ["HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.Helpers.cs"] = 1,
            ["HybridCPU_ISE/Machine/IseObservationService.cs"] = 4,
            ["HybridCPU_ISE/NonRTL/Processor/Configuration/ProcessorConfig.cs"] = 3
        });
        AssertCounts(root, "_numBanks", new Dictionary<string, int>
        {
            ["HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.cs"] = 15
        });
    }

    [Fact]
    public void CountConsumersCoverStorageIndexingArbitrationAndPressure()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string helpers = Helpers(root);

        foreach (string marker in new[]
                 {
                     "new int[NumBanks]",
                     "CurrentQueuedRequests > (NumBanks * HardwareQueueSaturationThreshold)",
                     "int trackedBanks = Math.Min(NumBanks, 16)",
                     "for (int i = 0; i < NumBanks; i++)",
                     "int totalQueueCapacity = NumBanks * HardwareQueueSaturationThreshold",
                     "new Queue<BankRequest>[NumBanks]",
                     "new Queue<BankRequest>[_numBanks]",
                     "roundRobinIndex % _numBanks"
                 })
        {
            Assert.Contains(marker, subsystem, StringComparison.Ordinal);
        }

        foreach (string marker in new[]
                 {
                     "(uint)bankId >= (uint)NumBanks",
                     "(roundRobinIndex + 1) % NumBanks",
                     "(currentCycle + p) % NumBanks",
                     "int maxAttempts = NumBanks"
                 })
        {
            Assert.Contains(marker, helpers, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WidthConsumersRemainResolverAndRawTimingArithmetic()
    {
        string root = FindRepositoryRoot();
        string routing = Routing(root);
        string subsystem = Subsystem(root);

        Assert.Contains(
            "return ResolveBankId(address, memory.BankWidthBytes, memory.NumBanks);",
            routing, StringComparison.Ordinal);
        Assert.Contains("bankWidthBytes > 0", routing,
            StringComparison.Ordinal);
        Assert.Contains(": DefaultBankWidthBytes;", routing,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"int\s+accessCycles\s*=\s*4\s*\+\s*\(length\s*/\s*BankWidthBytes\)")
            .Count);
    }

    [Fact]
    public void DiagnosticZerosAndDormantConfigAreNotPublishedGeometry()
    {
        string root = FindRepositoryRoot();
        string observation = Read(root, "HybridCPU_ISE", "Machine",
            "IseObservationService.cs");
        string config = Read(root, "HybridCPU_ISE", "NonRTL", "Processor",
            "Configuration", "ProcessorConfig.cs");
        string configConsumers = ReadProduction(root);

        Assert.Contains("NumBanks = 0,", observation,
            StringComparison.Ordinal);
        Assert.Contains("BankWidthBytes = 0,", observation,
            StringComparison.Ordinal);
        Assert.Contains("public int NumMemoryBanks { get; set; } = 8;",
            config, StringComparison.Ordinal);
        Assert.Contains("public int BankWidthBytes { get; set; } = 64;",
            config, StringComparison.Ordinal);
        Assert.DoesNotContain("config.BankWidthBytes", configConsumers,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("config.NumMemoryBanks", configConsumers,
            StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void RequestWireReplayAndExternalSurfacesHaveNoSnapshot()
    {
        string root = FindRepositoryRoot();
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");
        string token = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Types.cs");
        string external = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.DoesNotContain("GeometryGeneration", operations,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", token,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankGeometry", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometrySnapshot", external,
            StringComparison.Ordinal);
    }


    private static void AssertCounts(
        string root,
        string term,
        IReadOnlyDictionary<string, int> expected)
    {
        var actual = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string sourceRoot in new[]
                 {
                     "HybridCPU_ISE", "HybridCPU_Compiler",
                     "CpuInterfaceBridge", "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            foreach (string path in EnumerateSources(Path.Combine(root,
                         sourceRoot)))
            {
                if (string.Equals(Path.GetFullPath(path),
                        GeometryContractPath(root),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                int count = Regex.Matches(text, $@"\b{term}\b").Count;
                if (count == 0)
                {
                    continue;
                }

                actual[Path.GetRelativePath(root, path).Replace('\\', '/')] =
                    count;
            }
        }

        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            actual.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    private static string ReadProduction(string root) =>
        string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_ISE"),
                GeometryContractPath(root), BindingContractPath(root)),
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

    private static string GeometryContractPath(string root) =>
        Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "PhysicalMemoryBankGeometry.cs"));

    private static string BindingContractPath(string root) =>
        Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "PhysicalMemoryBankBinding.cs"));

    private static string Subsystem(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs");

    private static string Helpers(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Helpers.cs");

    private static string Routing(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Core", "Execution", "Memory", "LoadStore",
        "MemoryBankRouting.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6v-physical-memory-bank-geometry-snapshot-revalidation-decision.md");

    private static string ReadTree(
        string path,
        params string[] excludedPaths) =>
        string.Join("\n", EnumerateSources(path)
            .Where(source => !excludedPaths.Contains(
                Path.GetFullPath(source), StringComparer.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

    private static IEnumerable<string> EnumerateSources(string path) =>
        Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                .Where(source => !IsBuildOutput(source))
                .OrderBy(source => source, StringComparer.Ordinal)
            : [];

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void Order(string text, params string[] markers)
    {
        int offset = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, offset + 1,
                StringComparison.Ordinal);
            Assert.True(next > offset,
                $"Missing or out-of-order marker: {marker}");
            offset = next;
        }
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

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
