namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2ae freezes the retained adapter for the RF-07.2ad indexed-vector
/// synchronous fallback. It is a removal ledger, not a new public fault route.
/// </summary>
public sealed class Rf072aeIndexedVectorFallbackAdapterInventoryTests
{
    [Fact]
    public void SingleLaneVectorGenericExceptionAdapter_HasOneProductionOwnerAndTwoConcreteCallers()
    {
        string root = FindRepositoryRoot();
        string executeHelpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string[] callers = Directory.GetFiles(Path.Combine(root, "HybridCPU_ISE"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("ProjectSingleLaneNonFaultExceptionOutcome(", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(callers);
        Assert.EndsWith("CPU_Core.PipelineExecution.ExecuteHelpers.cs", callers[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, Count(executeHelpers, "ProjectSingleLaneNonFaultExceptionOutcome("));
        Assert.Contains("catch (Exception ex) when (pipeID.IsVectorOp)", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", executeHelpers, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorCaller_ProjectsThenCleansUpBeforeRetainedLegacyExceptionAdapter()
    {
        string helpers = Read(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string vectorCatch = Between(
            helpers,
            "catch (Exception ex) when (pipeID.IsVectorOp)",
            "catch (Exception ex)\n");

        AssertOrdered(vectorCatch, "ProjectSingleLaneNonFaultExceptionOutcome(ex)", "FailCloseSingleLaneExecuteAfterNonFaultException()");
        AssertOrdered(vectorCatch, "FailCloseSingleLaneExecuteAfterNonFaultException()", "if (outcome.Diagnostic!.LegacyFaultCategory");
        Assert.Contains("Core.ExecutionFaultContract.CreateWrappedException(", vectorCatch, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(", vectorCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", vectorCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.BackendUnavailable", vectorCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", vectorCatch, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionOwner_RequiresFatalInvariantViolationAndDoesNotCreatePublicFaultAuthority()
    {
        string helpers = Read(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string projection = Between(
            helpers,
            "private static Core.Execution.ExecutionOutcome ProjectSingleLaneNonFaultExceptionOutcome",
            "// RF-07.2a migrates");

        Assert.Contains("Rf07LegacyOutcomeProjection.ProjectException(exception)", projection, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.ArchitecturalFault", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireCoordinator", projection, StringComparison.Ordinal);
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endMarker);
        return source[start..end];
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
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
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
