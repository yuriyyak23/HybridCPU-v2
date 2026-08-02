using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6h valid-input-only MemoryBankResolution selection at the
/// range-guarded RF-06 legacy-carrier shadow projection.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126hShadowLegacyCarrierBankResolutionValidInputCutoverTests
{
    private const string ThisFile =
        "Rf126hShadowLegacyCarrierBankResolutionValidInputCutoverTests.cs";

    [Fact]
    public void AllResolvedBanksPreserveLoadCapabilityIncludingBankZero()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(
                    numBanks: MemoryBankId.BankCount,
                    bankWidthBytes: 64),
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
                                load,
                                out MemoryCapability capability));
                        Assert.Equal(MemoryCapabilityKind.Load,
                            capability.Kind);
                        Assert.Equal(new MemoryBankId(bank), capability.Bank);
                        FrozenMemoryRange footprint =
                            Assert.Single(capability.Footprint);
                        Assert.Equal(address, footprint.Address);
                        Assert.Equal(8UL, footprint.Length);
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
    public void StoreAndAtomicValidInputsPreserveKindLengthAndBank()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                var store = new StoreMicroOp
                {
                    Address = 5UL * 64UL + 4UL,
                    Size = 4
                };
                Assert.True(Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                    store, out MemoryCapability storeCapability));
                Assert.Equal(MemoryCapabilityKind.Store,
                    storeCapability.Kind);
                Assert.Equal(new MemoryBankId(5), storeCapability.Bank);
                Assert.Equal(4UL,
                    Assert.Single(storeCapability.Footprint).Length);

                var atomic = new AtomicMicroOp
                {
                    Address = 7UL * 64UL + 4UL,
                    Size = 1
                };
                Assert.True(Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                    atomic, out MemoryCapability atomicCapability));
                Assert.Equal(MemoryCapabilityKind.Atomic,
                    atomicCapability.Kind);
                Assert.Equal(new MemoryBankId(7), atomicCapability.Bank);
                Assert.Equal(4UL,
                    Assert.Single(atomicCapability.Footprint).Length);
            });
    }

    [Fact]
    public void UnavailableIncompleteAndWideBanksKeepExactFalseOutcomes()
    {
        AssertRawFalseOutcome(memory: null, address: 0x2000UL,
            expectedTelemetry: 1UL);
        AssertRawFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(
                numBanks: 8,
                bankWidthBytes: 0),
            address: 0x1000UL,
            expectedTelemetry: 1UL);
        AssertRawFalseOutcome(
            ProcessorMemoryScope.CreateMemorySubsystem(
                numBanks: 32,
                bankWidthBytes: 64),
            address: 17UL * 64UL,
            expectedTelemetry: 0UL);
    }

    [Fact]
    public void NullAndZeroLengthWinnersRemainBeforeResultSelection()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                null!,
                out _));

        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                var load = new LoadMicroOp
                {
                    Address = 3UL * 64UL,
                    Size = 0
                };
                Assert.False(
                    Rf06MemoryShadowOracle.TryProjectLegacyCarrier(
                        load,
                        out MemoryCapability capability));
                Assert.Equal(MemoryCapability.None, capability);
            });
    }

    [Fact]
    public void SourceOrderKeepsEveryFalseGateBeforeResolvedSelection()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "Scheduling",
            "Rf06MemoryShadowOracleDifferential.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        int nullGate = source.IndexOf(
            "ArgumentNullException.ThrowIfNull(carrier)",
            StringComparison.Ordinal);
        int none = source.IndexOf(
            "capability = MemoryCapability.None", nullGate,
            StringComparison.Ordinal);
        int rawRead = source.IndexOf(
            "int bank = carrier.MemoryBankId", none,
            StringComparison.Ordinal);
        int rangeGate = source.IndexOf(
            "if ((uint)bank >= MemoryBankId.BankCount)", rawRead,
            StringComparison.Ordinal);
        int kindGate = source.IndexOf(
            "else\n        {\n            return false;",
            rangeGate, StringComparison.Ordinal);
        int lengthGate = source.IndexOf(
            "if (length == 0)", kindGate, StringComparison.Ordinal);
        int selection = source.IndexOf(
            "MemoryBankResolution.Resolved(new MemoryBankId(bank))",
            lengthGate, StringComparison.Ordinal);
        int capability = source.IndexOf(
            "capability = MemoryCapability.Create(", selection,
            StringComparison.Ordinal);

        Assert.True(nullGate >= 0);
        Assert.True(none > nullGate);
        Assert.True(rawRead > none);
        Assert.True(rangeGate > rawRead);
        Assert.True(kindGate > rangeGate);
        Assert.True(lengthGate > kindGate);
        Assert.True(selection > lengthGate);
        Assert.True(capability > selection);
        Assert.Contains("bankResolution.Bank!.Value", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldResultFactoryManifestContainsOnlyThreeResolvedSites()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:: MemoryBankResolution.Resolved(new MemoryBankId(bank));",
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:MemoryBankResolution.Resolved(",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs:MemoryBankResolution.Resolved(new MemoryBankId(bank));"
            ],
            CaptureCallSites(root, Path.Combine(root, "HybridCPU_ISE"),
                @"\bMemoryBankResolution\.Resolved\s*\(",
                excludedFileName: "MemoryBankResolution.cs"));

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
            Assert.Empty(CaptureCallSites(root,
                Path.Combine(root, externalRoot),
                @"\bMemoryBankResolution\.(?:Resolved|UnavailableTopology|InvalidGeometry)\b"));
        }
    }

    [Fact]
    public void RawCarrierAndShadowProjectionCallerTopologyRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        Assert.Contains(
            "public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress);",
            carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", carrier,
            StringComparison.Ordinal);

        string differential = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Scheduling",
            "Rf06MemoryShadowOracleDifferential.cs");
        string shadow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "MicroOpScheduler.ShadowOracle.cs");
        Assert.Equal(4, Regex.Matches(differential,
            @"\bTryProjectLegacyCarrier\s*\(").Count);
        Assert.Equal(2, Regex.Matches(shadow,
            @"\bRf06MemoryShadowOracle\.TryProjectLegacyCarrier\s*\(").Count);

        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);
    }


    private static void AssertRawFalseOutcome(
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

    private static string[] CaptureCallSites(
        string repositoryRoot,
        string sourceRoot,
        string pattern,
        string? excludedFileName = null)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        foreach (string path in EnumerateSources(sourceRoot)
                     .Where(path => excludedFileName is null ||
                                    !path.EndsWith(excludedFileName,
                                        StringComparison.OrdinalIgnoreCase))
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

    private static string ReadSourceTree(
        string root,
        string? excludedFileName = null) =>
        string.Join("\n", EnumerateSources(root)
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
