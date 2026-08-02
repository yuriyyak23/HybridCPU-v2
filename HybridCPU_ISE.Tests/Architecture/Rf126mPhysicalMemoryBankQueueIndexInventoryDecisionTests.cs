using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6m decision-only closed-world inventory of pure bank arithmetic,
/// physical MemorySubsystem queue indexes, mutable geometry and diagnostics.
/// These tests authorize no production or invalid-input change.
/// </summary>
public sealed class Rf126mPhysicalMemoryBankQueueIndexInventoryDecisionTests
{
    private const string ThisFile =
        "Rf126mPhysicalMemoryBankQueueIndexInventoryDecisionTests.cs";

    [Fact]
    public void InventoryRecordsThePreDecisionPhysicalAuthorityGap()
    {
        string root = FindRepositoryRoot();
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF12",
            "rf12.6m-physical-memory-bank-queue-index-inventory-decision.md");

        Assert.Contains(
            "Paper section 3.7 defines only",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "scheduler-visible `MemoryBankId` family `0..15`",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "resolvable only for positive width and bank counts `1..16`",
            evidence, StringComparison.Ordinal);
        Assert.Contains(
            "paper does not define",
            evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "RF-12.6m closes the inventory but cannot authorize a checked-ID cutover",
            evidence, StringComparison.Ordinal);

