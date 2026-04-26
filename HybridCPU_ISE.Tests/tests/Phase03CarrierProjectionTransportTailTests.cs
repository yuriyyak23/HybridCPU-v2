using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CarrierProjectionTransportTailTests
{
    [Fact]
    public void LegacySlotCarrierMaterializer_AtomicProjection_PreservesCanonicalRegisterHazards()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(new[] { 4, 5 }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 3 }, slotDescriptor.WriteRegisters);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.True(slotDescriptor.IsMemoryOp);
        Assert.True(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_LegacyLoadProjection_RejoinsTypedLoadCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateLegacyAbsoluteLoadInstruction(destinationRegister: 7, address: 0x280));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 7 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.True(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(0x280UL, microOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.BaseRegID);
        Assert.Equal((ushort)7, microOp.DestRegID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_LegacyStoreProjection_RejoinsTypedStoreCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateLegacyAbsoluteStoreInstruction(sourceRegister: 6, address: 0x2C0));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 6 }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);
        Assert.Equal(0x2C0UL, microOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.BaseRegID);
        Assert.Equal((ushort)6, microOp.SrcRegID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CompilerEmittedAbsoluteMoveLoad_CanonicalizesToTypedLoadCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateCompilerEmittedAbsoluteLoadMoveInstruction(destinationRegister: 8, address: 0x300));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal((uint)InstructionsEnum.Load, slotDescriptor.OpCode);
        Assert.True(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 8 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(0x300UL, microOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.BaseRegID);
        Assert.Equal((ushort)8, microOp.DestRegID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CanonicalAbsoluteLoadAtZeroAddress_DoesNotReopenRawImmediateWrapperAuthority()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateLegacyAbsoluteLoadInstruction(destinationRegister: 7, address: 0));
        rawSlots[0].Immediate = 0x1234;

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x2000,
            bundleSerial: 38,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = InstructionsEnum.Load,
                        Class = InstructionClass.Memory,
                        SerializationClass = SerializationClass.Free,
                        Rd = 7,
                        Rs1 = VLIW_Instruction.NoArchReg,
                        Rs2 = VLIW_Instruction.NoArchReg,
                        Imm = 0,
                        HasAbsoluteAddressing = true
                    })
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(carrierBundle[0]);
        Assert.Equal(0UL, microOp.Address);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CompilerEmittedAbsoluteMoveStore_CanonicalizesToTypedStoreCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateCompilerEmittedAbsoluteStoreMoveInstruction(sourceRegister: 9, address: 0x340));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal((uint)InstructionsEnum.Store, slotDescriptor.OpCode);
        Assert.True(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 9 }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);
        Assert.Equal(0x340UL, microOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.BaseRegID);
        Assert.Equal((ushort)9, microOp.SrcRegID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CanonicalAbsoluteStoreAtZeroAddress_DoesNotReopenRawImmediateWrapperAuthority()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateLegacyAbsoluteStoreInstruction(sourceRegister: 6, address: 0));
        rawSlots[0].Immediate = 0x5678;

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x2000,
            bundleSerial: 39,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = InstructionsEnum.Store,
                        Class = InstructionClass.Memory,
                        SerializationClass = SerializationClass.MemoryOrdered,
                        Rd = VLIW_Instruction.NoArchReg,
                        Rs1 = VLIW_Instruction.NoArchReg,
                        Rs2 = 6,
                        Imm = 0,
                        HasAbsoluteAddressing = true
                    })
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(carrierBundle[0]);
        Assert.Equal(0UL, microOp.Address);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_RetainedRegisterMoveProjection_PreservesScalarCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateRetainedRegisterMoveInstruction(sourceRegister: 5, destinationRegister: 7));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        MoveMicroOp microOp = Assert.IsType<MoveMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 5 }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 7 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_RetainedImmediateMoveProjection_PreservesScalarCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateRetainedImmediateMoveInstruction(destinationRegister: 9, immediate: 0x1234));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        MoveMicroOp microOp = Assert.IsType<MoveMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 9 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_ExplicitNopeProjection_PreservesUnclassifiedEmptyCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateExplicitNopeInstruction());

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        NopMicroOp microOp = Assert.IsType<NopMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsEmptyOrNop);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(SlotClass.Unclassified, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(SlotClass.Unclassified, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_JalrProjection_PreservesCanonicalLinkAndBaseRegisterFacts()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.JALR, rd: 1, rs1: 2));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 2 }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 1 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal((ushort)1, microOp.DestRegID);
        Assert.Equal((ushort)2, microOp.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.Reg2ID);
        Assert.Equal(0UL, microOp.TargetAddress);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_JalTargetProjection_UsesCanonicalPcRelativeImmediate()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.JAL, rd: 1, immediate: 0x24));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(0x2024UL, microOp.TargetAddress);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_BeqProjection_PreservesCanonicalCompareRegisterFacts()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.BEQ, rd: 0, rs1: 1, rs2: 2));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 1, 2 }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
        Assert.Equal((ushort)1, microOp.Reg1ID);
        Assert.Equal((ushort)2, microOp.Reg2ID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_BeqTargetProjection_UsesCanonicalPcRelativeImmediateWhenRawTargetIsMissing()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.BEQ, rs1: 1, rs2: 2, immediate: 0x40));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(0x2040UL, microOp.TargetAddress);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CompilerEmittedJumpIfAbove_CanonicalizesToUnsignedSwapBranchCarrierTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateCompilerEmittedLegacyConditionalBranchInstruction(
                InstructionsEnum.JumpIfAbove,
                accumulatorRegister: 6,
                firstOperandRegister: 10,
                secondOperandRegister: 11,
                targetAddress: 0x350));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal((uint)InstructionsEnum.BLTU, slotDescriptor.OpCode);
        Assert.True(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 11, 10 }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
        Assert.Equal((ushort)11, microOp.Reg1ID);
        Assert.Equal((ushort)10, microOp.Reg2ID);
        Assert.Equal(0x350UL, microOp.TargetAddress);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CompilerEmittedJumpIfAbove_PrefersCanonicalAbsoluteBranchTargetOverRawWrapperSurface()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateCompilerEmittedLegacyConditionalBranchInstruction(
                InstructionsEnum.JumpIfAbove,
                accumulatorRegister: 6,
                firstOperandRegister: 10,
                secondOperandRegister: 11,
                targetAddress: 0x900));
        rawSlots[0].Src2Pointer = 0x100;

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x2000,
            bundleSerial: 40,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = InstructionsEnum.BLTU,
                        Class = InstructionClass.ControlFlow,
                        SerializationClass = SerializationClass.Free,
                        Rd = VLIW_Instruction.NoArchReg,
                        Rs1 = 11,
                        Rs2 = 10,
                        Imm = 0x900,
                        HasAbsoluteAddressing = true
                    })
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(carrierBundle[0]);
        Assert.Equal(0x900UL, microOp.TargetAddress);
        Assert.Equal((ushort)11, microOp.Reg1ID);
        Assert.Equal((ushort)10, microOp.Reg2ID);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CompilerEmittedJumpIfAbove_PublishesCanonicalProjectedBranchFactoryPayload()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateCompilerEmittedLegacyConditionalBranchInstruction(
                InstructionsEnum.JumpIfAbove,
                accumulatorRegister: 6,
                firstOperandRegister: 10,
                secondOperandRegister: 11,
                targetAddress: 0x350));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(
            rawSlots,
            RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder());
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal((uint)InstructionsEnum.BLTU, microOp.OpCode);
        Assert.Equal(0x350UL, microOp.TargetAddress);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
        Assert.Equal((ushort)11, microOp.Reg1ID);
        Assert.Equal((ushort)10, microOp.Reg2ID);
        Assert.Equal(new[] { 11, 10 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_Beq_PublishesCanonicalPcRelativeBranchFactoryPayload()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.BEQ, rs1: 1, rs2: 2, immediate: 0x40));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal((uint)InstructionsEnum.BEQ, microOp.OpCode);
        Assert.True(microOp.HasRelativeTargetDisplacement);
        Assert.Equal((short)0x40, microOp.RelativeTargetDisplacement);
        Assert.Equal(0x2040UL, microOp.TargetAddress);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_ScalarImmediateProjection_PreservesCanonicalSignExtendedImmediate()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 2, immediate: 0xFFFF));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 2 }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 7 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal((ushort)7, microOp.DestRegID);
        Assert.Equal((ushort)2, microOp.Src1RegID);
        Assert.Equal(unchecked((ulong)-1L), microOp.Immediate);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_SltProjection_PreservesCanonicalTwoSourceCompareFacts()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.SLT, rd: 4, rs1: 5, rs2: 6, immediate: 0x1234));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 5, 6 }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 4 }, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.UsesImmediate);
        Assert.Equal((ushort)5, microOp.Src1RegID);
        Assert.Equal((ushort)6, microOp.Src2RegID);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_StreamWaitProjection_NoLongerSurfacesAsMemoryCarrier()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_WAIT));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
    }

    [Theory]
    [InlineData(InstructionsEnum.STREAM_SETUP, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_START, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_WAIT, InstructionClass.SmtVt)]
    public void LegacySlotCarrierMaterializer_StreamControlProjection_PreservesCanonicalSingletonTransportFacts(
        InstructionsEnum opcode,
        InstructionClass expectedClass)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(opcode));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(expectedClass, microOp.InstructionClass);
        Assert.Equal(InstructionClassifier.GetSerializationClass(opcode), microOp.SerializationClass);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Theory]
    [InlineData(InstructionsEnum.STREAM_SETUP, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_START, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_WAIT, InstructionClass.SmtVt)]
    public void DirectFactoryStreamControlCarrier_ProjectsCanonicalMetadataBeforeManualPublication(
        InstructionsEnum opcode,
        InstructionClass expectedClass)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA660);

        StreamControlMicroOp microOp = CreateDirectFactoryStreamControlMicroOp(opcode);
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(expectedClass, microOp.InstructionClass);
        Assert.Equal(InstructionClassifier.GetSerializationClass(opcode), microOp.SerializationClass);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_EcallProjection_PreservesCanonicalA7ReadFact()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.ECALL));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 17 }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(SystemEventKind.Ecall, microOp.EventKind);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Fact(Skip = "ISE gap: VLOAD not in OpcodeRegistry — canonical decoder rejects. See ise_issues.md")]
    public void LegacySlotCarrierMaterializer_VectorLoadProjection_PublishesTwoSurfaceTransferMemoryShape()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VLOAD,
                destSrc1Pointer: 0x200,
                src2Pointer: 0x300,
                streamLength: 8,
                stride: 4));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorTransferMicroOp microOp = Assert.IsType<VectorTransferMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((0x300UL, 32UL), Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
        Assert.Equal((0x200UL, 32UL), Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));
        Assert.Equal(SlotClass.AluClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slotDescriptor.Placement.PinningKind);
    }

    [Fact(Skip = "ISE gap: VSTORE not in OpcodeRegistry — canonical decoder rejects. See ise_issues.md")]
    public void LegacySlotCarrierMaterializer_VectorStoreProjection_PublishesTwoSurfaceTransferMemoryShape()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VSTORE,
                destSrc1Pointer: 0x280,
                src2Pointer: 0x380,
                streamLength: 8,
                stride: 4));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorTransferMicroOp microOp = Assert.IsType<VectorTransferMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((0x280UL, 32UL), Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
        Assert.Equal((0x380UL, 32UL), Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));
        Assert.Equal(SlotClass.AluClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slotDescriptor.Placement.PinningKind);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorBinaryProjection_PublishesInPlaceMemoryShape()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VADD,
                destSrc1Pointer: 0x220,
                src2Pointer: 0x320,
                streamLength: 4,
                stride: 4));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorBinaryOpMicroOp microOp = Assert.IsType<VectorBinaryOpMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x220UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x320UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x220UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSQRT)]
    [InlineData(InstructionsEnum.VNOT)]
    [InlineData(InstructionsEnum.VPOPCNT)]
    [InlineData(InstructionsEnum.VCLZ)]
    [InlineData(InstructionsEnum.VCTZ)]
    [InlineData(InstructionsEnum.VBREV8)]
    [InlineData(InstructionsEnum.VREVERSE)]
    public void LegacySlotCarrierMaterializer_VectorUnaryProjection_PublishesSingleSurfaceMemoryShape(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x240,
                streamLength: 4,
                stride: 4));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorUnaryOpMicroOp microOp = Assert.IsType<VectorUnaryOpMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.VPERMUTE)]
    [InlineData(InstructionsEnum.VRGATHER)]
    public void LegacySlotCarrierMaterializer_VectorPermutationProjection_PublishesIndexedMemoryShape(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x2A0,
                src2Pointer: 0x3A0,
                streamLength: 4,
                stride: 4));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorPermutationMicroOp microOp = Assert.IsType<VectorPermutationMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSLIDEUP)]
    [InlineData(InstructionsEnum.VSLIDEDOWN)]
    public void LegacySlotCarrierMaterializer_VectorSlideProjection_PublishesSingleSurfaceMemoryShape(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x2C0,
                streamLength: 4,
                stride: 4,
                immediate: 1));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorSlideMicroOp microOp = Assert.IsType<VectorSlideMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2C0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x2C0UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRWI, 0x300, 7)]
    [InlineData(InstructionsEnum.CSRRSI, 0x300, 3)]
    [InlineData(InstructionsEnum.CSRRCI, 0x300, 5)]
    public void LegacySlotCarrierMaterializer_CsrImmediateProjection_DoesNotPublishPhantomSourceRegisterRead(
        InstructionsEnum opcode,
        ushort csrAddress,
        byte immediateValue)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 9,
                rs1: immediateValue,
                immediate: csrAddress));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(slotDescriptor.MicroOp);

        switch (opcode)
        {
            case InstructionsEnum.CSRRWI:
                Assert.IsType<CsrReadWriteImmediateMicroOp>(microOp);
                break;
            case InstructionsEnum.CSRRSI:
                Assert.IsType<CsrReadSetImmediateMicroOp>(microOp);
                break;
            case InstructionsEnum.CSRRCI:
                Assert.IsType<CsrReadClearImmediateMicroOp>(microOp);
                break;
            default:
                throw new InvalidOperationException($"Unexpected CSR immediate opcode {opcode}.");
        }

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 9 }, slotDescriptor.WriteRegisters);
        Assert.Equal((ulong)csrAddress, microOp.CSRAddress);
        Assert.Equal((ulong)(immediateValue & 0x1F), microOp.WriteValue);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRS)]
    [InlineData(InstructionsEnum.CSRRC)]
    public void LegacySlotCarrierMaterializer_CsrOptionalSourceProjection_WhenRegisterSourceIsZero_DoesNotPublishPhantomRegisterRead(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 9,
                rs1: 0,
                immediate: 0x300));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 9 }, slotDescriptor.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CsrrwProjection_WhenSourceRegisterIsX0_DoesNotPublishPhantomRegisterRead()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CSRRW,
                rd: 9,
                rs1: 0,
                immediate: CsrAddresses.Mstatus));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { 9 }, slotDescriptor.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRW, 4, typeof(CsrReadWriteMicroOp))]
    [InlineData(InstructionsEnum.CSRRWI, 7, typeof(CsrReadWriteImmediateMicroOp))]
    public void LegacySlotCarrierMaterializer_CsrCarrier_WhenDestinationIsX0_DoesNotPublishPhantomWriteRegister(
        InstructionsEnum opcode,
        byte sourceOrImmediate,
        Type expectedMicroOpType)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: sourceOrImmediate,
                immediate: CsrAddresses.Mstatus));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(slotDescriptor.MicroOp);
        Assert.IsType(expectedMicroOpType, microOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorComparisonProjection_PublishesPredicateCarrierWithoutMemoryWriteShape()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VCMPEQ,
                destSrc1Pointer: 0x200,
                src2Pointer: 0x300,
                streamLength: 2,
                stride: 4,
                immediate: 5));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorComparisonMicroOp microOp = Assert.IsType<VectorComparisonMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorMaskProjection_NoLongerSurfacesAsMemoryCarrier()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VMAND,
                streamLength: 8,
                immediate: (ushort)(1 | (2 << 4) | (3 << 8))));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorMaskOpMicroOp microOp = Assert.IsType<VectorMaskOpMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal(SlotClass.AluClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slotDescriptor.Placement.PinningKind);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorMaskPopCountProjection_PublishesScalarWritebackTruth()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VPOPC,
                streamLength: 8,
                immediate: (ushort)(1 | (6 << 8))));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorMaskPopCountMicroOp microOp = Assert.IsType<VectorMaskPopCountMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { 6 }, slotDescriptor.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS)]
    [InlineData(InstructionsEnum.VEXPAND)]
    public void LegacySlotCarrierMaterializer_VectorPredicativeMovementProjection_UsesDedicatedSingleSurfaceCarrier(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x280,
                src2Pointer: 0x380,
                streamLength: 4,
                stride: 4,
                predicateMask: 0x0F));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorPredicativeMovementMicroOp microOp =
            Assert.IsType<VectorPredicativeMovementMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x280UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x280UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slotDescriptor.Placement.PinningKind);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorFmaProjection_UsesDedicatedComputeCarrier()
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedTriOpDescriptor(descriptorAddress: 0x3A0, srcAPointer: 0x4A0, srcBPointer: 0x5A0, strideA: 4, strideB: 4);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VFMADD,
                destSrc1Pointer: 0x2A0,
                src2Pointer: 0x3A0,
                streamLength: 4,
                stride: 4,
                predicateMask: 0x0F));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorFmaMicroOp microOp = Assert.IsType<VectorFmaMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Empty(slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(3, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x4A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x5A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[2]);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slotDescriptor.Placement.PinningKind);
    }

    [Theory]
    [InlineData(InstructionsEnum.VREDMAX)]
    [InlineData(InstructionsEnum.VREDMIN)]
    [InlineData(InstructionsEnum.VREDMAXU)]
    [InlineData(InstructionsEnum.VREDMINU)]
    public void LegacySlotCarrierMaterializer_VectorReductionUnsignedExtremumProjection_PreservesScalarFootprint(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x2C0,
                streamLength: 4,
                stride: 4,
                predicateMask: 0xFF,
                dataType: DataTypeEnum.UINT32));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorReductionMicroOp microOp = Assert.IsType<VectorReductionMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2C0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x2C0UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.VREDAND)]
    [InlineData(InstructionsEnum.VREDOR)]
    [InlineData(InstructionsEnum.VREDXOR)]
    public void LegacySlotCarrierMaterializer_VectorReductionBitwiseProjection_PreservesScalarFootprint(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x300,
                streamLength: 4,
                stride: 4,
                predicateMask: 0xFF,
                dataType: DataTypeEnum.UINT32));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorReductionMicroOp microOp = Assert.IsType<VectorReductionMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x300UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x300UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorDotProductProjection_PreservesNonWideningScalarFootprint()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VDOT,
                destSrc1Pointer: 0x2E0,
                src2Pointer: 0x3E0,
                streamLength: 2,
                stride: 4,
                predicateMask: 0x0F));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorDotProductMicroOp microOp =
            Assert.IsType<VectorDotProductMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2E0UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3E0UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x2E0UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOTU, DataTypeEnum.UINT32)]
    [InlineData(InstructionsEnum.VDOTF, DataTypeEnum.FLOAT32)]
    public void LegacySlotCarrierMaterializer_VectorDotProductSecondaryProjection_PreservesNonWideningScalarFootprint(
        InstructionsEnum opcode,
        DataTypeEnum dataType)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x300,
                src2Pointer: 0x380,
                streamLength: 2,
                stride: 4,
                predicateMask: 0x0F,
                dataType: dataType));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorDotProductMicroOp microOp =
            Assert.IsType<VectorDotProductMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x300UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x380UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x300UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VectorDotProductFp8Projection_PreservesWideningScalarFootprint()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VDOT_FP8,
                destSrc1Pointer: 0x320,
                src2Pointer: 0x3A0,
                streamLength: 4,
                stride: 1,
                predicateMask: 0x0F,
                dataType: DataTypeEnum.FLOAT8_E4M3));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VectorDotProductMicroOp microOp =
            Assert.IsType<VectorDotProductMicroOp>(slotDescriptor.MicroOp);

        Assert.True(slotDescriptor.IsVectorOp);
        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x320UL, 4UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3A0UL, 4UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x320UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Fact]
    public void DirectFactory_VectorDotProductFp8Publication_PreservesWideningScalarFootprint()
    {
        VLIW_Instruction instruction = CreateVectorInstruction(
            InstructionsEnum.VDOT_FP8,
            destSrc1Pointer: 0x340,
            src2Pointer: 0x3C0,
            streamLength: 4,
            stride: 1,
            predicateMask: 0x0F,
            dataType: DataTypeEnum.FLOAT8_E4M3);

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        VectorDotProductMicroOp microOp = Assert.IsType<VectorDotProductMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VDOT_FP8, context));

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x340UL, 4UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3C0UL, 4UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x340UL, 4UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
    }

    [Fact]
    public void CanonicalFetchedTransport_VectorDotProductFp8_WideningContour_PreservesVectorTaxonomy()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA6A0);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(
                InstructionsEnum.VDOT_FP8,
                destSrc1Pointer: 0x360,
                src2Pointer: 0x3E0,
                streamLength: 4,
                stride: 1,
                predicateMask: 0x0F,
                dataType: DataTypeEnum.FLOAT8_E4M3));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0xA6A0);

        DecodedInstructionBundle canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        var legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        VectorDotProductMicroOp microOp = Assert.IsType<VectorDotProductMicroOp>(slot.MicroOp);

        Assert.False(canonicalBundle.HasDecodeFault);
        Assert.False(legalityDescriptor.HasDecodeFault);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.True(slot.IsVectorOp);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.WritesRegister);
        Assert.Equal((uint)InstructionsEnum.VDOT_FP8, slot.OpCode);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VmreadProjection_PreservesCanonicalRegisterWriteFact()
    {
        const byte fieldSelectorRegister = 3;
        const byte destinationRegister = 7;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.VMREAD,
                rd: destinationRegister,
                rs1: fieldSelectorRegister));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)fieldSelectorRegister }, slotDescriptor.ReadRegisters);
        Assert.Equal(new[] { (int)destinationRegister }, slotDescriptor.WriteRegisters);
        Assert.True(microOp.WritesRegister);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VmreadNoDestinationProjection_DoesNotPublishPhantomWritebackFact()
    {
        const byte fieldSelectorRegister = 3;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.VMREAD,
                rd: VLIW_Instruction.NoArchReg,
                rs1: fieldSelectorRegister));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)fieldSelectorRegister }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(VLIW_Instruction.NoArchReg, microOp.Rd);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
        Assert.False(microOp.WritesRegister);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VmwriteProjection_PreservesCanonicalReadRegisterFacts()
    {
        const byte fieldSelectorRegister = 2;
        const byte valueRegister = 9;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.VMWRITE,
                rs1: fieldSelectorRegister,
                rs2: valueRegister));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)fieldSelectorRegister, (int)valueRegister }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.WritesRegister);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VmptrldProjection_PreservesCanonicalPointerReadFact()
    {
        const byte pointerRegister = 11;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.VMPTRLD,
                rs1: pointerRegister));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)pointerRegister }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_VmclearProjection_PreservesCanonicalPointerReadFact()
    {
        const byte pointerRegister = 12;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.VMCLEAR,
                rs1: pointerRegister));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)pointerRegister }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVEXCPMASK, CsrAddresses.VexcpMask, 13)]
    [InlineData(InstructionsEnum.VSETVEXCPPRI, CsrAddresses.VexcpPri, 14)]
    public void LegacySlotCarrierMaterializer_VectorExceptionControlProjection_PreservesCanonicalFullSerialCsrFacts(
        InstructionsEnum opcode,
        ushort csrAddress,
        byte sourceRegister)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: VLIW_Instruction.NoArchReg,
                rs1: sourceRegister,
                rs2: VLIW_Instruction.NoArchReg));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(slotDescriptor.MicroOp);

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.False(slotDescriptor.WritesRegister);
        Assert.Equal(new[] { (int)sourceRegister }, slotDescriptor.ReadRegisters);
        Assert.Empty(slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.Csr, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.Equal(csrAddress, microOp.CSRAddress);
        Assert.Equal(sourceRegister, microOp.SrcRegID);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(slotDescriptor.ReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(slotDescriptor.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVEXCPMASK, 15)]
    [InlineData(InstructionsEnum.VSETVEXCPPRI, 16)]
    public void DirectFactoryVectorExceptionControlCsr_ProjectsCanonicalMetadataBeforeManualPublication(
        InstructionsEnum opcode,
        byte sourceRegister)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA620);

        CSRMicroOp microOp = CreateDirectFactoryVectorExceptionControlMicroOp(opcode, sourceRegister);
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(InstructionClass.Csr, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(new[] { (int)sourceRegister }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Equal(new[] { (int)sourceRegister }, slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVL)]
    [InlineData(InstructionsEnum.VSETVLI)]
    [InlineData(InstructionsEnum.VSETIVLI)]
    public void LegacySlotCarrierMaterializer_VectorConfigProjection_PreservesCanonicalSystemRegisterFacts(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorConfigInstruction(opcode);
        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(CreateBundle(instruction));
        VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(slotDescriptor.MicroOp);

        int[] expectedReadRegisters = opcode switch
        {
            InstructionsEnum.VSETVL => new[] { 5, 6 },
            InstructionsEnum.VSETVLI => new[] { 8 },
            _ => Array.Empty<int>()
        };

        int[] expectedWriteRegisters = opcode switch
        {
            InstructionsEnum.VSETVL => new[] { 4 },
            InstructionsEnum.VSETVLI => new[] { 7 },
            _ => new[] { 9 }
        };

        Assert.False(slotDescriptor.IsMemoryOp);
        Assert.False(slotDescriptor.IsControlFlow);
        Assert.True(slotDescriptor.WritesRegister);
        Assert.Equal(expectedReadRegisters, slotDescriptor.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, slotDescriptor.WriteRegisters);
        Assert.Equal(InstructionClass.System, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(expectedReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVL)]
    [InlineData(InstructionsEnum.VSETVLI)]
    [InlineData(InstructionsEnum.VSETIVLI)]
    public void DirectFactoryVectorConfigCarrier_ProjectsCanonicalMetadataBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA680);

        VConfigMicroOp microOp = CreateDirectFactoryVectorConfigMicroOp(opcode);
        core.TestSetDecodedBundle(microOp);

        int[] expectedReadRegisters = opcode switch
        {
            InstructionsEnum.VSETVL => new[] { 5, 6 },
            InstructionsEnum.VSETVLI => new[] { 8 },
            _ => Array.Empty<int>()
        };

        int[] expectedWriteRegisters = opcode switch
        {
            InstructionsEnum.VSETVL => new[] { 4 },
            InstructionsEnum.VSETVLI => new[] { 7 },
            _ => new[] { 9 }
        };

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(InstructionClass.System, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(expectedReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.True(slot.WritesRegister);
        Assert.Equal(expectedReadRegisters, slot.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    private static DecodedBundleSlotDescriptor DecodeAndMaterializeSingleSlot(
        VLIW_Instruction[] rawSlots,
        IDecoderFrontend? decoder = null)
    {
        decoder ??= new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2000, bundleSerial: 37);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return DecodedBundleSlotDescriptor.Create(0, Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]));
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static CSRMicroOp CreateDirectFactoryVectorExceptionControlMicroOp(
        InstructionsEnum opcode,
        byte sourceRegister)
    {
        VLIW_Instruction instruction = CreateScalarInstruction(
            opcode,
            rd: VLIW_Instruction.NoArchReg,
            rs1: sourceRegister,
            rs2: VLIW_Instruction.NoArchReg);

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            DataType = instruction.DataType,
            HasDataType = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<CsrReadWriteMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VConfigMicroOp CreateDirectFactoryVectorConfigMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorConfigInstruction(opcode);

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            DataType = instruction.DataType,
            HasDataType = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<VConfigMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static StreamControlMicroOp CreateDirectFactoryStreamControlMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateScalarInstruction(opcode);

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<StreamControlMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorConfigInstruction(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(4, 5, 6),
                StreamLength = 0,
                Stride = 0
            },
            InstructionsEnum.VSETVLI => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.UINT16,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    7,
                    8,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 1,
                Stride = 0
            },
            InstructionsEnum.VSETIVLI => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT16,
                PredicateMask = 0xFF,
                Immediate = 13,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    9,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 0,
                Stride = 0
            },
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateLegacyAbsoluteLoadInstruction(
        byte destinationRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Load,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateLegacyAbsoluteStoreInstruction(
        byte sourceRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Store,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                sourceRegister),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateCompilerEmittedAbsoluteLoadMoveInstruction(
        byte destinationRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateCompilerEmittedAbsoluteStoreMoveInstruction(
        byte sourceRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 2,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                sourceRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedRegisterMoveInstruction(
        byte sourceRegister,
        byte destinationRegister)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 0,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                sourceRegister,
                destinationRegister,
                VLIW_Instruction.NoArchReg),
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedImmediateMoveInstruction(
        byte destinationRegister,
        ulong immediate)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 1,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateExplicitNopeInstruction()
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Nope,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateCompilerEmittedLegacyConditionalBranchInstruction(
        InstructionsEnum opcode,
        byte accumulatorRegister,
        byte firstOperandRegister,
        byte secondOperandRegister,
        ulong targetAddress)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                accumulatorRegister,
                firstOperandRegister,
                secondOperandRegister),
            Src2Pointer = targetAddress,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateVectorInstruction(
        InstructionsEnum opcode,
        ulong destSrc1Pointer = 0,
        ulong src2Pointer = 0,
        ushort immediate = 0,
        byte predicateMask = 0xFF,
        uint streamLength = 1,
        ushort stride = 0,
        DataTypeEnum dataType = DataTypeEnum.INT32)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = predicateMask,
            DestSrc1Pointer = destSrc1Pointer,
            Src2Pointer = src2Pointer,
            Immediate = immediate,
            StreamLength = streamLength,
            Stride = stride,
            VirtualThreadId = 0
        };
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedTriOpDescriptor(
        ulong descriptorAddress,
        ulong srcAPointer,
        ulong srcBPointer,
        ushort strideA,
        ushort strideB)
    {
        byte[] descriptor = new byte[20];
        BitConverter.GetBytes(srcAPointer).CopyTo(descriptor, 0);
        BitConverter.GetBytes(srcBPointer).CopyTo(descriptor, 8);
        BitConverter.GetBytes(strideA).CopyTo(descriptor, 16);
        BitConverter.GetBytes(strideB).CopyTo(descriptor, 18);
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }
}


