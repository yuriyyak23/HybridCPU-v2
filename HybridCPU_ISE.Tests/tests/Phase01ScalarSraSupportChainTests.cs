using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.Phase04;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class Phase01IsaSurfaceNormalizationTests
{
    [Fact]
    public void RuntimeSupportSurface_KeepsNonExecutableContoursOutOfExecutableClaims()
    {
        foreach (string mnemonic in IsaV4Surface.DescriptorOnlyOpcodes)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
            Assert.False(status.IsExecutableClaim);
        }

        foreach (string mnemonic in IsaV4Surface.ParserOnlyOpcodes)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.ParserOnly, status.Status);
            Assert.False(status.IsExecutableClaim);
        }

        foreach (string mnemonic in IsaV4Surface.OptionalDisabledOpcodes)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalDisabled, status.Status);
            Assert.False(status.IsExecutableClaim);
        }

        foreach (string mnemonic in IsaV4Surface.ReservedOpcodes)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.False(status.IsExecutableClaim);
        }

        foreach (string mnemonic in IsaV4Surface.CarrierOnlyOpcodes)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.CarrierOnly, status.Status);
            Assert.False(status.IsExecutableClaim);
        }
    }
}

public sealed class Phase01ScalarSraSupportChainTests
{
    [Fact]
    public void ScalarSra_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(300, (ushort)InstructionsEnum.SRA);
        Assert.Equal((ushort)InstructionsEnum.SRA, IsaOpcodeValues.SRA);
        Assert.Contains("SRA", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SRA", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSra_MetadataAndClassifierPublishScalarAluFreeSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SRA));

        Assert.Equal("SRA", info.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SRA));
        Assert.Equal(InternalOpKind.Sra, InternalOpBuilder.MapToKind(IsaOpcodeValues.SRA));
    }

    [Fact]
    public void ScalarSra_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSraInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SRA, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSra_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSraInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSra_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRA,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.SRA, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.SRA,
                DataTypeEnum.INT32,
                reg1: 32,
                reg2: 5,
                reg3: 6));
    }

    [Fact]
    public void ScalarSra_RegistryMaterializesScalarMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSraMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SRA);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRA));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SRA, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x8000_0000_0000_0000UL, 1UL, 0xC000_0000_0000_0000UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFF0UL, 2UL, 0xFFFF_FFFF_FFFF_FFFCUL)]
    [InlineData(0x8000_0000_0000_0000UL, 65UL, 0xC000_0000_0000_0000UL)]
    [InlineData(0x8000_0000_0000_0000UL, 64UL, 0x8000_0000_0000_0000UL)]
    public void ScalarSra_ExecutionDispatcherUsesArithmeticRightShiftSemantics(
        ulong sourceValue,
        ulong shiftAmount,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, sourceValue);
        state.SetReg(6, shiftAmount);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSraIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSra_MicroOpWritebackRetiresToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5100, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_FFFF_F000UL);
        core.WriteCommittedArch(vtId, 6, 4);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSraMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_FFFF_FF00UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSraMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRA,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRA, context));
    }

    private static InstructionIR CreateScalarSraIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SRA,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSraInstruction(byte rd, byte rs1, byte rs2) => new()
    {
        OpCode = (uint)InstructionsEnum.SRA,
        DataTypeValue = DataTypeEnum.INT32,
        PredicateMask = 0xFF,
        DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
        StreamLength = 1,
        Stride = 0
    };
}

public sealed class Phase02ScalarAddiwSupportChainTests
{
    [Fact]
    public void ScalarAddiw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(301, (ushort)InstructionsEnum.ADDIW);
        Assert.Equal((ushort)InstructionsEnum.ADDIW, IsaOpcodeValues.ADDIW);
        Assert.Contains("ADDIW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("ADDIW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarAddiw_MetadataAndClassifierPublishScalarImmediateWordSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.ADDIW));

        Assert.Equal("ADDIW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.ADDIW));
        Assert.Equal(InternalOpKind.AddIW, InternalOpBuilder.MapToKind(IsaOpcodeValues.ADDIW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarAddiwIr(imm: 1));
        Assert.Equal(InternalOpKind.AddIW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarAddiw_DecoderAcceptsCanonicalScalarImmediateAbi()
    {
        VLIW_Instruction instruction = CreateScalarAddiwInstruction(rd: 7, rs1: 5, imm: -1);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.ADDIW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(-1, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarAddiw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarAddiwInstruction(rd: 7, rs1: 5, imm: 1);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarAddiw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.ADDIW,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5,
            immediate: -1);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(unchecked((ushort)-1), instruction.Immediate);
        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, 0);
        Assert.Equal(IsaOpcodeValues.ADDIW, ir.CanonicalOpcode.Value);
        Assert.Equal(-1, ir.Imm);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarImmediate(
                (uint)InstructionsEnum.ADDIW,
                DataTypeEnum.INT32,
                destReg: 32,
                srcReg: 5,
                immediate: 1));
    }

    [Fact]
    public void ScalarAddiw_RegistryMaterializesScalarImmediateMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarAddiwMicroOp(imm: -1);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.ADDIW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ADDIW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.ADDIW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal(ulong.MaxValue, microOp.Immediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarAddiw_RegistryFailsClosedWithoutProjectedImmediate()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ADDIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ADDIW, context));

        Assert.Contains("requires projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0UL, -1, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 1, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 1, 0UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 1, 2UL)]
    public void ScalarAddiw_ExecutionDispatcherUsesWordAddAndSignExtendsResult(
        ulong rs1,
        short imm,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarAddiwIr(imm),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarAddiw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5200, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_7FFF_FFFFUL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarAddiwMicroOp(imm: 1);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarAddiwMicroOp(short imm)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ADDIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = unchecked((ushort)imm),
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ADDIW, context));
    }

    private static InstructionIR CreateScalarAddiwIr(short imm) => new()
    {
        CanonicalOpcode = InstructionsEnum.ADDIW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = imm,
    };

    private static VLIW_Instruction CreateScalarAddiwInstruction(byte rd, byte rs1, short imm) =>
        InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.ADDIW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            imm);
}

