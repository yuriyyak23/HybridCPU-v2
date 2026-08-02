using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6r decision-only closed-world inventory of physical memory-bank
/// invalid geometry, fallback, live mutation and cancellation recomputation.
/// </summary>
public sealed class Rf126rPhysicalMemoryBankInvalidGeometryFallbackInventoryDecisionTests
{
    [Fact]
    public void PaperRejectsFallbackAndLaterDecisionDefinesExactCarrier()
    {
        string paper = Paper(FindRepositoryRoot());

        Assert.Contains(
            "An invalid or unavailable geometry produces an outer result with no physical",
            paper, StringComparison.Ordinal);
        Assert.Contains("index and no generation; it never produces physical bank zero",
            paper, StringComparison.Ordinal);
        Assert.Contains("synthetic `4096/16` substitution is not validation",
            paper, StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankResolution =", paper,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankGeometryUpdateResult =", paper,
            StringComparison.Ordinal);
        Assert.Contains("cancellation may not", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "re-resolve the request address against current geometry",
            paper, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0UL, 0, 0, 0)]
    [InlineData(4096UL, 0, 0, 1)]
    [InlineData(4096UL, -7, -9, 1)]
    [InlineData(61440UL, 0, 0, 15)]
    [InlineData(4096UL, 4096, 0, 1)]
    public void SyntheticResolverFallbackRemainsExact(
        ulong address,
        int bankWidthBytes,
        int numBanks,
        int expected)
    {
        Assert.Equal(expected,
            MemoryBankRouting.ResolveBankId(
                address, bankWidthBytes, numBanks));
    }

    [Fact]
    public void ResolverHasTwoDistinctInvalidGeometryBehaviors()
    {
        string routing = Routing(FindRepositoryRoot());

        Assert.Contains("UninitializedSchedulerVisibleBankId = -1", routing,
            StringComparison.Ordinal);
        Assert.Contains("if (Processor.Memory is { NumBanks: > 0, BankWidthBytes: > 0 } memory)",
            routing, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Increment(ref _schedulerVisibleUninitializedUseCount)",
            routing, StringComparison.Ordinal);
        Assert.Contains("private const int DefaultNumBanks = 16", routing,
            StringComparison.Ordinal);
        Assert.Contains("private const int DefaultBankWidthBytes = 4096", routing,
            StringComparison.Ordinal);
        Assert.Contains(": DefaultBankWidthBytes;", routing,
            StringComparison.Ordinal);
        Assert.Contains(": DefaultNumBanks;", routing,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NumBanksClampAndMutationOrderingRemainExact()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.True(
            subsystem.IndexOf("_numBanks = sanitized;", StringComparison.Ordinal) <
            subsystem.IndexOf("ReconfigureBankTopology();", StringComparison.Ordinal));
        Assert.True(
            subsystem.IndexOf("bool[] resizedBankOccupied = new bool[_numBanks];",
                StringComparison.Ordinal) <
            subsystem.IndexOf("int copiedBanks = existingBankQueues == null",
                StringComparison.Ordinal));
        Assert.Contains(
            "int copiedBanks = existingBankQueues == null ? 0 : Math.Min(existingBankQueues.Length, _numBanks);",
            subsystem, StringComparison.Ordinal);
        Assert.Contains(
            "resizedBankQueues[i] = new Queue<BankRequest>(existingBankQueues![i]);",
            subsystem, StringComparison.Ordinal);

        Processor processor = default;
        var memory = new MemorySubsystem(ref processor);
        memory.NumBanks = 0;
        Assert.Equal(1, memory.NumBanks);
        memory.NumBanks = -17;
        Assert.Equal(1, memory.NumBanks);
    }

    [Fact]
    public void WidthAcceptsInvalidValuesAndSyncTimingUsesRawDivisor()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Assert.Contains("private int _bankWidthBytes = 64;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", subsystem,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"int\s+accessCycles\s*=\s*4\s*\+\s*\(length\s*/\s*BankWidthBytes\)").Count);

