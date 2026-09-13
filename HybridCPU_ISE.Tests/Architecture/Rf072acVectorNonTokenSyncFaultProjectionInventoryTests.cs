namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2ac inventories typed projection boundaries for synchronous vector
/// memory families. It deliberately does not turn a non-production 2D helper
/// or a WB/retire exception into an execute-stage outcome.
/// </summary>
public sealed class Rf072acVectorNonTokenSyncFaultProjectionInventoryTests
{
    [Fact]
    public void ProductionRegistry_MaterializesOnlyIndexedGatherAndScatterFromThisFamilySet()
    {
        string root = FindRepositoryRoot();
        string registry = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs");

        Assert.Contains("GatherMicroOp gatherMicroOp = new GatherMicroOp", registry, StringComparison.Ordinal);
        Assert.Contains("StoreScatterMicroOp scatterMicroOp = new StoreScatterMicroOp", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("new Load2DMicroOp", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("new Store2DMicroOp", registry, StringComparison.Ordinal);

        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        string[] twoDConstructionFiles = Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(Path.Combine("MicroOps", "Vector", "VectorMicroOps.Memory.cs"), StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("new Load2DMicroOp", StringComparison.Ordinal) ||
                           File.ReadAllText(path).Contains("new Store2DMicroOp", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(twoDConstructionFiles);
    }

    [Fact]
    public void GatherAndScatter_KeepExecuteFaultsSeparateFromWriteBackPublicationFaults()
    {
        string root = FindRepositoryRoot();
        string vectorMemory = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string gather = ClassBody(vectorMemory, "GatherMicroOp", "StoreSegmentMicroOp");
        string scatter = ClassBody(vectorMemory, "StoreScatterMicroOp", null);

        foreach ((string body, string publicationMarker) in new[]
                 {
                     (gather, "private void PublishStagedGatherResultAtWriteBack"),
                     (scatter, "private void PublishStagedScatterWritesAtWriteBack")
                 })
        {
            string execute = MemberBody(body, "public override bool Execute");
            string writeBack = MemberBody(body, "public override void EmitWriteBackRetireRecords");
            string publication = MemberBody(body, publicationMarker);
            Assert.Contains("ReadBoundMainMemoryExact", execute, StringComparison.Ordinal);
            Assert.DoesNotContain("catch (", execute, StringComparison.Ordinal);
            Assert.Contains("PublishStaged", writeBack, StringComparison.Ordinal);
            Assert.Contains("ThrowIfBoundMainMemoryRangeUnavailable", publication, StringComparison.Ordinal);
            Assert.Contains("WriteBoundMainMemoryExact", publication, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionOutcome", writeBack, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExistingExecuteCatchPartitions_ProjectKnownFaultsAndFailCloseAllOtherSynchronousExceptions()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string faults = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Faults/CPU_Core.PipelineExecution.Exceptions.cs");

        AssertOrdered(helpers, "ProjectSingleLanePageFaultOutcome(pageFaultException)", "ProjectSingleLaneNonFaultExceptionOutcome(ex)");
        AssertOrdered(helpers, "ProjectSingleLaneAlignmentFaultOutcome(", "ProjectSingleLaneNonFaultExceptionOutcome(ex)");
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", MemberBody(helpers, "private static Core.Execution.ExecutionOutcome ProjectSingleLaneNonFaultExceptionOutcome"), StringComparison.Ordinal);

        Assert.Contains("ProjectExplicitPacketPageFaultOutcome(pageFaultException)", faults, StringComparison.Ordinal);
        Assert.Contains("ProjectExplicitPacketAlignmentFaultOutcome(", faults, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.ArchitecturalFault", faults, StringComparison.Ordinal);
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

    private static string MemberBody(string source, string marker)
    {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        string[] memberMarkers = { "\n        public ", "\n        private ", "\n            public ", "\n            private " };
        int end = memberMarkers
            .Select(value => source.IndexOf(value, start + marker.Length, StringComparison.Ordinal))
            .Where(index => index > start)
            .DefaultIfEmpty(source.Length)
            .Min();
        return source[start..end];
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
