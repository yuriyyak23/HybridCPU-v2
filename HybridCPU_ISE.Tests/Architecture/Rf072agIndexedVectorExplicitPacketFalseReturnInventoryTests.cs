namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2ag proves that the RF-07.2af explicit generic eligibility does not
/// expose an executable false return for the published indexed vector carriers.
/// </summary>
public sealed class Rf072agIndexedVectorExplicitPacketFalseReturnInventoryTests
{
    [Theory]
    [InlineData("public GatherMicroOp()", "public override void EmitWriteBackRetireRecords", "GatherMicroOp.Execute()")]
    [InlineData("public StoreScatterMicroOp()", "public override void EmitWriteBackRetireRecords", "StoreScatterMicroOp.Execute()")]
    public void IndexedVectorExecute_HasNoIncompleteFalseReturn(string classStart, string classEnd, string executeName)
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs"));
        string carrier = Between(source, classStart, classEnd);
        string execute = Between(
            carrier,
            "public override bool Execute(ref Processor.CPU_Core core)",
            "return _state == ExecutionState.Complete;\n        }");

        Assert.DoesNotContain("return false", execute, StringComparison.Ordinal);
        Assert.Contains("_state = ExecutionState.Complete", execute, StringComparison.Ordinal);
        Assert.Contains("return true", execute, StringComparison.Ordinal);
        Assert.Contains($"ThrowIfUnsupportedRuntimeContour();", execute, StringComparison.Ordinal);
        Assert.Contains(executeName, execute, StringComparison.Ordinal);
    }

    [Fact]
    public void PredicateMaskFalse_IsNotMicroOpExecuteOutcome_AndStagingPrecedesCompletion()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs"));
        string gather = Between(source, "public class GatherMicroOp", "public class StoreScatterMicroOp");
        string scatter = Between(source, "public class StoreScatterMicroOp", "private readonly struct Indexed2SrcDescriptor");

        Assert.Contains("private bool IsLaneActiveForGather", gather, StringComparison.Ordinal);
        Assert.Contains("private bool IsLaneActiveForScatter", scatter, StringComparison.Ordinal);
        Assert.Contains("return false;", gather, StringComparison.Ordinal);
        Assert.Contains("return false;", scatter, StringComparison.Ordinal);
        AssertOrdered(gather, "_stagedDestinationBuffer", "_state = ExecutionState.Complete");
        AssertOrdered(scatter, "_stagedWrites[_stagedWriteCount++]", "_state = ExecutionState.Complete");
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", gather, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", scatter, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