public sealed class Phase02ScalarAddwSupportChainTests
{
    [Fact]
    public void ScalarAddw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(302, (ushort)InstructionsEnum.ADDW);
        Assert.Equal((ushort)InstructionsEnum.ADDW, IsaOpcodeValues.ADDW);
        Assert.Contains("ADDW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("ADDW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarAddw_MetadataAndClassifierPublishScalarRegisterWordSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.ADDW));

        Assert.Equal("ADDW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.ADDW));
        Assert.Equal(InternalOpKind.AddW, InternalOpBuilder.MapToKind(IsaOpcodeValues.ADDW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarAddwIr());
        Assert.Equal(InternalOpKind.AddW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarAddw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarAddwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.ADDW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarAddw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarAddwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarAddw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.ADDW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.ADDW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.ADDW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 32,
                reg3: 6));
    }

    [Fact]
    public void ScalarAddw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarAddwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.ADDW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ADDW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.ADDW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0UL, 0x0000_0000_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 1UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 1UL, 0UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 1UL, 2UL)]
    [InlineData(1UL, 0xFFFF_FFFF_0000_0001UL, 2UL)]
    public void ScalarAddw_ExecutionDispatcherUsesWordAddAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarAddwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarAddw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5300, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_7FFF_FFFFUL);
        core.WriteCommittedArch(vtId, 6, 1);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarAddwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarAddwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ADDW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ADDW, context));
    }

    private static InstructionIR CreateScalarAddwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.ADDW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarAddwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.ADDW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase03ScalarSubwSupportChainTests
{
    [Fact]
    public void ScalarSubw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(303, (ushort)InstructionsEnum.SUBW);
        Assert.Equal((ushort)InstructionsEnum.SUBW, IsaOpcodeValues.SUBW);
        Assert.Contains("SUBW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SUBW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSubw_MetadataAndClassifierPublishScalarRegisterWordSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SUBW));

        Assert.Equal("SUBW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SUBW));
        Assert.Equal(InternalOpKind.SubW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SUBW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSubwIr());
        Assert.Equal(InternalOpKind.SubW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarSubw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSubwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SUBW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSubw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSubwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSubw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SUBW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.SUBW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.SUBW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarSubw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSubwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SUBW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SUBW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SUBW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0UL, 1UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 1UL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0UL, 0x0000_0000_8000_0000UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 1UL, 0UL)]
    [InlineData(1UL, 0xFFFF_FFFF_0000_0001UL, 0UL)]
    public void ScalarSubw_ExecutionDispatcherUsesWordSubtractAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSubwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSubw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5400, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0);
        core.WriteCommittedArch(vtId, 6, 0x0000_0000_8000_0000UL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSubwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSubwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SUBW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SUBW, context));
    }

    private static InstructionIR CreateScalarSubwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SUBW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSubwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SUBW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase04ScalarSllwSupportChainTests
{
    [Fact]
    public void ScalarSllw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(304, (ushort)InstructionsEnum.SLLW);
        Assert.Equal((ushort)InstructionsEnum.SLLW, IsaOpcodeValues.SLLW);
        Assert.Contains("SLLW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SLLW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSllw_MetadataAndClassifierPublishScalarRegisterWordShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SLLW));

        Assert.Equal("SLLW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SLLW));
        Assert.Equal(InternalOpKind.SllW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SLLW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSllwIr());
        Assert.Equal(InternalOpKind.SllW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarSllw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSllwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SLLW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSllw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSllwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSllw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SLLW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.SLLW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.SLLW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarSllw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSllwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SLLW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SLLW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SLLW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(1UL, 31UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 32UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 1UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_4000_0000UL, 1UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 1UL, 0xFFFF_FFFF_FFFF_FFFEUL)]
    public void ScalarSllw_ExecutionDispatcherUsesWordShiftAmountAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSllwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSllw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5500, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_4000_0000UL);
        core.WriteCommittedArch(vtId, 6, 1);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSllwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSllwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SLLW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SLLW, context));
    }

    private static InstructionIR CreateScalarSllwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SLLW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSllwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SLLW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase05ScalarSrlwSupportChainTests
{
    [Fact]
    public void ScalarSrlw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(305, (ushort)InstructionsEnum.SRLW);
        Assert.Equal((ushort)InstructionsEnum.SRLW, IsaOpcodeValues.SRLW);
        Assert.Contains("SRLW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SRLW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSrlw_MetadataAndClassifierPublishScalarRegisterWordShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SRLW));

        Assert.Equal("SRLW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SRLW));
        Assert.Equal(InternalOpKind.SrlW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SRLW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSrlwIr());
        Assert.Equal(InternalOpKind.SrlW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarSrlw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrlwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SRLW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSrlw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrlwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSrlw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRLW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.SRLW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.SRLW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarSrlw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSrlwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SRLW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRLW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SRLW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_8000_0000UL, 1UL, 0x0000_0000_4000_0000UL)]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 32UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 1UL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 31UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 1UL, 0x0000_0000_0000_0000UL)]
    public void ScalarSrlw_ExecutionDispatcherUsesLowWordLogicalShiftAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSrlwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSrlw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5600, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_8000_0000UL);
        core.WriteCommittedArch(vtId, 6, 32);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSrlwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSrlwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRLW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRLW, context));
    }

    private static InstructionIR CreateScalarSrlwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SRLW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSrlwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRLW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase06ScalarSrawSupportChainTests
{
    [Fact]
    public void ScalarSraw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(306, (ushort)InstructionsEnum.SRAW);
        Assert.Equal((ushort)InstructionsEnum.SRAW, IsaOpcodeValues.SRAW);
        Assert.Contains("SRAW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SRAW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSraw_MetadataAndClassifierPublishScalarRegisterWordArithmeticShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SRAW));

        Assert.Equal("SRAW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SRAW));
        Assert.Equal(InternalOpKind.SraW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SRAW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSrawIr());
        Assert.Equal(InternalOpKind.SraW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.ArithmeticShift, internalOp.Flags);
    }

    [Fact]
    public void ScalarSraw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrawInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SRAW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSraw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrawInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSraw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRAW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.SRAW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.SRAW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarSraw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSrawMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SRAW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRAW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SRAW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_8000_0000UL, 1UL, 0xFFFF_FFFF_C000_0000UL)]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 32UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 1UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 31UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 1UL, 0x0000_0000_0000_0000UL)]
    public void ScalarSraw_ExecutionDispatcherUsesLowWordArithmeticShiftAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSrawIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSraw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5700, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_8000_0000UL);
        core.WriteCommittedArch(vtId, 6, 1);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSrawMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_C000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSrawMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRAW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRAW, context));
    }

    private static InstructionIR CreateScalarSrawIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SRAW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSrawInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRAW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase07ScalarSlliwSupportChainTests
{
    [Fact]
    public void ScalarSlliw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(307, (ushort)InstructionsEnum.SLLIW);
        Assert.Equal((ushort)InstructionsEnum.SLLIW, IsaOpcodeValues.SLLIW);
        Assert.Contains("SLLIW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SLLIW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSlliw_MetadataAndClassifierPublishScalarImmediateWordShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SLLIW));

        Assert.Equal("SLLIW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SLLIW));
        Assert.Equal(InternalOpKind.SllIW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SLLIW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSlliwIr(imm: 1));
        Assert.Equal(InternalOpKind.SllIW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarSlliw_DecoderAcceptsCanonicalScalarImmediateAbi()
    {
        VLIW_Instruction instruction = CreateScalarSlliwInstruction(rd: 7, rs1: 5, imm: -1);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SLLIW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(-1, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSlliw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSlliwInstruction(rd: 7, rs1: 5, imm: 1);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSlliw_DecoderRejectsRegisterFormAlias()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SLLIW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6,
            immediate: 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSlliw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SLLIW,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5,
            immediate: -1);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(unchecked((ushort)-1), instruction.Immediate);
        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, 0);
        Assert.Equal(IsaOpcodeValues.SLLIW, ir.CanonicalOpcode.Value);
        Assert.Equal(-1, ir.Imm);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarImmediate(
                (uint)InstructionsEnum.SLLIW,
                DataTypeEnum.INT32,
                destReg: 32,
                srcReg: 5,
                immediate: 1));
    }

    [Fact]
    public void ScalarSlliw_RegistryMaterializesScalarImmediateMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSlliwMicroOp(imm: -1);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SLLIW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SLLIW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SLLIW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal(ulong.MaxValue, microOp.Immediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarSlliw_RegistryRejectsRegisterFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SLLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6,
            Immediate = 1,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SLLIW, context));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSlliw_RegistryFailsClosedWithoutProjectedImmediate()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SLLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SLLIW, context));

        Assert.Contains("requires projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1UL, -1, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(1UL, 32, 0x0000_0000_0000_0001UL)]
    [InlineData(0xFFFF_FFFF_0000_0001UL, 31, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_4000_0000UL, 1, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_8000_0000UL, 1, 0x0000_0000_0000_0000UL)]
    [InlineData(0xFFFF_FFFF_7FFF_FFFFUL, 1, 0xFFFF_FFFF_FFFF_FFFEUL)]
    public void ScalarSlliw_ExecutionDispatcherUsesLowWordImmediateShiftAndSignExtendsResult(
        ulong rs1,
        short imm,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSlliwIr(imm),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSlliw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5800, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_4000_0000UL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSlliwMicroOp(imm: 1);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSlliwMicroOp(short imm)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SLLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = unchecked((ushort)imm),
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SLLIW, context));
    }

    private static InstructionIR CreateScalarSlliwIr(short imm) => new()
    {
        CanonicalOpcode = InstructionsEnum.SLLIW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = imm,
    };

    private static VLIW_Instruction CreateScalarSlliwInstruction(byte rd, byte rs1, short imm) =>
        InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SLLIW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            imm);
}

