namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4k freezes the direct ScalarMemoryStoreCommit compatibility contour
/// and proves that it is not the mainline deferred-store family.
/// </summary>
public sealed class Rf084kScalarMemoryStoreCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void ScalarMemoryStoreEffectCarriesPayloadButNoIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string factory = Slice(
            types,
            "public static RetireWindowEffect ScalarMemoryStore(",
            "public static RetireWindowEffect PredicateState(");

        Assert.Contains("RetireWindowEffectKind.ScalarMemoryStore", factory, StringComparison.Ordinal);
        Assert.Contains("memoryAddress", factory, StringComparison.Ordinal);
        Assert.Contains("memoryData", factory, StringComparison.Ordinal);
        Assert.Contains("memoryAccessSize", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", factory, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectDispatcherIsTheOnlyCoreProducerAndMainlineUsesDeferredStore()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] captureFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("CaptureRetireWindowScalarMemoryStore(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/Dispatch/ExecutionDispatcherV4.MemoryAndControl.cs",
                "Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.Types.cs"
            ],
            captureFiles);

        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.MemoryAndControl.cs");
        string memory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory", "CPU_Core.PipelineExecution.Memory.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("if (resolved.HasStoreCommit)", dispatcher, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowScalarMemoryStore(", dispatcher, StringComparison.Ordinal);
        Assert.Contains("lane.DefersStoreCommitToWriteBack = true;", memory, StringComparison.Ordinal);
        Assert.Contains("retireBatch.AppendDeferredStoreLane(laneIndex);", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureRetireWindowScalarMemoryStore(", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedDispatcherCaptureHasOnlyTestSupportCoreCallerAndSelectedRetireOwner()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] dispatcherCallerFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("dispatcher.CaptureRetireWindowPublications(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], dispatcherCallerFiles);

        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("internal void CaptureRetireWindowPublications(", dispatcher, StringComparison.Ordinal);
        Assert.Contains("TEST-ONLY: drive ExecutionDispatcherV4 direct retire publication", testSupport, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.ScalarMemoryStore:", retire, StringComparison.Ordinal);
        Assert.Contains("\"Retire batch direct scalar store\"", retire, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.Atomic:", retire, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.DeferredStoreCommit:", retire, StringComparison.Ordinal);

        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        Assert.Contains("RF-08.4ak approves C-C for this retained compatibility contour", paper, StringComparison.Ordinal);
        Assert.Contains("This exclusion does not classify the retained API as production mainline", paper, StringComparison.Ordinal);
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
