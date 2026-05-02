using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Accelerators;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcNativeCarrierValidationTests
{
    [Fact]
    public void L7SdcNativeCarrierValidation_AccelSubmitWithoutSideband_Rejects()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0xA000));

        Assert.Contains("ACCEL_SUBMIT", ex.Message, StringComparison.Ordinal);
        Assert.Contains("typed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(AcceleratorCommandDescriptor), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_CleanLane7SubmitWithSideband_Decodes()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            rawSlots,
            CreateAnnotations(7, descriptor, CreateSystemSingletonSlotMetadata()),
            bundleAddress: 0xA010);

        InstructionIR instruction = decoded.GetDecodedSlot(7).RequireInstruction();
        Assert.Same(descriptor, instruction.AcceleratorCommandDescriptor);
        Assert.Equal(descriptor.DescriptorReference, instruction.AcceleratorCommandDescriptorReference);
        Assert.Equal(InstructionClass.System, instruction.Class);
        Assert.Equal(SerializationClass.MemoryOrdered, instruction.SerializationClass);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_SidebandOnWrongLane_Rejects()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                CreateAnnotations(6, descriptor, CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0xA020));

        Assert.Contains("lane 7", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("slot 6", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_SidebandOnWrongSlotClass_Rejects()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                CreateAnnotations(7, descriptor, CreateDmaStreamSlotMetadata()),
                bundleAddress: 0xA030));

        Assert.Contains("SystemSingleton", ex.Message, StringComparison.Ordinal);
        Assert.Contains("lane7", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_SidebandOnWrongOpcode_Rejects()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_POLL);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                CreateAnnotations(7, descriptor, CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0xA040));

        Assert.Contains(nameof(AcceleratorCommandDescriptor), ex.Message, StringComparison.Ordinal);
        Assert.Contains("ACCEL_SUBMIT", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("reserved")]
    [InlineData("vt-hint")]
    [InlineData("raw-pointer")]
    [InlineData("word3-policy-gap")]
    public void L7SdcNativeCarrierValidation_DirtyRawCarrierFields_Reject(string mutation)
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        switch (mutation)
        {
            case "reserved":
                rawSlots[7].Reserved = 1;
                break;
            case "vt-hint":
                rawSlots[7].VirtualThreadId = 1;
                break;
            case "raw-pointer":
                rawSlots[7].Src2Pointer = 0x9000;
                break;
            case "word3-policy-gap":
                rawSlots[7].Word3 |= VLIW_Instruction.RetiredPolicyGapMask;
                break;
        }

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                CreateAnnotations(7, descriptor, CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0xA050));

        Assert.Contains("slot 7", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_AccelSubmitStillCannotDecodeLane6()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                CreateAnnotations(6, descriptor, CreateDmaStreamSlotMetadata()),
                bundleAddress: 0xA060));

        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), ex.Message, StringComparison.Ordinal);
        Assert.Contains("lane 7", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_CustomRegistryOpcodeIsNotCarrierAuthority()
    {
        InstructionRegistry.Clear();
        InstructionRegistry.Initialize();

        try
        {
            InstructionRegistry.RegisterAccelerator(new MatMulAccelerator());
            Assert.True(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));
            Assert.False(OpcodeRegistry.IsSystemDeviceCommandOpcode(0xC000));

            var raw = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);
            raw.OpCode = 0xC000;

            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => new VliwDecoderV4().Decode(in raw, slotIndex: 7));

            Assert.Contains("custom accelerator", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(nameof(AcceleratorSubmitMicroOp), ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            InstructionRegistry.Clear();
            InstructionRegistry.Initialize();
        }
    }

    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcNativeCarrierValidation_DirectExecuteForEveryCarrier_RemainsFailClosed(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass serialization,
        Type carrierType)
    {
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        Assert.Equal(serialization, carrier.SerializationClass);
        Assert.Equal(carrierType, carrier.GetType());
        AssertCarrierExecuteFailsWithoutMemoryMutation(carrier);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_DescriptorBackedSubmitExecute_RemainsFailClosed()
    {
        AcceleratorSubmitMicroOp submit = new(
            L7SdcTestDescriptorFactory.ParseValidDescriptor());

        Assert.NotNull(submit.CommandDescriptor);
        AssertCarrierExecuteFailsWithoutMemoryMutation(submit);
    }

    [Fact]
    public void L7SdcNativeCarrierValidation_DmaStreamComputeLane6Path_RemainsSeparate()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.DmaStreamCompute,
            VirtualThreadId = 0
        };
        VliwBundleAnnotations annotations = CreateDmaAnnotations(descriptor);

        DecodedInstructionBundle decoded =
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0xB000);
        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);

        DmaStreamComputeMicroOp dma = Assert.IsType<DmaStreamComputeMicroOp>(carriers[6]);
        Assert.Same(descriptor, dma.Descriptor);
        Assert.Equal(SlotClass.DmaStreamClass, dma.Placement.RequiredSlotClass);
        Assert.NotEqual(SlotClass.SystemSingleton, dma.Placement.RequiredSlotClass);
    }

    internal static VliwBundleAnnotations CreateAnnotations(
        int slotIndex,
        AcceleratorCommandDescriptor descriptor,
        SlotMetadata? slotMetadata = null)
    {
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        metadata[slotIndex] = new InstructionSlotMetadata(
            VtId.Create(0),
            slotMetadata ?? CreateSystemSingletonSlotMetadata())
        {
            AcceleratorCommandDescriptor = descriptor
        };

        return new VliwBundleAnnotations(metadata);
    }

    internal static SlotMetadata CreateSystemSingletonSlotMetadata()
    {
        return new SlotMetadata
        {
            StealabilityPolicy = StealabilityPolicy.NotStealable,
            AdmissionMetadata = MicroOpAdmissionMetadata.Default with
            {
                IsStealable = false,
                HasSideEffects = true,
                Placement = new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.SystemSingleton,
                    PinningKind = SlotPinningKind.HardPinned,
                    PinnedLaneId = 7,
                    DomainTag = 0
                }
            }
        };
    }

    internal static SlotMetadata CreateDmaStreamSlotMetadata()
    {
        return new SlotMetadata
        {
            StealabilityPolicy = StealabilityPolicy.NotStealable,
            AdmissionMetadata = MicroOpAdmissionMetadata.Default with
            {
                IsStealable = false,
                HasSideEffects = true,
                Placement = new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.DmaStreamClass,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = 0
                }
            }
        };
    }

    private static VliwBundleAnnotations CreateDmaAnnotations(
        DmaStreamComputeDescriptor descriptor)
    {
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        metadata[6] = new InstructionSlotMetadata(
            VtId.Create(0),
            SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = descriptor
        };
        return new VliwBundleAnnotations(metadata);
    }

    private static void AssertCarrierExecuteFailsWithoutMemoryMutation(MicroOp carrier)
    {
        Processor.MainMemoryArea previousMainMemory = Processor.MainMemory;
        var previousMemorySubsystem = Processor.Memory;
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x1000);
        Processor.Memory = null;
        byte[] original = { 0x3D, 0x7A, 0x88, 0x21 };
        try
        {
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x100, original));
            var core = new Processor.CPU_Core(0);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => carrier.Execute(ref core));

            Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("backend execution", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("token lifecycle", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("staged writes", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("commit", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(carrier.TryGetPrimaryWriteBackResult(out ulong value));
            Assert.Equal(0UL, value);

            byte[] observed = new byte[original.Length];
            Assert.True(Processor.MainMemory.TryReadPhysicalRange(0x100, observed));
            Assert.Equal(original, observed);
        }
        finally
        {
            Processor.MainMemory = previousMainMemory;
            Processor.Memory = previousMemorySubsystem;
        }
    }
}
