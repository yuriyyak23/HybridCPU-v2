using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-09.0 freezes the existing replay surfaces and RF-09.1 adds only the
/// immutable semantic entry contract. The PC-only LoopBuffer remains the
/// production source until a later separately certified cutover.
/// </summary>
public sealed class Rf090Rf091ReplayEntryFreezeTests
{
    [Fact]
    public void ReplayEntry_FreezesOnlySemanticCanonicalContent()
    {
        CanonicalBundle bundle = CreateBundle(CreateReplayContext(), IsaOpcodeValues.ADD);
        ReplayEntry entry = ReplayEntry.Create(bundle);

        Assert.Equal(bundle.SemanticKey, entry.SemanticKey);
        Assert.Same(bundle, entry.CanonicalBundle);
        Assert.True(entry.ValidationFingerprint.IsValid);
        Assert.True(entry.HasValidFrozenContent());

        string[] forbiddenFragments =
        [
            "MicroOp",
            "VliwOperationId",
            "ScheduledOperation",
            "ExecutionRecord",
            "AdmissionRecord",
            "Lane",
            "Scheduler",
            "ReplayToken",
        ];
        string[] publicProperties = typeof(ReplayEntry)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.All(forbiddenFragments, fragment =>
            Assert.DoesNotContain(publicProperties, property =>
                property.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ReplayEntry_UnboundCanonicalContextFailsClosed()
    {
        CanonicalBundle unbound = CreateBundle(CanonicalDecodeContext.Unbound, IsaOpcodeValues.ADD);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ReplayEntry.Create(unbound));

        Assert.Contains("explicitly replay-eligible", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayEntry_EverySemanticContextMutationChangesIdentityAndFingerprint()
    {
        CanonicalDecodeContext baseline = CreateReplayContext();
        ReplayEntry expected = ReplayEntry.Create(CreateBundle(baseline, IsaOpcodeValues.ADD));
        CanonicalDecodeContext[] mutations =
        [
            baseline with { ManifestVersion = "2.0" },
            baseline with { ManifestHash = "manifest-b" },
            baseline with { ExtensionConfigurationFingerprint = "extensions-b" },
            baseline with { DecoderEpoch = "epoch-b" },
            baseline with { DecoderVersion = "decoder-b" },
            baseline with { PrivilegeContext = "supervisor" },
            baseline with { DomainIdentity = "domain-b" },
            baseline with { AddressSpaceIdentity = "address-space-b" },
            baseline with { VectorConfigurationFingerprint = "vector-b" },
            baseline with { ExecutableMemoryInvalidationEpoch = 9 },
            baseline with { CodeGenerationEpoch = 4 },
        ];

        foreach (CanonicalDecodeContext mutation in mutations)
        {
            ReplayEntry actual = ReplayEntry.Create(CreateBundle(mutation, IsaOpcodeValues.ADD));
            Assert.NotEqual(expected.SemanticKey, actual.SemanticKey);
            Assert.NotEqual(expected.ValidationFingerprint, actual.ValidationFingerprint);
        }

        ReplayEntry rawMutation = ReplayEntry.Create(CreateBundle(baseline, IsaOpcodeValues.SUB));
        Assert.NotEqual(expected.SemanticKey, rawMutation.SemanticKey);
        Assert.NotEqual(expected.ValidationFingerprint, rawMutation.ValidationFingerprint);
    }

    [Fact]
    public void ExistingPhaseTemplateAndReplayTokenMechanismsRemainDistinct()
    {
        string root = FindRepositoryRoot();
        string substrate = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Certificates", "ReplayPhaseSubstrate.cs");
        string loopBuffer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Fetch", "LoopBuffer.cs");
        string replayToken = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Replay", "ReplayToken.cs");
        string replayEntry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf09ReplayEntry.cs");

        Assert.Contains("public readonly struct ReplayPhaseKey", substrate, StringComparison.Ordinal);
        Assert.Contains("public readonly struct ClassCapacityTemplate", substrate, StringComparison.Ordinal);
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey", substrate, StringComparison.Ordinal);
        Assert.Contains("#if TESTING", loopBuffer, StringComparison.Ordinal);
        Assert.Contains("public bool TryReplay(", loopBuffer, StringComparison.Ordinal);
        Assert.Contains("public class ReplayToken", replayToken, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayToken", Slice(
            replayEntry,
            "public sealed class ReplayEntry",
            "private static ReplayEntryValidationFingerprint"), StringComparison.Ordinal);
    }

    [Fact]
    public void LaterServingCutoverPreservesFrozenEntryAndAllocatesNoAttemptIdentity()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string loopBuffer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Fetch", "LoopBuffer.cs");
        string replayEntry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf09ReplayEntry.cs");

        Assert.Contains("_loopBuffer.TryGetReplayEntry(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.BeginSemanticLoad(", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("_loopBuffer.StoreSlot(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("private Decoder.ReplayEntry? _replayEntry;", loopBuffer, StringComparison.Ordinal);
        Assert.Contains("ReplayEntry.Create(", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", replayEntry, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", replayEntry, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", replayEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperStatusAndEvidenceKeepRf091BoundedAndRf092Open()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string inventory = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF09",
            "rf09.0-entry-inventory-and-freeze.md");
        string entry = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF09",
            "rf09.1-immutable-semantic-replay-entry.md");

        Assert.Contains("RF-09 semantic-entry freeze and cutover order", paper, StringComparison.Ordinal);
        Assert.Contains("digest alone is never equality", paper, StringComparison.Ordinal);
        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 entry inventory/freeze", status, StringComparison.Ordinal);
        Assert.Contains("Current executable writes do not universally advance", inventory, StringComparison.Ordinal);
        Assert.Contains("It is not a cache-serving cutover", entry, StringComparison.Ordinal);
        Assert.Contains("ReplayToken", inventory, StringComparison.Ordinal);
    }

    private static CanonicalBundle CreateBundle(
        CanonicalDecodeContext context,
        ushort opcode)
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = opcode,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };
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
        var metadata = new BundleMetadata
        {
            SlotMetadata = Enumerable.Repeat(
                SlotMetadata.Default,
                BundleMetadata.BundleSlotCount).ToArray(),
        };
        return CanonicalBundle.Create(raw, slots, metadata, 0x1000, 1, context);
    }

    private static CanonicalDecodeContext CreateReplayContext() => new()
    {
        ManifestVersion = "1.0",
        ManifestHash = "manifest-a",
        ExtensionConfigurationFingerprint = "extensions-a",
        DecoderEpoch = "epoch-a",
        DecoderVersion = "decoder-a",
        PrivilegeContext = "user",
        DomainIdentity = "domain-a",
        AddressSpaceIdentity = "address-space-a",
        VectorConfigurationFingerprint = "vector-a",
        ExecutableMemoryInvalidationEpoch = 8,
        CodeGenerationEpoch = 3,
        IsReplayEligible = true,
    };

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
