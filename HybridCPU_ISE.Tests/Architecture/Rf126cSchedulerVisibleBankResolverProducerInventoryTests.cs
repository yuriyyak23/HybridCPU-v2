using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6c decision-only closed-world inventory of the scheduler-visible
/// bank resolver producer and its direct production/test callers.
/// </summary>
public sealed class Rf126cSchedulerVisibleBankResolverProducerInventoryTests
{
    private const string ThisFile =
        "Rf126cSchedulerVisibleBankResolverProducerInventoryTests.cs";
    private const string DirectCallPattern =
        @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\(";

    [Fact]
    public void ResolverProducerAndLegacyOutcomeBehaviorRemainFrozen()
    {
        string routing = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Execution", "Memory", "LoadStore",
            "MemoryBankRouting.cs");

        Assert.Contains(
            "public static int ResolveSchedulerVisibleBankId(ulong address)",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "Processor.Memory is { NumBanks: > 0, BankWidthBytes: > 0 } memory",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "return ResolveBankId(address, memory.BankWidthBytes, memory.NumBanks)",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _schedulerVisibleUninitializedUseCount)",
            routing, StringComparison.Ordinal);
        Assert.Contains("return UninitializedSchedulerVisibleBankId", routing,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static bool IsResolvedSchedulerVisibleBankId(int bankId) => bankId >= 0",
            routing, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionDirectCallerManifestContainsExactlyThreeCallSites()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address);",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Assist/AssistMicroOp.cs:return MemoryBankRouting.ResolveSchedulerVisibleBankId(address);",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs:public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress);"
            ],
            CaptureCallSites(root, Path.Combine(root, "HybridCPU_ISE")));

        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "HybridCPU_Compiler")));
        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "CpuInterfaceBridge")));
        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "TestAssemblerConsoleApps")));
    }

    [Fact]
    public void ExecutableResolverTestsContainExactlyThreeDirectCalls()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(
            [
                "HybridCPU_ISE.Tests/tests/Phase09MemoryBankRoutingFallbackTelemetryTests.cs:() => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x1000UL));",
                "HybridCPU_ISE.Tests/tests/Phase09MemoryBankRoutingFallbackTelemetryTests.cs:() => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x180UL));",
                "HybridCPU_ISE.Tests/tests/Phase09MemoryBankRoutingFallbackTelemetryTests.cs:action: () => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x2000UL));"
            ],
            CaptureCallSites(root,
                Path.Combine(root, "HybridCPU_ISE.Tests", "tests")));
    }

    [Fact]
    public void DirectCallerRolesAndRawAbsenceHandlingStayDistinct()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string assist = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");

        Assert.Contains("public int MemoryBankId =>", loadStore,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankId = ResolveMemoryBankId(BaseAddress)",
            assist, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankRouting.IsResolvedSchedulerVisibleBankId(MemoryBankId)",
            assist, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryBank(MemoryBankId)",
            assist, StringComparison.Ordinal);
        Assert.Contains(
            "int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address)",
            projection, StringComparison.Ordinal);
        Assert.Contains(
            "!MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)",
            projection, StringComparison.Ordinal);
        Assert.Contains("new MemoryBankId(bank)", projection,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", loadStore,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", assist,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            projection, StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionAndTestSupportMutationSeamsStayExplicit()
    {
        string root = FindRepositoryRoot();
        string scope = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "ProcessorMemoryScope.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");

        Assert.Contains("MemorySubsystem? savedMemory = Processor.Memory",
            scope, StringComparison.Ordinal);
        Assert.Contains("Processor.Memory = memory", scope,
            StringComparison.Ordinal);
        Assert.Contains("Processor.Memory = savedMemory", scope,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetField(", scope, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue(", scope, StringComparison.Ordinal);
        Assert.Contains("internal static void ResetTelemetryForTesting()",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(ref _schedulerVisibleUninitializedUseCount, 0)",
            routing, StringComparison.Ordinal);
    }


    [Fact]
    public void PaperAuthorityAndSelectedResultCallerRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string contractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "MemoryBankResolution.cs"));
        string production = string.Join(Environment.NewLine,
            EnumerateSources(Path.Combine(root, "HybridCPU_ISE"))
                .Where(path => !string.Equals(Path.GetFullPath(path),
                    contractPath, StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.Contains(
            "`MemoryBankResolution` is a three-way result: `Resolved(MemoryBankId)`, `UnavailableTopology`, or `InvalidGeometry`",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "Resolution does not grant memory admission", paper,
            StringComparison.Ordinal);
        Match[] callers = Regex.Matches(
                production,
                @"\bMemoryBankResolution\.Resolved\s*\(")
            .Cast<Match>()
            .ToArray();
        Assert.Equal(3, callers.Length);
        Assert.All(callers, caller =>
            Assert.Equal("MemoryBankResolution.Resolved(", caller.Value));
    }

    private static string[] CaptureCallSites(string repositoryRoot,
        string sourceRoot)
    {
        var regex = new Regex(DirectCallPattern,
            RegexOptions.CultureInvariant);
        var entries = new List<string>();
        foreach (string path in EnumerateSources(sourceRoot)
                     .Where(path => !path.EndsWith(ThisFile,
                         StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(repositoryRoot, path)
                .Replace('\\', '/');
            foreach (string line in File.ReadLines(path))
            {
                if (regex.IsMatch(line))
                {
                    entries.Add($"{relative}:{line.Trim()}");
                }
            }
        }

        entries.Sort(StringComparer.Ordinal);
        return entries.ToArray();
    }

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
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
                    "ResearchPaper", "section", "md base")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
