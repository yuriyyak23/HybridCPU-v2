using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using CloseToRtlAddUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.AddUwInstruction;
using CloseToRtlSh1addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh1addUwInstruction;
using CloseToRtlSh2add = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addInstruction;
using CloseToRtlSh2addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addUwInstruction;
using CloseToRtlSh3add = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addInstruction;
using CloseToRtlSh3addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addUwInstruction;
using CloseToRtlSlliUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.SlliUwInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase03;

public sealed class ScalarAddressGenerationUwExecutableTests
{
    public static IEnumerable<object[]> AddressGenerationOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.SH2ADD, "SH2ADD", 345, InternalOpKind.Sh2Add, false };
        yield return new object[] { InstructionsEnum.SH3ADD, "SH3ADD", 346, InternalOpKind.Sh3Add, false };
        yield return new object[] { InstructionsEnum.ADD_UW, "ADD.UW", 347, InternalOpKind.AddUw, false };
        yield return new object[] { InstructionsEnum.SH1ADD_UW, "SH1ADD.UW", 348, InternalOpKind.Sh1AddUw, false };
        yield return new object[] { InstructionsEnum.SH2ADD_UW, "SH2ADD.UW", 349, InternalOpKind.Sh2AddUw, false };
        yield return new object[] { InstructionsEnum.SH3ADD_UW, "SH3ADD.UW", 350, InternalOpKind.Sh3AddUw, false };
        yield return new object[] { InstructionsEnum.SLLI_UW, "SLLI.UW", 351, InternalOpKind.SlliUw, true };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.SH2ADD, 1UL, 2UL, 0UL, 6UL };
        yield return new object[] { InstructionsEnum.SH3ADD, 1UL, 2UL, 0UL, 10UL };
        yield return new object[] { InstructionsEnum.ADD_UW, 0xFFFF_FFFF_0000_0001UL, 2UL, 0UL, 3UL };
        yield return new object[] { InstructionsEnum.SH1ADD_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 1UL, 0UL, 0x1_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.SH2ADD_UW, 0x0000_0000_8000_0000UL, 0UL, 0UL, 0x2_0000_0000UL };
        yield return new object[] { InstructionsEnum.SH3ADD_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 1UL, 0UL, 0x7_FFFF_FFF9UL };
        yield return new object[] { InstructionsEnum.SLLI_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 0UL, 63UL, 0x8000_0000_0000_0000UL };
    }

    [Theory]
    [MemberData(nameof(AddressGenerationOpcodeCases))]
    public void AddressGeneration_OpcodeStatusSurfaceAndCloseToRtlObjects_AreRuntimeClosed(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind,
        bool _)
    {
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal((ushort)opcode, ResolveOpcodeValue(opcode));
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(mnemonic, out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarAddressGeneration", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap[mnemonic]);

        Assert.True(Enum.TryParse<InstructionsEnum>(
            mnemonic.Replace(".", "_", StringComparison.Ordinal),
            out InstructionsEnum _));
        Assert.Contains(OpcodeRegistry.Opcodes, info => string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        AssertCloseToRtlObject(opcode, mnemonic);
    }

    [Theory]
    [MemberData(nameof(AddressGenerationOpcodeCases))]
    public void AddressGeneration_ClassifierRegistryAndMaterializer_PublishTypedScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind,
        bool usesImmediate)
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;
        const ushort imm6 = 4;

        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free), InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(usesImmediate ? InstructionFlags.UsesImmediate : InstructionFlags.TwoOperand, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = rd,
            Reg2ID = rs1,
            Reg3ID = usesImmediate ? (ushort)0 : rs2,
            HasImmediate = true,
            Immediate = usesImmediate ? imm6 : (ushort)0,
        };
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(usesImmediate ? VLIW_Instruction.NoReg : rs2, scalar.Src2RegID);
        Assert.Equal(usesImmediate, scalar.UsesImmediate);
        Assert.Equal(usesImmediate ? imm6 : 0UL, scalar.Immediate);
        Assert.Equal(usesImmediate ? new[] { (int)rs1 } : new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
        Assert.False(scalar.IsMemoryOp);

        AssertInvalidMaterializerAlias(opcode, rd, rs1, rs2, usesImmediate);

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1, usesImmediate ? (byte)0 : rs2, usesImmediate ? imm6 : 0));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(rs1, internalOp.Rs1);
        Assert.Equal(usesImmediate ? 0 : rs2, internalOp.Rs2);
        Assert.Equal(usesImmediate ? imm6 : 0, internalOp.Immediate);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Theory]
    [MemberData(nameof(AddressGenerationOpcodeCases))]
    public void AddressGeneration_DecoderIrAndProjector_UseCanonicalRegisterOrImm6Abi(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind,
        bool usesImmediate)
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const byte rs2 = 10;
        const ushort imm6 = 4;

        VLIW_Instruction instruction = usesImmediate
            ? CreateScalarAddressGenerationImmediateInstruction(opcode, rd, rs1, imm6)
            : CreateScalarInstruction(opcode, rd, rs1, rs2);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8A00, bundleSerial: 140);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(usesImmediate ? 0 : rs2, ir.Rs2);
        Assert.Equal(usesImmediate ? imm6 : 0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
        Assert.Null(ir.VectorPayload);
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(usesImmediate ? VLIW_Instruction.NoReg : rs2, scalar.Src2RegID);
        Assert.Equal(usesImmediate, scalar.UsesImmediate);
        Assert.Equal(usesImmediate ? imm6 : 0UL, scalar.Immediate);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        if (usesImmediate)
        {
            VLIW_Instruction registerAlias = InstructionEncoder.EncodeScalar(
                (uint)opcode,
                DataTypeEnum.UINT64,
                rd,
                rs1,
                rs2,
                immediate: imm6);
            Assert.Throws<InvalidOperationException>(() =>
                decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0x8A20, 141));

            VLIW_Instruction outOfRange = InstructionEncoder.EncodeScalar(
                (uint)opcode,
                DataTypeEnum.UINT64,
                rd,
                rs1,
                0,
                immediate: 64);
            Assert.Throws<InvalidOperationException>(() =>
                decoder.DecodeInstructionBundle(CreateBundle(outOfRange), 0x8A40, 142));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                InstructionEncoder.EncodeScalarAddressGenerationImmediate(
                    (uint)opcode,
                    DataTypeEnum.UINT64,
                    rd,
                    rs1,
                    immediate6: 64));
        }
        else
        {
            VLIW_Instruction immediateAlias = CreateScalarInstruction(opcode, rd, rs1, rs2, immediate: 1);
            Assert.Throws<InvalidOperationException>(() =>
                decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x8A60, 143));
        }
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void AddressGeneration_ScalarAluCloseToRtlAndGoldenVectors_DefineXlen64Semantics(
        InstructionsEnum opcode,
        ulong source,
        ulong addend,
        ulong immediate,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute((uint)opcode, source, addend, immediate);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, ExecuteCloseToRtlObject(opcode, source, addend, immediate));

        foreach ((ulong goldenSource, ulong goldenAddend, ulong goldenImmediate, ulong goldenExpected) in GetGoldenVectors(opcode))
        {
            Assert.Equal(goldenExpected, ScalarAluOps.Compute((uint)opcode, goldenSource, goldenAddend, goldenImmediate));
            Assert.Equal(goldenExpected, ExecuteCloseToRtlObject(opcode, goldenSource, goldenAddend, goldenImmediate));
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.SH2ADD, 2, 3UL, 0x100UL, 0, 0x10CUL)]
    [InlineData(InstructionsEnum.SH3ADD, 3, 3UL, 0x100UL, 0, 0x118UL)]
    [InlineData(InstructionsEnum.ADD_UW, 1, 0xFFFF_FFFF_0000_0001UL, 0x100UL, 0, 0x101UL)]
    [InlineData(InstructionsEnum.SH1ADD_UW, 2, 0xFFFF_FFFF_8000_0000UL, 0UL, 0, 0x1_0000_0000UL)]
    [InlineData(InstructionsEnum.SH2ADD_UW, 3, 0xFFFF_FFFF_8000_0000UL, 0UL, 0, 0x2_0000_0000UL)]
    [InlineData(InstructionsEnum.SH3ADD_UW, 1, 0xFFFF_FFFF_FFFF_FFFFUL, 1UL, 0, 0x7_FFFF_FFF9UL)]
    [InlineData(InstructionsEnum.SLLI_UW, 2, 0xFFFF_FFFF_FFFF_FFFFUL, 0UL, 63, 0x8000_0000_0000_0000UL)]
    public void AddressGeneration_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong sourceValue,
        ulong addendValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const ulong pc = 0x8B00UL;
        const ushort sourceRegister = 5;
        const ushort addendRegister = 6;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;
        bool usesImmediate = opcode == InstructionsEnum.SLLI_UW;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, addendRegister, addendValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots = CreateBundle(
            usesImmediate
                ? CreateScalarAddressGenerationImmediateInstruction(
                    opcode,
                    rd: (byte)destinationRegister,
                    rs1: (byte)sourceRegister,
                    immediate6: immediate6)
                : CreateScalarInstruction(
                    opcode,
                    rd: (byte)destinationRegister,
                    rs1: (byte)sourceRegister,
                    rs2: (byte)addendRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)opcode, decodeStatus.OpCode);
        Assert.False(decodeStatus.IsVectorOp);
        Assert.False(decodeStatus.IsMemoryOp);

        core.TestRunExecuteStageFromCurrentDecodeState();

        var executeStatus = core.TestReadExecuteStageStatus();
        Assert.True(executeStatus.Valid);
        Assert.True(executeStatus.ResultReady);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(pc, core.ReadCommittedPc(vtId));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

        AssertReplayPhasePreserved(core, scheduler, serializingEpochCountBefore);

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
    }

    [Theory]
    [MemberData(nameof(AddressGenerationOpcodeCases))]
    public void AddressGeneration_WriteToX0_IsDiscardedAtRetire(
        InstructionsEnum opcode,
        string _,
        int __,
        InternalOpKind ___,
        bool usesImmediate)
    {
        Assert.Equal(__, (int)opcode);
        Assert.Equal(___, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        const int vtId = 1;
        const ulong pc = 0x8C00UL;
        const ushort sourceRegister = 8;
        const ushort addendRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, addendRegister, 0x20UL);

        VLIW_Instruction[] rawSlots = CreateBundle(
            usesImmediate
                ? CreateScalarAddressGenerationImmediateInstruction(opcode, rd: 0, rs1: (byte)sourceRegister, immediate6: 63)
                : CreateScalarInstruction(opcode, rd: 0, rs1: (byte)sourceRegister, rs2: (byte)addendRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0xFFFF_FFFF_FFFF_FFFFUL, core.ReadArch(vtId, sourceRegister));
        Assert.Equal(0x20UL, core.ReadArch(vtId, addendRegister));
    }

    [Theory]
    [InlineData(InstructionsEnum.SH2ADD, 3UL, 0x100UL, 0, 0x10CUL)]
    [InlineData(InstructionsEnum.ADD_UW, 0xFFFF_FFFF_0000_0001UL, 0x100UL, 0, 0x101UL)]
    [InlineData(InstructionsEnum.SLLI_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 0UL, 63, 0x8000_0000_0000_0000UL)]
    public void AddressGeneration_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong addendValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, addendValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = opcode == InstructionsEnum.SLLI_UW
            ? CreateInstructionIr(opcode, rd, rs1, 0, immediate6)
            : CreateInstructionIr(opcode, rd, rs1, rs2, 0);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 144,
                vtId);

        Assert.Equal(0UL, core.ReadArch(vtId, rd));
        RetireRecord record = Assert.Single(snapshot.RetireRecords);
        Assert.True(record.IsRegisterWrite);
        Assert.Equal(vtId, record.VtId);
        Assert.Equal(rd, record.ArchReg);
        Assert.Equal(expectedResult, record.Value);
        Assert.False(snapshot.HasTypedEffect);

        RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
            ref core,
            dispatcher,
            instruction,
            state,
            bundleSerial: 144,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [InlineData(InstructionsEnum.SH3ADD_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 1UL, 0, 0x7_FFFF_FFF9UL)]
    [InlineData(InstructionsEnum.SLLI_UW, 0xFFFF_FFFF_FFFF_FFFFUL, 0UL, 63, 0x8000_0000_0000_0000UL)]
    public void AddressGeneration_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong addendValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const int vtId = 2;
        const ulong pc = 0x8D00UL;
        const ushort sourceRegister = 5;
        const ushort addendRegister = 6;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        bool usesImmediate = opcode == InstructionsEnum.SLLI_UW;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, addendRegister, addendValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction = usesImmediate
            ? CreateScalarAddressGenerationImmediateInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                immediate6: immediate6,
                virtualThreadId: (byte)vtId)
            : CreateScalarInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)addendRegister,
                virtualThreadId: (byte)vtId);
        ScalarALUMicroOp microOp = DecodeAndMaterializeScalar(instruction, vtId);
        HybridCPU_ISE.Core.ReplayToken rollbackToken = microOp.CreateRollbackToken(vtId);
        rollbackToken.CaptureRegisterState(ref core, [(int)destinationRegister]);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: pc,
            admissionExecutionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared);
        core.TestRunMemoryStageFromCurrentExecuteState();
        core.TestLatchMemoryToWriteBackTransferState();
        core.TestRunWriteBackStage();

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void AddressGeneration_CompilerHelpersOpenForUwAndImmediateRowsWithoutAliases()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        foreach (string required in new[]
        {
            "InstructionsEnum.ADD_UW",
            "InstructionsEnum.SH1ADD_UW",
            "InstructionsEnum.SH2ADD",
            "InstructionsEnum.SH3ADD",
            "InstructionsEnum.SH2ADD_UW",
            "InstructionsEnum.SH3ADD_UW",
            "InstructionsEnum.SLLI_UW",
            "AddUnsignedWord",
            "ShiftLeftOneAndAddUnsignedWord",
            "ShiftLeftTwoAndAdd",
            "ShiftLeftTwoAndAddUnsignedWord",
            "ShiftLeftThreeAndAdd",
            "ShiftLeftThreeAndAddUnsignedWord",
            "ShiftLeftUnsignedWordByImmediate",
            "ADD.UW",
            "SLLI.UW"
        })
        {
            Assert.Contains(required, compilerSource, StringComparison.Ordinal);
        }

        foreach (string forbidden in new[]
        {
            "AddUw",
            "Sh1AddUw",
            "Sh2AddUw",
            "Sh3AddUw",
            "SlliUw"
        })
        {
            Assert.DoesNotContain(forbidden, compilerSource, StringComparison.Ordinal);
        }

        Assert.False(CloseToRtlSh2add.RequiresVmxProjection);
        Assert.False(CloseToRtlSh3add.RequiresVmxProjection);
        Assert.False(CloseToRtlAddUw.RequiresVmxProjection);
        Assert.False(CloseToRtlSh1addUw.RequiresVmxProjection);
        Assert.False(CloseToRtlSh2addUw.RequiresVmxProjection);
        Assert.False(CloseToRtlSh3addUw.RequiresVmxProjection);
        Assert.False(CloseToRtlSlliUw.RequiresVmxProjection);
        Assert.True(CloseToRtlSh2add.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlSh3add.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlAddUw.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlSh1addUw.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlSh2addUw.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlSh3addUw.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlSlliUw.NoHiddenMultiOpEmission);
        Assert.False(CloseToRtlSh2add.CompilerHelperAllowed);
        Assert.False(CloseToRtlSh3add.CompilerHelperAllowed);
        Assert.False(CloseToRtlAddUw.CompilerHelperAllowed);
        Assert.False(CloseToRtlSh1addUw.CompilerHelperAllowed);
        Assert.False(CloseToRtlSh2addUw.CompilerHelperAllowed);
        Assert.False(CloseToRtlSh3addUw.CompilerHelperAllowed);
        Assert.False(CloseToRtlSlliUw.CompilerHelperAllowed);
    }

    [Fact]
    public void AddressGeneration_AdjacentCarryChecksumAndMultiPrecisionRows_RemainFailClosed()
    {
        foreach (string mnemonic in new[]
        {
            "CRC32",
            "CRC64",
            "ADC",
            "SBC",
            "ADDC",
            "SUBC"
        })
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim, mnemonic);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
        }
    }

    private static void AssertInvalidMaterializerAlias(
        InstructionsEnum opcode,
        byte rd,
        byte rs1,
        byte rs2,
        bool usesImmediate)
    {
        if (usesImmediate)
        {
            Assert.Throws<DecodeProjectionFaultException>(() =>
                InstructionRegistry.CreateMicroOp(
                    (uint)opcode,
                    new DecoderContext
                    {
                        OpCode = (uint)opcode,
                        Reg1ID = rd,
                        Reg2ID = rs1,
                        Reg3ID = rs2,
                        HasImmediate = true,
                        Immediate = 4,
                    }));
            Assert.Throws<DecodeProjectionFaultException>(() =>
                InstructionRegistry.CreateMicroOp(
                    (uint)opcode,
                    new DecoderContext
                    {
                        OpCode = (uint)opcode,
                        Reg1ID = rd,
                        Reg2ID = rs1,
                        Reg3ID = 0,
                        HasImmediate = true,
                        Immediate = 64,
                    }));
            return;
        }

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)opcode,
                new DecoderContext
                {
                    OpCode = (uint)opcode,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => IsaOpcodeValues.SH2ADD,
            InstructionsEnum.SH3ADD => IsaOpcodeValues.SH3ADD,
            InstructionsEnum.ADD_UW => IsaOpcodeValues.ADD_UW,
            InstructionsEnum.SH1ADD_UW => IsaOpcodeValues.SH1ADD_UW,
            InstructionsEnum.SH2ADD_UW => IsaOpcodeValues.SH2ADD_UW,
            InstructionsEnum.SH3ADD_UW => IsaOpcodeValues.SH3ADD_UW,
            InstructionsEnum.SLLI_UW => IsaOpcodeValues.SLLI_UW,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar address-generation opcode.")
        };

    private static void AssertCloseToRtlObject(InstructionsEnum opcode, string mnemonic)
    {
        Assert.Equal("ExecutableScalarAlu", GetEvidenceBoundary(opcode));
        Assert.Equal(64, GetXLen(opcode));
        Assert.True(GetNoLsuBypassAuthority(opcode));
        Assert.True(GetNoHiddenMultiOpEmission(opcode));
        Assert.False(GetCompilerHelperAllowed(opcode));
        Assert.False(GetRequiresVmxProjection(opcode));
        Assert.True(GetHasOpcodeAllocation(opcode));
        Assert.True(GetIsExecutable(opcode));

        switch (opcode)
        {
            case InstructionsEnum.SH2ADD:
                Assert.Equal(CloseToRtlSh2add.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSh2add.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlSh2add.OperandShape);
                Assert.True(CloseToRtlSh2add.RetireWritesDestination(1));
                Assert.False(CloseToRtlSh2add.RetireWritesDestination(0));
                break;
            case InstructionsEnum.SH3ADD:
                Assert.Equal(CloseToRtlSh3add.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSh3add.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlSh3add.OperandShape);
                Assert.True(CloseToRtlSh3add.RetireWritesDestination(1));
                Assert.False(CloseToRtlSh3add.RetireWritesDestination(0));
                break;
            case InstructionsEnum.ADD_UW:
                Assert.Equal(CloseToRtlAddUw.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlAddUw.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlAddUw.OperandShape);
                Assert.Equal(32, CloseToRtlAddUw.SourceWidth);
                Assert.True(CloseToRtlAddUw.RetireWritesDestination(1));
                Assert.False(CloseToRtlAddUw.RetireWritesDestination(0));
                break;
            case InstructionsEnum.SH1ADD_UW:
                Assert.Equal(CloseToRtlSh1addUw.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSh1addUw.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlSh1addUw.OperandShape);
                Assert.Equal(32, CloseToRtlSh1addUw.SourceWidth);
                Assert.True(CloseToRtlSh1addUw.RetireWritesDestination(1));
                Assert.False(CloseToRtlSh1addUw.RetireWritesDestination(0));
                break;
            case InstructionsEnum.SH2ADD_UW:
                Assert.Equal(CloseToRtlSh2addUw.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSh2addUw.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlSh2addUw.OperandShape);
                Assert.Equal(32, CloseToRtlSh2addUw.SourceWidth);
                Assert.True(CloseToRtlSh2addUw.RetireWritesDestination(1));
                Assert.False(CloseToRtlSh2addUw.RetireWritesDestination(0));
                break;
            case InstructionsEnum.SH3ADD_UW:
                Assert.Equal(CloseToRtlSh3addUw.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSh3addUw.Opcode);
                Assert.Equal("rd, rs1, rs2", CloseToRtlSh3addUw.OperandShape);
                Assert.Equal(32, CloseToRtlSh3addUw.SourceWidth);
                Assert.True(CloseToRtlSh3addUw.RetireWritesDestination(1));
                Assert.False(CloseToRtlSh3addUw.RetireWritesDestination(0));
                break;
            case InstructionsEnum.SLLI_UW:
                Assert.Equal(CloseToRtlSlliUw.Mnemonic, mnemonic);
                Assert.Equal((ushort)opcode, CloseToRtlSlliUw.Opcode);
                Assert.Equal("rd, rs1, imm6", CloseToRtlSlliUw.OperandShape);
                Assert.Equal(32, CloseToRtlSlliUw.SourceWidth);
                Assert.Equal(6, CloseToRtlSlliUw.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlSlliUw.ImmediateMask);
                Assert.True(CloseToRtlSlliUw.RetireWritesDestination(1));
                Assert.False(CloseToRtlSlliUw.RetireWritesDestination(0));
                break;
        }
    }

    private static string GetEvidenceBoundary(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.EvidenceBoundary,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.EvidenceBoundary,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.EvidenceBoundary,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.EvidenceBoundary,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.EvidenceBoundary,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.EvidenceBoundary,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.EvidenceBoundary,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static int GetXLen(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.XLen,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.XLen,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.XLen,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.XLen,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.XLen,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.XLen,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.XLen,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetNoLsuBypassAuthority(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.NoLsuBypassAuthority,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.NoLsuBypassAuthority,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.NoLsuBypassAuthority,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.NoLsuBypassAuthority,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.NoLsuBypassAuthority,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.NoLsuBypassAuthority,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.NoLsuBypassAuthority,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetNoHiddenMultiOpEmission(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.NoHiddenMultiOpEmission,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.NoHiddenMultiOpEmission,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.NoHiddenMultiOpEmission,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.NoHiddenMultiOpEmission,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.NoHiddenMultiOpEmission,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.NoHiddenMultiOpEmission,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.NoHiddenMultiOpEmission,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetCompilerHelperAllowed(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.CompilerHelperAllowed,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.CompilerHelperAllowed,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.CompilerHelperAllowed,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.CompilerHelperAllowed,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.CompilerHelperAllowed,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.CompilerHelperAllowed,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.CompilerHelperAllowed,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetRequiresVmxProjection(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.RequiresVmxProjection,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.RequiresVmxProjection,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.RequiresVmxProjection,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.RequiresVmxProjection,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.RequiresVmxProjection,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.RequiresVmxProjection,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.RequiresVmxProjection,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetHasOpcodeAllocation(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.HasOpcodeAllocation,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.HasOpcodeAllocation,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.HasOpcodeAllocation,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.HasOpcodeAllocation,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.HasOpcodeAllocation,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.HasOpcodeAllocation,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.HasOpcodeAllocation,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool GetIsExecutable(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.IsExecutable,
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.IsExecutable,
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.IsExecutable,
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.IsExecutable,
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.IsExecutable,
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.IsExecutable,
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.IsExecutable,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ulong ExecuteCloseToRtlObject(
        InstructionsEnum opcode,
        ulong source,
        ulong addend,
        ulong immediate) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.Execute(source, addend),
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.Execute(source, addend),
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.Execute(source, addend),
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.Execute(source, addend),
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.Execute(source, addend),
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.Execute(source, addend),
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.Execute(source, immediate),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static IEnumerable<(ulong Source, ulong Addend, ulong Immediate, ulong Expected)> GetGoldenVectors(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SH2ADD => CloseToRtlSh2add.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.SH3ADD => CloseToRtlSh3add.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.ADD_UW => CloseToRtlAddUw.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.SH1ADD_UW => CloseToRtlSh1addUw.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.SH2ADD_UW => CloseToRtlSh2addUw.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.SH3ADD_UW => CloseToRtlSh3addUw.GetLocalGoldenVectors().Select(vector => (vector.Source, vector.Addend, 0UL, vector.Expected)),
            InstructionsEnum.SLLI_UW => CloseToRtlSlliUw.GetLocalGoldenVectors().Select(vector => (vector.Source, 0UL, (ulong)vector.Immediate6, vector.Expected)),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static MicroOpScheduler PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong retiredPc,
        out long serializingEpochCountBefore)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: retiredPc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

        MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
        var capacityState = new SlotClassCapacityState();
        capacityState.InitializeFromLaneMap();
        scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
        scheduler.TestSetClassTemplateValid(true);
        scheduler.TestSetClassTemplateDomainId(0);
        serializingEpochCountBefore = scheduler.SerializingEpochCount;

        Assert.True(core.GetReplayPhaseContext().IsActive);
        Assert.True(scheduler.TestGetReplayPhaseContext().IsActive);
        return scheduler;
    }

    private static void AssertReplayPhasePreserved(
        Processor.CPU_Core core,
        MicroOpScheduler scheduler,
        long serializingEpochCountBefore)
    {
        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
        Assert.True(replayPhase.IsActive);
        Assert.True(schedulerPhase.IsActive);
        Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
    }

    private static ScalarALUMicroOp DecodeAndMaterializeScalar(
        VLIW_Instruction instruction,
        int vtId)
    {
        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8E00, bundleSerial: 145);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static InstructionIR CreateInstructionIr(
        InstructionsEnum opcode,
        byte rd,
        byte rs1,
        byte rs2,
        long immediate)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = immediate
        };
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] slots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < slots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = slots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            rs2,
            predicateMask: 0xFF,
            immediate: immediate);
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }

    private static VLIW_Instruction CreateScalarAddressGenerationImmediateInstruction(
        InstructionsEnum opcode,
        byte rd,
        byte rs1,
        ushort immediate6,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarAddressGenerationImmediate(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            immediate6,
            predicateMask: 0xFF);
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }

}
