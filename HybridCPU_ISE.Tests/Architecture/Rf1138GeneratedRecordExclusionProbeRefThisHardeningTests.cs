using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1138GeneratedRecordExclusionProbeRefThisHardeningTests
{
    [Fact]
    public void ExclusionProbeUsesOneStableIdentityAdapter()
    {
        string retire = Retire();
        Assert.Equal(2, Regex.Matches(retire,
            @"EmitExclusionProbeRetireRecordsWithStableCoreIdentity\(").Count);
        Assert.Single(Regex.Matches(retire,
            @"microOp\.EmitWriteBackRetireRecords\(\s*ref stableCoreIdentity,"));
        Assert.DoesNotContain("lane.MicroOp.EmitWriteBackRetireRecords(\n                    ref this", retire);
    }

    [Fact]
    public void FailClosedProbeOrderAndMeaningRemainFrozen()
    {
        string retire = Retire();
        Order(retire, "Span<RetireRecord> probeRetireRecords = stackalloc RetireRecord[4];",
            "lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);",
            "EmitExclusionProbeRetireRecordsWithStableCoreIdentity(",
            "if (probeRetireRecordCount != 0)",
            "cannot mix MicroOp-owned retire emission with generated retire records");
        Assert.Contains("cannot mix generated retire records with typed generated effects", retire);
    }

    [Fact]
    public void ProductionRetireHasNoRefThisAndOnlyAtomicTestSupportRemains()
    {
        string root = Root();
        string production = Sources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(Retire(), @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs"), @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
    }

    [Fact]
    public void EvidenceClosesOnlyGeneratedRecordExclusionProbe()
    {
        string root = Root();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.38-generated-record-exclusion-probe-ref-this-hardening.md");
        Assert.Contains("RF-11.38 generated-record exclusion-probe ref-this hardening", ledger);
        Assert.Contains("changes no production state declaration", evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence);
        Assert.Contains("RF-11.39", ledger);
    }

    private static string Retire() => Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
        "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
    private static void Order(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, marker);
            prior = current;
        }
    }
    private static string Sources(string path) => string.Join('\n',
        Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\"))
            .Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string Root()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
