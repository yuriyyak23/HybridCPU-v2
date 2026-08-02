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
/// RF-12.6f behavior-preserving selection of typed non-resolved bank outcomes
/// at the one RF-06 specialized capability projection unresolved gate.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126fSpecializedCapabilityNonResolvedResultCutoverTests
{
    private const string ThisFile =
        "Rf126fSpecializedCapabilityNonResolvedResultCutoverTests.cs";

    private static readonly MethodInfo BuildMemoryCapability =
        typeof(Rf06SpecializedCapabilityProjection).GetMethod(
            "BuildMemoryCapability",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(
            nameof(Rf06SpecializedCapabilityProjection),
            "BuildMemoryCapability");

    [Fact]
    public void EveryValidBankRetainsExactCapabilityAndZeroTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            var memory = ProcessorMemoryScope.CreateMemorySubsystem(
                MemoryBankId.BankCount,
                bankWidthBytes: 64);

            ProcessorMemoryScope.WithProcessorMemory(memory, () =>
            {
                for (int bank = 0; bank < MemoryBankId.BankCount; bank++)
                {
                    MemoryCapability capability = InvokeBuild(
                        new MemoryProbe((checked((ulong)bank * 64UL), 8UL)));

                    Assert.Equal(MemoryCapabilityKind.Load, capability.Kind);
                    Assert.Equal(new MemoryBankId(bank), capability.Bank);
                }
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
    public void UnavailableTopologyRetainsExactFaultAndSingleCounterMutation()
    {
        AssertNonResolvedOutcome(memory: null, address: 0x2000UL);
    }

    [Fact]
    public void InvalidIncompleteGeometryRetainsExactFaultAndSingleCounterMutation()
    {
        AssertNonResolvedOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(
                numBanks: 8,
                bankWidthBytes: 0),
            address: 0x1000UL);
    }

    [Fact]
    public void WidePositiveGeometryRetainsLegacySuccessAndConstructorWinner()
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
    public void FootprintFaultStillWinsBeforeResolutionAndTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            InvalidOperationException exception =
                ProcessorMemoryScope.WithProcessorMemory<InvalidOperationException>(
                    memory: null,
                    () => Assert.Throws<InvalidOperationException>(() =>
                        InvokeBuild(new MemoryProbe(
                            (0x1000UL, 16UL),
                            (0x1008UL, 16UL)))));

            Assert.Equal(
                "RF-06.5 family footprint contains overlapping normalized ranges.",
                exception.Message);
            Assert.Equal(0UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void SourceOrderAndClosedWorldFactoryManifestMatchSelectedCutover()
    {
        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");

        int rawCall = projection.IndexOf(
            "int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address)",
            StringComparison.Ordinal);
        int rawPredicate = projection.IndexOf(
            "!MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)",
            rawCall, StringComparison.Ordinal);
        int unavailable = projection.IndexOf(
            "MemoryBankResolution.UnavailableTopology",
            rawPredicate, StringComparison.Ordinal);
        int invalid = projection.IndexOf(
            "MemoryBankResolution.InvalidGeometry",
            unavailable, StringComparison.Ordinal);
        int resolved = projection.IndexOf(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            invalid, StringComparison.Ordinal);
        int typedGate = projection.IndexOf(
            "if (!bankResolution.IsResolved)",
            resolved, StringComparison.Ordinal);
        int capability = projection.IndexOf(
            "return MemoryCapability.Create(",
            typedGate, StringComparison.Ordinal);

        Assert.True(rawCall >= 0);
        Assert.True(rawPredicate > rawCall);
        Assert.True(unavailable > rawPredicate);
        Assert.True(invalid > unavailable);
        Assert.True(resolved > invalid);
        Assert.True(typedGate > resolved);
        Assert.True(capability > typedGate);
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);

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

        Assert.Equal(3, CaptureCallSites(root,
            Path.Combine(root, "HybridCPU_ISE"),
            @"\bMemoryBankRouting\.ResolveSchedulerVisibleBankId\s*\(").Length);
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


    private static void AssertNonResolvedOutcome(
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
            "RF-12.6f memory-capability projection probe";
    }
}
