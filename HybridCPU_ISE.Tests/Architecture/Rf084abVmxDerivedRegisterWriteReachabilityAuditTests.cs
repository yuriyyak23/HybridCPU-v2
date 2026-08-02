namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ab distinguishes retained VMX outcome publication adapters from
/// production-reachable VMX-derived RegisterWrite effects.
/// </summary>
public sealed class Rf084abVmxDerivedRegisterWriteReachabilityAuditTests
{
    [Fact]
    public void BothProductionVmxIngressContoursMaterializeFailClosedEffects()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Types", "MicroOp.IO.cs");
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Dispatch", "ExecutionDispatcherV4.VmxCompatibility.cs");

        Assert.Contains("public sealed class VmxMicroOp : MicroOp", microOp, StringComparison.Ordinal);
        Assert.Contains("_resolvedRetireEffect = VmxRetireEffect.Fault(", microOp, StringComparison.Ordinal);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", microOp, StringComparison.Ordinal);

        string directEffect = Slice(
            direct,
            "private static VmxRetireEffect CreateRemovedFrontendFaultEffect(",
            "[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        Assert.Contains(
            "return VmxRetireEffect.Fault(operation, VmExitReason.SecurityPolicyViolation);",
            directEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionResolverCanReturnOnlyFaultOrNoOp()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.VmxRetire.cs");
        string resolver = Slice(
            retire,
            "private static Core.VmxRetireOutcome ApplyRemovedFrontendFailClosedEffect(",
            "[MethodImpl(MethodImplOptions.AggressiveInlining)]\n            private void PublishReplayInvalidation");

        Assert.Contains("return Core.VmxRetireOutcome.NoOp();", resolver, StringComparison.Ordinal);
        Assert.Contains("return Core.VmxRetireOutcome.Fault(", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("new Core.VmxRetireOutcome", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("HasRegisterWriteback", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoredStackPointer", resolver, StringComparison.Ordinal);
    }

    [Fact]
    public void OutcomeModelHasNoSuccessFactoryThatCanPublishARegister()
    {
        string root = FindRepositoryRoot();
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Virtualization",
            "Compatibility", "Frontend", "Retire", "VmxRetireModel.cs");
        string outcome = Slice(
            model,
            "public readonly record struct VmxRetireOutcome(",
            "\n    }\n}");

        Assert.Contains("public static VmxRetireOutcome Fault(", outcome, StringComparison.Ordinal);
        Assert.Contains("public static VmxRetireOutcome NoOp()", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Success(", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterWrite(", outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterAndPcPublicationRemainDormantPrivateAdapters()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.VmxRetire.cs");
        string publication = Slice(
            retire,
            "private void RetireVmxOutcomeRecords(",
            "internal Core.VmxRetireOutcome ApplyRetiredVmxEffectForTesting(");

        Assert.Contains("RetireRecord.RegisterWrite(", publication, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.PcWrite(", publication, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(", publication, StringComparison.Ordinal);
        Assert.Contains("private void RetireVmxOutcomeRecords(", retire, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetireVmxOutcomeRecordsForTesting",
            retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExactIdentityAttachmentStillExcludesVmx()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string attach = Slice(
            fsp,
            "private void AttachRf08PostStageBIdentityTemplate(",
            "private byte ResolveForegroundRunnableVirtualThreadMask()");

        Assert.Contains("ScalarALUMicroOp", attach, StringComparison.Ordinal);
        Assert.DoesNotContain("VmxMicroOp", attach, StringComparison.Ordinal);
        Assert.DoesNotContain("VmxRetireEffect", attach, StringComparison.Ordinal);
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