public sealed class Phase08ScalarSrliwSupportChainTests
{
    [Fact]
    public void ScalarSrliw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(308, (ushort)InstructionsEnum.SRLIW);
        Assert.Equal((ushort)InstructionsEnum.SRLIW, IsaOpcodeValues.SRLIW);
        Assert.Contains("SRLIW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SRLIW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSrliw_MetadataAndClassifierPublishScalarImmediateWordShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SRLIW));

        Assert.Equal("SRLIW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SRLIW));
        Assert.Equal(InternalOpKind.SrlIW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SRLIW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSrliwIr(imm: 1));
        Assert.Equal(InternalOpKind.SrlIW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarSrliw_DecoderAcceptsCanonicalScalarImmediateAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrliwInstruction(rd: 7, rs1: 5, imm: -1);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SRLIW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(-1, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSrliw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSrliwInstruction(rd: 7, rs1: 5, imm: 1);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSrliw_DecoderRejectsRegisterFormAlias()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRLIW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6,
            immediate: 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSrliw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SRLIW,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5,
            immediate: -1);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(unchecked((ushort)-1), instruction.Immediate);
        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, 0);
        Assert.Equal(IsaOpcodeValues.SRLIW, ir.CanonicalOpcode.Value);
        Assert.Equal(-1, ir.Imm);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarImmediate(
                (uint)InstructionsEnum.SRLIW,
                DataTypeEnum.INT32,
                destReg: 32,
                srcReg: 5,
                immediate: 1));
    }

