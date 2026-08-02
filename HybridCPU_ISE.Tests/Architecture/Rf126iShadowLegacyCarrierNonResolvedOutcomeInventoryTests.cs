using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6i decision-only inventory of the shadow legacy-carrier projection's
/// non-resolved/out-of-range bank outcomes and five false-result callers.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126iShadowLegacyCarrierNonResolvedOutcomeInventoryTests
{
    private const string ThisFile =
        "Rf126iShadowLegacyCarrierNonResolvedOutcomeInventoryTests.cs";

    [Fact]
    public void PaperDefinesExactBankTaxonomyWithoutGrantingAdmissionAuthority()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "`MemoryBankResolution` is a three-way result: `Resolved(MemoryBankId)`, `UnavailableTopology`, or `InvalidGeometry`",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "Geometry is resolvable only with positive bank width and a bank count in `1..16`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Bank zero is valid", paper,
            StringComparison.Ordinal);
        Assert.Contains("Resolution does not grant memory admission", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RangeKindAndLengthFalseWinnersRemainDistinctAndOrdered()
    {
        string source = ProjectionSource();
        string method = Slice(source,
            "public static bool TryProjectLegacyCarrier(",
            "public static Rf06MemoryShadowDecision EvaluateLegacyCarrier(");

        int nullGate = method.IndexOf(
            "ArgumentNullException.ThrowIfNull(carrier)",
            StringComparison.Ordinal);
        int none = method.IndexOf(
            "capability = MemoryCapability.None", nullGate,
            StringComparison.Ordinal);
        int rawRead = method.IndexOf(
            "int bank = carrier.MemoryBankId", none,
            StringComparison.Ordinal);
        int range = method.IndexOf(
            "if ((uint)bank >= MemoryBankId.BankCount)", rawRead,
            StringComparison.Ordinal);
        int unavailable = method.IndexOf(
            "MemoryBankResolution.UnavailableTopology", range,
            StringComparison.Ordinal);
        int invalid = method.IndexOf(
            "MemoryBankResolution.InvalidGeometry", unavailable,
            StringComparison.Ordinal);
        int unchangedFalse = method.IndexOf(
            "if (!nonResolvedBank.IsResolved)", invalid,
            StringComparison.Ordinal);
        int kind = method.IndexOf(
            "else\n        {\n            return false;", unchangedFalse,
            StringComparison.Ordinal);
        int length = method.IndexOf(
            "if (length == 0)", kind, StringComparison.Ordinal);
        int resolved = method.IndexOf(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            length, StringComparison.Ordinal);

        Assert.True(nullGate >= 0);
        Assert.True(none > nullGate);
        Assert.True(rawRead > none);
        Assert.True(range > rawRead);
        Assert.True(unavailable > range);
        Assert.True(invalid > unavailable);
        Assert.True(unchangedFalse > invalid);
        Assert.True(kind > unchangedFalse);
        Assert.True(length > kind);
        Assert.True(resolved > length);
    }

    [Fact]
    public void NonResolvedBankOutcomesKeepExactCounterAndFalseBehavior()
    {
        AssertFalseOutcome(memory: null, address: 0x2000UL,
            expectedTelemetry: 1UL);
        AssertFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(8, 0),
            address: 0x1000UL,
            expectedTelemetry: 1UL);
        AssertFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
            address: 17UL * 64UL,
            expectedTelemetry: 0UL);
    }

    [Fact]
    public void WideLowAliasAndZeroLengthRemainOutsideNonResolvedClassification()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
                () =>
                {
                    var validWideLow = new LoadMicroOp
                    {
                        Address = 0,
                        Size = 8
                    };
                    Assert.True(
                        Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                            validWideLow,
                            out MemoryCapability capability));
                    Assert.Equal(new MemoryBankId(0), capability.Bank);

                    var zeroLength = new LoadMicroOp
                    {
                        Address = 0,
                        Size = 0
                    };
                    Assert.False(
                        Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                            zeroLength,
                            out capability));
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
    public void ClosedWorldProductionManifestContainsExactlyFiveCallSites()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, int> calls = CaptureProductionCallCounts(root);

        Assert.Equal(5, calls.Values.Sum());
        Assert.Equal(2, calls.Count);
        Assert.Equal(3, calls[
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs"]);
        Assert.Equal(2, calls[
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/MicroOpScheduler.ShadowOracle.cs"]);
    }

    [Fact]
    public void FiveCallersKeepTheirDistinctFalseDispositions()
    {
        string root = FindRepositoryRoot();
        string differential = ProjectionSource();
        string shadow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "MicroOpScheduler.ShadowOracle.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "return Reject(Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable);",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "\"LegacyCarrierNotRepresentable\",\n                new Rf06MemoryShadowDecision(false, Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable),\n                EvaluateContract(admission, state)",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "if (Rf06MemoryShadowOracle.TryProjectLegacyCarrier(selected.LegacyCarrier, out MemoryCapability legacyCapability))\n                    legacyState = legacyState.Consume(legacyCapability, vt);",
            differential, StringComparison.Ordinal);
        Assert.Contains(
            "if (!Rf06MemoryShadowOracle.TryProjectLegacyCarrier(carrier, out MemoryCapability capability))\n                return;",
            shadow, StringComparison.Ordinal);
        Assert.Contains(
            "if (!Rf06MemoryShadowOracle.TryProjectLegacyCarrier(carrier, out _))\n            {\n                throw new InvalidOperationException(",
            shadow, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWireReplayReflectionAndTestSupportSeamsStayBounded()
    {
        string root = FindRepositoryRoot();
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            string text = ReadSourceTree(Path.Combine(root, externalRoot));
            Assert.DoesNotContain("TryProjectLegacyCarrier", text,
                StringComparison.Ordinal);
            Assert.DoesNotContain("MemoryBankResolution", text,
                StringComparison.Ordinal);
        }

        string production = ReadSourceTree(Path.Combine(root,
            "HybridCPU_ISE"));
        Assert.DoesNotMatch(
            new Regex(
                @"(?:Replay|Certificate|Serializer|Telemetry).{0,80}MemoryBankResolution|MemoryBankResolution.{0,80}(?:Replay|Certificate|Serializer|Telemetry)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
            production);

        string scope = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "ProcessorMemoryScope.cs");
        Assert.Contains("Processor.Memory = memory", scope,
            StringComparison.Ordinal);
        Assert.Contains("Processor.Memory = savedMemory", scope,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue(", scope, StringComparison.Ordinal);
        Assert.DoesNotContain("GetField(", scope, StringComparison.Ordinal);
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
                        load,
                        out MemoryCapability capability));
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

    private static Dictionary<string, int> CaptureProductionCallCounts(
        string repositoryRoot)
    {
        var regex = new Regex(
            @"\bTryProjectLegacyCarrier\s*\(",
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

            string relative = Path.GetRelativePath(repositoryRoot, path)
                .Replace('\\', '/');
            result.Add(relative, count);
        }

        return result;
    }

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
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return source[start..end];
    }

    private static string ReadSourceTree(string root) =>
        string.Join("\n", EnumerateSources(root)
            .Where(path => !path.EndsWith(ThisFile,
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
