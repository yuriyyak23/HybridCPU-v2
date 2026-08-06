using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1137BoundedBatchMicroOpEmissionRefThisHardeningTests
{
    [Fact]
    public void TwoBoundedBatchEmitCallersUseOneStableIdentityAdapter()
    {
        string retire = Retire();
        Assert.Equal(3, Regex.Matches(retire,
            @"EmitBatchMicroOpRetireRecordsWithStableCoreIdentity\(").Count);
        Assert.Single(Regex.Matches(retire,
            @"retireBatch\.EmitMicroOpRetireRecords\(ref stableCoreIdentity, lane\)"));
        Assert.Empty(Regex.Matches(retire,
            @"retireBatch\.EmitMicroOpRetireRecords\(\s*ref this,"));
    }

    [Fact]
    public void BatchCaptureAndSelectedPrefixPublicationRemainFrozen()
    {
        string root = Root();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        Order(types, "lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);",
            "lane.MicroOp.EmitWriteBackRetireRecords(", "_retireRecords,",
            "ref _retireRecordCount);");
        Assert.Equal(0, Regex.Matches(types, @"\bcore\s*=(?!=)").Count);

        string retire = Retire();
        Order(retire, "CaptureRetiredWriteBackLaneEffects(",
            "PrevalidateRetireWindowBatchForPublication(",
            "RetireCoordinator.Retire(retireBatch.RetireRecords);");
    }

    [Fact]
    public void ResidualIsOneAtomicTestSupportCall()
    {
        string root = Root();
        string production = Sources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(Retire(), @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(Retire(),
            @"lane\.MicroOp\.EmitWriteBackRetireRecords\(\s*ref this,"));
        Assert.Empty(Regex.Matches(Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs"), @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
    }

    [Fact]
    public void EvidenceClosesOnlyBoundedBatchEmission()
    {
        string root = Root();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.37-bounded-batch-microop-emission-ref-this-hardening.md");
        Assert.Contains("RF-11.37 bounded-batch MicroOp emission ref-this hardening", ledger);
        Assert.Contains("no production state declaration", evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence);
        Assert.Contains("RF-11.38", ledger);
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
