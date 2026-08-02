namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-08 documentation reconciliation: one current status and a closed RF-09 gate.</summary>
public sealed class Rf08Rf09DocumentationCurrentStateTests
{
    [Fact]
    public void CurrentIndexDistinguishesCompletedContoursHistoricalAuditsAndOpenFamilies()
    {
        string root = FindRepositoryRoot();
        string status = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/00_CURRENT_STATUS_AND_READING_ORDER.md");

        Assert.Contains("| RF-08 | closed |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.3d | complete production contour |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.3a / 3b | complete historical topology audits |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.3n | complete blocker audit |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.3o | complete architecture decision A |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.4a | complete behavior fix |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.4b | complete executable audit |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-08.4bb | complete; RF-08 exit accepted |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
        Assert.DoesNotContain("full typed-effect-union prevalidation remains open", status, StringComparison.Ordinal);
    }

    [Fact]
    public void SchedulerDecisionNamesOwnerCallersApprovedDecisionAAndUnauthorisedDecisionB()
    {
        string root = FindRepositoryRoot();
        string blocker = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/01_SCHEDULER_CHOICE_BLOCKER.md");

        Assert.Contains("## Owner, callers and affected surfaces", blocker, StringComparison.Ordinal);
        Assert.Contains("### A. Preserve the current RF-08 scheduler boundary", blocker, StringComparison.Ordinal);
        Assert.Contains("### B. Authorize a separate typed-scheduler topology program", blocker, StringComparison.Ordinal);
        Assert.Contains("## Full option-B blocker ledger", blocker, StringComparison.Ordinal);
        Assert.Contains("| 1 | Exact authorized contour |", blocker, StringComparison.Ordinal);
        Assert.Contains("| 26 | Required proof matrix |", blocker, StringComparison.Ordinal);
        Assert.Contains("| B6", blocker, StringComparison.Ordinal);
        Assert.Contains("Owner: architecture owner for the typed scheduler boundary.", blocker, StringComparison.Ordinal);
        Assert.Contains("Decision A is the\napproved RF-08 disposition", blocker, StringComparison.Ordinal);
        Assert.Contains("revised only by a separate architecture revision", blocker, StringComparison.Ordinal);
        Assert.Contains("candidate-policy specification, differential evidence and\nB1--B6 proof", blocker, StringComparison.Ordinal);
        Assert.Contains("does not itself grant decision B", blocker, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentDocumentationMatchesTheLiveLoadBlockerAndSelectedPrefixPrevalidation()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/CPU_Core.PipelineExecution.Fsp.cs");
        string stageFlow = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string migration = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/04_CoreMigration/04_RF07_RF13_Core_Migration.md");

        Assert.Contains("ScalarClusterIssueEntry[] entries", fsp, StringComparison.Ordinal);
        Assert.Contains("if (candidate is not Core.ScalarALUMicroOp)", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("Core.LoadMicroOp", fsp, StringComparison.Ordinal);

        Assert.Contains("## Local-code verification basis", Read(root,
            "Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/01_SCHEDULER_CHOICE_BLOCKER.md"), StringComparison.Ordinal);

        int capture = stageFlow.IndexOf(
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane)",
            StringComparison.Ordinal);
        int prevalidate = stageFlow.IndexOf(
            "PrevalidateRetireWindowBatchForPublication(",
            StringComparison.Ordinal);
        int finalize = stageFlow.IndexOf(
            "FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane)",
            StringComparison.Ordinal);
        int publish = stageFlow.IndexOf(
            "ApplyRetireBatchImmediateEffects(",
            StringComparison.Ordinal);
        Assert.True(capture >= 0 && capture < prevalidate && prevalidate < finalize && finalize < publish);

        Assert.Contains("### RF-08.3n scalar-load typed Stage-B topology blocker audit", migration, StringComparison.Ordinal);
        Assert.Contains("## RF-09 — Semantic replay identity and immutable entries (closed; RF-09.0/09.1/09.2/09.3/09.4 complete)", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void OverviewAndGovernancePointToTheCanonicalGateWithoutStaleCurrentClaims()
    {
        string root = FindRepositoryRoot();
        string overview = Read(root, "Documentation/ArchitectureAuthorityRefactor/00_Overview/00_README.md");
        string governance = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md");
        string evidenceReadme = Read(root, "Documentation/ArchitectureAuthorityRefactor/Evidence/README.md");

        foreach (string document in new[] { overview, governance, evidenceReadme })
        {
            Assert.Contains("08_RF08_RF09/00_CURRENT_STATUS_AND_READING_ORDER.md", document, StringComparison.Ordinal);
            Assert.DoesNotContain("smallest scalar RF-08.3 linkage contour is next", document, StringComparison.Ordinal);
            Assert.DoesNotContain("RF-09 may begin after RF-08 operation/retire identity freezes", document, StringComparison.Ordinal);
        }

        Assert.Contains("RF-09 | closed; RF-09.0/09.1/09.2/09.3/09.4 complete", overview, StringComparison.Ordinal);
        Assert.Contains("RF-08.3n", governance, StringComparison.Ordinal);
        Assert.Contains("RF-09 is closed", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("RF-09.0 entry inventory", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("RF-09.1 immutable", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("RF-09.2 context/code-epoch invalidation", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("RF-09.3", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("non-serving semantic", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("RF-09.4", evidenceReadme, StringComparison.Ordinal);
        Assert.Contains("bounded immutable serving", evidenceReadme, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectureOwnerDecisionAPreservesOnlyTheNamedScalarLoadContours()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root,
            "ResearchPaper/section/md base/5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string blocker = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/01_SCHEDULER_CHOICE_BLOCKER.md");

        Assert.Contains("#### RF-08.3o architecture-owner decision: preserve scalar-load scheduler topology", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08 mandatory exact-issued-attempt identity coverage", paper, StringComparison.Ordinal);
        Assert.Contains("owner-foreground, auxiliary-memory, exact-slot", paper, StringComparison.Ordinal);
        Assert.Contains("direct/compatibility, replay and inter-core scalar-load contours", paper, StringComparison.Ordinal);
        Assert.Contains("typed-FSP `ScalarALUMicroOp` effects", paper, StringComparison.Ordinal);
        Assert.Contains("candidate-policy specification, differential evidence and staged B1--B6", paper, StringComparison.Ordinal);
        Assert.Contains("approved residual exclusion is admissible at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("It forbids downstream\nidentity reconstruction in EX, MEM, WB or retirement", paper, StringComparison.Ordinal);
        Assert.Contains("Review trigger:", paper, StringComparison.Ordinal);
        Assert.Contains("### A. Preserve the current RF-08 scheduler boundary — approved", blocker, StringComparison.Ordinal);
        Assert.Contains("Option B remains **not authorized", blocker, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found from the test output directory.");
    }
}
