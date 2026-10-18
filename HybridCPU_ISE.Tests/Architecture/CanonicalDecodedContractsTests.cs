using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class CanonicalDecodedContractsTests
{
    [Fact]
    public void CanonicalBundle_DeepCopiesRawInstructionAndSidebandSnapshots()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD };
        var slotMetadata = new SlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < slotMetadata.Length; index++)
        {
            slotMetadata[index] = SlotMetadata.Default;
        }

        var legacySlots = Enumerable.Range(0, BundleMetadata.BundleSlotCount)
            .Select(index => DecodedInstruction.CreateEmpty(index))
            .ToArray();
        legacySlots[0] = DecodedInstruction.CreateOccupied(0, CreateAddIr());
        var metadata = new BundleMetadata { SlotMetadata = slotMetadata };

        CanonicalBundle canonical = CanonicalBundle.Create(raw, legacySlots, metadata, 0x4000, 7);
        byte[] rawBeforeMutation = canonical.GetSlot(0).RawSlot.Content.ToArray();
        byte[] sidebandBeforeMutation = canonical.BundleSideband.Content.ToArray();
        byte[] instructionBeforeMutation = canonical.GetSlot(0).InstructionPayload.Content.ToArray();

        raw[0].OpCode = IsaOpcodeValues.SUB;
        slotMetadata[0] = SlotMetadata.NotStealable;
        byte[] callerOwnedCopy = canonical.GetSlot(0).InstructionPayload.Content.ToArray();
        callerOwnedCopy[0] ^= 0xFF;

        Assert.True(rawBeforeMutation.AsSpan().SequenceEqual(canonical.GetSlot(0).RawSlot.Content.Span));
        Assert.True(sidebandBeforeMutation.AsSpan().SequenceEqual(canonical.BundleSideband.Content.Span));
        Assert.True(instructionBeforeMutation.AsSpan().SequenceEqual(canonical.GetSlot(0).InstructionPayload.Content.Span));
        Assert.Equal(BundleMetadata.BundleSlotCount, canonical.Slots.Length);
        Assert.False(canonical.IsReplayEligible);

        var annotationRaw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        annotationRaw[0] = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD };
        var annotationMetadata = new BundleMetadata
        {
            SlotMetadata = Enumerable.Repeat(SlotMetadata.Default, BundleMetadata.BundleSlotCount).ToArray()
        };
        var defaultSlotAnnotations = Enumerable.Range(0, BundleMetadata.BundleSlotCount)
            .Select(index => DecodedInstruction.CreateEmpty(index))
            .ToArray();
        defaultSlotAnnotations[0] = DecodedInstruction.CreateOccupied(0, CreateAddIr());
        CanonicalBundle defaultAnnotations = CanonicalBundle.Create(
            annotationRaw, defaultSlotAnnotations, annotationMetadata, 0x4000, 7);
        DecodedInstruction[] alteredSlotAnnotations = (DecodedInstruction[])defaultSlotAnnotations.Clone();
        alteredSlotAnnotations[0] = DecodedInstruction.CreateOccupied(
            0,
            CreateAddIr(),
            new HybridCPU_ISE.Arch.InstructionSlotMetadata(VtId.Create(1), SlotMetadata.Default));
        CanonicalBundle altered = CanonicalBundle.Create(
            annotationRaw, alteredSlotAnnotations, annotationMetadata, 0x4000, 7);
        Assert.NotEqual(defaultAnnotations.SemanticKey, altered.SemanticKey);
    }

    [Fact]
    public void SemanticInstructionKey_MissesWhenAnyIdentityFactorOrRawByteChanges()
    {
        byte[] raw = [1, 2, 3, 4];
        CanonicalDecodeContext baseline = CreateReplayContext();
        SemanticInstructionKey key = SemanticInstructionKey.Create(raw, "annotations-a", baseline);

        CanonicalDecodeContext[] mutations =
        [
            baseline with { ManifestVersion = "2.0" },
            baseline with { ManifestHash = "manifest-b" },
            baseline with { ExtensionConfigurationFingerprint = "extensions-b" },
            baseline with { DecoderEpoch = "epoch-b" },
            baseline with { DecoderVersion = "decoder-b" },
            baseline with { PrivilegeContext = "supervisor" },
            baseline with { DomainIdentity = "domain-b" },
            baseline with { AddressSpaceIdentity = "as-b" },
            baseline with { VectorConfigurationFingerprint = "vector-b" },
            baseline with { ExecutableMemoryInvalidationEpoch = 9 },
            baseline with { CodeGenerationEpoch = 4 },
            baseline with { IsReplayEligible = false },
        ];

        Assert.All(mutations, context => Assert.NotEqual(key, SemanticInstructionKey.Create(raw, "annotations-a", context)));
        Assert.NotEqual(key, SemanticInstructionKey.Create(raw, "annotations-b", baseline));
        Assert.NotEqual(key, SemanticInstructionKey.Create([1, 2, 3, 5], "annotations-a", baseline));
        Assert.Equal(key, SemanticInstructionKey.Create(raw, "annotations-a", baseline));
        Assert.DoesNotContain(typeof(SemanticInstructionKey).GetProperties(), property =>
            property.Name.Contains("Operation", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Attempt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VliwDecoderV4_PublishesLegacyAndCanonicalProjectionsInParity()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };

        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(raw, 0x6000, 19);
        CanonicalBundle canonical = Assert.IsType<CanonicalBundle>(decoded.CanonicalBundle);
        DecodedInstruction legacy = decoded.GetDecodedSlot(0);
        CanonicalDecodedInstruction projected = canonical.GetSlot(0);

        Assert.True(projected.IsOccupied);
        Assert.Equal((uint)legacy.CanonicalOpcode, projected.Opcode);
        Assert.Equal(legacy.Class, projected.InstructionClass);
        Assert.Equal(legacy.SerializationClass, projected.SerializationClass);
        Assert.Equal(legacy.Rd.Value, projected.Rd);
        Assert.Equal(legacy.Rs1.Value, projected.Rs1);
        Assert.Equal(legacy.Rs2.Value, projected.Rs2);
        Assert.Equal(legacy.Imm, projected.Immediate);
        Assert.False(canonical.IsReplayEligible);
    }

    [Fact]
    public void DecodeFailure_IsTypedAndCarriesStableRawHash()
    {
        DecodeFailure failure = DecodeFailure.Create(
            DecodeFailureCode.OperandEncoding,
            slotIndex: 2,
            field: "rs2",
            rawBytes: [9, 8, 7],
            message: "Non-canonical register form.");

        Assert.Equal(DecodeFailureCode.OperandEncoding, failure.Code);
        Assert.Equal(2, failure.SlotIndex);
        Assert.Equal("rs2", failure.Field);
        Assert.Equal(64, failure.RawHash.Length);
    }

    [Fact]
    public void CanonicalPayloadSnapshot_RoundTripsFrozenSerializedValue()
    {
        var source = new SnapshotRoundTripValue(7, "payload", true);
        CanonicalPayloadSnapshot snapshot = CanonicalPayloadSnapshot.FromObject("test", source);

        Assert.Equal(source, snapshot.Deserialize<SnapshotRoundTripValue>());
        Assert.Equal(64, snapshot.ContentSha256.Length);
    }

    private static InstructionIR CreateAddIr() => new()
    {
        CanonicalOpcode = IsaOpcode.FromRawValue(IsaOpcodeValues.ADD),
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 3,
        Rs1 = 1,
        Rs2 = 2,
        Imm = 0,
    };

    private static CanonicalDecodeContext CreateReplayContext() => new()
    {
        ManifestVersion = "1.0",
        ManifestHash = "manifest-a",
        ExtensionConfigurationFingerprint = "extensions-a",
        DecoderEpoch = "epoch-a",
        DecoderVersion = "decoder-a",
        PrivilegeContext = "user",
        DomainIdentity = "domain-a",
        AddressSpaceIdentity = "as-a",
        VectorConfigurationFingerprint = "vector-a",
        ExecutableMemoryInvalidationEpoch = 8,
        CodeGenerationEpoch = 3,
        IsReplayEligible = true,
    };

    private sealed record SnapshotRoundTripValue(int Slot, string Name, bool Enabled);
}
