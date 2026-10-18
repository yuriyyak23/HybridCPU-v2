namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// Executable RF-07.2y ledger for the only vector MicroOp async-token contours.
/// This is a source/caller audit; it introduces no runtime policy.
/// </summary>
public sealed class Rf072yVectorCompletedTokenDeliveryCoverageTests
{
    [Fact]
    public void VectorAsyncTokenBranches_AreExhaustiveAndUseTypedPageFaultBeforeLegacyGuards()
    {
        string root = FindRepositoryRoot();
        string vectorMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");

        Assert.Equal(0, Count(vectorMemory, "memSub.EnqueueRead("));
        Assert.Equal(1, Count(vectorMemory, "memSub.EnqueueWrite("));
        Assert.Equal(1, Count(vectorMemory, "_requestToken.IsComplete"));

        Assert.Contains("TryAcceptVectorSegmentLoad", vectorMemory, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion", vectorMemory, StringComparison.Ordinal);
        Assert.Contains(
            "LoadSegmentMicroOp observed failed completed controller read.",
            vectorMemory,
            StringComparison.Ordinal);
        AssertOrdered(
            vectorMemory,
            "StoreSegmentMicroOp observed failed completed write token.",
            "_requestToken.ThrowIfFailed(\"StoreSegmentMicroOp.Execute()\")");
        Assert.Contains("isWrite: false", vectorMemory, StringComparison.Ordinal);
        Assert.Contains("isWrite: true", vectorMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorCompletedTokenPageFaults_ReachSharedTypedCatchBeforeVectorFatalTail()
    {
        string root = FindRepositoryRoot();
        string executeHelpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");

        int pageFaultCatch = executeHelpers.IndexOf(
            "catch (Core.PageFaultException pageFaultException)",
            StringComparison.Ordinal);
        int vectorFatalTail = executeHelpers.IndexOf(
            "catch (Exception ex) when (pipeID.IsVectorOp)",
            StringComparison.Ordinal);

        Assert.True(pageFaultCatch >= 0);
        Assert.True(vectorFatalTail > pageFaultCatch);
        Assert.Contains("ProjectSingleLanePageFaultOutcome(pageFaultException)", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("DeliverSingleLanePageFaultOutcome(outcome, pageFaultException)", executeHelpers, StringComparison.Ordinal);
    }

    private static void AssertOrdered(string source, string first, string second)
    {
        int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, first);
        Assert.True(secondIndex > firstIndex, second);
    }

    private static int Count(string source, string value)
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

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
