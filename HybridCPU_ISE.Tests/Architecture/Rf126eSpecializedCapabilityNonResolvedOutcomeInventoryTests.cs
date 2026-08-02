using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6e decision-only inventory of the specialized capability projection's
/// unavailable/invalid scheduler-visible bank outcomes and fallback telemetry.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126eSpecializedCapabilityNonResolvedOutcomeInventoryTests
{
    private const string ThisFile =
        "Rf126eSpecializedCapabilityNonResolvedOutcomeInventoryTests.cs";

    private static readonly MethodInfo BuildMemoryCapability =
        typeof(Rf06SpecializedCapabilityProjection).GetMethod(
            "BuildMemoryCapability",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(
            nameof(Rf06SpecializedCapabilityProjection),
            "BuildMemoryCapability");

    [Fact]
    public void PaperDefinesThreeWayResultAndTelemetryDoesNotGrantAuthority()
    {
        string root = FindRepositoryRoot();
        string architecture = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string telemetry = Read(root, "ResearchPaper", "section", "md base",
            "8_Telemetry_Validation_and_Evaluation_Methodology.md");

        Assert.Contains(
            "`MemoryBankResolution` is a three-way result: `Resolved(MemoryBankId)`, `UnavailableTopology`, or `InvalidGeometry`",
            architecture, StringComparison.Ordinal);
        Assert.Contains(
            "Geometry is resolvable only with positive bank width and a bank count in `1..16`",
            architecture, StringComparison.Ordinal);
        Assert.Contains(
            "Unavailable or invalid geometry has no bank value and may not become bank zero",
            architecture, StringComparison.Ordinal);
        Assert.Contains(
            "Telemetry says what happened; proof and boundary documents determine what may be",
            telemetry, StringComparison.Ordinal);
        Assert.Contains(
            "bank, hazard, and cross-domain counters are interpreted as",
            telemetry, StringComparison.Ordinal);
    }

    [Fact]
    public void RawProducerMutationAndProjectionWinnerOrderRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");

        int topologyGate = routing.IndexOf(
            "Processor.Memory is { NumBanks: > 0, BankWidthBytes: > 0 } memory",
            StringComparison.Ordinal);
        int pureResolver = routing.IndexOf(
            "return ResolveBankId(address, memory.BankWidthBytes, memory.NumBanks)",
            topologyGate, StringComparison.Ordinal);
        int telemetryMutation = routing.IndexOf(
            "Interlocked.Increment(ref _schedulerVisibleUninitializedUseCount)",
            pureResolver, StringComparison.Ordinal);
        int rawSentinel = routing.IndexOf(
            "return UninitializedSchedulerVisibleBankId",
            telemetryMutation, StringComparison.Ordinal);

        Assert.True(topologyGate >= 0);
        Assert.True(pureResolver > topologyGate);
        Assert.True(telemetryMutation > pureResolver);
        Assert.True(rawSentinel > telemetryMutation);

        int freeze = projection.IndexOf(
            "ImmutableArray<FrozenMemoryRange> footprint = FreezeRanges(selected)",
            StringComparison.Ordinal);
        int rawCall = projection.IndexOf(
            "int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address)",
            freeze, StringComparison.Ordinal);
        int rawGate = projection.IndexOf(
            "!MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)",
            rawCall, StringComparison.Ordinal);
        int checkedConstruction = projection.IndexOf(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            rawGate, StringComparison.Ordinal);

        Assert.True(freeze >= 0);
        Assert.True(rawCall > freeze);
        Assert.True(rawGate > rawCall);
        Assert.True(checkedConstruction > rawGate);
    }

    [Fact]
    public void UnavailableAndIncompleteGeometryKeepOneSentinelOneMutationAndOneFault()
    {
        AssertNonResolvedLegacyOutcome(memory: null, address: 0x2000UL);
        AssertNonResolvedLegacyOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(
                numBanks: 8,
                bankWidthBytes: 0),
            address: 0x1000UL);
    }

    [Fact]
    public void WidePositiveInvalidGeometryRetainsAddressDependentLegacySplit()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            var memory = ProcessorMemoryScope.CreateMemorySubsystem(
                numBanks: 32,
                bankWidthBytes: 64);

            ProcessorMemoryScope.WithProcessorMemory(memory, () =>
            {
                MemoryCapability lowRawBank =
                    InvokeBuild(new MemoryProbe((0UL, 8UL)));
                Assert.Equal(new MemoryBankId(0), lowRawBank.Bank);

                ArgumentOutOfRangeException exception =
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        InvokeBuild(new MemoryProbe((17UL * 64UL, 8UL))));
                Assert.Equal("value", exception.ParamName);
                Assert.Equal(17, exception.ActualValue);
            });

            Assert.Equal(0UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void ClosedWorldOutcomeFactoriesAndRawCallersStayExplicit()
    {
        string root = FindRepositoryRoot();
        string production = string.Join(Environment.NewLine,
            EnumerateSources(Path.Combine(root, "HybridCPU_ISE"))
                .Where(path => !path.EndsWith("MemoryBankResolution.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.Equal(3, Regex.Matches(production,
            @"\bMemoryBankResolution\.Resolved\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.UnavailableTopology\b").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.InvalidGeometry\b").Count);

        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address);",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Assist/AssistMicroOp.cs:return MemoryBankRouting.ResolveSchedulerVisibleBankId(address);",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs:public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress);"
            ],
            CaptureCallSites(root, Path.Combine(root, "HybridCPU_ISE"),
                @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\("));

        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "HybridCPU_Compiler"),
            @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\("));
        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "CpuInterfaceBridge"),
            @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\("));
        Assert.Empty(CaptureCallSites(root,
            Path.Combine(root, "TestAssemblerConsoleApps"),
            @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\("));
    }

    [Fact]
    public void TelemetryAndTestSupportMutationSeamsRemainBounded()
    {
        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        string scope = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "ProcessorMemoryScope.cs");
        string probe = Read(root, "HybridCPU_ISE.Tests", "Architecture",
            "Rf126dSpecializedCapabilityBankResolutionValidInputCutoverTests.cs");

        Assert.Contains(
            "internal static ulong SchedulerVisibleUninitializedUseCount",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "internal static void ResetTelemetryForTesting()",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(ref _schedulerVisibleUninitializedUseCount, 0)",
            routing, StringComparison.Ordinal);
        Assert.Contains("Processor.Memory = memory", scope,
            StringComparison.Ordinal);
        Assert.Contains("Processor.Memory = savedMemory", scope,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetField(", scope, StringComparison.Ordinal);
        Assert.Contains("BindingFlags.NonPublic | BindingFlags.Static", probe,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue(", probe, StringComparison.Ordinal);
    }


    private static void AssertNonResolvedLegacyOutcome(
        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memory,
        ulong address)
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            InvalidOperationException exception =
                ProcessorMemoryScope.WithProcessorMemory<InvalidOperationException>(
                    memory,
                    () => Assert.Throws<InvalidOperationException>(() =>
                        InvokeBuild(new MemoryProbe((address, 8UL)))));

            Assert.Equal(
                "RF-06.5 memory-bearing family projection requires a resolved scheduler-visible bank.",
                exception.Message);
            Assert.Equal(1UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    private static MemoryCapability InvokeBuild(MicroOp carrier)
    {
        try
        {
            return (MemoryCapability)BuildMemoryCapability.Invoke(
                obj: null,
                parameters: [carrier])!;
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static string[] CaptureCallSites(
        string repositoryRoot,
        string sourceRoot,
        string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
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

    private sealed class MemoryProbe : MicroOp
    {
        internal MemoryProbe(
            params (ulong Address, ulong Length)[] readRanges)
        {
            ReadMemoryRanges = readRanges;
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() =>
            "RF-12.6e memory-capability projection probe";
    }
}
