using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf094ImmutableSemanticServingCutoverTests
{
    [Fact]
    public void SemanticLoopHitReturnsImmutableEntryAndFreshCarriersPerAttempt()
    {
        CanonicalDecodeContext context = CreateContext();
        VLIW_Instruction[] raw = CreateRawBundle();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            raw,
            VliwBundleAnnotations.Empty,
            0x1000,
            7,
            context);
        ReplayEntry entry = ReplayEntry.Create(decoded.CanonicalBundle!);
        MicroOp?[] liveCarriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                raw,
                decoded);

        var loopBuffer = new LoopBuffer();
        loopBuffer.Initialize();
        loopBuffer.BeginSemanticLoad(0x1000, 4, entry, liveCarriers, context);
        loopBuffer.CommitLoad();

        Assert.True(loopBuffer.TryGetReplayEntry(0x1000, context, out ReplayEntry? firstHit));
        Assert.Same(entry, firstHit);
        DecodedBundleRuntimeState first =
            Processor.CPU_Core.TestBuildReplayDecodedBundleState(firstHit!, 0x1000);

        Assert.True(loopBuffer.TryGetReplayEntry(0x1000, context, out ReplayEntry? secondHit));
        DecodedBundleRuntimeState second =
            Processor.CPU_Core.TestBuildReplayDecodedBundleState(secondHit!, 0x1000);

        Assert.Equal(DecodedBundleStateKind.Canonical, first.TransportFacts.StateKind);
        Assert.Same(entry.CanonicalBundle, first.CanonicalDecode.CanonicalBundle);
        Assert.Equal(IsaOpcodeValues.ADD, first.TransportFacts.Slots[0].OpCode);
        Assert.NotSame(
            first.TransportFacts.Slots[0].MicroOp,
            second.TransportFacts.Slots[0].MicroOp);
    }

    [Fact]
    public void ContextMutationInvalidatesSemanticServingAndRequiresLiveFallback()
    {
        CanonicalDecodeContext context = CreateContext();
        VLIW_Instruction[] raw = CreateRawBundle();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            raw,
            VliwBundleAnnotations.Empty,
            0x1000,
            0,
            context);
        ReplayEntry entry = ReplayEntry.Create(decoded.CanonicalBundle!);
        MicroOp?[] liveCarriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                raw,
                decoded);
        var loopBuffer = new LoopBuffer();
        loopBuffer.Initialize();
        loopBuffer.BeginSemanticLoad(0x1000, 3, entry, liveCarriers, context);
        loopBuffer.CommitLoad();

        CanonicalDecodeContext changed = context with { CodeGenerationEpoch = 9 };
        Assert.False(loopBuffer.TryGetReplayEntry(0x1000, changed, out _));
        Assert.Equal(LoopBuffer.BufferState.Empty, loopBuffer.State);
        Assert.Equal(
            ReplayPhaseInvalidationReason.CertificateMutation,
            loopBuffer.CurrentReplayPhase.LastInvalidationReason);
    }

    [Fact]
    public void VectorPayloadIsRehydratedFromFrozenCanonicalSnapshot()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.VADD,
            DestSrc1Pointer = 0x1000,
            Src2Pointer = 0x2000,
            StreamLength = 32,
            Stride = 8,
            RowStride = 64,
            TailAgnostic = true,
            MaskAgnostic = true,
            DataType = 2,
        };
        CanonicalDecodeContext context = CreateContext();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            raw,
            VliwBundleAnnotations.Empty,
            0x2000,
            0,
            context);
        ReplayEntry entry = ReplayEntry.Create(decoded.CanonicalBundle!);

        DecodedBundleRuntimeState first =
            Processor.CPU_Core.TestBuildReplayDecodedBundleState(entry, 0x2000);
        DecodedBundleRuntimeState second =
            Processor.CPU_Core.TestBuildReplayDecodedBundleState(entry, 0x2000);

        Assert.True(first.TransportFacts.Slots[0].IsVectorOp);
        Assert.IsAssignableFrom<VectorMicroOp>(first.TransportFacts.Slots[0].MicroOp);
        Assert.NotSame(
            first.TransportFacts.Slots[0].MicroOp,
            second.TransportFacts.Slots[0].MicroOp);
    }

    [Fact]
    public void ProductionContourHasNoPcOnlyMutableCarrierServingOrIdentityAllocation()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string loopBuffer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Stages", "Fetch", "LoopBuffer.cs");
        string projector = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL",
            "Core", "Decoder", "DecodedBundleTransportProjector.cs");

        Assert.DoesNotContain(".TryReplay(", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("_replayFetchBuffer", stageFlow, StringComparison.Ordinal);
        Assert.Contains("TryGetReplayEntry", stageFlow, StringComparison.Ordinal);
        Assert.Contains("BeginSemanticLoad", stageFlow, StringComparison.Ordinal);
        Assert.Contains("BuildReplayDecodedBundleState", stageFlow, StringComparison.Ordinal);
        Assert.Contains("#if TESTING", loopBuffer, StringComparison.Ordinal);
        Assert.Contains("Production replay never stores a MicroOp carrier", loopBuffer, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", projector, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationClosesRf09WithoutExpandingClaims()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string gate = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "02_RF09_ENTRY_GATE.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF09", "rf09.4-immutable-semantic-serving-cutover.md");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");

        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
        Assert.Contains("RF-09 execution status: closed", gate, StringComparison.Ordinal);
        Assert.Contains("RF-09 exit is accepted", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-10 entry inventory/freeze", status, StringComparison.Ordinal);
        Assert.Contains("It is not universal decode", paper, StringComparison.Ordinal);
        Assert.Contains("universal rollback", evidence, StringComparison.Ordinal);
        Assert.Contains("complete memory model", evidence, StringComparison.Ordinal);
    }

    private static VLIW_Instruction[] CreateRawBundle()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };
        return raw;
    }

    private static CanonicalDecodeContext CreateContext() => new()
    {
        ManifestVersion = "1",
        ManifestHash = "manifest-a",
        ExtensionConfigurationFingerprint = "extensions-a",
        DecoderEpoch = "decoder-epoch-a",
        DecoderVersion = "decoder-version-a",
        PrivilegeContext = "machine",
        DomainIdentity = "domain-a",
        AddressSpaceIdentity = "address-space-a",
        VectorConfigurationFingerprint = "vector-a",
        ExecutableMemoryInvalidationEpoch = 7,
        CodeGenerationEpoch = 3,
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