    [Fact]
    public void ScalarSrliw_RegistryMaterializesScalarImmediateMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSrliwMicroOp(imm: -1);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SRLIW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRLIW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SRLIW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal(ulong.MaxValue, microOp.Immediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarSrliw_RegistryRejectsRegisterFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6,
            Immediate = 1,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRLIW, context));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSrliw_RegistryFailsClosedWithoutProjectedImmediate()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRLIW, context));

        Assert.Contains("requires projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 1, 0x0000_0000_4000_0000UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 1, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0001_8000_0000UL, 0, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 31, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 32, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_8000_0000UL, -1, 0x0000_0000_0000_0001UL)]
    public void ScalarSrliw_ExecutionDispatcherUsesLowWordLogicalImmediateShiftAndSignExtendsResult(
        ulong rs1,
        short imm,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSrliwIr(imm),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSrliw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5A00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSrliwMicroOp(imm: 1);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0x0000_0000_7FFF_FFFFUL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSrliwMicroOp(short imm)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRLIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = unchecked((ushort)imm),
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRLIW, context));
    }

    private static InstructionIR CreateScalarSrliwIr(short imm) => new()
    {
        CanonicalOpcode = InstructionsEnum.SRLIW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = imm,
    };

    private static VLIW_Instruction CreateScalarSrliwInstruction(byte rd, byte rs1, short imm) =>
        InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SRLIW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            imm);
}

public sealed class Phase09ScalarSraiwSupportChainTests
{
    [Fact]
    public void ScalarSraiw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(309, (ushort)InstructionsEnum.SRAIW);
        Assert.Equal((ushort)InstructionsEnum.SRAIW, IsaOpcodeValues.SRAIW);
        Assert.Contains("SRAIW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("SRAIW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarSraiw_MetadataAndClassifierPublishScalarImmediateWordArithmeticShiftSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SRAIW));

        Assert.Equal("SRAIW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SRAIW));
        Assert.Equal(InternalOpKind.SraIW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SRAIW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSraiwIr(imm: 1));
        Assert.Equal(InternalOpKind.SraIW, internalOp.Kind);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.ArithmeticShift, internalOp.Flags);
    }

    [Fact]
    public void ScalarSraiw_DecoderAcceptsCanonicalScalarImmediateAbi()
    {
        VLIW_Instruction instruction = CreateScalarSraiwInstruction(rd: 7, rs1: 5, imm: -1);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SRAIW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(-1, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSraiw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSraiwInstruction(rd: 7, rs1: 5, imm: 1);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSraiw_DecoderRejectsRegisterFormAlias()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.SRAIW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6,
            immediate: 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSraiw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SRAIW,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5,
            immediate: -1);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(unchecked((ushort)-1), instruction.Immediate);
        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, 0);
        Assert.Equal(IsaOpcodeValues.SRAIW, ir.CanonicalOpcode.Value);
        Assert.Equal(-1, ir.Imm);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarImmediate(
                (uint)InstructionsEnum.SRAIW,
                DataTypeEnum.INT32,
                destReg: 32,
                srcReg: 5,
                immediate: 1));
    }

    [Fact]
    public void ScalarSraiw_RegistryMaterializesScalarImmediateMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSraiwMicroOp(imm: -1);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SRAIW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRAIW));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SRAIW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal(ulong.MaxValue, microOp.Immediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarSraiw_RegistryRejectsRegisterFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRAIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6,
            Immediate = 1,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRAIW, context));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSraiw_RegistryFailsClosedWithoutProjectedImmediate()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRAIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRAIW, context));

        Assert.Contains("requires projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 1, 0xFFFF_FFFF_C000_0000UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 1, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0001_8000_0000UL, 0, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0xFFFF_FFFF_8000_0000UL, 31, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 32, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_8000_0000UL, -1, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarSraiw_ExecutionDispatcherUsesLowWordArithmeticImmediateShiftAndSignExtendsResult(
        ulong rs1,
        short imm,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSraiwIr(imm),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarSraiw_MicroOpWritebackRetiresSignExtendedWordResultToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5A00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_8000_0000UL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSraiwMicroOp(imm: 1);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_C000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSraiwMicroOp(short imm)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SRAIW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = unchecked((ushort)imm),
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SRAIW, context));
    }

    private static InstructionIR CreateScalarSraiwIr(short imm) => new()
    {
        CanonicalOpcode = InstructionsEnum.SRAIW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = imm,
    };

    private static VLIW_Instruction CreateScalarSraiwInstruction(byte rd, byte rs1, short imm) =>
        InstructionEncoder.EncodeScalarImmediate(
            (uint)InstructionsEnum.SRAIW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            imm);
}

