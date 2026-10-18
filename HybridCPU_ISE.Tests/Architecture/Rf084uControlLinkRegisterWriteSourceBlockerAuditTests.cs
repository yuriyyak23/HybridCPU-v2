namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4u freezes the JAL/JALR link-register producer contours separately
/// from the already approved PcWrite effect.
/// </summary>
public sealed class Rf084uControlLinkRegisterWriteSourceBlockerAuditTests
{
    [Fact]
    public void BranchMicroOpPublishesLinkAndPcAsSeparateRetireRecords()
    {
        string root = FindRepositoryRoot();
        string branch = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");

        Assert.Contains("public sealed class BranchMicroOp : MicroOp", branch, StringComparison.Ordinal);
        Assert.Contains("IsStealable = false", branch, StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.BranchControl, 7)", branch, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.JAL or Processor.CPU_Core.IsaOpcodeValues.JALR", branch, StringComparison.Ordinal);
        Assert.Contains("WritesRegister = publishesLinkRegister", branch, StringComparison.Ordinal);
        Assert.Contains("ResolveLinkRegisterValue(executionPc)", branch, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _capturedPrimaryWriteBackResult)", branch, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)", branch, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineHasSingleLaneAndExplicitPacketLane7SurfacesButNoRf08CarrierSource()
    {
        string root = FindRepositoryRoot();
        string controlFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "ControlFlow", "CPU_Core.PipelineExecution.ControlFlow.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("private bool TryExecuteScalarBranchMicroOp()", controlFlow, StringComparison.Ordinal);
        Assert.Contains("private bool TryExecuteExplicitPacketLane7Branch(", controlFlow, StringComparison.Ordinal);
        Assert.Contains("MaterializeBranchExecuteCarrier(ref branchLane, branchPayload)", controlFlow, StringComparison.Ordinal);
        Assert.Contains("candidate.PostStageBIdentityTemplate = null", fsp, StringComparison.Ordinal);
        Assert.Contains("if (candidate is not Core.ScalarALUMicroOp)", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("BranchMicroOp", ExtractIdentityTemplateMethod(fsp), StringComparison.Ordinal);
    }

    [Fact]
    public void DirectDispatcherHasDistinctEagerAndRetireWindowLinkWriteSurfaces()
    {
        string root = FindRepositoryRoot();
        string dispatch = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.MemoryAndControl.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.cs");
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        Assert.Contains("private static ExecutionResult ExecuteControlFlow(", dispatch, StringComparison.Ordinal);
        Assert.Contains("state.WriteRegister(vtId, instr.Rd, result.Value)", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static void CaptureControlFlowRetireWindowPublications(", dispatch, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", dispatch, StringComparison.Ordinal);
        Assert.Contains("InstructionClass.ControlFlow => ExecuteControlFlow", dispatcher, StringComparison.Ordinal);
        Assert.Contains("CaptureControlFlowRetireWindowPublications", dispatcher, StringComparison.Ordinal);

        string[] directCaptureCallers = FindCallerFiles(
            coreRoot,
            "dispatcher.CaptureRetireWindowPublications(");
        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], directCaptureCallers);
    }

    [Fact]
    public void PaperApprovesLinkWriteSeparatelyFromPcWrite()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains(
            "| scalar-register StreamEngine compatibility `RegisterWrite` |",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "outside typed-FSP RF-08.3d/RF-08.4av",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "existing `PcWrite` producer contours",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "approved control-link `RegisterWrite`",
            paper,
            StringComparison.Ordinal);
    }

    private static string ExtractIdentityTemplateMethod(string fsp)
    {
        int start = fsp.IndexOf("private void AttachRf08PostStageBIdentityTemplate(", StringComparison.Ordinal);
        int end = fsp.IndexOf(
            "private byte ResolveForegroundRunnableVirtualThreadMask()",
            start,
            StringComparison.Ordinal);
        return fsp[start..end];
    }

    private static string[] FindCallerFiles(string coreRoot, string marker) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

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
