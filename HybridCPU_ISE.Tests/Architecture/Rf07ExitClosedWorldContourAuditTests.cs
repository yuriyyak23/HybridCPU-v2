namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitClosedWorldContourAuditTests
{
    [Fact]
    public void ConcreteExecuteBodies_HaveClosedFalseAndCatchOwnerSets()
    {
        string root = FindRepositoryRoot();
        ExecuteBody[] bodies = ReadExecuteBodies(root).ToArray();

        string[] literalFalseOwners = bodies
            .Where(body => body.Text.Contains("return false", StringComparison.Ordinal))
            .Select(body => body.Owner)
            .OrderBy(owner => owner, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "LoadMicroOp", "LoadSegmentMicroOp", "StoreMicroOp", "StoreSegmentMicroOp", "VectorTransferMicroOp" },
            literalFalseOwners);

        ExecuteBody[] catchBodies = bodies
            .Where(body => body.Text.Contains("catch (", StringComparison.Ordinal))
            .OrderBy(body => body.Owner, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "LoadMicroOp", "StoreMicroOp" }, catchBodies.Select(body => body.Owner));
        foreach (ExecuteBody body in catchBodies)
        {
            Assert.Equal(1, CountOccurrences(body.Text, "catch (PageFaultException)"));
            Assert.Equal(1, CountOccurrences(body.Text, "catch (Exception ex)"));
            Assert.Equal(3, CountOccurrences(body.Text, "if (this.IsSpeculative)"));
            Assert.Contains("throw new PageFaultException(", body.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProductionExecuteCallersAndRuntimeNotReadyOwners_AreClosedWorld()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string explicitStage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string materialization = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        string memory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");

        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp)"));
        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp)"));
        Assert.Equal(1, CountOccurrences(helpers, "ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!)"));
        Assert.Equal(1, CountOccurrences(helpers, "pipeEX.ResultReady = false"));
        Assert.Contains("Preserve the legacy not-ready carrier and timing.", helpers, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(explicitStage, "lane.ResultReady = false"));
        Assert.Contains("TryPrepareExplicitPacketExecuteMemoryCarrierLane", explicitStage, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(materialization, "lane.ResultReady = false"));
        Assert.Contains("claims readiness from MEM completion", materialization, StringComparison.Ordinal);
        Assert.Equal(6, CountOccurrences(memory, "lane.ResultReady = false"));
        Assert.Contains("TryAcceptExplicitPacketScalarLoad", memory, StringComparison.Ordinal);
        Assert.Contains("TryAcceptExplicitPacketScalarStore", memory, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion", memory, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitMemoryTerminalFailures_AreEitherArchitecturalFaultOrTypedFatalAndNeverNotReady()
    {
        string root = FindRepositoryRoot();
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string memory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");
        string memoryStage = MemberBody(stage, "private void PipelineStage_Memory");
        string work = MemberBody(memory, "private void ExecuteExplicitPacketMemoryWork");
        string fatalProjector = MemberBody(memory,
            "internal static Core.Execution.ExecutionOutcome\n                ProjectExplicitPacketMemoryNonFaultExceptionOutcome");

        Assert.Contains("ProjectExplicitPacketCompletedMemoryRequestFailureOutcome", work, StringComparison.Ordinal);
        Assert.Contains("DeliverExplicitPacketCompletedMemoryRequestFailureOutcome", work, StringComparison.Ordinal);
        Assert.Contains("catch (Core.PageFaultException)", memoryStage, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", memoryStage, StringComparison.Ordinal);
        Assert.Contains("ProjectExplicitPacketMemoryNonFaultExceptionOutcome(exception)", memoryStage, StringComparison.Ordinal);
        Assert.Contains("FlushPipeline(Core.AssistInvalidationReason.Trap)", memoryStage, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultReady = false", memoryStage, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", fatalProjector, StringComparison.Ordinal);
        Assert.DoesNotContain("Retryable", fatalProjector, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", memoryStage, StringComparison.Ordinal);
    }

    private static IEnumerable<ExecuteBody> ReadExecuteBodies(string root)
    {
        string directory = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps");
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => ExtractExecuteBodies(File.ReadAllText(path)));
    }

    private static IEnumerable<ExecuteBody> ExtractExecuteBodies(string source)
    {
        const string marker = "public override bool Execute(ref Processor.CPU_Core core)";
        int searchStart = 0;
        while (true)
        {
            int methodStart = source.IndexOf(marker, searchStart, StringComparison.Ordinal);
            if (methodStart < 0)
                yield break;
            int ownerStart = source.LastIndexOf("class ", methodStart, StringComparison.Ordinal);
            Assert.True(ownerStart >= 0, "Concrete Execute requires a class owner.");
            int nameStart = ownerStart + "class ".Length;
            int nameEnd = source.IndexOfAny(new[] { ' ', ':', '\r', '\n' }, nameStart);
            Assert.True(nameEnd > nameStart, "Concrete Execute owner name was not readable.");
            string owner = source[nameStart..nameEnd];
            int bodyStart = methodStart + marker.Length;
            while (char.IsWhiteSpace(source[bodyStart]))
                bodyStart++;
            int bodyEnd;
            if (source.AsSpan(bodyStart).StartsWith("=>", StringComparison.Ordinal))
            {
                bodyEnd = source.IndexOf(';', bodyStart);
                Assert.True(bodyEnd > bodyStart, "Expression Execute must terminate.");
            }
            else
            {
                Assert.Equal('{', source[bodyStart]);
                int depth = 0;
                bodyEnd = bodyStart;
                for (; bodyEnd < source.Length; bodyEnd++)
                {
                    if (source[bodyEnd] == '{') depth++;
                    else if (source[bodyEnd] == '}' && --depth == 0) break;
                }
                Assert.True(bodyEnd < source.Length, "Block Execute must terminate.");
            }

            yield return new ExecuteBody(owner, source[methodStart..(bodyEnd + 1)]);
            searchStart = bodyEnd + 1;
        }
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
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return source[start..(index + 1)];
        }
        throw new InvalidOperationException($"Unterminated member: {marker}");
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ExecuteBody(string Owner, string Text);
}
