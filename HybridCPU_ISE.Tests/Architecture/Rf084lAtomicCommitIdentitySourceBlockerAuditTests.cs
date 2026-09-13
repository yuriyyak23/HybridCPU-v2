namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4l freezes both AtomicCommit producer contours while preserving
/// retire-owned reservation, memory mutation, outcome and register publication.
/// </summary>
public sealed class Rf084lAtomicCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void AtomicRetirePayloadHasRuntimeFactsButNoIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string effect = Slice(
            model,
            "public readonly struct AtomicRetireEffect",
            "public readonly struct AtomicRetireOutcome");

        foreach (string member in new[]
                 {
                     "Opcode", "AccessSize", "Address", "SourceValue",
                     "DestinationRegister", "CoreId", "VirtualThreadId",
                     "AcquireOrdering", "ReleaseOrdering"
                 })
        {
            Assert.Contains(member, effect, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ScheduledOperation", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", effect, StringComparison.Ordinal);
    }

    [Fact]
    public void AtomicCommitHasMainlineMicroOpAndDirectDispatcherProducersOnly()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] resolverFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(".ResolveRetireEffect(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/Dispatch/ExecutionDispatcherV4.Scalar.cs",
                "Pipeline/MicroOps/Types/MicroOp.Misc.cs"
            ],
            resolverFiles);

        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.Scalar.cs");

        Assert.Contains("pipeEX.GeneratedAtomicEffect = MaterializeLaneAtomicEffect(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedAtomicEffect = executeLane.GeneratedAtomicEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedAtomicEffect = memoryLane.GeneratedAtomicEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedAtomicEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowAtomicEffect(effect);", direct, StringComparison.Ordinal);
    }

    [Fact]
    public void ReservationMemoryOutcomeAndDerivedRegisterWriteRemainRetireOwned()
    {
        string root = FindRepositoryRoot();
        string atomicMemory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("AtomicReservationRegistry.RegisterReservation(", atomicMemory, StringComparison.Ordinal);
        Assert.Contains("AtomicReservationRegistry.ConsumeReservation(", atomicMemory, StringComparison.Ordinal);
        Assert.Contains("internal AtomicRetireOutcome ApplyResolvedRetireEffect", atomicMemory, StringComparison.Ordinal);
        Assert.Contains("Core.AtomicRetireOutcome retiredAtomicOutcome =", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)", retire, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", retire, StringComparison.Ordinal);
        Assert.Contains("| `DeferredStoreCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4aj approves C-C for this closed mainline contour", paper, StringComparison.Ordinal);
        Assert.Contains("| `ScalarMemoryStoreCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ak approves C-C for this retained compatibility contour", paper, StringComparison.Ordinal);
        Assert.Contains("| `AtomicCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4al approves C-C for this closed two-contour family", paper, StringComparison.Ordinal);
        Assert.Contains("optional returned `RegisterWrite` remains separate", paper, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
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
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
