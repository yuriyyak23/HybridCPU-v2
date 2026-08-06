namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4bb freezes the formal RF-08 exit against the current paper ledger,
/// production caller graph, retained-unreachable triggers and non-claims.
/// </summary>
public sealed class Rf084bbConsolidatedExitEvidenceTests
{
    private static readonly string[] ExpectedRegisterWriteConstructorFiles =
    [
        "Architecture/Registers/Architectural/CPU_Core.Registers.cs",
        "Architecture/State/Architectural/CPU_Core.StateData.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.CsrAndSmtVt.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.MemoryAndControl.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.Scalar.cs",
        "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs",
        "Pipeline/MicroOps/Control/MicroOp.Control.cs",
        "Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeQueryCapsMicroOp.cs",
        "Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeStatusMicroOp.cs",
        "Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs",
        "Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs",
        "Pipeline/MicroOps/Replay/ReplayToken.cs",
        "Pipeline/MicroOps/Types/MicroOp.Misc.cs",
        "Pipeline/MicroOps/Vector/MicroOp.Compute.cs",
        "Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs",
        "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs",
        "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs",
        "State/LiveCpuStateAdapter.cs"
    ];

    private static readonly string[] ApprovedResidualDecisionIds =
    [
        "RF-08.3o",
        "RF-08.4d",
        "RF-08.4f",
        "RF-08.4r",
        "RF-08.4x",
        "RF-08.4af",
        "RF-08.4ah",
        "RF-08.4ai",
        "RF-08.4aj",
        "RF-08.4ak",
        "RF-08.4al",
        "RF-08.4am",
        "RF-08.4an",
        "RF-08.4ao",
        "RF-08.4ap",
        "RF-08.4aq",
        "RF-08.4ar",
        "RF-08.4as",
        "RF-08.4at",
        "RF-08.4au",
        "RF-08.4av",
        "RF-08.4az",
        "RF-08.4ba"
    ];

    [Fact]
    public void SoleMigratedContourRemainsTypedFspScalarAluRegisterWrite()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] freezers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "RetireVisibleEffectIdentity.Freeze(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Pipeline/Scheduling/PostStageBIssuedAttempt.cs"], freezers);

        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("if (candidate is not Core.ScalarALUMicroOp)", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("Core.LoadMicroOp", Slice(
            fsp,
            "private void AttachRf08PostStageBIdentityTemplate(",
            "private byte ResolveForegroundRunnableVirtualThreadMask()"), StringComparison.Ordinal);
        Assert.Contains(
            "lane.PostStageBIssuedAttempt.CompleteScalarRegisterWrite(retireRecord)",
            retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndExitArtifactEnumerateEveryApprovedResidualDecision()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4bb-consolidated-exit-evidence.md");

        foreach (string decisionId in ApprovedResidualDecisionIds)
        {
            Assert.Contains(decisionId, paper, StringComparison.Ordinal);
            Assert.Contains(decisionId, evidence, StringComparison.Ordinal);
        }

        Assert.Contains("RF-08.4bb consolidated exit acceptance", paper, StringComparison.Ordinal);
        Assert.Contains("23 approved residual exclusion rows", evidence, StringComparison.Ordinal);
        Assert.Contains("No open production-reachable retire-visible family remains", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorInventoryAndSevenUnreachableSourcesRemainClosed()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] actual = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "RetireRecord.RegisterWrite(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedRegisterWriteConstructorFiles, actual);

        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4bb-consolidated-exit-evidence.md");
        foreach (string retainedSource in new[]
        {
            "CPU_Core.Registers.cs",
            "DmaStreamComputeQueryCapsMicroOp.cs",
            "DmaStreamComputeStatusMicroOp.cs",
            "CPU_Core.PipelineExecution.VmxRetire.cs",
            "CustomAcceleratorMicroOp",
            "MoveMicroOp",
            "IncrDecrMicroOp"
        })
        {
            Assert.Contains(retainedSource, evidence, StringComparison.Ordinal);
        }

        Assert.Contains("seven retained-unreachable constructor sources", evidence, StringComparison.Ordinal);
        Assert.Contains("production retire eligibility", evidence, StringComparison.Ordinal);
        Assert.Contains("success-capable VMX", evidence, StringComparison.Ordinal);
        Assert.Contains("registry initialization", evidence, StringComparison.Ordinal);
        Assert.Contains("fail-closed direct CSR helper", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedPrefixOrderAndBackendOwnersRemainProtected()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4bb-consolidated-exit-evidence.md");

        int capture = stageFlow.IndexOf(
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane)",
            StringComparison.Ordinal);
        int prevalidate = stageFlow.IndexOf(
            "PrevalidateRetireWindowBatchForPublication(",
            capture,
            StringComparison.Ordinal);
        int finalize = stageFlow.IndexOf(
            "FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane)",
            prevalidate,
            StringComparison.Ordinal);
        int apply = stageFlow.IndexOf(
            "ApplyRetireBatchImmediateEffects(",
            finalize,
            StringComparison.Ordinal);

        Assert.True(capture >= 0 && prevalidate > capture && finalize > prevalidate && apply > finalize);
        foreach (string owner in new[]
        {
            "PhysicalRegisterFile",
            "RenameMap",
            "CommitMap",
            "FreeList",
            "RetireCoordinator"
        })
        {
            Assert.Contains(owner, evidence, StringComparison.Ordinal);
        }

        Assert.Contains("commit-before-common-prevalidation", evidence, StringComparison.Ordinal);
        Assert.Contains("no-op bounded marker", evidence, StringComparison.Ordinal);
        Assert.Contains("register-before-memory", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitClosesRf08WithoutStartingRf09OrExpandingClaims()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string entryGate = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "02_RF09_ENTRY_GATE.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4bb-consolidated-exit-evidence.md");

        Assert.Contains("| RF-08 | closed |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
        Assert.Contains("RF-08 exit gate: accepted", entryGate, StringComparison.Ordinal);
        Assert.Contains("RF-09 execution status: closed", entryGate, StringComparison.Ordinal);

        foreach (string nonClaim in new[]
        {
            "universal scalar-load",
            "universal scalar-ALU",
            "universal rollback",
            "precise-exception theorem",
            "complete memory model"
        })
        {
            Assert.Contains(nonClaim, evidence, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "B1",
            Slice(status, "## First next phase task", "## Reading order"),
            StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return source[start..end];
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
