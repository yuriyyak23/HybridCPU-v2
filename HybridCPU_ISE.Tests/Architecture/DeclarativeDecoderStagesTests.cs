using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class DeclarativeDecoderStagesTests
{
    [Fact]
    public void RawBundleReader_CopiesExactlyEightSlotsWithoutSemanticMutation()
    {
        var input = new VLIW_Instruction[8];
        input[0].OpCode = IsaOpcodeValues.ADD;
        input[0].Immediate = 17;

        RawBundle bundle = RawBundleReader.Read(input);
        input[0].OpCode = IsaOpcodeValues.SUB;
        input[0].Immediate = 99;

        Assert.Equal(8, bundle.Slots.Length);
        Assert.Equal(IsaOpcodeValues.ADD, bundle.Slots[0].Opcode);
        Assert.Equal(17u, bundle.Slots[0].Instruction.Immediate);
    }

    [Fact]
    public void RawBundleReader_RejectsNonEightSlotInput()
    {
        Assert.Throws<ArgumentException>(() => RawBundleReader.Read(new VLIW_Instruction[7]));
    }

    [Fact]
    public void OpcodeDescriptorLookup_UsesGeneratedCatalogAndReturnsTypedUnknownFailure()
    {
        var legal = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD };
        RawSlot legalSlot = RawSlotReader.Read(in legal, 0);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in legalSlot, out var descriptor, out var legalFailure));
        Assert.Equal("ADD", descriptor.Mnemonic);
        Assert.Null(legalFailure);

        var illegal = new VLIW_Instruction { OpCode = 0xFFFE };
        RawSlot illegalSlot = RawSlotReader.Read(in illegal, 3);
        Assert.False(OpcodeDescriptorLookup.TryLookup(in illegalSlot, out _, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.UnknownOpcode, failure!.Code);
        Assert.Equal(3, failure.SlotIndex);
        Assert.Equal("opcode", failure.Field);
        Assert.False(string.IsNullOrWhiteSpace(failure.RawHash));
    }

    [Theory]
    [InlineData(14u)]
    [InlineData(15u)]
    [InlineData(18u)]
    [InlineData(147u)]
    [InlineData(148u)]
    public void OpcodeDescriptorLookup_MapsProhibitedRawAliasesBeforeUnknownOpcode(uint rawOpcode)
    {
        var instruction = new VLIW_Instruction { OpCode = rawOpcode };
        RawSlot slot = RawSlotReader.Read(in instruction, 2);

        Assert.False(OpcodeDescriptorLookup.TryLookup(in slot, out _, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ProhibitedOpcode, failure!.Code);
        Assert.Equal("opcode", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        InvalidOpcodeException legacyFailure = Assert.Throws<InvalidOpcodeException>(
            () => legacyDecoder.Decode(in instruction, 2));
        Assert.True(legacyFailure.IsProhibited);
    }

    [Theory]
    [InlineData(45u)]
    [InlineData(52u)]
    [InlineData(55u)]
    public void OpcodeDescriptorLookup_MapsKnownUnsupportedOptionalContoursBeforeUnknownOpcode(uint rawOpcode)
    {
        var instruction = new VLIW_Instruction { OpCode = rawOpcode };
        RawSlot slot = RawSlotReader.Read(in instruction, 4);

        Assert.False(OpcodeDescriptorLookup.TryLookup(in slot, out _, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.UnsupportedOpcode, failure!.Code);
        Assert.Equal("opcode", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        InvalidOpcodeException legacyFailure = Assert.Throws<InvalidOpcodeException>(
            () => legacyDecoder.Decode(in instruction, 4));
        Assert.False(legacyFailure.IsProhibited);
    }

    public static IEnumerable<object[]> StaticDecodeMutationCorpus()
    {
        yield return Mutation(
            "non-atomic-acquire",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Acquire = true },
            DecodeFailureCode.ReservedEncoding,
            "AcquireRelease");
        yield return Mutation(
            "non-vadd-saturating",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Saturating = true },
            DecodeFailureCode.ReservedEncoding,
            "Saturating");
        yield return Mutation(
            "non-reduction-reduction-bit",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Reduction = true },
            DecodeFailureCode.ReservedEncoding,
            "Reduction");
        yield return Mutation(
            "scalar-indexed-vector-bit",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Indexed = true },
            DecodeFailureCode.ReservedEncoding,
            "Indexed");
        yield return Mutation(
            "scalar-2d-vector-bit",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Is2D = true },
            DecodeFailureCode.ReservedEncoding,
            "Is2D");
        yield return Mutation(
            "scalar-tail-agnostic-vector-bit",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, TailAgnostic = true },
            DecodeFailureCode.ReservedEncoding,
            "TailMaskAgnostic");
        yield return Mutation(
            "scalar-mask-agnostic-vector-bit",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, MaskAgnostic = true },
            DecodeFailureCode.ReservedEncoding,
            "TailMaskAgnostic");
        yield return Mutation(
            "fence-nonzero-payload",
            new VLIW_Instruction { OpCode = IsaOpcodeValues.FENCE, Immediate = 1 },
            DecodeFailureCode.ReservedEncoding,
            "FencePayload");
        yield return Mutation(
            "scalar-immediate-register-alias",
            new VLIW_Instruction
            {
                OpCode = IsaOpcodeValues.SLLIW,
                Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
            },
            DecodeFailureCode.ReservedEncoding,
            "rs2");
    }

    [Theory]
    [MemberData(nameof(StaticDecodeMutationCorpus))]
    public void DeclarativeDecoderPipeline_StaticExtensionMutationCorpusHasTypedFailureAndLegacyRejectParity(
        string mutationId,
        VLIW_Instruction instruction,
        DecodeFailureCode expectedCode,
        string expectedField)
    {
        RawSlot slot = RawSlotReader.Read(in instruction, 1);

        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(expectedCode, failure!.Code);
        Assert.Equal(expectedField, failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        Exception legacyFailure = Assert.ThrowsAny<Exception>(() => legacyDecoder.Decode(in instruction, 1));
        Assert.True(
            legacyFailure is InvalidOpcodeException or InvalidOperationException,
            $"{mutationId} produced unexpected legacy exception type {legacyFailure.GetType().FullName}.");
    }

    [Fact]
    public void MatrixTileProjectionFaultsRemainTypedPostDecodeOutcomesRatherThanDecodeFailures()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.MTILE_LOAD,
            DataType = (byte)DataTypeEnum.INT32,
            StreamLength = 0,
            DestSrc1Pointer = 0x1000,
            Src2Pointer = 0x2000,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 3);

        Assert.True(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.NotNull(decoded);
        Assert.Null(failure);

        InstructionIR legacyProjection = new VliwDecoderV4().Decode(in instruction, 3);
        Assert.True(legacyProjection.MatrixTileProjection.HasValue);
        Assert.Equal(
            MatrixTileIrProjectionFaultKind.InvalidShapeEncoding,
            legacyProjection.MatrixTileProjection.Value.FaultKind);
    }

    [Fact]
    public void DeclarativeDecoderPipeline_BundlePreservesCanonicalEmptySlotsAndAcceptedLegacyParity()
    {
        var bundle = new VLIW_Instruction[8];
        bundle[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
        };
        bundle[3] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.VADD,
            DestSrc1Pointer = 0x1000,
            Src2Pointer = 0x2000,
            StreamLength = 8,
            DataType = (byte)DataTypeEnum.INT32,
        };

        Assert.True(DeclarativeDecoderPipeline.TryDecodeBundle(
            bundle,
            bundleAnnotations: null,
            bundleAddress: 0x5200,
            bundleSerial: 17,
            out var declarative,
            out var failure));
        Assert.Null(failure);
        Assert.NotNull(declarative);
        Assert.Equal(8, declarative!.Slots.Length);
        Assert.True(declarative.CanonicalBundle.GetSlot(0).IsOccupied);
        Assert.True(declarative.CanonicalBundle.GetSlot(3).IsOccupied);
        Assert.False(declarative.CanonicalBundle.GetSlot(1).IsOccupied);
        Assert.Equal(0u, declarative.CanonicalBundle.GetSlot(1).Opcode);
        Assert.Null(declarative.Slots[1]);

        DecodedInstructionBundle legacy = new VliwDecoderV4().DecodeInstructionBundle(
            bundle,
            bundleAddress: 0x5200,
            bundleSerial: 17);
        for (int slotIndex = 0; slotIndex < bundle.Length; slotIndex++)
        {
            Assert.Equal(
                legacy.GetDecodedSlot(slotIndex).IsOccupied,
                declarative.CanonicalBundle.GetSlot(slotIndex).IsOccupied);
        }
    }

    [Fact]
    public void DeclarativeDecoderPipeline_BundleRejectsNonCanonicalEmptySlotWithLegacyParity()
    {
        var bundle = new VLIW_Instruction[8];
        bundle[4].Immediate = 1;

        Assert.False(DeclarativeDecoderPipeline.TryDecodeBundle(
            bundle,
            bundleAnnotations: null,
            bundleAddress: 0x5210,
            bundleSerial: 0,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.BundleShape, failure!.Code);
        Assert.Equal("empty-slot", failure.Field);

        Assert.Throws<InvalidOpcodeException>(() => new VliwDecoderV4().DecodeInstructionBundle(
            bundle,
            bundleAddress: 0x5210));
    }

    [Fact]
    public void DeclarativeDecoderPipeline_SidebandMutationCorpusMapsStaticRejectsWithLegacyParity()
    {
        DmaStreamComputeDescriptor dmaDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        AcceleratorCommandDescriptor acceleratorDescriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor();

        AssertSidebandReject(
            CreateBundle(6, new VLIW_Instruction { OpCode = IsaOpcodeValues.DmaStreamCompute }),
            annotations: null,
            "DmaStreamComputeDescriptor");

        AssertSidebandReject(
            CreateBundle(5, new VLIW_Instruction { OpCode = IsaOpcodeValues.DmaStreamCompute }),
            annotations: null,
            "slot");

        AssertSidebandReject(
            CreateBundle(7, new VLIW_Instruction { OpCode = IsaOpcodeValues.ACCEL_SUBMIT }),
            annotations: null,
            "AcceleratorCommandDescriptor");

        AssertSidebandReject(
            CreateBundle(7, new VLIW_Instruction { OpCode = IsaOpcodeValues.ACCEL_POLL }),
            CreateAnnotations(7, InstructionSlotMetadata.Default with
            {
                AcceleratorCommandDescriptor = acceleratorDescriptor,
            }),
            "AcceleratorCommandDescriptor");

        AssertSidebandReject(
            new VLIW_Instruction[8],
            CreateAnnotations(2, InstructionSlotMetadata.Default with
            {
                DmaStreamComputeDescriptor = dmaDescriptor,
            }),
            "DescriptorSideband");
    }

    [Fact]
    public void OperandDecoder_ProjectsCanonicalPackedRegisterAbi()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Immediate = 0xFFF9,
            Word1 = VLIW_Instruction.PackArchRegs(3, 4, 5),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 2);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out var lookupFailure));
        Assert.Null(lookupFailure);

        Assert.True(OperandDecoder.TryDecode(in slot, in descriptor, out var operands, out var failure));
        Assert.Null(failure);
        Assert.Equal((byte)3, operands.Rd);
        Assert.Equal((byte)4, operands.Rs1);
        Assert.Equal((byte)5, operands.Rs2);
        Assert.Equal(-7, operands.Immediate);
    }

    [Fact]
    public void OperandDecoder_ReturnsTypedFailureForMalformedPackedRegisterAbi()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Word1 = 0x0040UL,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 6);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

        Assert.False(OperandDecoder.TryDecode(in slot, in descriptor, out _, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.OperandEncoding, failure!.Code);
        Assert.Equal(6, failure.SlotIndex);
        Assert.Equal("Word1", failure.Field);
    }

    [Fact]
    public void EncodingConstraintValidator_RejectsScalarImmediateRegisterAliasWithTypedFailure()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.SLLIW,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 1);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));
        Assert.True(OperandDecoder.TryDecode(in slot, in descriptor, out var operands, out _));

        Assert.False(EncodingConstraintValidator.TryValidate(
            in slot,
            in descriptor,
            in operands,
            out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal(1, failure.SlotIndex);
        Assert.Equal("rs2", failure.Field);
    }

    [Fact]
    public void EncodingConstraintValidator_RejectsOutOfRangeImm6WithTypedFailure()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ROLI,
            Immediate = 64,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 0),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 4);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));
        Assert.True(OperandDecoder.TryDecode(in slot, in descriptor, out var operands, out _));

        Assert.False(EncodingConstraintValidator.TryValidate(
            in slot,
            in descriptor,
            in operands,
            out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal("Immediate", failure.Field);
    }

    [Fact]
    public void EncodingConstraintValidator_RegistersStableNamedCSharpConstraintFamilies()
    {
        Assert.Contains("scalar-unary-rs2-and-immediate-zero", EncodingConstraintValidator.RegisteredConstraintIds);
        Assert.Contains("counter-read-source-and-immediate-zero", EncodingConstraintValidator.RegisteredConstraintIds);
        Assert.Contains("control-flow-no-legacy-src2-target", EncodingConstraintValidator.RegisteredConstraintIds);
        Assert.Equal(
            EncodingConstraintValidator.RegisteredConstraintIds.Count,
            EncodingConstraintValidator.RegisteredConstraintIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("opcode-flag-legality", EncodingConstraintValidator.RegisteredRawFormConstraintIds);
        Assert.Contains("fence-zero-payload", EncodingConstraintValidator.RegisteredRawFormConstraintIds);
    }

    [Fact]
    public void EncodingConstraintValidator_RejectsRawAtomicFlagOnNonAtomicOpcode()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Acquire = true,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 2);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

        Assert.False(EncodingConstraintValidator.TryValidateRawForm(in slot, in descriptor, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal("AcquireRelease", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        Assert.Throws<InvalidOpcodeException>(() => legacyDecoder.Decode(in instruction, 2));
    }

    [Fact]
    public void EncodingConstraintValidator_RejectsNonCanonicalFencePayload()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.FENCE,
            Immediate = 1,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 7);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

        Assert.False(EncodingConstraintValidator.TryValidateRawForm(in slot, in descriptor, out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal("FencePayload", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        Assert.Throws<InvalidOpcodeException>(() => legacyDecoder.Decode(in instruction, 7));
    }

    [Fact]
    public void ExtensionPayloadDecoder_ProjectsVectorPayloadFromFrozenRawSlotAndSideband()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.VADD,
            DestSrc1Pointer = 0x1000,
            Src2Pointer = 0x2000,
            StreamLength = 32,
            Stride = 8,
            RowStride = 64,
            Indexed = true,
            Is2D = true,
            TailAgnostic = true,
            MaskAgnostic = true,
            Saturating = true,
            PredicateMask = 0x3,
            DataType = 2,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 3);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

        Assert.True(ExtensionPayloadDecoder.TryDecode(
            in slot,
            in descriptor,
            InstructionSlotMetadata.Default,
            out var payload,
            out var failure));
        Assert.Null(failure);
        Assert.True(payload.HasValue);
        Assert.Equal(0x1000UL, payload.Value.PrimaryPointer);
        Assert.Equal(0x2000UL, payload.Value.SecondaryPointer);
        Assert.Equal(32u, payload.Value.StreamLength);
        Assert.True(payload.Value.Indexed);
        Assert.True(payload.Value.Is2D);
        Assert.True(payload.Value.Saturating);
    }

    [Fact]
    public void SidebandValidator_RejectsDmaCarrierOutsideItsFixedLaneWithTypedFailure()
    {
        var instruction = new VLIW_Instruction { OpCode = IsaOpcodeValues.DmaStreamCompute };
        RawSlot slot = RawSlotReader.Read(in instruction, 0);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

        Assert.False(SidebandValidator.TryValidate(
            in slot,
            in descriptor,
            InstructionSlotMetadata.Default,
            out var failure));
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.Sideband, failure!.Code);
        Assert.Equal(0, failure.SlotIndex);
        Assert.Equal("slot", failure.Field);
    }

    [Fact]
    public void CanonicalInstructionIrBuilder_ProducesImmutableSemanticSlotWithoutLegacyIr()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Immediate = 7,
            Word1 = VLIW_Instruction.PackArchRegs(3, 4, 5),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 5);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));
        Assert.True(OperandDecoder.TryDecode(in slot, in descriptor, out var operands, out _));
        Assert.True(ExtensionPayloadDecoder.TryDecode(
            in slot,
            in descriptor,
            InstructionSlotMetadata.Default,
            out var extensionPayload,
            out _));

        Assert.True(CanonicalInstructionIrBuilder.TryBuild(
            in slot,
            in descriptor,
            in operands,
            extensionPayload,
            InstructionSlotMetadata.Default,
            out var canonical,
            out var failure));
        Assert.Null(failure);
        Assert.True(canonical.IsOccupied);
        Assert.Equal((uint)IsaOpcodeValues.ADD, canonical.Opcode);
        Assert.Equal((byte)3, canonical.Rd);
        Assert.Equal((byte)4, canonical.Rs1);
        Assert.Equal((byte)5, canonical.Rs2);
        Assert.Equal(7, canonical.Immediate);
        Assert.Equal("DeclarativeInstructionSemantics", canonical.InstructionPayload.Kind);
    }

    [Fact]
    public void DeclarativeDecoderPipeline_ProducesFrozenSlotForLegalScalarEncoding()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 0);

        Assert.True(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(failure);
        Assert.NotNull(decoded);
        Assert.Equal("ADD", decoded!.Descriptor.Mnemonic);
        Assert.Equal((byte)1, decoded.Operands.Rd);
        Assert.Equal((uint)IsaOpcodeValues.ADD, decoded.CanonicalInstruction.Opcode);
    }

    [Fact]
    public void DeclarativeDecoderPipeline_ReturnsTypedFailureForIllegalRawEncoding()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Acquire = true,
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 6);

        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal("AcquireRelease", failure.Field);
    }

    [Fact]
    public void DeclarativeDecoderPipeline_RejectsDscStatusNonZeroRs2WithLegacyParity()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.DSC_STATUS,
            Word1 = VLIW_Instruction.PackArchRegs(5, 4, 3),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 6);

        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
        Assert.Equal("rs2", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        Assert.Throws<InvalidOpcodeException>(() => legacyDecoder.Decode(in instruction, 6));
    }

    [Fact]
    public void DeclarativeDecoderPipeline_AcceptsCleanDscQueryCapsCarrier()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.DSC_QUERY_CAPS,
            Word1 = VLIW_Instruction.PackArchRegs(5, 0, 0),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 6);

        Assert.True(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(failure);
        Assert.NotNull(decoded);
        Assert.Equal((byte)5, decoded!.Operands.Rd);
        Assert.Equal((byte)0, decoded.Operands.Rs1);
        Assert.Equal((byte)0, decoded.Operands.Rs2);
    }

    [Fact]
    public void DeclarativeDecoderPipeline_RejectsAcceleratorCarrierOutsideLane7WithLegacyParity()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ACCEL_QUERY_CAPS,
            Word1 = VLIW_Instruction.PackArchRegs(5, 0, 0),
        };
        RawSlot slot = RawSlotReader.Read(in instruction, 6);

        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.Sideband, failure!.Code);
        Assert.Equal("AcceleratorCarrier", failure.Field);

        var legacyDecoder = new VliwDecoderV4();
        Assert.Throws<InvalidOpcodeException>(() => legacyDecoder.Decode(in instruction, 6));
    }

    private static object[] Mutation(
        string mutationId,
        VLIW_Instruction instruction,
        DecodeFailureCode expectedCode,
        string expectedField) =>
    [
        mutationId,
        instruction,
        expectedCode,
        expectedField,
    ];

    private static void AssertSidebandReject(
        VLIW_Instruction[] bundle,
        VliwBundleAnnotations? annotations,
        string expectedField)
    {
        Assert.False(DeclarativeDecoderPipeline.TryDecodeBundle(
            bundle,
            annotations,
            bundleAddress: 0x5300,
            bundleSerial: 0,
            out var decoded,
            out var failure));
        Assert.Null(decoded);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.Sideband, failure!.Code);
        Assert.Equal(expectedField, failure.Field);

        Assert.Throws<InvalidOpcodeException>(() => new VliwDecoderV4().DecodeInstructionBundle(
            bundle,
            annotations,
            bundleAddress: 0x5300));
    }

    private static VLIW_Instruction[] CreateBundle(int slotIndex, VLIW_Instruction instruction)
    {
        var bundle = new VLIW_Instruction[8];
        bundle[slotIndex] = instruction;
        return bundle;
    }

    private static VliwBundleAnnotations CreateAnnotations(
        int slotIndex,
        InstructionSlotMetadata slotMetadata)
    {
        var metadata = new InstructionSlotMetadata[8];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        metadata[slotIndex] = slotMetadata;
        return new VliwBundleAnnotations(metadata);
    }
}