        string production = ReadSourceTree(
            Path.Combine(root, "HybridCPU_ISE"),
            "PhysicalMemoryBankIndex.cs");
        Assert.DoesNotMatch(
            new Regex(
                @"\b(?:record\s+struct|readonly\s+struct|class)\s+(?:PhysicalMemoryBankIndex|PhysicalBankId)\b",
                RegexOptions.CultureInvariant),
            production);
    }

    [Fact]
    public void PureResolverKeepsSyntheticFallbackWideRangeAndZeroAliases()
    {
        Assert.Equal(0, MemoryBankRouting.ResolveBankId(0, 64, 16));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(64, 64, 16));
        Assert.Equal(15, MemoryBankRouting.ResolveBankId(15 * 64UL, 64, 16));
        Assert.Equal(0, MemoryBankRouting.ResolveBankId(16 * 64UL, 64, 16));
        Assert.Equal(17, MemoryBankRouting.ResolveBankId(17 * 64UL, 64, 32));

        Assert.Equal(0, MemoryBankRouting.ResolveBankId(0, 0, 0));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, 0, 0));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, -7, -9));
        Assert.Equal(15,
            MemoryBankRouting.ResolveBankId(15 * 4096UL, 0, 0));
    }

    [Fact]
    public void ResolverAndComputeBankCallerManifestsAreExact()
    {
        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.cs");

        Assert.Equal(1, Regex.Matches(routing,
            @"return\s+ResolveBankId\(address,\s*memory\.BankWidthBytes,\s*memory\.NumBanks\)").Count);
        Assert.Equal(1, Regex.Matches(helpers,
            @"Core\.Memory\.MemoryBankRouting\.ResolveBankId\(\s*address,\s*BankWidthBytes,\s*NumBanks\)").Count);
        Assert.Equal(2, Regex.Matches(helpers,
            @"\bComputeBankId\s*\(").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"\bComputeBankId\s*\(").Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"\bComputeBankId\s*\(").Count);

        Assert.Equal(1, Regex.Matches(helpers,
            @"private\s+PhysicalMemoryBankIndex\s+ComputeBankId\(ulong\s+address\)").Count);
        Assert.Equal(5, Regex.Matches(
            helpers + "\n" + operations + "\n" + subsystem,
            @"\bComputeBankId\s*\(").Count - 1);
    }

    [Fact]
    public void WideBankAndMutableGeometryBehaviorsRemainExplicit()
    {
        MemorySubsystem wide = CreateMemory(numBanks: 32, bankWidthBytes: 64);
        MemorySubsystem.MemoryRequestToken wideToken = wide.EnqueueRead(
            0, 17UL * 64UL, 1, new byte[1]);
        Assert.Equal(1, wide.CurrentQueuedRequests);
        Assert.Null(typeof(MemorySubsystem.MemoryRequestToken).GetProperty(
            "BankId", BindingFlags.Public | BindingFlags.Instance));
        Assert.False(wide.CancelPendingRequest(wideToken));
        Assert.Equal(1, wide.CurrentQueuedRequests);

        MemorySubsystem remapped = CreateMemory(
            numBanks: 4, bankWidthBytes: 64);
        MemorySubsystem.MemoryRequestToken remappedToken =
            remapped.EnqueueRead(0, 64, 1, new byte[1]);
        Assert.Equal(1, remapped.CurrentQueuedRequests);
        remapped.BankWidthBytes = 128;
        Assert.True(remapped.CancelPendingRequest(remappedToken));
        Assert.Equal(0, remapped.CurrentQueuedRequests);

        MemorySubsystem shrunk = CreateMemory(numBanks: 4, bankWidthBytes: 64);
        MemorySubsystem.MemoryRequestToken droppedToken = shrunk.EnqueueRead(
            0, 3UL * 64UL, 1, new byte[1]);
        Assert.Equal(1, shrunk.CurrentQueuedRequests);
        shrunk.NumBanks = 2;
        Assert.Equal(0, shrunk.CurrentQueuedRequests);
        Assert.False(shrunk.CancelPendingRequest(droppedToken));

        shrunk.NumBanks = 0;
        Assert.Equal(1, shrunk.NumBanks);
        shrunk.NumBanks = -17;
        Assert.Equal(1, shrunk.NumBanks);
    }

    [Fact]
    public void QueueIndexArbitrationAndOccupancyTopologyRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.Helpers.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.Operations.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(2, Regex.Matches(operations,
            @"bankQueues\[physicalBankBinding\.BankIndex\.Value\]").Count);
        Assert.Contains(
            "return RemoveQueuedBankRequest(",
            operations, StringComparison.Ordinal);
        Assert.Contains(
            "if ((uint)bankId >= (uint)NumBanks || bankQueues[bankId].Count == 0)",
            helpers, StringComparison.Ordinal);

        Assert.Contains("private int SelectBankRoundRobin()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("private int SelectBankWeightedFair()", helpers,
            StringComparison.Ordinal);
        Assert.Contains("private int SelectBankPriority()", helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "int targetBank = (int)((currentCycle + p) % NumBanks)",
            helpers, StringComparison.Ordinal);
        Assert.Contains("int selectedBank = SelectNextBank()", helpers,
            StringComparison.Ordinal);

        Assert.Contains("int sanitized = Math.Max(1, value)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains(
            "Queue<BankRequest>[] resizedBankQueues = new Queue<BankRequest>[_numBanks]",
            subsystem, StringComparison.Ordinal);
        Assert.Contains(
            "resizedBankQueues[i] = new Queue<BankRequest>(existingBankQueues![i])",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("int trackedBanks = Math.Min(NumBanks, 16)",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("if (i >= trackedBanks)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("mask |= Core.ResourceMaskBuilder.ForMemoryBank128(i)",
            subsystem, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalBankTelemetryAndReplayWiresRemainRawAndPositional()
    {
        PropertyInfo bankProperty = typeof(MemorySubsystem.BurstEventArgs)
            .GetProperty(nameof(MemorySubsystem.BurstEventArgs.BankId))!;
        Assert.Equal(typeof(int), bankProperty.PropertyType);
        Assert.True(bankProperty.CanWrite);

        string root = FindRepositoryRoot();
        string performance = Read(root, "HybridCPU_ISE", "NonRTL",
            "Processor", "Performance", "Processor.Performance.cs");
        string burstTrace = Read(root, "HybridCPU_ISE", "NonRTL",
            "Processor", "Performance",
            "PerformanceReport.BurstTraceSurface.cs");
        string traceSink = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TraceSink.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "ReplayEngine.cs");

        Assert.Contains("BankId = e.BankId", performance,
            StringComparison.Ordinal);
        Assert.Contains(
            "Timestamp,Address,Length,IsRead,BankId,Duration",
            burstTrace, StringComparison.Ordinal);
        Assert.Contains(
            "{trace.Timestamp},{trace.Address},{trace.Length},{trace.IsRead},{trace.BankId},{trace.Duration}",
            burstTrace, StringComparison.Ordinal);
        Assert.Contains("writer.Write(evt.BankQueueDepths?.Length ?? 0)",
            traceSink, StringComparison.Ordinal);
        Assert.Contains("writer.Write(depth)", traceSink,
            StringComparison.Ordinal);
        Assert.Contains("evt.BankQueueDepths = new int[bankQueueLen]",
            replay, StringComparison.Ordinal);
        Assert.Contains("evt.BankQueueDepths[i] = reader.ReadInt32()",
            replay, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSupportExternalAndReflectionSeamsAreClosedWorld()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(55, CountMatches(root, "HybridCPU_ISE.Tests",
            @"\bCreateMemorySubsystem\s*\(", ThisFile));

        string scope = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "ProcessorMemoryScope.cs");
        Assert.Contains("Processor.Memory = memory", scope,
            StringComparison.Ordinal);
        Assert.Contains("NumBanks = numBanks", scope,
            StringComparison.Ordinal);
        Assert.Contains("BankWidthBytes = bankWidthBytes", scope,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue(", scope, StringComparison.Ordinal);

        string tests = ReadSourceTree(
            Path.Combine(root, "HybridCPU_ISE.Tests"),
            ThisFile,
            "Rf126qPhysicalMemoryBankIndexProducerValidInputCutoverTests.cs");
        Assert.DoesNotMatch(new Regex(
                @"GetField\s*\(\s*""(?:bankQueues|bankOccupied|roundRobinIndex|_numBanks)""|GetMethod\s*\(\s*""ComputeBankId""",
                RegexOptions.CultureInvariant),
            tests);

        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            string text = ReadSourceTree(Path.Combine(root, externalRoot));
            Assert.DoesNotContain("MemoryBankRouting.ResolveBankId(",
                text, StringComparison.Ordinal);
            Assert.DoesNotContain("ComputeBankId(", text,
                StringComparison.Ordinal);
        }

        string assembler = Read(root, "TestAssemblerConsoleApps",
            "SimpleAsmApp.Init.cs");
        Assert.Contains("((ulong)(bankId & 0x7) * 0x40UL)",
            assembler, StringComparison.Ordinal);
    }


    private static MemorySubsystem CreateMemory(
        int numBanks,
        int bankWidthBytes)
    {
        Processor proc = default;
        return new MemorySubsystem(ref proc)
        {
            NumBanks = numBanks,
            BankWidthBytes = bankWidthBytes
        };
    }

    private static int CountMatches(
        string repositoryRoot,
        string relativeRoot,
        string pattern,
        string? excludedFileName = null)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return EnumerateSources(Path.Combine(repositoryRoot, relativeRoot))
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Sum(path => regex.Matches(File.ReadAllText(path)).Count);
    }

    private static string ReadSourceTree(
        string root,
        params string[] excludedFileNames) =>
        string.Join("\n", EnumerateSources(root)
            .Where(path => !excludedFileNames.Any(excludedFileName =>
                path.EndsWith(excludedFileName,
                    StringComparison.OrdinalIgnoreCase)))
            .Select(File.ReadAllText));

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

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