public sealed class Phase10ScalarMulwSupportChainTests
{
    [Fact]
    public void ScalarMulw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(310, (ushort)InstructionsEnum.MULW);
        Assert.Equal((ushort)InstructionsEnum.MULW, IsaOpcodeValues.MULW);
        Assert.Contains("MULW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("MULW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarMulw_MetadataAndClassifierPublishScalarRegisterWordMultiplySemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.MULW));

        Assert.Equal("MULW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(3, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.MULW));
        Assert.Equal(InternalOpKind.MulW, InternalOpBuilder.MapToKind(IsaOpcodeValues.MULW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarMulwIr());
        Assert.Equal(InternalOpKind.MulW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarMulw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarMulwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.MULW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarMulw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarMulwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarMulw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.MULW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.MULW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.MULW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarMulw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarMulwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.MULW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.MULW));
        Assert.Equal(3, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.MULW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0002UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0006UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFEUL)]
    [InlineData(0x0000_0001_0000_0002UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0006UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0001UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFEUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0002UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0001UL)]
    public void ScalarMulw_ExecutionDispatcherUsesLowWordProductAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarMulwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Fact]
    public void ScalarMulw_MicroOpWritebackRetiresSignExtendedLowWordProductToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5B00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0001_7FFF_FFFFUL);
        core.WriteCommittedArch(vtId, 6, 2);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarMulwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_FFFF_FFFEUL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarMulwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.MULW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.MULW, context));
    }

    private static InstructionIR CreateScalarMulwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.MULW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarMulwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.MULW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase11ScalarDivwSupportChainTests
{
    [Fact]
    public void ScalarDivw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(311, (ushort)InstructionsEnum.DIVW);
        Assert.Equal((ushort)InstructionsEnum.DIVW, IsaOpcodeValues.DIVW);
        Assert.Contains("DIVW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("DIVW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarDivw_MetadataAndClassifierPublishScalarRegisterWordDivideSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.DIVW));

        Assert.Equal("DIVW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(16, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.DIVW));
        Assert.Equal(InternalOpKind.DivW, InternalOpBuilder.MapToKind(IsaOpcodeValues.DIVW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarDivwIr());
        Assert.Equal(InternalOpKind.DivW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.Signed, internalOp.Flags);
    }

    [Fact]
    public void ScalarDivw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarDivwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.DIVW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarDivw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarDivwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarDivw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DIVW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.DIVW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.DIVW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarDivw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarDivwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.DIVW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DIVW));
        Assert.Equal(16, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.DIVW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFDUL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0xFFFF_FFFF_FFFF_FFFDUL)]
    [InlineData(0x0000_0001_0000_0006UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarDivw_ExecutionDispatcherUsesLowWordSignedQuotientAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarDivwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFDUL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0xFFFF_FFFF_FFFF_FFFDUL)]
    [InlineData(0x0000_0001_0000_0006UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarDivw_MicroOpExecutionUsesLowWordSignedQuotientAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5C00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, rs2);

        ScalarALUMicroOp microOp = MaterializeScalarDivwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarDivw_MicroOpWritebackRetiresSignExtendedQuotientToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5C40, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_FFFF_FFF9UL);
        core.WriteCommittedArch(vtId, 6, 2);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarDivwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_FFFF_FFFDUL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarDivwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.DIVW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.DIVW, context));
    }

    private static InstructionIR CreateScalarDivwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.DIVW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarDivwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DIVW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase12ScalarDivuwSupportChainTests
{
    [Fact]
    public void ScalarDivuw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(312, (ushort)InstructionsEnum.DIVUW);
        Assert.Equal((ushort)InstructionsEnum.DIVUW, IsaOpcodeValues.DIVUW);
        Assert.Contains("DIVUW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("DIVUW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarDivuw_MetadataAndClassifierPublishScalarRegisterUnsignedWordDivideSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.DIVUW));

        Assert.Equal("DIVUW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(16, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.DIVUW));
        Assert.Equal(InternalOpKind.DivuW, InternalOpBuilder.MapToKind(IsaOpcodeValues.DIVUW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarDivuwIr());
        Assert.Equal(InternalOpKind.DivuW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarDivuw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarDivuwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.DIVUW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarDivuw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarDivuwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarDivuw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DIVUW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.DIVUW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.DIVUW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarDivuw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarDivuwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.DIVUW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DIVUW));
        Assert.Equal(16, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.DIVUW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_FFFF_FFF8UL, 0x0000_0000_0000_0002UL, 0x0000_0000_7FFF_FFFCUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0002UL, 0x0000_0000_4000_0000UL)]
    [InlineData(0x0000_0001_0000_0006UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0001UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarDivuw_ExecutionDispatcherUsesLowWordUnsignedQuotientAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarDivuwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_FFFF_FFF8UL, 0x0000_0000_0000_0002UL, 0x0000_0000_7FFF_FFFCUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0002UL, 0x0000_0000_4000_0000UL)]
    [InlineData(0x0000_0001_0000_0006UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0002UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0001UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarDivuw_MicroOpExecutionUsesLowWordUnsignedQuotientAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5D00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, rs2);

        ScalarALUMicroOp microOp = MaterializeScalarDivuwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarDivuw_MicroOpWritebackRetiresSignExtendedUnsignedQuotientToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5D40, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_8000_0000UL);
        core.WriteCommittedArch(vtId, 6, 1);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarDivuwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarDivuwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.DIVUW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.DIVUW, context));
    }

    private static InstructionIR CreateScalarDivuwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.DIVUW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarDivuwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DIVUW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase13ScalarRemwSupportChainTests
{
    [Fact]
    public void ScalarRemw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(313, (ushort)InstructionsEnum.REMW);
        Assert.Equal((ushort)InstructionsEnum.REMW, IsaOpcodeValues.REMW);
        Assert.Contains("REMW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("REMW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarRemw_MetadataAndClassifierPublishScalarRegisterWordRemainderSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.REMW));

        Assert.Equal("REMW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(16, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.REMW));
        Assert.Equal(InternalOpKind.RemW, InternalOpBuilder.MapToKind(IsaOpcodeValues.REMW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarRemwIr());
        Assert.Equal(InternalOpKind.RemW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.Signed, internalOp.Flags);
    }

    [Fact]
    public void ScalarRemw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarRemwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.REMW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarRemw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarRemwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarRemw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.REMW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.REMW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.REMW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarRemw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarRemwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.REMW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.REMW));
        Assert.Equal(16, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.REMW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0001_0000_0007UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0005UL)]
    public void ScalarRemw_ExecutionDispatcherUsesLowWordSignedRemainderAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarRemwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0001_0000_0007UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_0000_0005UL, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0005UL)]
    public void ScalarRemw_MicroOpExecutionUsesLowWordSignedRemainderAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5E00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, rs2);

        ScalarALUMicroOp microOp = MaterializeScalarRemwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarRemw_MicroOpWritebackRetiresSignExtendedRemainderToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5E40, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_FFFF_FFF9UL);
        core.WriteCommittedArch(vtId, 6, 2);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarRemwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_FFFF_FFFFUL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarRemwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.REMW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.REMW, context));
    }

    private static InstructionIR CreateScalarRemwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.REMW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarRemwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.REMW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase14ScalarRemuwSupportChainTests
{
    [Fact]
    public void ScalarRemuw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(314, (ushort)InstructionsEnum.REMUW);
        Assert.Equal((ushort)InstructionsEnum.REMUW, IsaOpcodeValues.REMUW);
        Assert.Contains("REMUW", IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Contains("REMUW", IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Fact]
    public void ScalarRemuw_MetadataAndClassifierPublishScalarRegisterUnsignedWordRemainderSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.REMUW));

        Assert.Equal("REMUW", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(16, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.REMUW));
        Assert.Equal(InternalOpKind.RemuW, InternalOpBuilder.MapToKind(IsaOpcodeValues.REMUW));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarRemuwIr());
        Assert.Equal(InternalOpKind.RemuW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarRemuw_DecoderAcceptsCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarRemuwInstruction(rd: 7, rs1: 5, rs2: 6);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.REMUW, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(6, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarRemuw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarRemuwInstruction(rd: 7, rs1: 5, rs2: 6);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarRemuw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.REMUW,
            DataTypeEnum.INT32,
            reg1: 7,
            reg2: 5,
            reg3: 6);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 6), instruction.Word1);
        Assert.Equal(IsaOpcodeValues.REMUW, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalar(
                (uint)InstructionsEnum.REMUW,
                DataTypeEnum.INT32,
                reg1: 7,
                reg2: 5,
                reg3: 32));
    }

    [Fact]
    public void ScalarRemuw_RegistryMaterializesScalarRegisterMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarRemuwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.REMUW);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.REMUW));
        Assert.Equal(16, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.REMUW, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5, 6 }, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0x0000_0000_0000_0007UL)]
    [InlineData(0x0000_0001_0000_0007UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0001UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_8000_0000UL)]
    public void ScalarRemuw_ExecutionDispatcherUsesLowWordUnsignedRemainderAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, rs2);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarRemuwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0006UL, 0x0000_0000_0000_0003UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_FFFF_FFF9UL, 0x0000_0000_0000_0002UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_0000_0007UL, 0x0000_0000_FFFF_FFFEUL, 0x0000_0000_0000_0007UL)]
    [InlineData(0x0000_0001_0000_0007UL, 0xFFFF_FFFF_0000_0003UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0001UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_8000_0000UL)]
    public void ScalarRemuw_MicroOpExecutionUsesLowWordUnsignedRemainderAndSignExtendsResult(
        ulong rs1,
        ulong rs2,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5F00, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, rs2);

        ScalarALUMicroOp microOp = MaterializeScalarRemuwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarRemuw_MicroOpWritebackRetiresSignExtendedUnsignedRemainderToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5F40, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_8000_0000UL);
        core.WriteCommittedArch(vtId, 6, 0);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarRemuwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarRemuwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.REMUW,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.REMUW, context));
    }

    private static InstructionIR CreateScalarRemuwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.REMUW,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 6,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarRemuwInstruction(byte rd, byte rs1, byte rs2) =>
        InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.REMUW,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
}

