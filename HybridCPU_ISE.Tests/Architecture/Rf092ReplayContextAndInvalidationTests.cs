using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf092ReplayContextAndInvalidationTests
{
    [Fact]
    public void MainMemoryEverySuccessfulPhysicalWriteAndAnnotationMutationAdvancesEpoch()
    {
        var memory = new Processor.MainMemoryArea();
        memory.AllocateMemory(0, 512);
        ulong initial = memory.ReplayRelevantMutationEpoch;

        Assert.True(memory.TryWritePhysicalRange(16, new byte[] { 1, 2, 3, 4 }));
        ulong afterWrite = memory.ReplayRelevantMutationEpoch;
        Assert.True(afterWrite > initial);

        memory.PublishVliwBundleAnnotations(0, CreateAnnotations());
        ulong afterPublish = memory.ReplayRelevantMutationEpoch;
        Assert.True(afterPublish > afterWrite);

        memory.ClearVliwBundleAnnotations(0);
        Assert.True(memory.ReplayRelevantMutationEpoch > afterPublish);
    }

    [Fact]
    public void LiveContextCapturesEveryRf09SemanticFactorAndChangesWithOwners()
    {
        var memory = new Processor.MainMemoryArea();
        memory.AllocateMemory(0, 512);
        var core = new Processor.CPU_Core(
            7,
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation));

        CanonicalDecodeContext baseline = core.TestCaptureReplayDecodeContext();
        Assert.True(baseline.IsReplayEligible);
        Assert.NotEqual(CanonicalDecodeContext.Unbound, baseline);
        Assert.Contains("VliwDecoderV4", baseline.ExtensionConfigurationFingerprint);
        Assert.Contains(memory.ReplayAddressSpaceIdentity.ToString(), baseline.AddressSpaceIdentity);

        core.VectorConfig.VL++;
        Assert.NotEqual(baseline, core.TestCaptureReplayDecodeContext());
        core.VectorConfig.VL--;

        core.CsrMemDomainCert = 0x55;
        Assert.NotEqual(baseline, core.TestCaptureReplayDecodeContext());
        core.CsrMemDomainCert = 0;

        core.ArchContexts[0].CurrentPrivilege = PrivilegeLevel.Supervisor;
        Assert.NotEqual(baseline, core.TestCaptureReplayDecodeContext());
        core.ArchContexts[0].CurrentPrivilege = PrivilegeLevel.Machine;

        Assert.True(memory.TryWritePhysicalRange(32, new byte[] { 9 }));
        Assert.NotEqual(
            baseline.ExecutableMemoryInvalidationEpoch,
            core.TestCaptureReplayDecodeContext().ExecutableMemoryInvalidationEpoch);

        ulong beforeSerializing = core.TestReplayCodeGenerationEpoch;
        core.InvalidateAllVliwFetchState(ReplayPhaseInvalidationReason.SerializingEvent);
        Assert.True(core.TestReplayCodeGenerationEpoch > beforeSerializing);
    }

    [Fact]
    public void CanonicalDecodeBindsExplicitContextIntoSemanticKey()
    {
        CanonicalDecodeContext context = CreateContext(11, 3);
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        var decoded = new VliwDecoderV4().DecodeInstructionBundle(
            raw,
            CreateAnnotations(),
            bundleAddress: 0x1000,
            bundleSerial: 4,
            decodeContext: context);

        Assert.Equal(context.ExecutableMemoryInvalidationEpoch,
            decoded.CanonicalBundle.SemanticKey.ExecutableMemoryInvalidationEpoch);
        Assert.Equal(context.CodeGenerationEpoch,
            decoded.CanonicalBundle.SemanticKey.CodeGenerationEpoch);
        Assert.True(decoded.CanonicalBundle.SemanticKey.IsReplayEligible);
    }

    [Fact]
    public void TransitionalMutableLoopBufferRequiresFullContextEquality()
    {
        var loopBuffer = new LoopBuffer();
        loopBuffer.Initialize();
        CanonicalDecodeContext context = CreateContext(5, 2);
        loopBuffer.BeginLoad(0x2000, 4, context);
        loopBuffer.CommitLoad();
        var target = new MicroOp?[BundleMetadata.BundleSlotCount];

        Assert.True(loopBuffer.TryReplay(0x2000, target, context));

        CanonicalDecodeContext changed = context with { DomainIdentity = "domain-b" };
        Assert.False(loopBuffer.TryReplay(0x2000, target, changed));
        Assert.Equal(LoopBuffer.BufferState.Empty, loopBuffer.State);
        Assert.Equal(
            ReplayPhaseInvalidationReason.CertificateMutation,
            loopBuffer.CurrentReplayPhase.LastInvalidationReason);
    }

    [Fact]
    public void ProductionServingUsesContextGuardWithoutMergingSchedulerKeysOrReplayToken()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string loopBuffer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Fetch", "LoopBuffer.cs");
        string substrate = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Certificates", "ReplayPhaseSubstrate.cs");
        string replayToken = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Replay", "ReplayToken.cs");

        Assert.Contains(
            "_loopBuffer.TryGetReplayEntry(",
            stageFlow,
            StringComparison.Ordinal);
        Assert.Contains("CaptureReplayDecodeContext()", stageFlow, StringComparison.Ordinal);
        Assert.Contains("_loadedDecodeContext", loopBuffer, StringComparison.Ordinal);
        Assert.Contains("public readonly struct ReplayPhaseKey", substrate, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalDecodeContext", substrate, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalDecodeContext", replayToken, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperStatusAndEvidenceCloseRf092ButKeepServingCutoverOpen()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF09",
            "rf09.2-context-and-code-epoch-invalidation.md");

        Assert.Contains("RF-09 context authority and mutation closure", paper, StringComparison.Ordinal);
        Assert.Contains("every successful physical write conservatively advances", paper, StringComparison.Ordinal);
        Assert.Contains("| RF-09.2 | complete context/invalidation gate |", status, StringComparison.Ordinal);
        Assert.Contains("RF-09.4", status, StringComparison.Ordinal);
        Assert.Contains("Physical-write mutation matrix", evidence, StringComparison.Ordinal);
        Assert.Contains("still stores mutable `MicroOp` references", evidence, StringComparison.Ordinal);
        Assert.Contains("does not claim executable-page precision", evidence, StringComparison.Ordinal);
    }

    private static VliwBundleAnnotations CreateAnnotations() =>
        VliwBundleAnnotations.Empty;

    private static CanonicalDecodeContext CreateContext(
        ulong executableEpoch,
        ulong codeEpoch) => new()
    {
        ManifestVersion = "1",
        ManifestHash = "manifest",
        ExtensionConfigurationFingerprint = "extensions",
        DecoderEpoch = "decoder-epoch",
        DecoderVersion = "decoder-version",
        PrivilegeContext = "machine",
        DomainIdentity = "domain-a",
        AddressSpaceIdentity = "address-space-a",
        VectorConfigurationFingerprint = "vector-a",
        ExecutableMemoryInvalidationEpoch = executableEpoch,
        CodeGenerationEpoch = codeEpoch,
        IsReplayEligible = true,
    };

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
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
