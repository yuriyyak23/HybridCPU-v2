namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2ab freezes the production caller/catch ownership for vector-memory
/// families that do not own an asynchronous memory token. This is an inventory
/// guard: it does not add an outcome route to a synchronous MicroOp.
/// </summary>
public sealed class Rf072abVectorNonTokenMemoryCallerCatchInventoryTests
{
    [Fact]
    public void NonTokenVectorMemoryMicroOps_DoNotAdvertiseFalseAsAnExecutionDisposition()
    {
        string vectorMemory = Read("HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");

        foreach ((string name, string? next) in new[]
                 {
                     ("Load2DMicroOp", "GatherMicroOp"),
                     ("Store2DMicroOp", "StoreScatterMicroOp"),
                     ("GatherMicroOp", "StoreSegmentMicroOp"),
                     ("StoreScatterMicroOp", null)
                 })
        {
            string body = ClassBody(vectorMemory, name, next);
            string execute = ExecuteBody(body);
            Assert.Contains("public override bool Execute", body, StringComparison.Ordinal);
            Assert.DoesNotContain("return false", execute, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionOutcome", body, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionRecord", body, StringComparison.Ordinal);
        }

        Assert.Contains("BurstIO.BurstRead2D", ClassBody(vectorMemory, "Load2DMicroOp", "GatherMicroOp"), StringComparison.Ordinal);
        Assert.Contains("BurstIO.BurstWrite2D", ClassBody(vectorMemory, "Store2DMicroOp", "StoreScatterMicroOp"), StringComparison.Ordinal);
        Assert.Contains("EmitWriteBackRetireRecords", ClassBody(vectorMemory, "GatherMicroOp", "StoreSegmentMicroOp"), StringComparison.Ordinal);
        Assert.Contains("EmitWriteBackRetireRecords", ClassBody(vectorMemory, "StoreScatterMicroOp", null), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSingleLaneAndExplicitPacketCallers_KeepExistingFaultPartitions()
    {
        string executeHelpers = Read("HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stageFlow = Read("HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");

        Assert.Contains("bool success = ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp);", executeHelpers, StringComparison.Ordinal);
        AssertOrdered(executeHelpers,
            "catch (Core.PageFaultException pageFaultException)",
            "catch (Exception ex) when (pipeID.IsVectorOp)");
        Assert.Contains("ProjectSingleLanePageFaultOutcome(pageFaultException)", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneNonFaultExceptionOutcome(ex)", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", executeHelpers, StringComparison.Ordinal);

        Assert.Contains("bool success = ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!);", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = success;", executeHelpers, StringComparison.Ordinal);
        AssertOrdered(stageFlow,
            "catch (Core.PageFaultException pageFaultException)",
            "catch (Exception exception)");
        Assert.Contains("RethrowExplicitPacketExecutePageFault(pageFaultException)", stageFlow, StringComparison.Ordinal);
        Assert.Contains("FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane, exception)", stageFlow, StringComparison.Ordinal);
    }

    private static void AssertOrdered(string source, string first, string second)
    {
        int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, first);
        Assert.True(secondIndex > firstIndex, second);
    }

    private static string ClassBody(string source, string className, string? nextClassName)
    {
        string marker = $"class {className}";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        int end = nextClassName is null
            ? source.Length
            : source.IndexOf($"class {nextClassName}", start + marker.Length, StringComparison.Ordinal);
        Assert.True(end > start, nextClassName ?? "EOF");
        return source[start..end];
    }

    private static string ExecuteBody(string classBody)
    {
        const string marker = "public override bool Execute";
        int start = classBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        int nextMember = classBody.IndexOf("\n        public ", start + marker.Length, StringComparison.Ordinal);
        return nextMember < 0 ? classBody[start..] : classBody[start..nextMember];
    }

    private static string Read(string relativePath)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

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

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
