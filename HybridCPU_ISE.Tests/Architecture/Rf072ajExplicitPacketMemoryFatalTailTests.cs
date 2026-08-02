using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072ajExplicitPacketMemoryFatalTailTests
{
    [Fact]
    public void ExplicitPacketMemoryUnknownException_ProjectsNoEffectFatalInvariantViolation()
    {
        ExecutionOutcome outcome =
            Processor.CPU_Core.ProjectExplicitPacketMemoryNonFaultExceptionOutcome(
                new InvalidOperationException("RF-07.2aj synthetic memory-stage failure"));

        Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
        Assert.NotNull(outcome.Diagnostic);
        Assert.Null(outcome.Result);
        Assert.False(outcome.HasArchitecturalEffects);
    }

    [Fact]
    public void ExplicitPacketMemoryFatalTail_HasPageFaultEscapeFlushAndNoNotReadyFallback()
    {
        string root = FindRepositoryRoot();
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string memory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");
        string stageBody = MemberBody(stage, "private void PipelineStage_Memory");
        string projector = MemberBody(memory,
            "internal static Core.Execution.ExecutionOutcome\n                ProjectExplicitPacketMemoryNonFaultExceptionOutcome");

        Assert.Contains("catch (Core.PageFaultException)", stageBody, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", stageBody, StringComparison.Ordinal);
        Assert.Contains("ProjectExplicitPacketMemoryNonFaultExceptionOutcome(exception)", stageBody, StringComparison.Ordinal);
        Assert.Contains("FlushPipeline(Core.AssistInvalidationReason.Trap)", stageBody, StringComparison.Ordinal);
        Assert.Contains("CreateWrappedException(", stageBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultReady = false", stageBody, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("Retryable", projector, StringComparison.Ordinal);
    }

    private static string MemberBody(string source, string marker)
    {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start, marker);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"Unterminated member: {marker}");
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