public sealed class Phase15ScalarSextwSupportChainTests
{
    [Fact]
    public void ScalarSextw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(320, (ushort)InstructionsEnum.SEXT_W);
        Assert.Equal((ushort)InstructionsEnum.SEXT_W, IsaOpcodeValues.SEXT_W);
        Assert.Contains("SEXT.W", IsaV4Surface.MandatoryInteger64RepairOpcodes);
    }

    [Fact]
    public void ScalarSextw_MetadataAndClassifierPublishScalarUnarySignExtendWordSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.SEXT_W));

        Assert.Equal("SEXT.W", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.SEXT_W));
        Assert.Equal(InternalOpKind.SextW, InternalOpBuilder.MapToKind(IsaOpcodeValues.SEXT_W));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarSextwIr());
        Assert.Equal(InternalOpKind.SextW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.Signed, internalOp.Flags);
    }

    [Fact]
    public void ScalarSextw_DecoderAcceptsCanonicalPackedUnaryAbi()
    {
        VLIW_Instruction instruction = CreateScalarSextwInstruction(rd: 7, rs1: 5);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.SEXT_W, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarSextw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarSextwInstruction(rd: 7, rs1: 5);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSextw_DecoderRejectsRegisterFormAlias()
    {
        VLIW_Instruction instruction = CreateScalarSextwInstruction(rd: 7, rs1: 5);
        instruction.Word1 = VLIW_Instruction.PackArchRegs(7, 5, 6);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSextw_DecoderRejectsImmediateFormAlias()
    {
        VLIW_Instruction instruction = CreateScalarSextwInstruction(rd: 7, rs1: 5);
        instruction.Immediate = 1;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Immediate-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSextw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarUnary(
            (uint)InstructionsEnum.SEXT_W,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(0, instruction.Immediate);
        Assert.Equal(IsaOpcodeValues.SEXT_W, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarUnary(
                (uint)InstructionsEnum.SEXT_W,
                DataTypeEnum.INT32,
                destReg: 7,
                srcReg: 32));
    }

    [Fact]
    public void ScalarSextw_RegistryMaterializesScalarUnaryMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarSextwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SEXT_W);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SEXT_W));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.SEXT_W, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.DoesNotContain(0, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarSextw_RegistryRejectsRegisterFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SEXT_W, context));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarSextw_RegistryRejectsImmediateFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = 1,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SEXT_W, context));

        Assert.Contains("Immediate-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0001_0000_0001UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0001_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarSextw_ExecutionDispatcherSignExtendsLowWordAndIgnoresHighBits(
        ulong rs1,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, 0xFFFF_FFFF_FFFF_FFFFUL);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarSextwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0xFFFF_FFFF_8000_0000UL)]
    [InlineData(0x0000_0001_0000_0001UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0x0000_0001_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL)]
    public void ScalarSextw_MicroOpExecutionSignExtendsLowWordAndIgnoresHighBits(
        ulong rs1,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6000, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, 0xFFFF_FFFF_FFFF_FFFFUL);

        ScalarALUMicroOp microOp = MaterializeScalarSextwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarSextw_MicroOpWritebackRetiresSignExtendedWordToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6040, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_8000_0000UL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarSextwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0xFFFF_FFFF_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarSextwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SEXT_W, context));
    }

    private static InstructionIR CreateScalarSextwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.SEXT_W,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarSextwInstruction(byte rd, byte rs1) =>
        InstructionEncoder.EncodeScalarUnary(
            (uint)InstructionsEnum.SEXT_W,
            DataTypeEnum.INT32,
            rd,
            rs1);
}

