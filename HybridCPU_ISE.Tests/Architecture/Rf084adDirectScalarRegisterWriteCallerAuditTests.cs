namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ad freezes direct scalar-dispatch and live-state RegisterWrite
/// caller/reachability boundaries without treating direct API arguments as
/// issued-attempt identity.
/// </summary>
public sealed class Rf084adDirectScalarRegisterWriteCallerAuditTests
{
    [Fact]
    public void ScalarDispatcherRetainsDistinctEagerAndBoundedWriteSurfaces()
    {
        string root = FindRepositoryRoot();
        string scalar = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.Scalar.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.cs");

        Assert.Contains("private static ExecutionResult ExecuteScalarAlu(", scalar, StringComparison.Ordinal);
        Assert.Contains("state.WriteRegister(vtId, instr.Rd, result)", scalar, StringComparison.Ordinal);
        Assert.Contains(
            "private static void CaptureScalarAluRetireWindowPublications(",
            scalar,
            StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", scalar, StringComparison.Ordinal);
        Assert.Contains(
            "InstructionClass.ScalarAlu => ExecuteScalarAlu(instr, state, vtId)",
            dispatcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "CaptureScalarAluRetireWindowPublications(instr, state, ref retireBatch, vtId)",
            dispatcher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedDispatcherHasOnlyTestSupportCoreCallerAndNoCoreConstruction()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        Assert.Equal(
            ["Pipeline/Core/CPU_Core.TestSupport.cs"],
            FindCallerFiles(coreRoot, "dispatcher.CaptureRetireWindowPublications("));
        Assert.Empty(FindCallerFiles(coreRoot, "new ExecutionDispatcherV4("));
    }

    [Fact]
    public void LiveAdapterProductionCallersDoNotInvokeRegisterWrite()
    {
        string root = FindRepositoryRoot();
        string liveState = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "State", "LiveCpuStateAdapter.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string systemApply = Slice(
            retire,
            "private void ApplySystemEventKindToVirtualThread(",
            "private void ApplyPipelineEventToVirtualThread(");
        string pipelineApply = Slice(
            retire,
            "private void ApplyPipelineEventToVirtualThread(",
            "private PrivilegeLevel ResolveRetiredSystemEventPrivilege(");

        Assert.Contains("public void WriteRegister(byte vtId, int regId, ulong value)", liveState, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(normalizedVtId", liveState, StringComparison.Ordinal);
        Assert.Contains("CreateLiveCpuStateAdapter(normalizedVtId)", systemApply, StringComparison.Ordinal);
        Assert.Contains("CreateLiveCpuStateAdapter(normalizedVtId)", pipelineApply, StringComparison.Ordinal);
        Assert.DoesNotContain(".WriteRegister(", systemApply, StringComparison.Ordinal);
        Assert.DoesNotContain(".WriteRegister(", pipelineApply, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCommittedWriteIsDirectSetupSurfaceWithNoCoreCaller()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");

        Assert.Contains("public void WriteCommittedArch(int vtId, int archReg, ulong value)", state, StringComparison.Ordinal);
        Assert.Contains(
            "RetireCoordinator.Retire(RetireRecord.RegisterWrite(normalizedVtId, archReg, value))",
            state,
            StringComparison.Ordinal);
        Assert.Equal(
            ["Architecture/State/Architectural/CPU_Core.StateData.cs"],
            FindCallerFiles(coreRoot, "WriteCommittedArch("));
    }

    [Fact]
    public void ArchitecturalRegisterHelperConstructorIsBehindFailClosedCsrSurface()
    {
        string root = FindRepositoryRoot();
        string registers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Architectural", "CPU_Core.Registers.cs");
        string vectorConfig = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural", "CPU_Core.VectorConfig.cs");
        string csrRead = Slice(
            vectorConfig,
            "public void ExecuteCSRRead(",
            "private ulong ReadPackedPriorities()");

        Assert.Contains("private void WriteActiveArchValue(", registers, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", registers, StringComparison.Ordinal);
        Assert.Contains("if (ShouldRejectRetainedDirectHelperSurface())", csrRead, StringComparison.Ordinal);
        Assert.Contains("throw CreateUnsupportedDirectCsrHelperSurfaceException(", csrRead, StringComparison.Ordinal);
        Assert.Contains("WriteVectorScalarRegisterValue(destReg, nameof(destReg), value)", csrRead, StringComparison.Ordinal);
        Assert.Contains("private static bool ShouldRejectRetainedDirectHelperSurface() => true;", vectorConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperNowApprovesDirectScalarAndLiveStateExclusion()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains(
            "approved direct scalar/live/setup `RegisterWrite` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("retained `LegacyCpuStateAdapter.WriteIntRegister`/`WriteRegister`", paper, StringComparison.Ordinal);
    }

    private static string[] FindCallerFiles(string coreRoot, string marker) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
