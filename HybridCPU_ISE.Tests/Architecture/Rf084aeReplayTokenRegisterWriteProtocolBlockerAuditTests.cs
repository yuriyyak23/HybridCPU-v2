namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ae freezes the ReplayToken architectural-register restore contour,
/// its caller reachability and the current prevalidation/owner limitations.
/// </summary>
public sealed class Rf084aeReplayTokenRegisterWriteProtocolBlockerAuditTests
{
    [Fact]
    public void RollbackRepublishesRegisterSnapshotsOneByOneThroughRetire()
    {
        string root = FindRepositoryRoot();
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");
        string rollback = Slice(
            replay,
            "public void Rollback(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core)",
            "public bool CanSafelyRollback()");

        Assert.Contains("foreach (var kvp in PreExecutionRegisterState)", rollback, StringComparison.Ordinal);
        Assert.Contains("core.RetireCoordinator.Retire(RetireRecord.RegisterWrite(vtId, regId, value))", rollback, StringComparison.Ordinal);
        Assert.Contains("foreach (var (address, data) in PreExecutionMemoryState)", rollback, StringComparison.Ordinal);
        AssertOrdered(
            rollback,
            "foreach (var kvp in PreExecutionRegisterState)",
            "foreach (var (address, data) in PreExecutionMemoryState)");
    }

    [Fact]
    public void RollbackDoesNotEnforceItsAvailableFullStatePrecheck()
    {
        string root = FindRepositoryRoot();
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");
        string rollback = Slice(
            replay,
            "public void Rollback(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core)",
            "public bool CanSafelyRollback()");
        string safeCheck = Slice(
            replay,
            "public bool CanSafelyRollback()",
            "private int ResolveOwnerThreadId(");

        Assert.DoesNotContain("CanSafelyRollback(", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("HasFullyBoundRollbackMemoryState(", rollback, StringComparison.Ordinal);
        Assert.Contains("HasFullyBoundRollbackMemoryState()", safeCheck, StringComparison.Ordinal);
        Assert.Contains("return false;", safeCheck, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerAndCapturedStateRemainPubliclyMutableWithLiveVtFallback()
    {
        string root = FindRepositoryRoot();
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");

        Assert.Contains("public Dictionary<int, ulong> PreExecutionRegisterState { get; set; }", replay, StringComparison.Ordinal);
        Assert.Contains("public List<(ulong Address, byte[] Data)> PreExecutionMemoryState { get; set; }", replay, StringComparison.Ordinal);
        Assert.Contains("public int OwnerThreadId { get; set; }", replay, StringComparison.Ordinal);
        Assert.Contains("return core.ReadActiveVirtualThreadId();", replay, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<ReplayToken>(json)", replay, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCoreHasNoRollbackInvocationCaller()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        Assert.Equal(
            [
                "Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs",
                "Pipeline/MicroOps/Types/MicroOp.cs"
            ],
            FindCallerFiles(coreRoot, "CreateRollbackToken("));
        Assert.Equal(
            ["Pipeline/MicroOps/Replay/ReplayToken.cs"],
            FindCallerFiles(coreRoot, "CaptureRegisterState("));
        Assert.Equal(
            ["Pipeline/MicroOps/Replay/ReplayToken.cs"],
            FindCallerFiles(coreRoot, "Rollback(ref"));
        Assert.Equal(
            ["Pipeline/MicroOps/Replay/ReplayToken.cs"],
            FindCallerFiles(coreRoot, "CanSafelyRollback("));
    }

    [Fact]
    public void StoreOverrideCapturesOnlyExactMemoryRollbackState()
    {
        string root = FindRepositoryRoot();
        string memory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string storeToken = Slice(
            memory,
            "public override HybridCPU_ISE.Core.ReplayToken CreateRollbackToken(",
            "public override bool Execute(ref Processor.CPU_Core core)");

        Assert.Contains("HasSideEffects = true", storeToken, StringComparison.Ordinal);
        Assert.Contains("token.CaptureMemoryState(Address, Size)", storeToken, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureRegisterState(", storeToken, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperBoundedRollbackClaimIsReconciledByLaterC_CExclusion()
    {
        string root = FindRepositoryRoot();
        string replayPaper = Read(root, "ResearchPaper", "section", "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string admissionPaper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("Replay Tokens as Bounded Rollback Carriers", replayPaper, StringComparison.Ordinal);
        Assert.Contains("Registers are republished through retire-backed architectural publication", replayPaper, StringComparison.Ordinal);
        Assert.Contains("does not establish all-or-none register-plus-memory", replayPaper, StringComparison.Ordinal);
        Assert.Contains(
            "| scalar-register StreamEngine compatibility `RegisterWrite` |",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "| atomic returned-result `RegisterWrite` |",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "| replay-rollback `RegisterWrite` |",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "RF-08.4af approves C-C",
            admissionPaper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayTokenCarriesNoRf08IssuedAttemptIdentity()
    {
        string root = FindRepositoryRoot();
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");

        Assert.DoesNotContain("ScheduledOperation", replay, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", replay, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", replay, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", replay, StringComparison.Ordinal);
    }

    private static string[] FindCallerFiles(string coreRoot, string marker) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static void AssertOrdered(string source, string firstMarker, string secondMarker)
    {
        int first = source.IndexOf(firstMarker, StringComparison.Ordinal);
        int second = source.IndexOf(secondMarker, StringComparison.Ordinal);
        Assert.True(first >= 0, $"Missing marker: {firstMarker}");
        Assert.True(second > first, $"Expected '{secondMarker}' after '{firstMarker}'.");
    }

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
