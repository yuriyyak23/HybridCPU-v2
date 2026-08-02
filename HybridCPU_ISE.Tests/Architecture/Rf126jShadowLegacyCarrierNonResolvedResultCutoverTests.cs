using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6j behavior-preserving non-resolved result selection at the existing
/// unsigned bank-range gate in the RF-06 shadow legacy-carrier projection.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126jShadowLegacyCarrierNonResolvedResultCutoverTests
{
    private const string ThisFile =
        "Rf126jShadowLegacyCarrierNonResolvedResultCutoverTests.cs";

    [Fact]
    public void AllResolvedLoadBanksRemainExactIncludingBankZero()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(
                    MemoryBankId.BankCount, 64),
                () =>
                {
                    for (int bank = 0; bank < MemoryBankId.BankCount; bank++)
                    {
                        ulong address = checked((ulong)bank * 64UL + 8UL);
                        var load = new LoadMicroOp
                        {
                            Address = address,
                            Size = 8
                        };

                        Assert.True(
                            Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                                load, out MemoryCapability capability));
                        Assert.Equal(MemoryCapabilityKind.Load,
                            capability.Kind);
                        Assert.Equal(new MemoryBankId(bank), capability.Bank);
                        FrozenMemoryRange range =
                            Assert.Single(capability.Footprint);
                        Assert.Equal(address, range.Address);
                        Assert.Equal(8UL, range.Length);
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
    public void NullTopologySelectsUnavailableAndKeepsFalseAndCounter()
    {
        AssertFalseOutcome(memory: null, address: 0x2000UL,
            expectedTelemetry: 1UL);

        string method = ProjectionMethod();
        Assert.Contains(
            "Processor.Memory is null\n                    ? MemoryBankResolution.UnavailableTopology",
            method, StringComparison.Ordinal);
    }

    [Fact]
    public void IncompleteTopologySelectsInvalidAndKeepsFalseAndCounter()
    {
        AssertFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(8, 0),
            address: 0x1000UL,
            expectedTelemetry: 1UL);

        string method = ProjectionMethod();
        Assert.Contains(
            ": MemoryBankResolution.InvalidGeometry;",
            method, StringComparison.Ordinal);
    }

    [Fact]
    public void WideGeometryAndZeroLengthKeepLegacySplit()
    {
        AssertFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
            address: 17UL * 64UL,
            expectedTelemetry: 0UL);

        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
                () =>
                {
                    var lowBank = new LoadMicroOp
                    {
                        Address = 0,
                        Size = 8
                    };
                    Assert.True(
                        Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                            lowBank, out MemoryCapability capability));
                    Assert.Equal(new MemoryBankId(0), capability.Bank);

                    var zeroLength = new LoadMicroOp
                    {
                        Address = 0,
                        Size = 0
                    };
                    Assert.False(
                        Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                            zeroLength, out capability));
                    Assert.Equal(MemoryCapability.None, capability);
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
    public void SourceOrderPreservesWinnerAndMutationBoundaries()
    {
        string method = ProjectionMethod();
        int none = Index(method, "capability = MemoryCapability.None");
        int rawRead = Index(method, "int bank = carrier.MemoryBankId", none);
        int range = Index(method,
            "if ((uint)bank >= MemoryBankId.BankCount)", rawRead);
        int topology = Index(method, "Processor.Memory is null", range);
        int unavailable = Index(method,
            "MemoryBankResolution.UnavailableTopology", topology);
        int invalid = Index(method,
            "MemoryBankResolution.InvalidGeometry", unavailable);
        int sameFalse = Index(method,
            "if (!nonResolvedBank.IsResolved)", invalid);
        int kind = Index(method,
            "else\n        {\n            return false;", sameFalse);
        int length = Index(method, "if (length == 0)", kind);
        int resolved = Index(method,
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))", length);
        int publish = Index(method,
            "capability = MemoryCapability.Create(", resolved);

        Assert.True(none >= 0);
        Assert.True(rawRead > none);
        Assert.True(range > rawRead);
        Assert.True(topology > range);
        Assert.True(unavailable > topology);
        Assert.True(invalid > unavailable);
        Assert.True(sameFalse > invalid);
        Assert.True(kind > sameFalse);
        Assert.True(length > kind);
        Assert.True(resolved > length);
        Assert.True(publish > resolved);
        Assert.Equal(1, Regex.Matches(method,
            @"\bProcessor\.Memory is null\b").Count);
        Assert.Contains("bankResolution.Bank!.Value", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FiveCallerTopologyAndFalseDispositionsRemainFrozen()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, int> calls = CaptureProductionCallCounts(root);
        Assert.Equal(5, calls.Values.Sum());
        Assert.Equal(3, calls[
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs"]);
        Assert.Equal(2, calls[
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/MicroOpScheduler.ShadowOracle.cs"]);

        string differential = ProjectionSource();
        string shadow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "Scheduling", "MicroOpScheduler.ShadowOracle.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(
            "return Reject(Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable);",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "new Rf06MemoryShadowDecision(false, Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable)",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "legacyState = legacyState.Consume(legacyCapability, vt);",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "if (!Rf06MemoryShadowOracle.TryProjectLegacyCarrier(carrier, out MemoryCapability capability))\n                return;",
            shadow, StringComparison.Ordinal);
        Assert.Contains(
            "if (!Rf06MemoryShadowOracle.TryProjectLegacyCarrier(carrier, out _))\n            {\n                throw new InvalidOperationException(",
            shadow, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultFactoryAndExternalSeamManifestIsExact()
    {
        string root = FindRepositoryRoot();
        string production = ReadSourceTree(Path.Combine(root,
            "HybridCPU_ISE"), excludedFileName: "MemoryBankResolution.cs");
        Assert.Equal(3, Regex.Matches(production,
            @"\bMemoryBankResolution\.Resolved\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.UnavailableTopology\b").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.InvalidGeometry\b").Count);

        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            string text = ReadSourceTree(Path.Combine(root, externalRoot));
            Assert.DoesNotMatch(
                @"\bMemoryBankResolution\.(?:Resolved|UnavailableTopology|InvalidGeometry)\b",
                text);
        }
    }


    private static void AssertFalseOutcome(
        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memory,
        ulong address,
        ulong expectedTelemetry)
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(memory, () =>
            {
                var load = new LoadMicroOp
                {
                    Address = address,
                    Size = 8
                };
                Assert.False(
                    Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                        load, out MemoryCapability capability));
                Assert.Equal(MemoryCapability.None, capability);
            });
            Assert.Equal(expectedTelemetry,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    private static int Index(string source, string value, int start = 0)
    {
        int index = source.IndexOf(value, start, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Missing source marker: {value}");
        return index;
    }

    private static string ProjectionMethod() =>
        Slice(ProjectionSource(),
            "public static bool TryProjectLegacyCarrier(",
            "public static Rf06MemoryShadowDecision EvaluateLegacyCarrier(");

    private static string ProjectionSource()
    {
        string root = FindRepositoryRoot();
        return Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Scheduling", "Rf06MemoryShadowOracleDifferential.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string Slice(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = Index(source, startMarker);
        int end = Index(source, endMarker, start);
        return source[start..end];
    }

    private static Dictionary<string, int> CaptureProductionCallCounts(
        string repositoryRoot)
    {
        var regex = new Regex(@"\bTryProjectLegacyCarrier\s*\(",
            RegexOptions.CultureInvariant);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string path in EnumerateSources(Path.Combine(repositoryRoot,
                     "HybridCPU_ISE")))
        {
            int count = File.ReadLines(path).Count(line =>
                regex.IsMatch(line) &&
                !line.Contains("public static bool TryProjectLegacyCarrier",
                    StringComparison.Ordinal));
            if (count == 0)
                continue;
            result.Add(Path.GetRelativePath(repositoryRoot, path)
                .Replace('\\', '/'), count);
        }
        return result;
    }

    private static string ReadSourceTree(
        string root,
        string? excludedFileName = null) =>
        string.Join("\n", EnumerateSources(root)
            .Where(path => !path.EndsWith(ThisFile,
                StringComparison.OrdinalIgnoreCase))
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
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
