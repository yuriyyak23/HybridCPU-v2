using HybridCPU_ISE.Arch;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CanonicalOpcodeIdentityTests
{
    [Fact]
    public void VliwCompatDecoderV4_LegacyMoveLoadProjection_PublishesDedicatedCanonicalOpcodeIdentity()
    {
        IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                7,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = 0x280,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);

        Assert.Equal(InstructionsEnum.Load, ir.CanonicalOpcode.ToInstructionsEnum());
        Assert.Equal((uint)InstructionsEnum.Load, (uint)ir.CanonicalOpcode);
    }

    [Fact]
    public void VliwCompatDecoderV4_LegacyConditionalBranchProjection_PublishesDedicatedCanonicalOpcodeIdentity()
    {
        IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)InstructionsEnum.JumpIfAbove,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(6, 10, 11),
            Src2Pointer = 0x350,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);

        Assert.Equal(InstructionsEnum.BLTU, ir.CanonicalOpcode.ToInstructionsEnum());
        Assert.Equal((uint)InstructionsEnum.BLTU, (uint)ir.CanonicalOpcode);
    }

    [Fact]
    public void VliwCompatDecoderV4_LegacyConditionalBranchProjection_DoesNotDependOnScalarStreamLengthHeuristic()
    {
        IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)InstructionsEnum.JumpIfEqual,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(0, 2, 3),
            Immediate = 0x44,
            StreamLength = 4,
            Stride = 2,
            VirtualThreadId = 0
        };

        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);

        Assert.Equal(InstructionsEnum.BEQ, ir.CanonicalOpcode.ToInstructionsEnum());
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal((byte)2, ir.Rs1);
        Assert.Equal((byte)3, ir.Rs2);
    }

    [Fact]
    public void VliwDecoderV4_PublishedScalarPackedArchTuple_RemainsValidatedWithoutScalarStreamLengthHeuristic()
    {
        var decoder = new VliwDecoderV4();
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)InstructionsEnum.ADDI,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 1UL | (33UL << 16) | ((ulong)VLIW_Instruction.NoReg << 32),
            Immediate = 7,
            StreamLength = 4,
            VirtualThreadId = 0
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => decoder.Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
        Assert.Contains("flat architectural register ids", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_LegacyMoveLoadProjection_RegistersRuntimeCarrierThroughCanonicalOpcodeIdentity()
    {
        VLIW_Instruction[] rawSlots = CreateBundle(new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                7,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = 0x280,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        });

        IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2000, bundleSerial: 37);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.Load, microOp.OpCode);
    }

    [Fact]
    public void DecodedBundleSlotCarrierBuilder_CanonicalTrapDescriptor_PublishesCanonicalOpcodeIdentity()
    {
        var trapMicroOp = new TrapMicroOp
        {
            OpCode = (uint)InstructionsEnum.Move,
            UndecodedOpCode = (uint)InstructionsEnum.Move
        };

        var canonicalInstruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Load,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.Free,
            Rd = 7,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
        };

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x2000,
            bundleSerial: 37,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(0, canonicalInstruction)
            },
            bundleMetadata: BundleMetadata.Default);

        MicroOp?[] carrierBundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        carrierBundle[0] = trapMicroOp;

        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc: 0x2000,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        Assert.Equal((uint)InstructionsEnum.Load, transportFacts.Slots[0].OpCode);
    }

    [Fact]
    public void DecodedBundleSlotCarrierBuilder_CanonicalVectorClassification_UsesCanonicalOpcodeIdentity()
    {
        var trapMicroOp = new TrapMicroOp
        {
            OpCode = 0xFFFF,
            UndecodedOpCode = 0xFFFF
        };

        var canonicalInstruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.VADD,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x3000,
            bundleSerial: 41,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(0, canonicalInstruction)
            },
            bundleMetadata: BundleMetadata.Default);

        MicroOp?[] carrierBundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        carrierBundle[0] = trapMicroOp;

        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc: 0x3000,
                carrierBundle,
                canonicalBundle,
                dependencySummary: null);

        Assert.True(transportFacts.Slots[0].IsVectorOp);
    }

    [Fact]
    public void BundleLegalityAnalyzer_MayWriteArchitecturalRegister_UsesCanonicalOpcodeIdentity()
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.JAL,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = 5,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 4,
        };

        Assert.True(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.JALR, InstructionClass.ControlFlow, SerializationClass.Free, true)]
    [InlineData(InstructionsEnum.JumpIfEqual, InstructionClass.ControlFlow, SerializationClass.Free, false)]
    [InlineData(InstructionsEnum.BEQ, InstructionClass.ControlFlow, SerializationClass.Free, false)]
    [InlineData(InstructionsEnum.VSETVL, InstructionClass.System, SerializationClass.FullSerial, true)]
    [InlineData(InstructionsEnum.VSETVLI, InstructionClass.System, SerializationClass.FullSerial, true)]
    [InlineData(InstructionsEnum.VSETIVLI, InstructionClass.System, SerializationClass.FullSerial, true)]
    [InlineData(InstructionsEnum.FENCE, InstructionClass.System, SerializationClass.MemoryOrdered, false)]
    [InlineData(InstructionsEnum.VMREAD, InstructionClass.Vmx, SerializationClass.VmxSerial, true)]
    [InlineData(InstructionsEnum.VMWRITE, InstructionClass.Vmx, SerializationClass.VmxSerial, false)]
    [InlineData(InstructionsEnum.VMCLEAR, InstructionClass.Vmx, SerializationClass.VmxSerial, false)]
    [InlineData(InstructionsEnum.VMPTRLD, InstructionClass.Vmx, SerializationClass.VmxSerial, false)]
    public void BundleLegalityAnalyzer_PublishedWriteContours_UseCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        bool expectedWritesRegister)
    {
        byte rd = 5;
        byte rs1 = 2;
        byte rs2 = 3;

        switch (opcode)
        {
            case InstructionsEnum.JALR:
                rs2 = 0;
                break;
            case InstructionsEnum.VMREAD:
                rs2 = 0;
                break;
            case InstructionsEnum.VMWRITE:
                rd = 0;
                break;
        }

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = instructionClass,
            SerializationClass = serializationClass,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0,
        };

        Assert.Equal(expectedWritesRegister, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.ECALL, "17")]
    [InlineData(InstructionsEnum.VSETVL, "2,3")]
    [InlineData(InstructionsEnum.VSETVLI, "2")]
    [InlineData(InstructionsEnum.VSETIVLI, "")]
    public void BundleLegalityAnalyzer_PublishedSystemReadContours_UseCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        string expectedReadRegistersCsv)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.FullSerial,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
        };

        int[] expectedReadRegisters = string.IsNullOrEmpty(expectedReadRegistersCsv)
            ? Array.Empty<int>()
            : Array.ConvertAll(expectedReadRegistersCsv.Split(','), int.Parse);

        Assert.Equal(expectedReadRegisters, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.ADDI, InstructionClass.ScalarAlu, SerializationClass.Free, "2")]
    [InlineData(InstructionsEnum.LUI, InstructionClass.ScalarAlu, SerializationClass.Free, "")]
    [InlineData(InstructionsEnum.JAL, InstructionClass.ControlFlow, SerializationClass.Free, "")]
    [InlineData(InstructionsEnum.JALR, InstructionClass.ControlFlow, SerializationClass.Free, "2")]
    [InlineData(InstructionsEnum.JumpIfEqual, InstructionClass.ControlFlow, SerializationClass.Free, "2,3")]
    [InlineData(InstructionsEnum.BEQ, InstructionClass.ControlFlow, SerializationClass.Free, "2,3")]
    [InlineData(InstructionsEnum.CSRRW, InstructionClass.Csr, SerializationClass.CsrOrdered, "2")]
    [InlineData(InstructionsEnum.CSRRSI, InstructionClass.Csr, SerializationClass.CsrOrdered, "")]
    [InlineData(InstructionsEnum.LR_W, InstructionClass.Atomic, SerializationClass.AtomicSerial, "2")]
    [InlineData(InstructionsEnum.AMOADD_W, InstructionClass.Atomic, SerializationClass.AtomicSerial, "2,3")]
    [InlineData(InstructionsEnum.VMREAD, InstructionClass.Vmx, SerializationClass.VmxSerial, "2")]
    [InlineData(InstructionsEnum.VMWRITE, InstructionClass.Vmx, SerializationClass.VmxSerial, "2,3")]
    [InlineData(InstructionsEnum.VMCLEAR, InstructionClass.Vmx, SerializationClass.VmxSerial, "2")]
    [InlineData(InstructionsEnum.VMPTRLD, InstructionClass.Vmx, SerializationClass.VmxSerial, "2")]
    public void BundleLegalityAnalyzer_PublishedReadContours_UseCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        string expectedReadRegistersCsv)
    {
        byte rd = 1;
        byte rs1 = 2;
        byte rs2 = opcode is InstructionsEnum.JALR or InstructionsEnum.VMREAD or InstructionsEnum.VMCLEAR or InstructionsEnum.VMPTRLD
            ? (byte)0
            : (byte)3;

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = instructionClass,
            SerializationClass = serializationClass,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0,
        };

        int[] expectedReadRegisters = string.IsNullOrEmpty(expectedReadRegistersCsv)
            ? Array.Empty<int>()
            : Array.ConvertAll(expectedReadRegistersCsv.Split(','), int.Parse);

        Assert.Equal(expectedReadRegisters, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Fact]
    public void BundleLegalityAnalyzer_RetainedControlFlowFallback_DoesNotPublishPhantomWriteback()
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.JumpIfNotEqual,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = 5,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
        };

        Assert.False(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
        Assert.Equal(new[] { 2, 3 }, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.ADDI, "2")]
    [InlineData(InstructionsEnum.LUI, "")]
    [InlineData(InstructionsEnum.AUIPC, "")]
    public void BundleLegalityAnalyzer_PublishedScalarImmediateReadContours_UseCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        string expectedReadRegistersCsv)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 7,
        };

        int[] expectedReadRegisters = string.IsNullOrEmpty(expectedReadRegistersCsv)
            ? Array.Empty<int>()
            : Array.ConvertAll(expectedReadRegistersCsv.Split(','), int.Parse);

        Assert.Equal(expectedReadRegisters, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.Move, 2, 0, "2")]
    [InlineData(InstructionsEnum.Move_Num, 0, 0, "")]
    public void BundleLegalityAnalyzer_RetainedScalarFallback_UsesCarrierRegisterPresence(
        InstructionsEnum opcode,
        byte rs1,
        byte rs2,
        string expectedReadRegistersCsv)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 7,
        };

        int[] expectedReadRegisters = string.IsNullOrEmpty(expectedReadRegistersCsv)
            ? Array.Empty<int>()
            : Array.ConvertAll(expectedReadRegistersCsv.Split(','), int.Parse);

        Assert.Equal(expectedReadRegisters, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Fact]
    public void BundleLegalityAnalyzer_RetainedScalarFallback_TreatsNoArchRegAsAbsentCarrierRegister()
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Move_Num,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 7,
        };

        Assert.Empty(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Fact]
    public void BundleLegalityAnalyzer_RetainedMemoryFallback_UsesCarrierRegisterPresenceAndNoArchSentinel()
    {
        var loadInstruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Load,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.Free,
            Rd = 7,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
        };

        var storeInstruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Store,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.MemoryOrdered,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = 6,
            Imm = 0,
        };

        Assert.Empty(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(loadInstruction));
        Assert.Equal(new[] { 6 }, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(storeInstruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER, SerializationClass.Free, true, new[] { 1 })]
    [InlineData(InstructionsEnum.VSCATTER, SerializationClass.MemoryOrdered, false, new[] { 1, 2 })]
    public void BundleLegalityAnalyzer_VectorMemoryContours_UseCanonicalSerializationIdentity(
        InstructionsEnum opcode,
        SerializationClass serializationClass,
        bool expectedWritesRegister,
        int[] expectedReadRegisters)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Memory,
            SerializationClass = serializationClass,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        Assert.Equal(expectedWritesRegister, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
        Assert.Equal(expectedReadRegisters, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER, InstructionClass.Memory, SerializationClass.Free, 0)]
    [InlineData(InstructionsEnum.VSCATTER, InstructionClass.Memory, SerializationClass.MemoryOrdered, 1)]
    [InlineData(InstructionsEnum.AMOADD_W, InstructionClass.Atomic, SerializationClass.AtomicSerial, 2)]
    [InlineData(InstructionsEnum.MTILE_STORE, InstructionClass.Memory, SerializationClass.MemoryOrdered, 1)]
    public void BundleLegalityAnalyzer_DependencySummary_UsesCanonicalStructuralResourceIdentity(
        InstructionsEnum opcode,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        int expectedStructuralKind)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = instructionClass,
            SerializationClass = serializationClass,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        DecodedBundleDependencySummary dependencySummary = AnalyzeSingleSlotDependencySummary(instruction);

        AssertStructuralMemoryMask(dependencySummary.AggregateResourceMask, expectedStructuralKind);
    }

    [Fact]
    public void BundleLegalityAnalyzer_VectorScalarFreeContour_UsesCanonicalOpcodeIdentity()
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.VADD,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 4,
            Rs1 = 5,
            Rs2 = 6,
            Imm = 0,
        };

        Assert.False(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
        Assert.Empty(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
        Assert.Empty(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister: false));
    }

    [Fact]
    public void BundleLegalityAnalyzer_VectorMaskPopScalarResultContour_UsesCanonicalDescriptorIdentity()
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.VPOPC,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 6,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
        };

        bool writesRegister = YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction);

        Assert.True(writesRegister);
        Assert.Empty(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
        Assert.Equal(new[] { 6 }, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister));
    }

    [Fact]
    public void LegalityChecker_PrivilegeGate_UsesCanonicalOpcodeIdentity()
    {
        var checker = new LegalityChecker();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.MRET,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.FullSerial,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        LegalityResult result = checker.Check(
            instruction,
            new HybridCPU_ISE.Tests.V5Phase2.StubHazardState { RawHazardResult = false },
            new HybridCPU_ISE.Tests.V5Phase2.StubResourceState { IsAvailableResult = true },
            PrivilegeLevel.User);

        Assert.Equal(LegalityResult.PrivilegeFault, result);
    }

    [Fact]
    public void MemoryUnit_TypedStore_UsesCanonicalOpcodeIdentity()
    {
        var bus = new HybridCPU_ISE.Tests.Phase07.FakeMemoryBus();
        var unit = new MemoryUnit(bus);
        var state = new HybridCPU_ISE.Tests.Phase07.Mem07FakeCpuState();
        state.SetReg(1, 0x120);
        state.SetReg(2, 0x11223344);

        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.SW,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.Free,
            Rd = 0,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        ulong effectiveAddress = unit.Execute(instruction, state);

        Assert.Equal(0x120UL, effectiveAddress);
        Assert.Equal(0x11223344U, bus.LoadWord(0x120));
    }

    [Fact]
    public void VmxExecutionUnit_VmxOn_UsesCanonicalOpcodeIdentity()
    {
        var csr = new CsrFile();
        var vmcs = new VmcsManager();
        var vmx = new VmxExecutionUnit(csr, vmcs);
        var state = new HybridCPU_ISE.Tests.Phase09.Vmx09FakeCpuState();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.VMXON,
            Class = InstructionClass.Vmx,
            SerializationClass = SerializationClass.VmxSerial,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        ExecutionResult result = vmx.Execute(instruction, state, PrivilegeLevel.Machine);

        Assert.False(result.VmxFaulted);
        Assert.Equal(1UL, csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
    }

    [Fact]
    public void ExecutionDispatcherV4_CanRoute_SystemDirectCompatSurface_UsesCanonicalOpcodeIdentity()
    {
        var dispatcher = new ExecutionDispatcherV4();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.FENCE,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.FullSerial,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(instruction));
    }

    [Theory]
    [InlineData(InstructionsEnum.YIELD, true)]
    [InlineData(InstructionsEnum.WFE, true)]
    [InlineData(InstructionsEnum.SEV, true)]
    [InlineData(InstructionsEnum.POD_BARRIER, true)]
    [InlineData(InstructionsEnum.VT_BARRIER, true)]
    [InlineData(InstructionsEnum.STREAM_SETUP, false)]
    [InlineData(InstructionsEnum.STREAM_START, false)]
    [InlineData(InstructionsEnum.STREAM_WAIT, false)]
    public void ExecutionDispatcherV4_RequiresPipelineEventQueueForEagerExecute_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        bool expected)
    {
        MethodInfo requiresQueue = typeof(ExecutionDispatcherV4).GetMethod(
            "RequiresWiredPipelineEventQueueForEagerExecute",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RequiresWiredPipelineEventQueueForEagerExecute method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        object? requires = requiresQueue.Invoke(null, new object[] { instruction });

        Assert.Equal(expected, Assert.IsType<bool>(requires));
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVL, true)]
    [InlineData(InstructionsEnum.VSETVLI, true)]
    [InlineData(InstructionsEnum.VSETIVLI, true)]
    [InlineData(InstructionsEnum.FENCE, false)]
    public void ExecutionDispatcherV4_IsPipelineOnlyVectorConfigSurface_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        bool expected)
    {
        MethodInfo isPipelineOnlyVectorConfig = typeof(ExecutionDispatcherV4).GetMethod(
            "IsPipelineOnlyVectorConfigSystemSurface",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IsPipelineOnlyVectorConfigSystemSurface method was not found.");

        object? isPipelineOnly = isPipelineOnlyVectorConfig.Invoke(null, new object[] { opcode });

        Assert.Equal(expected, Assert.IsType<bool>(isPipelineOnly));
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE, true)]
    [InlineData(InstructionsEnum.FENCE_I, true)]
    [InlineData(InstructionsEnum.ECALL, true)]
    [InlineData(InstructionsEnum.EBREAK, true)]
    [InlineData(InstructionsEnum.MRET, true)]
    [InlineData(InstructionsEnum.SRET, true)]
    [InlineData(InstructionsEnum.WFI, true)]
    [InlineData(InstructionsEnum.VSETVLI, false)]
    [InlineData(InstructionsEnum.STREAM_SETUP, false)]
    [InlineData(InstructionsEnum.STREAM_START, false)]
    public void ExecutionDispatcherV4_IsRetireWindowPublicationOnlySystemEagerExecuteSurface_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        bool expected)
    {
        MethodInfo isDirectCompatOnlySystem = typeof(ExecutionDispatcherV4).GetMethod(
            "IsRetireWindowPublicationOnlySystemEagerExecuteSurface",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IsRetireWindowPublicationOnlySystemEagerExecuteSurface method was not found.");

        object? isDirectCompatOnly = isDirectCompatOnlySystem.Invoke(null, new object[] { opcode });

        Assert.Equal(expected, Assert.IsType<bool>(isDirectCompatOnly));
    }

    [Theory]
    [InlineData(InstructionsEnum.VMXON, true)]
    [InlineData(InstructionsEnum.VMREAD, true)]
    [InlineData(InstructionsEnum.VMPTRLD, true)]
    [InlineData(InstructionsEnum.FENCE, false)]
    public void ExecutionDispatcherV4_IsRetireWindowPublicationOnlyVmxEagerExecuteSurface_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        bool expected)
    {
        MethodInfo isDirectCompatOnlyVmx = typeof(ExecutionDispatcherV4).GetMethod(
            "IsRetireWindowPublicationOnlyVmxEagerExecuteSurface",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IsRetireWindowPublicationOnlyVmxEagerExecuteSurface method was not found.");

        object? isDirectCompatOnly = isDirectCompatOnlyVmx.Invoke(null, new object[] { opcode });

        Assert.Equal(expected, Assert.IsType<bool>(isDirectCompatOnly));
    }

    [Theory]
    [InlineData(InstructionsEnum.STREAM_SETUP, "inner-unit stub success")]
    [InlineData(InstructionsEnum.STREAM_START, "inner-unit stub success")]
    [InlineData(InstructionsEnum.STREAM_WAIT, "CaptureRetireWindowPublications")]
    public void ExecutionDispatcherV4_ThrowUnsupportedEagerStreamControlOpcode_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        string expectedMessageToken)
    {
        MethodInfo throwUnsupportedEagerStreamControl = typeof(ExecutionDispatcherV4).GetMethod(
            "ThrowUnsupportedEagerStreamControlOpcode",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ThrowUnsupportedEagerStreamControlOpcode method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        var outer = Assert.Throws<TargetInvocationException>(
            () => throwUnsupportedEagerStreamControl.Invoke(null, new object[] { instruction }));
        InvalidOperationException ex = Assert.IsType<InvalidOperationException>(outer.InnerException);

        Assert.Contains(expectedMessageToken, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.JAL, false, TraceEventKind.JumpExecuted)]
    [InlineData(InstructionsEnum.JALR, true, TraceEventKind.JumpExecuted)]
    [InlineData(InstructionsEnum.BEQ, false, TraceEventKind.BranchNotTaken)]
    [InlineData(InstructionsEnum.BNE, true, TraceEventKind.BranchTaken)]
    public void ExecutionDispatcherV4_ClassifyControlFlowTraceEvent_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        bool pcRedirected,
        TraceEventKind expectedKind)
    {
        MethodInfo classifyControlFlowEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyControlFlowEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyControlFlowEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = 2,
            Rs2 = opcode == InstructionsEnum.JALR ? (byte)0 : (byte)3,
            Imm = 0x10,
        };

        object? traceKind = classifyControlFlowEvent.Invoke(
            null,
            new object[] { instruction, pcRedirected ? ExecutionResult.Redirect(0x200) : ExecutionResult.Ok() });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Fact]
    public void ExecutionDispatcherV4_ClassifyControlFlowTraceEvent_BeqWithZeroSecondOperand_RemainsBranchContour()
    {
        MethodInfo classifyControlFlowEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyControlFlowEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyControlFlowEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.BEQ,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = 0,
            Rs1 = 2,
            Rs2 = 0,
            Imm = 0x10,
        };

        object? traceKind = classifyControlFlowEvent.Invoke(
            null,
            new object[] { instruction, ExecutionResult.Redirect(0x200) });

        Assert.Equal(TraceEventKind.BranchTaken, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.JALR, false, 2, 0)]
    [InlineData(InstructionsEnum.BEQ, true, 2, 0)]
    public void InstructionRegistry_TryCreatePublishedControlFlowMicroOp_UsesCentralBranchAuthority(
        InstructionsEnum opcode,
        bool expectedConditional,
        byte rs1,
        byte rs2)
    {
        MethodInfo tryCreatePublishedControlFlowMicroOp = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedControlFlowMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreatePublishedControlFlowMicroOp method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0x10,
        };

        object?[] args = { instruction, null };
        object? created = tryCreatePublishedControlFlowMicroOp.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(created));

        BranchMicroOp branchMicroOp = Assert.IsType<BranchMicroOp>(args[1]);
        Assert.Equal(expectedConditional, branchMicroOp.IsConditional);
    }

    [Fact]
    public void InstructionRegistry_TryCreatePublishedControlFlowMicroOp_PreservesCanonicalAbsoluteBranchTarget()
    {
        MethodInfo tryCreatePublishedControlFlowMicroOp = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedControlFlowMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreatePublishedControlFlowMicroOp method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.BLTU,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = 11,
            Rs2 = 10,
            Imm = 0x900,
            HasAbsoluteAddressing = true,
        };

        object?[] args = { instruction, null };
        object? created = tryCreatePublishedControlFlowMicroOp.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(created));

        BranchMicroOp branchMicroOp = Assert.IsType<BranchMicroOp>(args[1]);
        Assert.True(branchMicroOp.IsConditional);
        Assert.Equal(0x900UL, branchMicroOp.TargetAddress);
        Assert.Equal((ushort)11, branchMicroOp.Reg1ID);
        Assert.Equal((ushort)10, branchMicroOp.Reg2ID);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRW, typeof(CsrReadWriteMicroOp), 0x341, (byte)1, (byte)2)]
    [InlineData(InstructionsEnum.CSRRWI, typeof(CsrReadWriteImmediateMicroOp), 0x305, (byte)1, (byte)0x1F)]
    [InlineData(InstructionsEnum.CSRRSI, typeof(CsrReadSetImmediateMicroOp), 0x300, (byte)1, (byte)5)]
    [InlineData(InstructionsEnum.CSR_CLEAR, typeof(CsrClearMicroOp), 0x000, (byte)0, (byte)0)]
    public void InstructionRegistry_TryCreatePublishedCsrMicroOp_UsesCentralCsrAuthority(
        InstructionsEnum opcode,
        Type expectedMicroOpType,
        long immediate,
        byte destinationRegister,
        byte sourceOrImmediate)
    {
        MethodInfo tryCreatePublishedCsrMicroOp = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedCsrMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreatePublishedCsrMicroOp method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = destinationRegister,
            Rs1 = sourceOrImmediate,
            Rs2 = 0,
            Imm = immediate,
            CsrAddress = CsrAddresses.Mtvec,
        };

        object?[] args = { instruction, null };
        object? created = tryCreatePublishedCsrMicroOp.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(created));

        CSRMicroOp csrMicroOp = Assert.IsAssignableFrom<CSRMicroOp>(args[1]);
        Assert.IsType(expectedMicroOpType, csrMicroOp);
        Assert.Equal((ulong)(ushort)immediate, csrMicroOp.CSRAddress);

        switch (csrMicroOp)
        {
            case CsrReadWriteMicroOp:
                Assert.Equal((ushort)sourceOrImmediate, csrMicroOp.SrcRegID);
                break;
            case CsrReadWriteImmediateMicroOp:
            case CsrReadSetImmediateMicroOp:
                Assert.Equal((ulong)sourceOrImmediate, csrMicroOp.WriteValue);
                break;
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVEXCPMASK, CsrAddresses.VexcpMask, (byte)5, "5")]
    [InlineData(InstructionsEnum.VSETVEXCPPRI, CsrAddresses.VexcpPri, (byte)0, "")]
    public void InstructionRegistry_TryCreatePublishedCsrMicroOp_PreservesVectorExceptionControlContours(
        InstructionsEnum opcode,
        ushort expectedCsrAddress,
        byte sourceRegister,
        string expectedReadRegistersCsv)
    {
        MethodInfo tryCreatePublishedCsrMicroOp = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedCsrMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreatePublishedCsrMicroOp method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.FullSerial,
            Rd = 0,
            Rs1 = sourceRegister,
            Rs2 = 0,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? created = tryCreatePublishedCsrMicroOp.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(created));

        CsrReadWriteMicroOp csrMicroOp = Assert.IsType<CsrReadWriteMicroOp>(args[1]);
        Assert.Equal((ulong)expectedCsrAddress, csrMicroOp.CSRAddress);
        Assert.Equal(
            ParseRegisterCsv(expectedReadRegistersCsv),
            Assert.IsAssignableFrom<IReadOnlyList<int>>(csrMicroOp.ReadRegisters));
        Assert.False(csrMicroOp.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE, (byte)0, (byte)0, "")]
    [InlineData(InstructionsEnum.ECALL, (byte)0, (byte)0, "17")]
    [InlineData(InstructionsEnum.VSETVL, (byte)5, (byte)6, "5,6")]
    [InlineData(InstructionsEnum.VSETVLI, (byte)7, (byte)0, "7")]
    [InlineData(InstructionsEnum.VSETIVLI, (byte)0, (byte)0, "")]
    public void InstructionRegistry_TryResolvePublishedSystemReadRegisters_UsesCentralSystemAuthority(
        InstructionsEnum opcode,
        byte rs1,
        byte rs2,
        string expectedReadRegistersCsv)
    {
        MethodInfo tryResolvePublishedSystemReadRegisters = typeof(InstructionRegistry).GetMethod(
            "TryResolvePublishedSystemReadRegisters",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryResolvePublishedSystemReadRegisters method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.System,
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = 0,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? resolved = tryResolvePublishedSystemReadRegisters.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(resolved));
        Assert.Equal(
            ParseRegisterCsv(expectedReadRegistersCsv),
            Assert.IsAssignableFrom<IReadOnlyList<int>>(args[1]));
    }

    [Theory]
    [InlineData(InstructionsEnum.VMXON, VmxOperationKind.VmxOn, 0, 0, 0)]
    [InlineData(InstructionsEnum.VMLAUNCH, VmxOperationKind.VmLaunch, 0, 0, 0)]
    [InlineData(InstructionsEnum.VMREAD, VmxOperationKind.VmRead, 1, 2, 0)]
    [InlineData(InstructionsEnum.VMWRITE, VmxOperationKind.VmWrite, 0, 2, 3)]
    [InlineData(InstructionsEnum.VMCLEAR, VmxOperationKind.VmClear, 0, 4, 0)]
    [InlineData(InstructionsEnum.VMPTRLD, VmxOperationKind.VmPtrLd, 0, 5, 0)]
    public void InstructionRegistry_TryResolvePublishedVmxOperationKind_UsesCentralVmxAuthority(
        InstructionsEnum opcode,
        VmxOperationKind expectedOperationKind,
        byte rd,
        byte rs1,
        byte rs2)
    {
        MethodInfo tryResolvePublishedVmxOperationKind = typeof(InstructionRegistry).GetMethod(
            "TryResolvePublishedVmxOperationKind",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryResolvePublishedVmxOperationKind method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Vmx,
            SerializationClass = SerializationClass.VmxSerial,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? resolved = tryResolvePublishedVmxOperationKind.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(resolved));
        Assert.Equal(expectedOperationKind, Assert.IsType<VmxOperationKind>(args[1]));
    }

    private static int[] ParseRegisterCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<int>();
        }

        string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var registers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            registers[i] = int.Parse(parts[i]);
        }

        return registers;
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE, InstructionClass.System, SerializationClass.MemoryOrdered, SystemEventKind.Fence)]
    [InlineData(InstructionsEnum.ECALL, InstructionClass.System, SerializationClass.FullSerial, SystemEventKind.Ecall)]
    [InlineData(InstructionsEnum.SRET, InstructionClass.System, SerializationClass.FullSerial, SystemEventKind.Sret)]
    [InlineData(InstructionsEnum.WFI, InstructionClass.System, SerializationClass.FullSerial, SystemEventKind.Wfi)]
    [InlineData(InstructionsEnum.YIELD, InstructionClass.SmtVt, SerializationClass.Free, SystemEventKind.Yield)]
    [InlineData(InstructionsEnum.WFE, InstructionClass.SmtVt, SerializationClass.FullSerial, SystemEventKind.Wfe)]
    [InlineData(InstructionsEnum.POD_BARRIER, InstructionClass.SmtVt, SerializationClass.FullSerial, SystemEventKind.PodBarrier)]
    public void InstructionRegistry_TryResolvePublishedSystemEventKind_UsesCentralSystemAuthority(
        InstructionsEnum opcode,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        SystemEventKind expectedEventKind)
    {
        MethodInfo tryResolvePublishedSystemEventKind = typeof(InstructionRegistry).GetMethod(
            "TryResolvePublishedSystemEventKind",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryResolvePublishedSystemEventKind method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = instructionClass,
            SerializationClass = serializationClass,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? resolved = tryResolvePublishedSystemEventKind.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(resolved));
        Assert.Equal(expectedEventKind, Assert.IsType<SystemEventKind>(args[1]));
    }

    [Theory]
    [InlineData(InstructionsEnum.STREAM_SETUP, false)]
    [InlineData(InstructionsEnum.STREAM_START, false)]
    [InlineData(InstructionsEnum.STREAM_WAIT, true)]
    public void InstructionRegistry_TryResolvePublishedStreamControlRetireContour_UsesCentralStreamControlAuthority(
        InstructionsEnum opcode,
        bool expectedSerializingBoundaryFollowThrough)
    {
        MethodInfo tryResolvePublishedStreamControlRetireContour = typeof(InstructionRegistry).GetMethod(
            "TryResolvePublishedStreamControlRetireContour",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryResolvePublishedStreamControlRetireContour method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? resolved = tryResolvePublishedStreamControlRetireContour.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(resolved));
        Assert.Equal(expectedSerializingBoundaryFollowThrough, Assert.IsType<bool>(args[1]));
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRW, 0x341, (byte)1, (byte)2, true)]
    [InlineData(InstructionsEnum.CSR_CLEAR, 0x000, (byte)0, (byte)0, false)]
    public void ExecutionDispatcherV4_RequiresWiredCsrFileForExecutionSurface_UsesCentralCsrAuthority(
        InstructionsEnum opcode,
        long immediate,
        byte destinationRegister,
        byte sourceRegister,
        bool expected)
    {
        var dispatcher = new ExecutionDispatcherV4();
        MethodInfo requiresWiredCsrFileForExecutionSurface = typeof(ExecutionDispatcherV4).GetMethod(
            "RequiresWiredCsrFileForExecutionSurface",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RequiresWiredCsrFileForExecutionSurface method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = destinationRegister,
            Rs1 = sourceRegister,
            Rs2 = 0,
            Imm = immediate,
            CsrAddress = CsrAddresses.Mtvec,
        };

        object? requires = requiresWiredCsrFileForExecutionSurface.Invoke(dispatcher, new object[] { instruction });

        Assert.Equal(expected, Assert.IsType<bool>(requires));
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE, TraceEventKind.FenceExecuted)]
    [InlineData(InstructionsEnum.FENCE_I, TraceEventKind.FenceExecuted)]
    [InlineData(InstructionsEnum.ECALL, TraceEventKind.TrapTaken)]
    [InlineData(InstructionsEnum.EBREAK, TraceEventKind.TrapTaken)]
    [InlineData(InstructionsEnum.MRET, TraceEventKind.PrivilegeReturn)]
    [InlineData(InstructionsEnum.SRET, TraceEventKind.PrivilegeReturn)]
    [InlineData(InstructionsEnum.WFI, TraceEventKind.WfiEntered)]
    public void ExecutionDispatcherV4_ClassifySystemTraceEvent_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        TraceEventKind expectedKind)
    {
        MethodInfo classifySystemEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifySystemEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifySystemEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.System,
            SerializationClass = opcode == InstructionsEnum.FENCE
                ? SerializationClass.MemoryOrdered
                : SerializationClass.FullSerial,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        object? traceKind = classifySystemEvent.Invoke(null, new object[] { instruction });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.YIELD, TraceEventKind.VtYield)]
    [InlineData(InstructionsEnum.STREAM_WAIT, TraceEventKind.VtYield)]
    [InlineData(InstructionsEnum.WFE, TraceEventKind.VtWfe)]
    [InlineData(InstructionsEnum.SEV, TraceEventKind.VtSev)]
    [InlineData(InstructionsEnum.POD_BARRIER, TraceEventKind.PodBarrierEntered)]
    [InlineData(InstructionsEnum.VT_BARRIER, TraceEventKind.VtBarrierEntered)]
    public void ExecutionDispatcherV4_ClassifySmtVtTraceEvent_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        TraceEventKind expectedKind)
    {
        MethodInfo classifySmtVtEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifySmtVtEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifySmtVtEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.SmtVt,
            SerializationClass = opcode == InstructionsEnum.YIELD
                ? SerializationClass.Free
                : SerializationClass.FullSerial,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        object? traceKind = classifySmtVtEvent.Invoke(null, new object[] { instruction });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.CSR_CLEAR, TraceEventKind.CsrWrite)]
    [InlineData(InstructionsEnum.CSRRW, TraceEventKind.CsrWrite)]
    [InlineData(InstructionsEnum.CSRRWI, TraceEventKind.CsrWrite)]
    [InlineData(InstructionsEnum.CSRRS, TraceEventKind.CsrRead)]
    [InlineData(InstructionsEnum.CSRRCI, TraceEventKind.CsrRead)]
    public void ExecutionDispatcherV4_ClassifyCsrTraceEvent_UsesCanonicalOpcodeIdentity(
        InstructionsEnum opcode,
        TraceEventKind expectedKind)
    {
        MethodInfo classifyCsrEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyCsrEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyCsrEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 0,
            Imm = 0x341,
            CsrAddress = CsrAddresses.Mepc,
        };

        object? traceKind = classifyCsrEvent.Invoke(null, new object[] { instruction });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER, SerializationClass.Free, TraceEventKind.LoadExecuted)]
    [InlineData(InstructionsEnum.VSCATTER, SerializationClass.MemoryOrdered, TraceEventKind.StoreExecuted)]
    [InlineData(InstructionsEnum.MTILE_STORE, SerializationClass.MemoryOrdered, TraceEventKind.StoreExecuted)]
    public void ExecutionDispatcherV4_ClassifyMemoryTraceEvent_UsesCanonicalSerializationIdentity(
        InstructionsEnum opcode,
        SerializationClass serializationClass,
        TraceEventKind expectedKind)
    {
        MethodInfo classifyMemoryEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyMemoryEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyMemoryEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Memory,
            SerializationClass = serializationClass,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
        };

        object? traceKind = classifyMemoryEvent.Invoke(null, new object[] { instruction });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.LR_W, 4)]
    [InlineData(InstructionsEnum.SC_D, 8)]
    [InlineData(InstructionsEnum.AMOADD_W, 4)]
    [InlineData(InstructionsEnum.AMOMAXU_D, 8)]
    public void InstructionRegistry_TryResolvePublishedAtomicAccessSize_UsesCentralAtomicAuthority(
        InstructionsEnum opcode,
        byte expectedAccessSize)
    {
        MethodInfo tryResolvePublishedAtomicAccessSize = typeof(InstructionRegistry).GetMethod(
            "TryResolvePublishedAtomicAccessSize",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryResolvePublishedAtomicAccessSize method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Atomic,
            SerializationClass = SerializationClass.AtomicSerial,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
        };

        object?[] args = { instruction, null };
        object? resolved = tryResolvePublishedAtomicAccessSize.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(resolved));
        Assert.Equal(expectedAccessSize, Assert.IsType<byte>(args[1]));
    }

    [Theory]
    [InlineData(InstructionsEnum.LR_W, 0UL, TraceEventKind.LrExecuted)]
    [InlineData(InstructionsEnum.SC_D, 0UL, TraceEventKind.ScSucceeded)]
    [InlineData(InstructionsEnum.SC_D, 1UL, TraceEventKind.ScFailed)]
    [InlineData(InstructionsEnum.AMOADD_W, 0UL, TraceEventKind.AmoWordExecuted)]
    [InlineData(InstructionsEnum.AMOAND_W, 0UL, TraceEventKind.AmoWordExecuted)]
    [InlineData(InstructionsEnum.AMOSWAP_W, 0UL, TraceEventKind.AmoWordExecuted)]
    [InlineData(InstructionsEnum.AMOADD_D, 0UL, TraceEventKind.AmoDwordExecuted)]
    [InlineData(InstructionsEnum.AMOMIN_D, 0UL, TraceEventKind.AmoDwordExecuted)]
    [InlineData(InstructionsEnum.AMOMAXU_D, 0UL, TraceEventKind.AmoDwordExecuted)]
    public void ExecutionDispatcherV4_ClassifyAtomicTraceEvent_UsesCanonicalDescriptorIdentity(
        InstructionsEnum opcode,
        ulong resultValue,
        TraceEventKind expectedKind)
    {
        MethodInfo classifyAtomicEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyAtomicEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyAtomicEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Atomic,
            SerializationClass = SerializationClass.AtomicSerial,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
        };

        object? traceKind = classifyAtomicEvent.Invoke(
            null,
            new object[] { instruction, ExecutionResult.Ok(resultValue) });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Theory]
    [InlineData(InstructionsEnum.CSR_CLEAR, 0x305, 0UL)]
    [InlineData(InstructionsEnum.CSRRW, 0x341, 0x341UL)]
    [InlineData(InstructionsEnum.CSRRWI, 0x300, 0x300UL)]
    public void ExecutionDispatcherV4_GetTracePayload_UsesCanonicalOpcodeIdentity(
        InstructionsEnum opcode,
        long immediate,
        ulong expectedPayload)
    {
        MethodInfo getTracePayload = typeof(ExecutionDispatcherV4).GetMethod(
            "GetTracePayload",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GetTracePayload method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = 0,
            Rs1 = 1,
            Rs2 = 0,
            Imm = immediate,
            CsrAddress = CsrAddresses.Mtvec,
        };

        object? payload = getTracePayload.Invoke(null, new object[] { instruction, TraceEventKind.CsrWrite });

        Assert.Equal(expectedPayload, Assert.IsType<ulong>(payload));
    }

    [Theory]
    [InlineData(InstructionsEnum.VMXON, false, TraceEventKind.VmxOn)]
    [InlineData(InstructionsEnum.VMXOFF, false, TraceEventKind.VmxOff)]
    [InlineData(InstructionsEnum.VMREAD, false, TraceEventKind.VmcsRead)]
    [InlineData(InstructionsEnum.VMWRITE, false, TraceEventKind.VmcsWrite)]
    [InlineData(InstructionsEnum.VMCLEAR, false, TraceEventKind.VmcsWrite)]
    [InlineData(InstructionsEnum.VMPTRLD, false, TraceEventKind.VmcsWrite)]
    [InlineData(InstructionsEnum.VMLAUNCH, false, TraceEventKind.VmEntry)]
    [InlineData(InstructionsEnum.VMLAUNCH, true, TraceEventKind.VmEntryFailed)]
    [InlineData(InstructionsEnum.VMRESUME, false, TraceEventKind.VmEntry)]
    [InlineData(InstructionsEnum.VMRESUME, true, TraceEventKind.VmEntryFailed)]
    public void ExecutionDispatcherV4_ClassifyVmxTraceEvent_UsesCanonicalOpcodeIdentity(
        InstructionsEnum opcode,
        bool vmxFaulted,
        TraceEventKind expectedKind)
    {
        MethodInfo classifyVmxEvent = typeof(ExecutionDispatcherV4).GetMethod(
            "ClassifyVmxEvent",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClassifyVmxEvent method was not found.");

        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Vmx,
            SerializationClass = SerializationClass.VmxSerial,
            Rd = 0,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        ExecutionResult result = vmxFaulted ? ExecutionResult.VmxFault() : ExecutionResult.Ok();
        object? traceKind = classifyVmxEvent.Invoke(null, new object[] { instruction, result });

        Assert.Equal(expectedKind, Assert.IsType<TraceEventKind>(traceKind));
    }

    [Fact]
    public void ExecutionDispatcherV4_ExecuteScalarAlu_UsesCanonicalOpcodeIdentity()
    {
        var dispatcher = new ExecutionDispatcherV4();
        var state = new HybridCPU_ISE.Tests.Phase04.FakeCpuState();
        state.SetReg(1, 7);
        state.SetReg(2, 5);
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Addition,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        ExecutionResult result = dispatcher.Execute(instruction, state);

        Assert.Equal(12UL, result.Value);
        Assert.Equal(12UL, state.ReadIntRegister(3));
    }

    [Fact]
    public void ExecutionDispatcherV4_ResolveSystemDirectCompatRetireTransaction_UsesCanonicalOpcodeIdentity()
    {
        var dispatcher = new ExecutionDispatcherV4();
        var state = new HybridCPU_ISE.Tests.Phase04.FakeCpuState();
        state.SetInstructionPointer(0x4000);
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.FENCE,
            Class = InstructionClass.System,
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.FENCE),
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        RetireWindowCaptureSnapshot transaction =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state);

        Assert.True(transaction.HasPipelineEvent);
        Assert.IsType<FenceEvent>(transaction.PipelineEvent);
        Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
    }

    [Fact]
    public void ExecutionDispatcherV4_ResolveMemoryDirectCompatRetireTransaction_UsesCanonicalOpcodeIdentity()
    {
        var bus = new HybridCPU_ISE.Tests.Phase07.FakeMemoryBus();
        var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(bus));
        var state = new HybridCPU_ISE.Tests.Phase07.Mem07FakeCpuState();
        state.SetReg(1, 0x200);
        state.SetReg(2, 0xAA55CC33);
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.SW,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.Free,
            Rd = 0,
            Rs1 = 1,
            Rs2 = 2,
            Imm = 0,
        };

        RetireWindowCaptureSnapshot transaction =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state);

        Assert.True(transaction.HasScalarMemoryStoreEffect);
        Assert.Equal(0x200UL, transaction.MemoryAddress);
        Assert.Equal(0xAA55CC33UL, transaction.MemoryData);
        Assert.Equal((byte)4, transaction.MemoryAccessSize);
    }

    [Fact]
    public void ExecutionDispatcherV4_ResolveCsrDirectCompatRetireTransaction_UsesCanonicalOpcodeIdentity()
    {
        var dispatcher = new ExecutionDispatcherV4();
        var state = new HybridCPU_ISE.Tests.Phase04.FakeCpuState();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.CSR_CLEAR,
            Class = InstructionClass.Csr,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        RetireWindowCaptureSnapshot transaction =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state);

        Assert.True(transaction.HasCsrEffect);
        Assert.True(transaction.CsrEffect.ClearsArchitecturalExceptionState);
    }

    [Fact]
    public void ExecutionDispatcherV4_ResolveSmtVtDirectCompatRetireTransaction_UsesCanonicalOpcodeIdentity()
    {
        var dispatcher = new ExecutionDispatcherV4();
        var state = new HybridCPU_ISE.Tests.Phase04.FakeCpuState();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.STREAM_WAIT,
            Class = InstructionClass.SmtVt,
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.STREAM_WAIT),
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
        };

        RetireWindowCaptureSnapshot transaction =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state);

        Assert.Equal(RetireWindowCaptureEffectKind.SerializingBoundary, transaction.TypedEffectKind);
        Assert.True(transaction.HasSerializingBoundaryEffect);
    }

    [Fact]
    public void InstructionIr_NoLongerPublishesLegacyOpcodeBridge()
    {
        Assert.Null(typeof(InstructionIR).GetProperty("Opcode"));
    }

    [Fact]
    public void DecodedInstruction_NoLongerPublishesLegacyOpcodeBridge()
    {
        Assert.Null(typeof(DecodedInstruction).GetProperty("Opcode"));
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE, InternalOpKind.Fence)]
    [InlineData(InstructionsEnum.FENCE_I, InternalOpKind.FenceI)]
    [InlineData(InstructionsEnum.ECALL, InternalOpKind.Ecall)]
    [InlineData(InstructionsEnum.EBREAK, InternalOpKind.Ebreak)]
    [InlineData(InstructionsEnum.MRET, InternalOpKind.Mret)]
    [InlineData(InstructionsEnum.SRET, InternalOpKind.Sret)]
    [InlineData(InstructionsEnum.WFI, InternalOpKind.Wfi)]
    [InlineData(InstructionsEnum.CSRRW, InternalOpKind.CsrReadWrite)]
    [InlineData(InstructionsEnum.CSRRSI, InternalOpKind.CsrReadSet)]
    [InlineData(InstructionsEnum.CSRRCI, InternalOpKind.CsrReadClear)]
    [InlineData(InstructionsEnum.CSR_CLEAR, InternalOpKind.CsrClear)]
    [InlineData(InstructionsEnum.YIELD, InternalOpKind.Yield)]
    [InlineData(InstructionsEnum.STREAM_WAIT, InternalOpKind.Yield)]
    [InlineData(InstructionsEnum.WFE, InternalOpKind.Wfe)]
    [InlineData(InstructionsEnum.SEV, InternalOpKind.Sev)]
    [InlineData(InstructionsEnum.POD_BARRIER, InternalOpKind.PodBarrier)]
    [InlineData(InstructionsEnum.VT_BARRIER, InternalOpKind.VtBarrier)]
    [InlineData(InstructionsEnum.STREAM_SETUP, InternalOpKind.Store)]
    [InlineData(InstructionsEnum.STREAM_START, InternalOpKind.Store)]
    [InlineData(InstructionsEnum.VMXON, InternalOpKind.VmxOn)]
    [InlineData(InstructionsEnum.VMRESUME, InternalOpKind.VmResume)]
    [InlineData(InstructionsEnum.VMREAD, InternalOpKind.VmRead)]
    [InlineData(InstructionsEnum.VMWRITE, InternalOpKind.VmWrite)]
    [InlineData(InstructionsEnum.VMCLEAR, InternalOpKind.VmClear)]
    [InlineData(InstructionsEnum.VMPTRLD, InternalOpKind.VmPtrLd)]
    [InlineData(InstructionsEnum.Interrupt, InternalOpKind.Interrupt)]
    public void InternalOpBuilder_MapToKind_UsesCanonicalDescriptorForPublishedSystemFamiliesWithRetainedFallback(
        InstructionsEnum opcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind((ushort)opcode));
    }

    [Theory]
    [InlineData(InstructionsEnum.Division, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.Modulus, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.SLT, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.SLTI, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.BLT, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.BGE, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.SRAI, InternalOpFlags.ArithmeticShift)]
    [InlineData(InstructionsEnum.JumpIfBelow, InternalOpFlags.Signed)]
    [InlineData(InstructionsEnum.BLTU, InternalOpFlags.None)]
    [InlineData(InstructionsEnum.SLTU, InternalOpFlags.None)]
    public void InternalOpBuilder_Build_UsesCanonicalDescriptorForPublishedFlagsWithRetainedLegacyFallback(
        InstructionsEnum opcode,
        InternalOpFlags expectedFlags)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 4,
        };

        InternalOp op = new InternalOpBuilder().Build(instruction);

        Assert.Equal(expectedFlags, op.Flags);
    }

    [Theory]
    [InlineData(InstructionsEnum.JAL, InternalOpKind.Jal)]
    [InlineData(InstructionsEnum.JALR, InternalOpKind.Jalr)]
    [InlineData(InstructionsEnum.BEQ, InternalOpKind.Branch)]
    [InlineData(InstructionsEnum.BLT, InternalOpKind.Branch)]
    [InlineData(InstructionsEnum.JumpIfEqual, InternalOpKind.Branch)]
    [InlineData(InstructionsEnum.JumpIfNotEqual, InternalOpKind.Branch)]
    public void InternalOpBuilder_MapToKind_UsesCanonicalDescriptorForPublishedControlFlowWithRetainedLegacyFallback(
        InstructionsEnum opcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind((ushort)opcode));
    }

    [Theory]
    [InlineData(InstructionsEnum.LR_W, InternalOpKind.LrW)]
    [InlineData(InstructionsEnum.AMOSWAP_W, InternalOpKind.AmoWord)]
    [InlineData(InstructionsEnum.SC_D, InternalOpKind.ScD)]
    [InlineData(InstructionsEnum.AMOADD_W, InternalOpKind.AmoWord)]
    [InlineData(InstructionsEnum.AMOOR_W, InternalOpKind.AmoWord)]
    [InlineData(InstructionsEnum.AMOMIN_D, InternalOpKind.AmoDword)]
    [InlineData(InstructionsEnum.AMOMAXU_D, InternalOpKind.AmoDword)]
    public void InternalOpBuilder_MapToKind_UsesCanonicalDescriptorForPublishedAtomicContours(
        InstructionsEnum opcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind((ushort)opcode));
    }

    [Theory]
    [InlineData(InstructionsEnum.LB, InternalOpKind.Load)]
    [InlineData(InstructionsEnum.SD, InternalOpKind.Store)]
    [InlineData(InstructionsEnum.Load, InternalOpKind.Load)]
    public void InternalOpBuilder_MapToKind_UsesCanonicalDescriptorForPublishedScalarMemoryWithRetainedFallback(
        InstructionsEnum opcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind((ushort)opcode));
    }

    [Theory]
    [InlineData(InstructionsEnum.Addition, InternalOpKind.Add)]
    [InlineData(InstructionsEnum.Division, InternalOpKind.Div)]
    [InlineData(InstructionsEnum.SLT, InternalOpKind.Slt)]
    [InlineData(InstructionsEnum.MULHSU, InternalOpKind.MulHsu)]
    [InlineData(InstructionsEnum.REMU, InternalOpKind.Remu)]
    [InlineData(InstructionsEnum.ADDI, InternalOpKind.AddI)]
    [InlineData(InstructionsEnum.SRAI, InternalOpKind.SraI)]
    [InlineData(InstructionsEnum.LUI, InternalOpKind.Lui)]
    [InlineData(InstructionsEnum.Modulus, InternalOpKind.Rem)]
    [InlineData(InstructionsEnum.Move_Num, InternalOpKind.Lui)]
    public void InternalOpBuilder_MapToKind_UsesCanonicalDescriptorForPublishedScalarContoursWithRetainedFallback(
        InstructionsEnum opcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind((ushort)opcode));
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static DecodedBundleDependencySummary AnalyzeSingleSlotDependencySummary(InstructionIR instruction)
    {
        var bundle = new DecodedInstructionBundle(
            bundleAddress: 0x4400,
            bundleSerial: 81,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(0, instruction)
            },
            bundleMetadata: BundleMetadata.Default);

        BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(bundle);

        Assert.True(legality.DependencySummary.HasValue);
        return legality.DependencySummary.Value;
    }

    private static void AssertStructuralMemoryMask(ResourceBitset aggregateResourceMask, int expectedStructuralKind)
    {
        ResourceBitset loadMask = aggregateResourceMask & ResourceMaskBuilder.ForLoad();
        ResourceBitset storeMask = aggregateResourceMask & ResourceMaskBuilder.ForStore();
        ResourceBitset atomicMask = aggregateResourceMask & ResourceMaskBuilder.ForAtomic();

        Assert.Equal(expectedStructuralKind == 0, loadMask.IsNonZero);
        Assert.Equal(expectedStructuralKind == 1, storeMask.IsNonZero);
        Assert.Equal(expectedStructuralKind == 2, atomicMask.IsNonZero);
    }
}


