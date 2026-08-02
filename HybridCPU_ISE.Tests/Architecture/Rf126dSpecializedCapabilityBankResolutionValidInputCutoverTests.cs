using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6d valid-input-only MemoryBankResolution selection at the RF-06
/// specialized capability projection. The raw resolver and every existing
/// unavailable/invalid winner remain in place.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126dSpecializedCapabilityBankResolutionValidInputCutoverTests
{
    private static readonly MethodInfo BuildMemoryCapability =
        typeof(Rf06SpecializedCapabilityProjection).GetMethod(
            "BuildMemoryCapability",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(
            nameof(Rf06SpecializedCapabilityProjection),
            "BuildMemoryCapability");

    [Fact]
    public void EveryResolvedBankHasExactCapabilityParityWithoutTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            var memory =
                ProcessorMemoryScope.CreateMemorySubsystem(
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
                    Assert.Equal(bank, capability.Bank!.Value.Value);
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
    public void BankZeroRemainsPresentAndDoesNotAliasNoMemory()
    {
        var memory = ProcessorMemoryScope.CreateMemorySubsystem(
            MemoryBankId.BankCount,
            bankWidthBytes: 64);
        MemoryCapability resolvedZero =
            ProcessorMemoryScope.WithProcessorMemory(memory,
                () => InvokeBuild(new MemoryProbe((0UL, 8UL))));
        MemoryCapability none =
            ProcessorMemoryScope.WithProcessorMemory(memory,
                () => InvokeBuild(new MemoryProbe()));

        Assert.Equal(new MemoryBankId(0), resolvedZero.Bank);
        Assert.Null(none.Bank);
        Assert.Equal(MemoryCapabilityKind.NoMemory, none.Kind);
    }

    [Fact]
    public void UnavailableTopologyKeepsExactExceptionAndSingleTelemetryMutation()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            InvalidOperationException exception =
                ProcessorMemoryScope.WithProcessorMemory<InvalidOperationException>(
                    memory: null,
                    () => Assert.Throws<InvalidOperationException>(() =>
                        InvokeBuild(new MemoryProbe((0x2000UL, 8UL)))));

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

    [Fact]
    public void IncompleteGeometryKeepsExactExceptionAndSingleTelemetryMutation()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            var memory =
                ProcessorMemoryScope.CreateMemorySubsystem(
                    numBanks: 8,
                    bankWidthBytes: 0);
            InvalidOperationException exception =
                ProcessorMemoryScope.WithProcessorMemory(memory,
                    () => Assert.Throws<InvalidOperationException>(() =>
                        InvokeBuild(new MemoryProbe((0x1000UL, 8UL)))));

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

    [Fact]
    public void WidePositiveGeometryKeepsCheckedConstructorWinnerWithoutTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            var memory =
                ProcessorMemoryScope.CreateMemorySubsystem(
                    numBanks: 32,
                    bankWidthBytes: 64);
            ArgumentOutOfRangeException exception =
                ProcessorMemoryScope.WithProcessorMemory(memory,
                    () => Assert.Throws<ArgumentOutOfRangeException>(() =>
                        InvokeBuild(new MemoryProbe((17UL * 64UL, 8UL)))));

            Assert.Equal("value", exception.ParamName);
            Assert.Equal(17, exception.ActualValue);
            Assert.Equal(0UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void FootprintFaultStillWinsBeforeResolverAndTelemetry()
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
    public void SourceKeepsRawResolverAndHasOneAuthorizedResultCaller()
    {
        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");

        int rawCall = projection.IndexOf(
            "int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address)",
            StringComparison.Ordinal);
        int rawCheck = projection.IndexOf(
            "!MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)",
            StringComparison.Ordinal);
        int typedSelection = projection.IndexOf(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            StringComparison.Ordinal);
        int capabilityCreate = projection.IndexOf(
            "return MemoryCapability.Create(", typedSelection,
            StringComparison.Ordinal);

        Assert.True(rawCall >= 0);
        Assert.True(rawCheck > rawCall);
        Assert.True(typedSelection > rawCheck);
        Assert.True(capabilityCreate > typedSelection);
        Assert.Contains("bankResolution.Bank!.Value", projection,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);

        const string resultCallPattern =
            @"\bMemoryBankResolution\.Resolved\s*\(";
        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:: MemoryBankResolution.Resolved(new MemoryBankId(bank));",
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:MemoryBankResolution.Resolved(",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs:MemoryBankResolution.Resolved(new MemoryBankId(bank));"
            ],
            CaptureCallSites(root, "HybridCPU_ISE", resultCallPattern,
                excludedFileName:
                "MemoryBankResolution.cs"));
        Assert.Empty(CaptureCallSites(root, "HybridCPU_Compiler",
            resultCallPattern));
        Assert.Empty(CaptureCallSites(root, "CpuInterfaceBridge",
            resultCallPattern));
        Assert.Empty(CaptureCallSites(root, "TestAssemblerConsoleApps",
            resultCallPattern));
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
        string relativeRoot,
        string pattern,
        string? excludedFileName = null)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        string sourceRoot = Path.Combine(repositoryRoot, relativeRoot);
        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs",
                     SearchOption.AllDirectories)
                     .Where(path => !IsBuildOutput(path))
                     .Where(path => excludedFileName is null ||
                                    !path.EndsWith(excludedFileName,
                                        StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.Ordinal))
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
            "RF-12.6d memory-capability projection probe";
    }
}
