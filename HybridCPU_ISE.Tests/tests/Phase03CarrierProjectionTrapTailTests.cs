using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CarrierProjectionTrapTailTests
{
    [Fact]
    public void TrapMicroOp_IsNonStealableByDefault()
    {
        var trapMicroOp = new TrapMicroOp();

        Assert.False(trapMicroOp.IsStealable);
        Assert.False(trapMicroOp.AdmissionMetadata.IsStealable);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_UnregisteredCanonicalOpcode_ProjectsCanonicalVtAndExplicitMetadataIntoTrapCarrier()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(new VLIW_Instruction
            {
                OpCode = 0xDEAD,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd: 1, rs1: 2, rs2: 0),
                Src2Pointer = 0,
                StreamLength = 0,
                Stride = 0,
                VirtualThreadId = 0
            });

        var slotMetadata = new SlotMetadata
        {
            StealabilityPolicy = StealabilityPolicy.NotStealable,
            LocalityHint = LocalityHint.Cold,
            BranchHint = BranchHint.Likely,
            AdmissionMetadata = MicroOpAdmissionMetadata.Default with
            {
                OwnerContextId = 11,
                DomainTag = 0x24,
                Placement = new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.AluClass,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = 0x24
                }
            }
        };
        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x5000,
            bundleSerial: 37,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = (Processor.CPU_Core.InstructionsEnum)0xDEAD,
                        Class = InstructionClass.System,
                        SerializationClass = SerializationClass.FullSerial,
                        Rd = 1,
                        Rs1 = 2,
                        Rs2 = 0,
                        Imm = 0
                    },
                    slotMetadata: new InstructionSlotMetadata(VtId.Create(3), slotMetadata))
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(carrierBundle[0]);
        Assert.Equal(3, trapMicroOp.VirtualThreadId);
        Assert.Equal(3, trapMicroOp.OwnerThreadId);
        Assert.Equal(11, trapMicroOp.OwnerContextId);
        Assert.Equal(0x24UL, trapMicroOp.Placement.DomainTag);
        Assert.Equal(11, trapMicroOp.AdmissionMetadata.OwnerContextId);
        Assert.Equal(0x24UL, trapMicroOp.AdmissionMetadata.DomainTag);
        Assert.Equal(LocalityHint.Cold, trapMicroOp.MemoryLocalityHint);
        Assert.False(trapMicroOp.IsStealable);
        Assert.False(trapMicroOp.AdmissionMetadata.IsStealable);
    }

    [Fact]
    public void CanonicalKnownVectorTrapCarrier_PreservesPublishedVectorClassificationForLoopBufferDetection()
    {
        const ulong pc = 0x5800;
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateVectorInstruction(opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD, streamLength: 64));
        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: pc,
            bundleSerial: 91,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.VADD,
                        Class = InstructionClass.ScalarAlu,
                        SerializationClass = SerializationClass.Free,
                        Rd = 0,
                        Rs1 = 0,
                        Rs2 = 0,
                        Imm = 0
                    })
            });

        MicroOp?[] carrierBundle =
        [
            new TrapMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                UndecodedOpCode = (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                TrapReason = "Synthetic vector MicroOp materialization failure"
            }
        ];
        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.VADD, trapMicroOp.OpCode);
        Assert.True(transportFacts.Slots[0].IsVectorOp);
        Assert.IsType<TrapMicroOp>(transportFacts.Slots[0].MicroOp);

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);
        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestSetDecodedBundleTransportFacts(transportFacts);

        Assert.Equal(2UL, core.TestResolveCurrentLoopBufferMaxIterations());
    }

    [Fact]
    public void CanonicalKnownAtomicTrapCarrier_PreservesPublishedMemoryRegisterAndPlacementFacts()
    {
        const ulong pc = 0x5820;
        const int expectedBankIntent = 9;
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                Processor.CPU_Core.InstructionsEnum.AMOADD_W,
                rd: 3,
                rs1: 4,
                rs2: 5,
                src2Pointer: 0x9000));
        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: pc,
            bundleSerial: 92,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.AMOADD_W,
                        Class = InstructionClass.Atomic,
                        SerializationClass = SerializationClass.AtomicSerial,
                        Rd = 3,
                        Rs1 = 4,
                        Rs2 = 5,
                        Imm = 0
                    })
            });

        MicroOp?[] carrierBundle =
        [
            new TrapMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W,
                UndecodedOpCode = (uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W,
                TrapReason = "Synthetic atomic MicroOp materialization failure",
                ProjectedMemoryBankIntent = expectedBankIntent
            }
        ];
        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.IsType<TrapMicroOp>(slot.MicroOp);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W, slot.OpCode);
        Assert.True(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 4, 5 }, slot.ReadRegisters);
        Assert.Equal(new[] { 3 }, slot.WriteRegisters);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, slot.Placement.PinningKind);
        Assert.Equal(0, slot.Placement.PinnedLaneId);
        Assert.Equal(expectedBankIntent, slot.MemoryBankIntent);
    }

    [Theory]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VGATHER, 0x5880UL, 0x1000UL, 0x2000UL)]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VSCATTER, 0x58A0UL, 0x3000UL, 0x4000UL)]
    public void CanonicalKnownVectorMemoryTrapCarrier_PreservesFetchedMemoryPlacementAndAuxiliaryPublicationTruth(
        Processor.CPU_Core.InstructionsEnum opcode,
        ulong pc,
        ulong destSrc1Pointer,
        ulong src2Pointer)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);

        VLIW_Instruction[] rawSlots =
            CreateBundle(InstructionEncoder.EncodeVector1D(
                (uint)opcode,
                DataTypeEnum.INT32,
                destSrc1Ptr: destSrc1Pointer,
                src2Ptr: src2Pointer,
                streamLength: 64));

        core.TestDecodeFetchedBundle(rawSlots, pc);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.False(canonicalBundle.HasDecodeFault);
        Assert.False(canonicalBundle.IsEmpty);
        Assert.False(legalityDescriptor.HasDecodeFault);
        Assert.False(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.True(slot.IsVectorOp);
        Assert.True(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.IsType<TrapMicroOp>(slot.MicroOp);
        Assert.Equal((uint)opcode, slot.OpCode);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, slot.Placement.PinningKind);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(2UL, core.TestResolveCurrentLoopBufferMaxIterations());
    }

    [Theory]
    [InlineData(Processor.CPU_Core.InstructionsEnum.LW, InstructionClass.Memory, 0UL, 0x6000, 6)]
    [InlineData(Processor.CPU_Core.InstructionsEnum.AMOADD_W, InstructionClass.Atomic, 0x9000UL, 0, 9)]
    public void LegacySlotCarrierMaterializer_CanonicalTrapMemoryBankProjection_UsesRawAddressContour(
        Processor.CPU_Core.InstructionsEnum opcode,
        InstructionClass instructionClass,
        ulong src2Pointer,
        ushort immediate,
        int expectedBankIntent)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 4, 5),
            Src2Pointer = src2Pointer,
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = instructionClass,
            SerializationClass = instructionClass == InstructionClass.Atomic
                ? SerializationClass.AtomicSerial
                : SerializationClass.Free,
            Rd = 3,
            Rs1 = 4,
            Rs2 = 5,
            Imm = immediate
        };

        int projectedBankIntent =
            DecodedBundleTransportProjector.ResolveProjectedTrapMemoryBankIntent(
                in rawInstruction,
                in instruction);

        Assert.Equal(expectedBankIntent, projectedBankIntent);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_CanonicalTrapMemoryBankProjection_PrefersDecodedAbsoluteMemoryAddressOverRawLegacyWrapperShape()
    {
        IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                7,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = 0x6000,
            StreamLength = 0,
            Stride = 0
        };

        InstructionIR instruction = decoder.Decode(in rawInstruction, slotIndex: 0);
        Assert.Equal(Processor.CPU_Core.InstructionsEnum.Load, instruction.CanonicalOpcode.ToInstructionsEnum());
        Assert.True(instruction.HasAbsoluteAddressing);
        Assert.Equal(0x6000L, instruction.Imm);

        VLIW_Instruction perturbedRawInstruction = rawInstruction;
        perturbedRawInstruction.Src2Pointer = 0x1000;

        int projectedBankIntent =
            DecodedBundleTransportProjector.ResolveProjectedTrapMemoryBankIntent(
                in perturbedRawInstruction,
                in instruction);

        Assert.Equal(6, projectedBankIntent);
    }

    [Fact]
    public void CanonicalKnownTrapMemoryCarrier_PreservesPublishedBankIntentThroughRuntimeTransportSurface()
    {
        const ulong pc = 0x5860;
        const int expectedBankIntent = 7;

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(Processor.CPU_Core.InstructionsEnum.LW, rd: 1, rs1: 2, src2Pointer: 0x7000),
                CreateScalarInstruction(Processor.CPU_Core.InstructionsEnum.SW, rs1: 3, rs2: 4, src2Pointer: 0x7000));
        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: pc,
            bundleSerial: 94,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.LW,
                        Class = InstructionClass.Memory,
                        SerializationClass = SerializationClass.Free,
                        Rd = 1,
                        Rs1 = 2,
                        Rs2 = 0,
                        Imm = 0
                    }),
                DecodedInstruction.CreateOccupied(
                    slotIndex: 1,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.SW,
                        Class = InstructionClass.Memory,
                        SerializationClass = SerializationClass.Free,
                        Rd = 0,
                        Rs1 = 3,
                        Rs2 = 4,
                        Imm = 0
                    })
            });

        MicroOp?[] carrierBundle =
        [
            new TrapMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LW,
                UndecodedOpCode = (uint)Processor.CPU_Core.InstructionsEnum.LW,
                TrapReason = "Synthetic load MicroOp materialization failure",
                ProjectedMemoryBankIntent = expectedBankIntent
            },
            new TrapMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SW,
                UndecodedOpCode = (uint)Processor.CPU_Core.InstructionsEnum.SW,
                TrapReason = "Synthetic store MicroOp materialization failure",
                ProjectedMemoryBankIntent = expectedBankIntent
            }
        ];
        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);
        core.TestSetDecodedBundleTransportFacts(transportFacts);

        DecodedBundleTransportFacts publishedFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        var runtimeFacts = core.TestReadDecodedSlotRuntimeIssueFacts(publishedFacts.Slots[0]);

        Assert.True(runtimeFacts.IsMemoryOp);
        Assert.Equal(expectedBankIntent, runtimeFacts.MemoryBankIntent);
        Assert.True(core.TestHasMemoryClusteringEvent(publishedFacts.Slots));
    }

    [Fact]
    public void CanonicalKnownControlTrapCarrier_PreservesPublishedControlFlowRegisterAndPlacementFacts()
    {
        const ulong pc = 0x5840;
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(Processor.CPU_Core.InstructionsEnum.JALR, rd: 1, rs1: 2));
        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: pc,
            bundleSerial: 93,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.JALR,
                        Class = InstructionClass.ControlFlow,
                        SerializationClass = SerializationClass.Free,
                        Rd = 1,
                        Rs1 = 2,
                        Rs2 = 0,
                        Imm = 0
                    })
            });

        MicroOp?[] carrierBundle =
        [
            new TrapMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.JALR,
                UndecodedOpCode = (uint)Processor.CPU_Core.InstructionsEnum.JALR,
                TrapReason = "Synthetic branch MicroOp materialization failure"
            }
        ];
        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.IsType<TrapMicroOp>(slot.MicroOp);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.JALR, slot.OpCode);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.IsControlFlow);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 2 }, slot.ReadRegisters);
        Assert.Equal(new[] { 1 }, slot.WriteRegisters);
        Assert.Equal(SlotClass.BranchControl, slot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, slot.Placement.PinningKind);
        Assert.Equal(7, slot.Placement.PinnedLaneId);
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] occupiedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < occupiedSlots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = occupiedSlots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        Processor.CPU_Core.InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        ulong src2Pointer = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = src2Pointer != 0 ? src2Pointer : immediate,
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateVectorInstruction(
        uint opCode,
        ulong streamLength)
    {
        return InstructionEncoder.EncodeVector1D(
            opCode,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: streamLength);
    }
}


