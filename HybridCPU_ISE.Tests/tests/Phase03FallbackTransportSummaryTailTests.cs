using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03FallbackTransportSummaryTailTests
{
    [Fact]
    public void DecodeFullBundle_FallbackTrapPath_PreservesRebuildableTransportSummary()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4800);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4800);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        PipelineControl control = core.GetPipelineControl();

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.False(legalityDescriptor.DependencySummary.HasValue);

        Assert.True(transportFacts.DependencySummary.HasValue);
        Assert.Equal((byte)0b0000_0011, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0b0000_0011, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.True(
            (transportFacts.AdmissionPrep.Flags & DecodedBundleAdmissionFlags.HasAuxiliaryClusterOps) != 0);

        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(transportFacts.Slots[0].MicroOp);
        AtomicMicroOp atomicMicroOp = Assert.IsType<AtomicMicroOp>(transportFacts.Slots[1].MicroOp);

        Assert.Equal(1UL, control.DecodeFallbackCount);
        Assert.Equal(1UL, control.DecodeFaultBundleCount);
        Assert.Equal(SlotClass.SystemSingleton, trapMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotClass.LsuClass, atomicMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.NotEqual(ResourceBitset.Zero, transportFacts.DependencySummary.Value.AggregateResourceMask);
    }

    [Fact]
    public void DecodeFullBundle_FallbackTrapPath_UsesMaterializedVectorSlots_ForLoopBufferIterationDetection()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4900);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateVectorInstruction(InstructionsEnum.VADD, streamLength: 64));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4900);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        ulong maxIterations = core.TestResolveCurrentLoopBufferMaxIterations();

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.IsType<TrapMicroOp>(transportFacts.Slots[0].MicroOp);
        Assert.True(transportFacts.Slots[1].IsVectorOp);
        Assert.Equal(2UL, maxIterations);
    }

    [Fact]
    public void DecodeFullBundle_FallbackTrapPath_ReplacesPriorCanonicalSnapshotsWithDecodeFaultState()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4A00);

        VLIW_Instruction[] canonicalRawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));
        core.TestSetCanonicalDecodedBundleTransportFacts(canonicalRawSlots, pc: 0x4A00, bundleSerial: 74);

        VLIW_Instruction[] fallbackRawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));
        core.TestDecodeFetchedBundle(fallbackRawSlots, pc: 0x4A00);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.True(transportFacts.DependencySummary.HasValue);
        Assert.Equal((byte)0b0000_0011, transportFacts.ValidNonEmptyMask);
        Assert.IsType<TrapMicroOp>(transportFacts.Slots[0].MicroOp);
        Assert.IsType<AtomicMicroOp>(transportFacts.Slots[1].MicroOp);
    }

    [Fact]
    public void DecodeFullBundle_RetainedTripleDestinationMoveDt5_FailsClosedAsDecodeFaultTrap()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4A80);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateRetainedTripleDestinationMoveInstruction(4, 5, 6, operand1: 0x120, operand2: 0x340),
                default);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4A80);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((uint)InstructionsEnum.Move, trapMicroOp.OpCode);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
    }

    [Fact]
    public void DecodeFullBundle_RetainedDualWriteMoveDt4_FailsClosedAsDecodeFaultTrap()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4AA0);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateRetainedDualWriteMoveInstruction(4, 5, operand1: 0x120, operand2: 0x340),
                default);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4AA0);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((uint)InstructionsEnum.Move, trapMicroOp.OpCode);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
    }

        [Theory]
        [InlineData(InstructionsEnum.Interrupt, 0x4AC0UL)]
        [InlineData(InstructionsEnum.InterruptReturn, 0x4AE0UL)]
        public void DecodeFullBundle_RetainedInterruptContours_FailClosedAsDecodeFaultTrap(
            InstructionsEnum opcode,
            ulong pc)
        {
            var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(opcode, rd: 4, rs1: 5, rs2: 6, immediate: 0x240),
                default);

        core.TestDecodeFetchedBundle(rawSlots, pc);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((uint)opcode, trapMicroOp.OpCode);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.False(slot.WritesRegister);
            Assert.Empty(slot.ReadRegisters);
            Assert.Empty(slot.WriteRegisters);
        }

        [Theory]
        [InlineData(14u, 0x4B80UL)]
        [InlineData(15u, 0x4BA0UL)]
        [InlineData(18u, 0x4BC0UL)]
        public void DecodeFullBundle_RetainedCallReturnJumpWrappers_FailClosedAsDecodeFaultTrap(
            uint rawOpcode,
            ulong pc)
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateRawScalarInstruction(rawOpcode, rd: 4, rs1: 5, rs2: 6, immediate: 0x280),
                    default);

            core.TestDecodeFetchedBundle(rawSlots, pc);

            var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
            BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

            Assert.True(canonicalBundle.HasDecodeFault);
            Assert.True(canonicalBundle.IsEmpty);
            Assert.True(legalityDescriptor.HasDecodeFault);
            Assert.True(legalityDescriptor.IsEmpty);
            Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
            Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
            Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
            Assert.Equal(rawOpcode, trapMicroOp.OpCode);
            Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.False(slot.WritesRegister);
            Assert.Empty(slot.ReadRegisters);
            Assert.Empty(slot.WriteRegisters);
        }

        [Theory]
        [InlineData(InstructionsEnum.MTILE_LOAD, 0x4B40UL)]
        [InlineData(InstructionsEnum.MTILE_STORE, 0x4B60UL)]
        [InlineData(InstructionsEnum.MTILE_MACC, 0x4B00UL)]
        [InlineData(InstructionsEnum.MTRANSPOSE, 0x4B20UL)]
        public void DecodeFullBundle_RuntimeOwnedMatrixTileContours_PublishTypedCarrierInsteadOfFallbackTrap(
            InstructionsEnum opcode,
            ulong pc)
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateGoldenMatrixTileInstruction(opcode),
                    default);

            core.TestDecodeFetchedBundle(rawSlots, pc);

            var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
            BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            MatrixTileMicroOp microOp = Assert.IsAssignableFrom<MatrixTileMicroOp>(slot.MicroOp);

            bool expectedMemoryCarrier =
                opcode is InstructionsEnum.MTILE_LOAD or InstructionsEnum.MTILE_STORE;

            Assert.False(canonicalBundle.HasDecodeFault);
            Assert.False(canonicalBundle.IsEmpty);
            Assert.False(legalityDescriptor.HasDecodeFault);
            Assert.False(legalityDescriptor.IsEmpty);
            Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
            Assert.Equal((uint)opcode, slot.OpCode);
            Assert.Equal(expectedMemoryCarrier, slot.IsMemoryOp);
            Assert.Equal(expectedMemoryCarrier, microOp.AdmissionMetadata.IsMemoryOp);
            Assert.Equal(
                expectedMemoryCarrier
                    ? SlotClass.MatrixTileStreamClass
                    : SlotClass.AluClass,
                slot.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, slot.Placement.PinningKind);
            Assert.False(slot.WritesRegister);
            Assert.Empty(slot.ReadRegisters);
            Assert.Empty(slot.WriteRegisters);
            Assert.True(microOp.PublishesTypedTileMicroOp);
            Assert.True(microOp.PublishesExecutionCaptureSemantics);
            Assert.False(microOp.UsesFallbackPath);
            AssertMatrixTileTypeAndMemoryShape(opcode, microOp);
        }

        [Fact]
        public void DecodeFullBundle_UnsupportedOptionalScalarXfmacContour_FailsClosedAsDecodeFaultTrap()
        {
            const ulong pc = 0x4B10UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateRawScalarInstruction(55u, rd: 4, rs1: 5, rs2: 6, immediate: 0x290),
                    default);

            core.TestDecodeFetchedBundle(rawSlots, pc);

            var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
            BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

            Assert.True(canonicalBundle.HasDecodeFault);
            Assert.True(canonicalBundle.IsEmpty);
            Assert.True(legalityDescriptor.HasDecodeFault);
            Assert.True(legalityDescriptor.IsEmpty);
            Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
            Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
            Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
            Assert.Equal(55u, trapMicroOp.OpCode);
            Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.False(slot.WritesRegister);
            Assert.Empty(slot.ReadRegisters);
            Assert.Empty(slot.WriteRegisters);
        }

        [Theory]
        [InlineData(45u, 0x4B12UL)]
        [InlineData(52u, 0x4B14UL)]
        public void DecodeFullBundle_UnsupportedOptionalScalarContours_FailClosedAsDecodeFaultTrap(
            uint rawOpcode,
            ulong pc)
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateRawScalarInstruction(rawOpcode, rd: 4, rs1: 5, rs2: 6, immediate: 0x292),
                    default);

            core.TestDecodeFetchedBundle(rawSlots, pc);

            var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
            BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

            Assert.True(canonicalBundle.HasDecodeFault);
            Assert.True(canonicalBundle.IsEmpty);
            Assert.True(legalityDescriptor.HasDecodeFault);
            Assert.True(legalityDescriptor.IsEmpty);
            Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
            Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
            Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
            Assert.Equal(rawOpcode, trapMicroOp.OpCode);
            Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.False(slot.WritesRegister);
            Assert.Empty(slot.ReadRegisters);
            Assert.Empty(slot.WriteRegisters);
        }

        private static VLIW_Instruction CreateGoldenMatrixTileInstruction(InstructionsEnum opcode)
        {
            foreach (MatrixTileExecutionGoldenVector vector in
                     MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors)
            {
                if (vector.Opcode == opcode)
                {
                    return vector.Carrier.CreateInstruction();
                }
            }

            throw new InvalidOperationException($"No positive matrix/tile golden carrier is published for {opcode}.");
        }

        private static void AssertMatrixTileTypeAndMemoryShape(
            InstructionsEnum opcode,
            MatrixTileMicroOp microOp)
        {
            switch (opcode)
            {
                case InstructionsEnum.MTILE_LOAD:
                    Assert.IsType<MtileLoadMicroOp>(microOp);
                    Assert.Equal(MatrixTileProjectedOperationKind.Load, microOp.OperationKind);
                    Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
                    Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
                    Assert.NotEmpty(microOp.AdmissionMetadata.ReadMemoryRanges);
                    Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
                    break;

                case InstructionsEnum.MTILE_STORE:
                    Assert.IsType<MtileStoreMicroOp>(microOp);
                    Assert.Equal(MatrixTileProjectedOperationKind.Store, microOp.OperationKind);
                    Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
                    Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);
                    Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
                    Assert.NotEmpty(microOp.AdmissionMetadata.WriteMemoryRanges);
                    break;

                case InstructionsEnum.MTILE_MACC:
                    Assert.IsType<MtileMaccMicroOp>(microOp);
                    Assert.Equal(MatrixTileProjectedOperationKind.Macc, microOp.OperationKind);
                    Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
                    Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
                    Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
                    Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
                    break;

                case InstructionsEnum.MTRANSPOSE:
                    Assert.IsType<MtransposeMicroOp>(microOp);
                    Assert.Equal(MatrixTileProjectedOperationKind.Transpose, microOp.OperationKind);
                    Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
                    Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
                    Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
                    Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unsupported matrix/tile opcode.");
            }
        }

        private static VLIW_Instruction[] CreateBundle(
            VLIW_Instruction slot0,
            VLIW_Instruction slot1)
        {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        rawSlots[1] = slot1;
        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return CreateRawScalarInstruction((uint)opcode, rd, rs1, rs2, immediate);
    }

    private static VLIW_Instruction CreateRawScalarInstruction(
        uint opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedDualWriteMoveInstruction(
        byte destinationRegister1,
        byte destinationRegister2,
        ulong operand1,
        ulong operand2)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 4,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister1,
                destinationRegister2,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = operand1,
            Word3 = operand2,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedTripleDestinationMoveInstruction(
        byte destinationRegister1,
        byte destinationRegister2,
        byte destinationRegister3,
        ulong operand1,
        ulong operand2)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 5,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister1,
                destinationRegister2,
                destinationRegister3),
            Src2Pointer = operand1,
            Word3 = operand2,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateVectorInstruction(
        InstructionsEnum opcode,
        ulong streamLength)
    {
        return InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: streamLength);
    }
}
