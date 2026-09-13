namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4aa freezes the complete lane-7 SystemDeviceCommand RegisterWrite
/// producer contour without changing accelerator or retirement behavior.
/// </summary>
public sealed class Rf084aaAcceleratorRegisterAbiSourceBlockerAuditTests
{
    private static readonly string[] CommandKinds =
    [
        "QueryCaps",
        "Submit",
        "Poll",
        "Status",
        "Wait",
        "Cancel",
        "Fence"
    ];

    [Fact]
    public void OneHardPinnedLane7CarrierOwnsAllSevenRegisterAbiCommands()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Lane7Accelerator", "SystemDeviceCommandMicroOp.cs");

        Assert.Contains("public enum SystemDeviceCommandKind : byte", microOp, StringComparison.Ordinal);
        Assert.Contains("IsStealable = false", microOp, StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", microOp, StringComparison.Ordinal);
        Assert.Contains("WritesRegister = DestinationRegister != 0", microOp, StringComparison.Ordinal);
        Assert.Contains("public override void EmitWriteBackRetireRecords(", microOp, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestinationRegister, abi.RegisterValue)", microOp, StringComparison.Ordinal);

        foreach (string kind in CommandKinds)
        {
            Assert.Contains($"SystemDeviceCommandKind.{kind}", microOp, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RegisterAbiClosesWriteRejectAndPreciseFaultSemantics()
    {
        string root = FindRepositoryRoot();
        string abi = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "ExternalAccelerators", "Tokens", "AcceleratorRegisterAbi.cs");
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "ExternalAccelerators", "ExternalAcceleratorRuntime.cs");

        Assert.Contains("NoWriteRejected = 0", abi, StringComparison.Ordinal);
        Assert.Contains("WriteRegister = 1", abi, StringComparison.Ordinal);
        Assert.Contains("NoWritePreciseFault = 2", abi, StringComparison.Ordinal);
        Assert.Contains("FromCapabilityQuery(", abi, StringComparison.Ordinal);
        Assert.Contains("FromSubmitAdmission(", abi, StringComparison.Ordinal);
        Assert.Contains("FromStatusLookup(", abi, StringComparison.Ordinal);
        Assert.Contains("non-trapping rejection writes zero to rd", abi, StringComparison.Ordinal);
        Assert.Contains("precise fault performs no architectural rd write", abi, StringComparison.Ordinal);
        Assert.Contains("runtime.QueryCaps(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("runtime.Submit(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("runtime.Poll(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("runtime.Status(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("runtime.Wait(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("runtime.Cancel(", ReadMicroOp(root), StringComparison.Ordinal);
        Assert.Contains("commitCompletedTokens: true", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineLane7RetirementIsReachableAndFenceResultIsCommitCoupled()
    {
        string root = FindRepositoryRoot();
        string microOp = ReadMicroOp(root);
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.Pipeline.Helpers.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");

        Assert.Contains("=> laneIndex < 6 || laneIndex == 7;", helpers, StringComparison.Ordinal);
        Assert.Contains("CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);", stageFlow, StringComparison.Ordinal);
        Assert.Contains("retireBatch.EmitMicroOpRetireRecords(", ReadRetire(root), StringComparison.Ordinal);
        Assert.Contains("core.GetExternalAcceleratorRuntime().FenceCommit(_capturedFenceHandle)", microOp, StringComparison.Ordinal);

        int resolve = microOp.IndexOf(
            "ExternalAcceleratorRuntimeCommandResult result =\n                ResolveRetireResult(ref core);",
            StringComparison.Ordinal);
        int fault = microOp.IndexOf("if (abi.RequiresPreciseFault)", resolve, StringComparison.Ordinal);
        int write = microOp.IndexOf("RetireRecord.RegisterWrite(", fault, StringComparison.Ordinal);
        Assert.True(resolve >= 0 && fault > resolve && write > fault);
    }

    [Fact]
    public void NoDirectDispatcherOrExactIssuedAttemptSourceExists()
    {
        string root = FindRepositoryRoot();
        string dispatchRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch");
        string dispatch = string.Join(
            "\n",
            Directory.GetFiles(dispatchRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.DoesNotContain("SystemDeviceCommandMicroOp", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_QUERY_CAPS", dispatch, StringComparison.Ordinal);

        string attach = Extract(
            fsp,
            "private void AttachRf08PostStageBIdentityTemplate(",
            "private byte ResolveForegroundRunnableVirtualThreadMask()");
        Assert.Contains("ScalarALUMicroOp", attach, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDeviceCommand", attach, StringComparison.Ordinal);
        Assert.DoesNotContain("Accelerator", attach, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperApprovesRegisterAbiWithoutMergingAcceleratorCommit()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("| scalar-register StreamEngine compatibility `RegisterWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("| atomic returned-result `RegisterWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("| `AcceleratorCommit` | lane-7 `ACCEL_FENCE` observe", paper, StringComparison.Ordinal);
        Assert.Contains("accelerator register ABI remains a separate residual", paper, StringComparison.Ordinal);
        Assert.Contains("approved accelerator command ABI `RegisterWrite`", paper, StringComparison.Ordinal);
        Assert.DoesNotContain("approved `SystemDeviceCommandMicroOp`", paper, StringComparison.Ordinal);
    }

    private static string ReadMicroOp(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Lane7Accelerator", "SystemDeviceCommandMicroOp.cs");

    private static string ReadRetire(string root) =>
        Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire",
            "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

    private static string Extract(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
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
