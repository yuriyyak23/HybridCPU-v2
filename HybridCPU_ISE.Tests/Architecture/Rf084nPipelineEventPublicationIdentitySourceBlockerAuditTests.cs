namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4n freezes the current PipelineEventPublication producers and late
/// application owner without merging SystemCommit or TrapCommit.
/// </summary>
public sealed class Rf084nPipelineEventPublicationIdentitySourceBlockerAuditTests
{
    [Fact]
    public void MainlinePipelineEventProducerIsTrapEntryAfterTypedSystemSplit()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string executeLane = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Execute", "CPU_Core.Pipeline.Stages.ScalarExecuteLaneState.cs");

        Assert.Contains("if (microOp is Core.SysEventMicroOp", retire, StringComparison.Ordinal);
        Assert.Contains("if (microOp is Core.TrapMicroOp trapMicroOp)", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedSystemEvent(", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedPipelineEvent(", retire, StringComparison.Ordinal);
        Assert.Contains("pipelineEvent is Core.Pipeline.TrapEntryEvent", types, StringComparison.Ordinal);
        Assert.Contains("public Core.Pipeline.PipelineEvent? GeneratedEvent;", executeLane, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCompatibilityHasSystemAndSmtVtCaptureProducersOnly()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] directProducers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "retireBatch.CaptureRetireWindowPipelineEvent(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/Dispatch/ExecutionDispatcherV4.CsrAndSmtVt.cs",
                "Execution/Dispatch/ExecutionDispatcherV4.System.cs"
            ],
            directProducers);

        string[] coreCallers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "dispatcher.CaptureRetireWindowPublications(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], coreCallers);
    }

    [Fact]
    public void EffectPayloadAndLateOwnerProvideNoExactAttemptIdentity()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        int factory = types.IndexOf("public static RetireWindowEffect PipelineEvent(", StringComparison.Ordinal);
        int nextFactory = types.IndexOf("public static RetireWindowEffect", factory + 1, StringComparison.Ordinal);
        string factoryBody = types[factory..nextFactory];

        Assert.DoesNotContain("ScheduledOperation", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", factoryBody, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.PipelineEvent:", retire, StringComparison.Ordinal);
        Assert.Contains("HandleRetiredSystemEventBoundary(", retire, StringComparison.Ordinal);
        Assert.Contains("| `TrapCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4am defines this family as page-fault delivery only", paper, StringComparison.Ordinal);
        Assert.Contains("| `PipelineEventPublication` | mainline `TrapMicroOp`/`TrapEntryEvent`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4an approves C-C for this closed producer group", paper, StringComparison.Ordinal);
        Assert.Contains("| separate architecture revision |", paper, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