        Processor processor = default;
        var memory = new MemorySubsystem(ref processor);
        memory.BankWidthBytes = 0;
        Assert.Equal(0, memory.BankWidthBytes);
        memory.BankWidthBytes = -64;
        Assert.Equal(-64, memory.BankWidthBytes);
    }

    [Fact]
    public void LiveRemapAndShrinkCompatibilityBehaviorRemainExact()
    {
        Processor processor = default;
        var remapped = new MemorySubsystem(ref processor)
        {
            NumBanks = 4,
            BankWidthBytes = 64
        };
        MemorySubsystem.MemoryRequestToken remappedToken =
            remapped.EnqueueRead(0, 64, 1, new byte[1]);
        remapped.BankWidthBytes = 128;
        Assert.True(remapped.CancelPendingRequest(remappedToken));
        Assert.Equal(0, remapped.CurrentQueuedRequests);
        Assert.False(remapped.CancelPendingRequest(remappedToken));
        Assert.Equal(0, remapped.CurrentQueuedRequests);

        Processor shrinkProcessor = default;
        var shrunk = new MemorySubsystem(ref shrinkProcessor)
        {
            NumBanks = 4,
            BankWidthBytes = 64
        };
        MemorySubsystem.MemoryRequestToken droppedToken =
            shrunk.EnqueueRead(0, 3UL * 64UL, 1, new byte[1]);
        shrunk.NumBanks = 2;
        Assert.Equal(0, shrunk.CurrentQueuedRequests);
        Assert.False(shrunk.CancelPendingRequest(droppedToken));
    }

    [Fact]
    public void CancellationValidatesBindingBeforeOneCapturedQueueRemoval()
    {
        string root = FindRepositoryRoot();
        string operations = Operations(root);
        string tokenSource = Subsystem(root);

        int removePending = operations.IndexOf(
            "pendingRequests.Remove(requestID);", StringComparison.Ordinal);
        int bindingValidation = operations.IndexOf(
            "!physicalBankBinding.IsWellFormed", StringComparison.Ordinal);
        int queueRemoval = operations.IndexOf(
            "return RemoveQueuedBankRequest(",
            StringComparison.Ordinal);
        Assert.True(bindingValidation >= 0 && removePending > bindingValidation &&
                    queueRemoval > removePending);
        Assert.DoesNotContain("ComputeBankId(token.Address)", operations,
            StringComparison.Ordinal);

        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Null(token.GetProperty("PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(token.GetProperty("GeometryGeneration",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain("PhysicalBankIndex", tokenSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration",
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
                "MemorySubsystem.Types.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WideTopologyLowSixteenProjectionAndDiagnosticAbsenceRemainRaw()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string observation = Read(root, "HybridCPU_ISE", "Machine",
            "IseObservationService.cs");

        Assert.Contains("int trackedBanks = Math.Min(NumBanks, 16);",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("if (i >= trackedBanks)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("memoryBankBudgetAtLeastOneMask |= (ushort)(1 << i);",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("NumBanks = 0,", observation, StringComparison.Ordinal);
        Assert.Contains("BankWidthBytes = 0,", observation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResultGenerationWireAndExternalMutationSurfacesRemainAbsent()
    {
        string root = FindRepositoryRoot();
        string generationContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryGeneration.cs"));
        string geometryContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankGeometry.cs"));
        string bindingContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankBinding.cs"));
        string resolutionContract = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankResolution.cs"));
        string production = string.Join("\n", new[]
        {
            ReadTree(Path.Combine(root, "HybridCPU_ISE"),
                generationContract, geometryContract, bindingContract,
                resolutionContract),
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            ReadTree(Path.Combine(root, "CpuInterfaceBridge")),
            ReadTree(Path.Combine(root, "HybridCPU_RoslynBridge")),
            ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryGeneration.Create(nextGenerationRaw)",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(geometryContract),
            StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(bindingContract),
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankResolution", production,
            StringComparison.Ordinal);
        Assert.Matches(
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankResolution\b",
            File.ReadAllText(resolutionContract));
        Assert.Contains(
            "PhysicalMemoryBankBinding PhysicalBankBinding",
            production, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(
            production,
            @"request\.PhysicalBankBinding").Count);
        Assert.DoesNotContain("MemoryBankGeometryGeneration",
            ReadTree(Path.Combine(root, "HybridCPU_Compiler")),
            StringComparison.Ordinal);

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("BankWidthBytes", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NumBanks", testSupport,
            StringComparison.Ordinal);
    }


    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Routing(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Core", "Execution", "Memory", "LoadStore",
        "MemoryBankRouting.cs");

    private static string Subsystem(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs");

    private static string Operations(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs");

    private static string ReadTree(string path, params string[] excludedPaths) =>
        string.Join("\n", Directory.EnumerateFiles(path, "*.cs",
                SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !excludedPaths.Contains(
                Path.GetFullPath(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