public sealed class Phase16ScalarZextwSupportChainTests
{
    [Fact]
    public void ScalarZextw_AllocatesStableMandatoryRepairOpcode()
    {
        Assert.Equal(321, (ushort)InstructionsEnum.ZEXT_W);
        Assert.Equal((ushort)InstructionsEnum.ZEXT_W, IsaOpcodeValues.ZEXT_W);
        Assert.Contains("ZEXT.W", IsaV4Surface.MandatoryInteger64RepairOpcodes);
    }

    [Fact]
    public void ScalarZextw_MetadataAndClassifierPublishScalarUnaryZeroExtendWordSemantics()
    {
        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(IsaOpcodeValues.ZEXT_W));

        Assert.Equal("ZEXT.W", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags, info.Flags);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(0, info.MemoryBandwidth);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(IsaOpcodeValues.ZEXT_W));
        Assert.Equal(InternalOpKind.ZextW, InternalOpBuilder.MapToKind(IsaOpcodeValues.ZEXT_W));

        InternalOp internalOp = new InternalOpBuilder().Build(CreateScalarZextwIr());
        Assert.Equal(InternalOpKind.ZextW, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        Assert.Equal(InternalOpDataType.Word, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
    }

    [Fact]
    public void ScalarZextw_DecoderAcceptsCanonicalPackedUnaryAbi()
    {
        VLIW_Instruction instruction = CreateScalarZextwInstruction(rd: 7, rs1: 5);

        InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal(IsaOpcodeValues.ZEXT_W, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(7, ir.Rd);
        Assert.Equal(5, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
    }

    [Fact]
    public void ScalarZextw_DecoderRejectsNonCanonicalPackedRegisterAbi()
    {
        VLIW_Instruction instruction = CreateScalarZextwInstruction(rd: 7, rs1: 5);
        instruction.Word1 = 32;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("legacy/global register encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarZextw_DecoderRejectsRegisterFormAlias()
    {
        VLIW_Instruction instruction = CreateScalarZextwInstruction(rd: 7, rs1: 5);
        instruction.Word1 = VLIW_Instruction.PackArchRegs(7, 5, 6);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarZextw_DecoderRejectsImmediateFormAlias()
    {
        VLIW_Instruction instruction = CreateScalarZextwInstruction(rd: 7, rs1: 5);
        instruction.Immediate = 1;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Immediate-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarZextw_EncoderEmitsCanonicalAbiAndRejectsInvalidRegisters()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarUnary(
            (uint)InstructionsEnum.ZEXT_W,
            DataTypeEnum.INT32,
            destReg: 7,
            srcReg: 5);

        Assert.Equal(VLIW_Instruction.PackArchRegs(7, 5, 0), instruction.Word1);
        Assert.Equal(0, instruction.Immediate);
        Assert.Equal(IsaOpcodeValues.ZEXT_W, new VliwDecoderV4().Decode(in instruction, 0).CanonicalOpcode.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarUnary(
                (uint)InstructionsEnum.ZEXT_W,
                DataTypeEnum.INT32,
                destReg: 7,
                srcReg: 32));
    }

    [Fact]
    public void ScalarZextw_RegistryMaterializesScalarUnaryMicroOpAndPublishesFacts()
    {
        ScalarALUMicroOp microOp = MaterializeScalarZextwMicroOp();
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.ZEXT_W);
        Assert.NotNull(descriptor);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ZEXT_W));
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister.GetValueOrDefault());
        Assert.False(descriptor.IsMemoryOp.GetValueOrDefault());
        Assert.Equal((uint)InstructionsEnum.ZEXT_W, microOp.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.UsesImmediate);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.HasSideEffects);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.Equal(new[] { 5 }, microOp.ReadRegisters);
        Assert.DoesNotContain(0, microOp.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.True(microOp.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, microOp.AdmissionMetadata.RegisterHazardMask);
        Assert.True(microOp.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void ScalarZextw_RegistryRejectsRegisterFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ZEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 6,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ZEXT_W, context));

        Assert.Contains("Register-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarZextw_RegistryRejectsImmediateFormAlias()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ZEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            Immediate = 1,
            HasImmediate = true
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ZEXT_W, context));

        Assert.Contains("Immediate-form aliasing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_8000_0000UL)]
    [InlineData(0x0000_0001_0000_0001UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_FFFF_FFFFUL)]
    public void ScalarZextw_ExecutionDispatcherZeroExtendsLowWordAndIgnoresHighBits(
        ulong rs1,
        ulong expected)
    {
        var state = new FakeCpuState();
        state.SetReg(5, rs1);
        state.SetReg(6, 0xFFFF_FFFF_FFFF_FFFFUL);

        ExecutionResult result = new ExecutionDispatcherV4().Execute(
            CreateScalarZextwIr(),
            state);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, state.ReadIntRegister(7));
    }

    [Theory]
    [InlineData(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL)]
    [InlineData(0x0000_0000_7FFF_FFFFUL, 0x0000_0000_7FFF_FFFFUL)]
    [InlineData(0x0000_0000_8000_0000UL, 0x0000_0000_8000_0000UL)]
    [InlineData(0x0000_0001_0000_0001UL, 0x0000_0000_0000_0001UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_FFFF_FFFFUL)]
    public void ScalarZextw_MicroOpExecutionZeroExtendsLowWordAndIgnoresHighBits(
        ulong rs1,
        ulong expected)
    {
        const int vtId = 1;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6100, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, rs1);
        core.WriteCommittedArch(vtId, 6, 0xFFFF_FFFF_FFFF_FFFFUL);

        ScalarALUMicroOp microOp = MaterializeScalarZextwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ScalarZextw_MicroOpWritebackRetiresZeroExtendedWordToOwningVirtualThread()
    {
        const int vtId = 2;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6140, activeVtId: vtId);
        core.WriteCommittedArch(vtId, 5, 0xFFFF_FFFF_8000_0000UL);
        core.WriteCommittedArch(vtId, 7, 0x1234);

        ScalarALUMicroOp microOp = MaterializeScalarZextwMicroOp();
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(0x1234UL, core.ReadArch(vtId, 7));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

        Assert.Equal(0x0000_0000_8000_0000UL, core.ReadArch(vtId, 7));
    }

    private static ScalarALUMicroOp MaterializeScalarZextwMicroOp()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ZEXT_W,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = 0,
            HasImmediate = true
        };

        return Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ZEXT_W, context));
    }

    private static InstructionIR CreateScalarZextwIr() => new()
    {
        CanonicalOpcode = InstructionsEnum.ZEXT_W,
        Class = InstructionClass.ScalarAlu,
        SerializationClass = SerializationClass.Free,
        Rd = 7,
        Rs1 = 5,
        Rs2 = 0,
        Imm = 0,
    };

    private static VLIW_Instruction CreateScalarZextwInstruction(byte rd, byte rs1) =>
        InstructionEncoder.EncodeScalarUnary(
            (uint)InstructionsEnum.ZEXT_W,
            DataTypeEnum.INT32,
            rd,
            rs1);
}
