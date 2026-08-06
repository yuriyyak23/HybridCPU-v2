using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf093ReplaySemanticShadowLookupTests
{
    [Fact]
    public void ShadowLookupFirstObservationMissesThenFullContentHitMatches()
    {
        var shadow = new ReplaySemanticShadowLookup();
        CanonicalBundle first = CreateBundle(CreateContext(), 0x1000, 1);
        CanonicalBundle relocated = CreateBundle(CreateContext(), 0x9000, 44);

        Assert.Equal(
            ReplaySemanticShadowObservation.Miss,
            shadow.ObserveLiveDecode(first));
        Assert.Equal(
            ReplaySemanticShadowObservation.EquivalentHit,
            shadow.ObserveLiveDecode(relocated));

        ReplaySemanticShadowMetrics metrics = shadow.Metrics;
        Assert.Equal(2UL, metrics.Observations);
        Assert.Equal(1UL, metrics.Misses);
        Assert.Equal(1UL, metrics.EquivalentHits);
        Assert.Equal(0UL, metrics.ContentMismatches);
        Assert.Equal(metrics.Observations, metrics.AccountedObservations);
    }

    [Fact]
    public void EverySemanticKeyMutationIsShadowMissNotFalseHit()
    {
        CanonicalDecodeContext context = CreateContext();
        var shadow = new ReplaySemanticShadowLookup();
        Assert.Equal(
            ReplaySemanticShadowObservation.Miss,
            shadow.ObserveLiveDecode(CreateBundle(context, 0x1000, 1)));

        CanonicalDecodeContext[] mutations =
        [
            context with { ManifestVersion = "2" },
            context with { ManifestHash = "manifest-b" },
            context with { ExtensionConfigurationFingerprint = "extensions-b" },
            context with { DecoderEpoch = "decoder-epoch-b" },
            context with { DecoderVersion = "decoder-version-b" },
            context with { PrivilegeContext = "supervisor" },
            context with { DomainIdentity = "domain-b" },
            context with { AddressSpaceIdentity = "address-space-b" },
            context with { VectorConfigurationFingerprint = "vector-b" },
            context with { ExecutableMemoryInvalidationEpoch = 8 },
            context with { CodeGenerationEpoch = 9 },
        ];

        foreach (CanonicalDecodeContext mutation in mutations)
        {
            Assert.Equal(
                ReplaySemanticShadowObservation.Miss,
                shadow.ObserveLiveDecode(CreateBundle(mutation, 0x1000, 1)));
        }

        Assert.Equal(
            ReplaySemanticShadowObservation.Miss,
            shadow.ObserveLiveDecode(
                CreateBundle(context, 0x1000, 1, IsaOpcodeValues.SUB)));
        Assert.Equal(0UL, shadow.Metrics.EquivalentHits);
        Assert.Equal(13UL, shadow.Metrics.Misses);
    }

    [Fact]
    public void ProductionDecodeObservesShadowButAlwaysPublishesLiveCanonicalState()
    {
        var memory = new Processor.MainMemoryArea();
        memory.AllocateMemory(0, 4096);
        var core = new Processor.CPU_Core(
            0,
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation));
        VLIW_Instruction[] raw = CreateRawBundle(IsaOpcodeValues.ADD);

        core.TestRunDecodeStageWithFetchedBundle(raw, 0x1000, VliwBundleAnnotations.Empty);
        ReplaySemanticShadowMetrics afterMiss = core.TestReplaySemanticShadowMetrics;
        Assert.Equal(1UL, afterMiss.Misses);
        Assert.Equal(0UL, afterMiss.EquivalentHits);

        core.TestRunDecodeStageWithFetchedBundle(raw, 0x1000, VliwBundleAnnotations.Empty);
        ReplaySemanticShadowMetrics afterHit = core.TestReplaySemanticShadowMetrics;
        Assert.Equal(1UL, afterHit.Misses);
        Assert.Equal(1UL, afterHit.EquivalentHits);
        Assert.Equal(
            IsaOpcodeValues.ADD,
            core.GetCurrentDecodedInstructionBundle().CanonicalBundle.Slots[0].Opcode);

        core.InvalidateAllVliwFetchState(ReplayPhaseInvalidationReason.SerializingEvent);
        Assert.Equal(1UL, core.TestReplaySemanticShadowMetrics.Invalidations);
        core.TestRunDecodeStageWithFetchedBundle(raw, 0x1000, VliwBundleAnnotations.Empty);
        Assert.Equal(2UL, core.TestReplaySemanticShadowMetrics.Misses);
    }

    [Fact]
    public void ShadowObserverHasNoCachedEntryServingOrAttemptIdentityApi()
    {
        Type shadowType = typeof(ReplaySemanticShadowLookup);
        Assert.DoesNotContain(
            shadowType.GetMethods(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic),
            method => method.ReturnType == typeof(ReplayEntry) ||
                      method.ReturnType == typeof(CanonicalBundle));

        string root = FindRepositoryRoot();
        string source = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf09ReplaySemanticShadowLookup.cs");
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MicroOp", source, StringComparison.Ordinal);
        Assert.Contains("ObserveLiveDecode", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperStatusAndEvidenceCloseRf093ButKeepServingCutoverOpen()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF09",
            "rf09.3-non-serving-semantic-shadow-lookup.md");

        Assert.Contains("RF-09 non-serving semantic shadow", paper, StringComparison.Ordinal);
        Assert.Contains("never returns a cached canonical bundle", paper, StringComparison.Ordinal);
        Assert.Contains("| RF-09.3 | complete non-serving shadow gate |", status, StringComparison.Ordinal);
        Assert.Contains("RF-09.4", status, StringComparison.Ordinal);
        Assert.Contains("full `SemanticInstructionKey` record equality", evidence, StringComparison.Ordinal);
        Assert.Contains("production semantic-cache theorem", evidence, StringComparison.Ordinal);
    }

    private static CanonicalBundle CreateBundle(
        CanonicalDecodeContext context,
        ulong address,
        ulong serial,
        ushort opcode = IsaOpcodeValues.ADD)
    {
        VLIW_Instruction[] raw = CreateRawBundle(opcode);
        var slots = Enumerable.Range(0, BundleMetadata.BundleSlotCount)
            .Select(index => DecodedInstruction.CreateEmpty(index))
            .ToArray();
        slots[0] = DecodedInstruction.CreateOccupied(0, new InstructionIR
        {
            CanonicalOpcode = IsaOpcode.FromRawValue(opcode),
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        });
        return CanonicalBundle.Create(
            raw,
            slots,
            BundleMetadata.Default,
            address,
            serial,
            context);
    }

    private static VLIW_Instruction[] CreateRawBundle(ushort opcode)
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = opcode,
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
