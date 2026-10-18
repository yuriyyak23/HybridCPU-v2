namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2aa freezes ownership of the non-token vector memory families.
/// This is an inventory guard, not a new policy or outcome implementation.
/// </summary>
public sealed class Rf072aaVectorNonTokenMemoryFamilyInventoryTests
{
    [Fact]
    public void VectorNonTokenMemoryFamilies_RemainSynchronousOrRetireStagedRatherThanAsyncTokenOwners()
    {
        string source = ReadVectorMemorySource();
        string load2D = ClassBody(source, "Load2DMicroOp", "GatherMicroOp");
        string store2D = ClassBody(source, "Store2DMicroOp", "StoreScatterMicroOp");
        string gather = ClassBody(source, "GatherMicroOp", "StoreSegmentMicroOp");
        string scatter = ClassBody(source, "StoreScatterMicroOp", endClassName: null);

        Assert.Contains("BurstIO.BurstRead2D", load2D, StringComparison.Ordinal);
        Assert.Equal(1, Count(load2D, "_requestToken")); // retained unused declaration only
        Assert.DoesNotContain("EnqueueRead", load2D, StringComparison.Ordinal);
        Assert.DoesNotContain("return false", load2D, StringComparison.Ordinal);

        Assert.Contains("BurstIO.BurstWrite2D", store2D, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestToken", store2D, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueWrite", store2D, StringComparison.Ordinal);
        Assert.DoesNotContain("return false", store2D, StringComparison.Ordinal);

        Assert.DoesNotContain("MemoryRequestToken", gather, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueRead", gather, StringComparison.Ordinal);
        Assert.Contains("EmitWriteBackRetireRecords", gather, StringComparison.Ordinal);

        Assert.DoesNotContain("MemoryRequestToken", scatter, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueWrite", scatter, StringComparison.Ordinal);
        Assert.Contains("EmitWriteBackRetireRecords", scatter, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorNonTokenMemoryFamilies_DoNotCreateParallelOutcomeOrRetirementAuthority()
    {
        string source = ReadVectorMemorySource();
        foreach (string className in new[]
                 {
                     "Load2DMicroOp", "Store2DMicroOp", "GatherMicroOp", "StoreScatterMicroOp"
                 })
        {
            string body = ClassBody(source, className, endClassName: null);
            Assert.DoesNotContain("ExecutionOutcome", body, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionRecord", body, StringComparison.Ordinal);
            Assert.DoesNotContain("RetireCoordinator", body, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicalRegisterFile", body, StringComparison.Ordinal);
        }
    }

    private static string ReadVectorMemorySource()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Vector",
            "VectorMicroOps.Memory.cs"));
    }

    private static string ClassBody(string source, string className, string? endClassName)
    {
        string startMarker = $"class {className}";
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = endClassName is null
            ? source.Length
            : source.IndexOf($"class {endClassName}", start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endClassName ?? "EOF");
        return source[start..end];
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
