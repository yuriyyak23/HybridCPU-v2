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
using CloseToRtlBclri = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitSetClearInvert.BclriInstruction;
using CloseToRtlBexti = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitExtract.BextiInstruction;
using CloseToRtlBinvi = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitSetClearInvert.BinviInstruction;
using CloseToRtlBseti = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitSetClearInvert.BsetiInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class NonVmxPhase02BitfieldImmediateExecutableTests
{
    public static IEnumerable<object[]> BitfieldImmediateOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.BSETI, "BSETI", 341, InternalOpKind.BsetI };
        yield return new object[] { InstructionsEnum.BCLRI, "BCLRI", 342, InternalOpKind.BclrI };
        yield return new object[] { InstructionsEnum.BINVI, "BINVI", 343, InternalOpKind.BinvI };
        yield return new object[] { InstructionsEnum.BEXTI, "BEXTI", 344, InternalOpKind.BextI };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.BSETI, 0x0000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0002UL };
        yield return new object[] { InstructionsEnum.BSETI, 0x0000_0000_0000_0000UL, (ushort)63, 0x8000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.BSETI, 0x0123_4567_89AB_CDEFUL, (ushort)4, 0x0123_4567_89AB_CDFFUL };
        yield return new object[] { InstructionsEnum.BCLRI, 0xFFFF_FFFF_FFFF_FFFFUL, (ushort)0, 0xFFFF_FFFF_FFFF_FFFEUL };
        yield return new object[] { InstructionsEnum.BCLRI, 0xFFFF_FFFF_FFFF_FFFFUL, (ushort)63, 0x7FFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.BCLRI, 0x0123_4567_89AB_CDFFUL, (ushort)4, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.BINVI, 0x0000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0002UL };
        yield return new object[] { InstructionsEnum.BINVI, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.BINVI, 0x0000_0000_0000_0010UL, (ushort)4, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.BEXTI, 0x0000_0000_0000_0001UL, (ushort)0, 1UL };
        yield return new object[] { InstructionsEnum.BEXTI, 0x0000_0000_0000_0001UL, (ushort)1, 0UL };
        yield return new object[] { InstructionsEnum.BEXTI, 0x8000_0000_0000_0000UL, (ushort)63, 1UL };
        yield return new object[] { InstructionsEnum.BEXTI, 0x0000_0000_0000_0010UL, (ushort)4, 1UL };
    }

    [Theory]
    [MemberData(nameof(BitfieldImmediateOpcodeCases))]
    public void BitfieldImmediate_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal((ushort)opcode, ResolveOpcodeValue(opcode));
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarBitfield", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalExtensions);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);

        AssertCloseToRtlObject(opcode, mnemonic);
    }

    [Theory]
    [MemberData(nameof(BitfieldImmediateOpcodeCases))]
    public void BitfieldImmediate_ClassifierRegistryAndMaterializer_PublishImmediateScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind)
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const ushort imm6 = 4;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Value.Flags);
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

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = 0,
                HasImmediate = true,
                Immediate = imm6,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.True(scalar.UsesImmediate);
        Assert.Equal(imm6, scalar.Immediate);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)opcode,
                new DecoderContext
                {
                    OpCode = (uint)opcode,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = 3,
                    HasImmediate = true,
                    Immediate = imm6,
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

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1, imm6));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(rs1, internalOp.Rs1);
        Assert.Equal(0, internalOp.Rs2);
        Assert.Equal(imm6, internalOp.Immediate);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Theory]
    [MemberData(nameof(BitfieldImmediateOpcodeCases))]
    public void BitfieldImmediate_DecoderIrAndProjector_RequireCanonicalImm6Payload(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const ushort imm6 = 4;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        VLIW_Instruction instruction = CreateScalarBitfieldImmediateInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            immediate6: imm6);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8200, bundleSerial: 130);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(imm6, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.True(scalar.UsesImmediate);
        Assert.Equal(imm6, scalar.Immediate);

        VLIW_Instruction registerAlias = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            3,
            immediate: imm6);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0x8220, 131));

        VLIW_Instruction outOfRange = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            0,
            immediate: 64);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(outOfRange), 0x8240, 132));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarBitfieldImmediate(
                (uint)opcode,
                DataTypeEnum.UINT64,
                rd,
                rs1,
                immediate6: 64));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void BitfieldImmediate_ScalarAluCloseToRtlAndGoldenVectors_DefineImm6Edges(
        InstructionsEnum opcode,
        ulong source,
        ushort immediate6,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)opcode,
            source,
            op2: 0,
            immediate: immediate6);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, ExecuteCloseToRtlObject(opcode, source, immediate6));

        foreach ((ulong goldenSource, ushort goldenImmediate, ulong goldenExpected) in GetGoldenVectors(opcode))
        {
            Assert.Equal(
                goldenExpected,
                ScalarAluOps.Compute((uint)opcode, goldenSource, op2: 0, immediate: goldenImmediate));
            Assert.Equal(goldenExpected, ExecuteCloseToRtlObject(opcode, goldenSource, goldenImmediate));
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.BSETI, 2, 0x0000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0002UL)]
    [InlineData(InstructionsEnum.BCLRI, 3, 0x0000_0000_0000_0003UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.BINVI, 1, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0000UL)]
    [InlineData(InstructionsEnum.BEXTI, 2, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    public void BitfieldImmediate_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const ulong pc = 0x8300UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarBitfieldImmediateInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                immediate6: immediate6));

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
    [MemberData(nameof(BitfieldImmediateOpcodeCases))]
    public void BitfieldImmediate_WriteToX0_IsDiscardedAtRetire(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        Assert.False(string.IsNullOrWhiteSpace(mnemonic));
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        const int vtId = 1;
        const ulong pc = 0x8400UL;
        const ushort sourceRegister = 8;
        const ulong sourceValue = 0xFFFF_FFFF_FFFF_FFFFUL;
        const ushort imm6 = 63;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarBitfieldImmediateInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister,
                immediate6: imm6));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(sourceValue, core.ReadArch(vtId, sourceRegister));
    }

    [Theory]
    [InlineData(InstructionsEnum.BSETI, 0x0000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0002UL)]
    [InlineData(InstructionsEnum.BCLRI, 0x0000_0000_0000_0003UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.BINVI, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0000UL)]
    [InlineData(InstructionsEnum.BEXTI, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    public void BitfieldImmediate_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1, immediate6);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 133,
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
            bundleSerial: 133,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [InlineData(InstructionsEnum.BSETI, 0x0000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0002UL)]
    [InlineData(InstructionsEnum.BCLRI, 0x0000_0000_0000_0003UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.BINVI, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0000UL)]
    [InlineData(InstructionsEnum.BEXTI, 0x0000_0000_0000_0002UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    public void BitfieldImmediate_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const int vtId = 2;
        const ulong pc = 0x8500UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarBitfieldImmediateInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                immediate6: immediate6,
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
    public void BitfieldImmediate_CompilerHelpersOpenWithoutAliasesOrVmxSpecificPath()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        foreach (string required in new[]
        {
            "InstructionsEnum.BSETI",
            "InstructionsEnum.BCLRI",
            "InstructionsEnum.BINVI",
            "InstructionsEnum.BEXTI",
            "SetBitImmediate",
            "ClearBitImmediate",
            "InvertBitImmediate",
            "ExtractBitImmediate"
        })
        {
            Assert.Contains(required, compilerSource, StringComparison.Ordinal);
        }

        foreach (string forbidden in new[]
        {
            "Bseti",
            "Bclri",
            "Binvi",
            "Bexti",
            "BitSetImmediate",
            "BitClearImmediate",
            "BitInvertImmediate",
            "BitExtractImmediate"
        })
        {
            Assert.DoesNotContain(forbidden, compilerSource, StringComparison.Ordinal);
        }

        Assert.False(CloseToRtlBseti.RequiresVmxProjection);
        Assert.False(CloseToRtlBclri.RequiresVmxProjection);
        Assert.False(CloseToRtlBinvi.RequiresVmxProjection);
        Assert.False(CloseToRtlBexti.RequiresVmxProjection);
        Assert.True(CloseToRtlBseti.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlBclri.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlBinvi.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlBexti.NoHiddenMultiOpEmission);
        Assert.False(CloseToRtlBseti.CompilerHelperAllowed);
        Assert.False(CloseToRtlBclri.CompilerHelperAllowed);
        Assert.False(CloseToRtlBinvi.CompilerHelperAllowed);
        Assert.False(CloseToRtlBexti.CompilerHelperAllowed);
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.BSETI => IsaOpcodeValues.BSETI,
            InstructionsEnum.BCLRI => IsaOpcodeValues.BCLRI,
            InstructionsEnum.BINVI => IsaOpcodeValues.BINVI,
            InstructionsEnum.BEXTI => IsaOpcodeValues.BEXTI,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar bitfield-immediate opcode.")
        };

    private static void AssertCloseToRtlObject(InstructionsEnum opcode, string mnemonic)
    {
        switch (opcode)
        {
            case InstructionsEnum.BSETI:
                Assert.Equal(CloseToRtlBseti.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlBseti.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlBseti.XLen);
                Assert.Equal(6, CloseToRtlBseti.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlBseti.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlBseti.Opcode);
                Assert.True(CloseToRtlBseti.HasOpcodeAllocation);
                Assert.True(CloseToRtlBseti.IsExecutable);
                Assert.True(CloseToRtlBseti.WritesScalarRegister);
                Assert.False(CloseToRtlBseti.HasSideEffects);
                Assert.False(CloseToRtlBseti.CompilerHelperAllowed);
                break;
            case InstructionsEnum.BCLRI:
                Assert.Equal(CloseToRtlBclri.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlBclri.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlBclri.XLen);
                Assert.Equal(6, CloseToRtlBclri.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlBclri.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlBclri.Opcode);
                Assert.True(CloseToRtlBclri.HasOpcodeAllocation);
                Assert.True(CloseToRtlBclri.IsExecutable);
                Assert.True(CloseToRtlBclri.WritesScalarRegister);
                Assert.False(CloseToRtlBclri.HasSideEffects);
                Assert.False(CloseToRtlBclri.CompilerHelperAllowed);
                break;
            case InstructionsEnum.BINVI:
                Assert.Equal(CloseToRtlBinvi.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlBinvi.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlBinvi.XLen);
                Assert.Equal(6, CloseToRtlBinvi.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlBinvi.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlBinvi.Opcode);
                Assert.True(CloseToRtlBinvi.HasOpcodeAllocation);
                Assert.True(CloseToRtlBinvi.IsExecutable);
                Assert.True(CloseToRtlBinvi.WritesScalarRegister);
                Assert.False(CloseToRtlBinvi.HasSideEffects);
                Assert.False(CloseToRtlBinvi.CompilerHelperAllowed);
                break;
            case InstructionsEnum.BEXTI:
                Assert.Equal(CloseToRtlBexti.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlBexti.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlBexti.XLen);
                Assert.Equal(6, CloseToRtlBexti.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlBexti.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlBexti.Opcode);
                Assert.True(CloseToRtlBexti.HasOpcodeAllocation);
                Assert.True(CloseToRtlBexti.IsExecutable);
                Assert.True(CloseToRtlBexti.WritesScalarRegister);
                Assert.False(CloseToRtlBexti.HasSideEffects);
                Assert.False(CloseToRtlBexti.CompilerHelperAllowed);
                Assert.False(CloseToRtlBexti.RequiresCanonicalBooleanResultAbi);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar bitfield-immediate opcode.");
        }
    }

    private static ulong ExecuteCloseToRtlObject(
        InstructionsEnum opcode,
        ulong source,
        ushort immediate6) =>
        opcode switch
        {
            InstructionsEnum.BSETI => CloseToRtlBseti.Execute(source, immediate6),
            InstructionsEnum.BCLRI => CloseToRtlBclri.Execute(source, immediate6),
            InstructionsEnum.BINVI => CloseToRtlBinvi.Execute(source, immediate6),
            InstructionsEnum.BEXTI => CloseToRtlBexti.Execute(source, immediate6),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar bitfield-immediate opcode.")
        };

    private static IEnumerable<(ulong Source, ushort Immediate6, ulong Expected)> GetGoldenVectors(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.BSETI => CloseToRtlBseti.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            InstructionsEnum.BCLRI => CloseToRtlBclri.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            InstructionsEnum.BINVI => CloseToRtlBinvi.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            InstructionsEnum.BEXTI => CloseToRtlBexti.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar bitfield-immediate opcode.")
        };
    }

    private static MicroOpScheduler PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong pc,
        out long serializingEpochCountBefore)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: pc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));
        MicroOpScheduler scheduler = core.TestGetFSPScheduler()
            ?? throw new InvalidOperationException("Expected initialized scheduler.");
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8600, bundleSerial: 134);
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
        ushort immediate6)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = 0,
            Imm = immediate6
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

    private static VLIW_Instruction CreateScalarBitfieldImmediateInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        ushort immediate6 = 0,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarBitfieldImmediate(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            immediate6,
            predicateMask: 0xFF);
        instruction.StreamLength = 0;
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }
}
