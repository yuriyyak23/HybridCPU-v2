using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6g decision-only closed-world inventory of the raw computed
/// LoadStoreMicroOp.MemoryBankId carrier and its scheduler/scoreboard fan-out.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126gLoadStoreComputedBankCarrierInventoryTests
{
    private const string ThisFile =
        "Rf126gLoadStoreComputedBankCarrierInventoryTests.cs";

    [Fact]
    public void PaperKeepsResolvedBankIdentitySeparateFromAdmissionAuthority()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "Existing `MemoryBankId` denotes a resolved scheduler-visible bank `0..15`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Bank zero is valid", paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Unavailable or invalid geometry has no bank value and may not become bank zero",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "Resolution does not grant memory admission", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComputedCarrierReevaluatesRawResolverAndTelemetryOnEveryRead()
    {
        string root = FindRepositoryRoot();
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");

        Assert.Contains(
            "public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress);",
            carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", carrier,
            StringComparison.Ordinal);

        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(memory: null, () =>
            {
                var load = new LoadMicroOp { Address = 0x2000UL };
                Assert.Equal(
                    MemoryBankRouting.UninitializedSchedulerVisibleBankId,
                    load.MemoryBankId);
                Assert.Equal(
                    MemoryBankRouting.UninitializedSchedulerVisibleBankId,
                    load.MemoryBankId);
            });

            Assert.Equal(2UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void DirectProductionReaderManifestStaysAtTwentyThreeSites()
    {
        string root = FindRepositoryRoot();
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Decoder/DecodedBundleDescriptor.cs"] = 2,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Admission/MicroOpScheduler.Admission.cs"] = 5,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/BundlePacking/MicroOpScheduler.PackBundle.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/MicroOpScheduler.FSPPipeline.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/MicroOpScheduler.ShadowOracle.cs"] = 9,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.SMT.cs"] = 2
        };

        Dictionary<string, int> actual = CaptureDirectReaderCounts(root);
        Assert.Equal(23, actual.Values.Sum());
        Assert.Equal(expected.Count, actual.Count);
        foreach ((string path, int count) in expected)
        {
            Assert.True(actual.TryGetValue(path, out int actualCount),
                $"Missing direct reader file: {path}");
            Assert.Equal(count, actualCount);
        }
    }

    [Fact]
    public void DecodeIntentAndLegalityFanOutRemainRawAndReevaluating()
    {
        string root = FindRepositoryRoot();
        string descriptor = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "DecodedBundleDescriptor.cs");
        string legality = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Legality", "BundleLegalityAnalyzer.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");

        Assert.Contains("int memoryBankIntent", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("public int MemoryBankIntent { get; }", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("? loadStoreMicroOp.MemoryBankId", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("return loadStoreMicroOp.MemoryBankId", descriptor,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (left.MemoryBankIntent < 0 || right.MemoryBankIntent < 0)",
            legality, StringComparison.Ordinal);
        Assert.Contains(
            "return left.MemoryBankIntent == right.MemoryBankIntent",
            legality, StringComparison.Ordinal);
        Assert.Contains(
            "currentSlotDescriptor.GetRuntimeExecutionMemoryBankIntent() >= 0",
            stageFlow, StringComparison.Ordinal);
        Assert.Contains("_fspScheduler.IsBankPendingForVT(bankId, vtId)",
            stageFlow, StringComparison.Ordinal);
        Assert.Contains(
            "slots[j].GetRuntimeExecutionMemoryBankIntent() == bankIntent",
            stageFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void AdmissionIndexesMasksAndInvalidGuardsRemainRaw()
    {
        string root = FindRepositoryRoot();
        string admission = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Admission",
            "MicroOpScheduler.Admission.cs");
        string shadow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "MicroOpScheduler.ShadowOracle.cs");

        Assert.Contains("candidate is LoadStoreMicroOp ls ? ls.MemoryBankId : -1",
            admission, StringComparison.Ordinal);
        Assert.Contains("memoryBankId >= 0 && IsBankPendingForVT",
            admission, StringComparison.Ordinal);
        Assert.Contains("if ((uint)memoryBankId >= 16)", admission,
            StringComparison.Ordinal);
        Assert.Contains("_bundleLocalOutstandingStoreBankMask & (1 << bankId)",
            admission, StringComparison.Ordinal);
        Assert.Contains("int shift = bankId * 2", admission,
            StringComparison.Ordinal);
        Assert.Contains("localOutstandingStoreBankMask |= (ushort)(1 << bundleStore.MemoryBankId)",
            shadow, StringComparison.Ordinal);
        Assert.Contains("(uint)bundleStore.MemoryBankId < 16", shadow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ScoreboardStorageTargetConflationAndReleaseStayExplicit()
    {
        string root = FindRepositoryRoot();
        string scheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "MicroOpScheduler.cs");
        string scoreboard = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Scoreboard",
            "MicroOpScheduler.Scoreboard.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");

        Assert.Contains(
            "private readonly int[,] _smtScoreboardBankId = new int[SMT_WAYS, SCOREBOARD_SLOTS]",
            scheduler, StringComparison.Ordinal);
        Assert.Contains(
            "SetSmtScoreboardPendingTyped(int targetId, int virtualThreadId, long currentCycle",
            scoreboard, StringComparison.Ordinal);
        Assert.Contains("_smtScoreboard[virtualThreadId, i] = targetId",
            scoreboard, StringComparison.Ordinal);
        Assert.Contains("_smtScoreboardBankId[virtualThreadId, i] = bankId",
            scoreboard, StringComparison.Ordinal);
        Assert.DoesNotContain("(uint)bankId >= MemoryBankId.BankCount",
            scoreboard, StringComparison.Ordinal);
        Assert.Contains(
            "SetSmtScoreboardPendingTyped(\n                                bankId, vtId",
            materialization.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains(
            "ClearSmtScoreboardEntry(lane.MshrVirtualThreadId, lane.MshrScoreboardSlot)",
            materialization, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWireReflectionAndTestSupportSeamsStayBounded()
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
            Assert.DoesNotContain("LoadStoreMicroOp", text,
                StringComparison.Ordinal);
            Assert.DoesNotContain("MemoryBankIntent", text,
                StringComparison.Ordinal);
            Assert.DoesNotContain("ResolveSchedulerVisibleBankId", text,
                StringComparison.Ordinal);
        }

        string helper = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "MicroOpTestHelper.cs");
        string schedulerTestSupport = Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "Scheduling",
            "MicroOpScheduler.TestSupport.cs");
        string coreTestSupport = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains(
            "((memoryBankId % runtimeNumBanks) + runtimeNumBanks) % runtimeNumBanks",
            helper, StringComparison.Ordinal);
        Assert.Contains(
            "internal void TestRecordBankPendingReject(int memoryBankId)",
            schedulerTestSupport, StringComparison.Ordinal);
        Assert.Contains(
            "int MemoryBankIntent",
            coreTestSupport, StringComparison.Ordinal);

        string nonGuardTests = string.Join("\n",
            EnumerateSources(Path.Combine(root, "HybridCPU_ISE.Tests"))
                .Where(path => !path.EndsWith(ThisFile,
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        Assert.DoesNotContain(
            "GetField(\"_smtScoreboardBankId",
            nonGuardTests, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SetValue(\"_smtScoreboardBankId",
            nonGuardTests, StringComparison.Ordinal);
    }


    private static Dictionary<string, int> CaptureDirectReaderCounts(
        string repositoryRoot)
    {
        var regex = new Regex(
            @"\b(?:loadStoreMicroOp|loadStoreCandidate|storeCandidate|ls|lsCandidate|lsOp|bundleMemoryOp|bundleStore|memoryCandidate|carrier)\.MemoryBankId\b",
            RegexOptions.CultureInvariant);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string path in EnumerateSources(
                     Path.Combine(repositoryRoot, "HybridCPU_ISE")))
        {
            string relative = Path.GetRelativePath(repositoryRoot, path)
                .Replace('\\', '/');
            if (relative.EndsWith(
                    "Decoder/Rf06SpecializedCapabilityProjection.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            int count = File.ReadLines(path).Count(line => regex.IsMatch(line));
            if (count > 0)
            {
                counts.Add(relative, count);
            }
        }

        return counts;
    }

    private static string ReadSourceTree(string root) =>
        string.Join("\n", EnumerateSources(root).Select(File.ReadAllText));

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
