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
using CloseToRtlAdc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AdcInstruction;
using CloseToRtlAddc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AddcInstruction;
using CloseToRtlClmulh = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CarrylessMultiply.ClmulhInstruction;
using CloseToRtlClmulr = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CarrylessMultiply.ClmulrInstruction;
using CloseToRtlCrc32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc32Instruction;
using CloseToRtlCrc64 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc64Instruction;
using CloseToRtlSbc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SbcInstruction;
using CloseToRtlSubc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SubcInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase04;

public sealed class ScalarCarryLessVariantsExecutableTests
{
    public static IEnumerable<object[]> OpcodeRows()
    {
        yield return new object[] { InstructionsEnum.CLMULH, "CLMULH", 352, InternalOpKind.ClMulH };
        yield return new object[] { InstructionsEnum.CLMULR, "CLMULR", 353, InternalOpKind.ClMulR };
    }

    public static IEnumerable<object[]> SemanticsRows()
    {
        yield return new object[] { InstructionsEnum.CLMULH, 0UL, 0UL, 0UL };
        yield return new object[] { InstructionsEnum.CLMULH, 1UL, 0x1234_5678_9ABC_DEF0UL, 0UL };
        yield return new object[] { InstructionsEnum.CLMULH, ulong.MaxValue, ulong.MaxValue, 0x5555_5555_5555_5555UL };
        yield return new object[] { InstructionsEnum.CLMULH, 0x8000_0000_0000_0000UL, 2UL, 1UL };
        yield return new object[] { InstructionsEnum.CLMULH, 0x1234_5678_9ABC_DEF0UL, 0x0FED_CBA9_8765_4321UL, 0x00E0_38D8_6888_50B0UL };

        yield return new object[] { InstructionsEnum.CLMULR, 0UL, 0UL, 0UL };
        yield return new object[] { InstructionsEnum.CLMULR, 1UL, 0x1234_5678_9ABC_DEF0UL, 0UL };
        yield return new object[] { InstructionsEnum.CLMULR, ulong.MaxValue, ulong.MaxValue, 0xAAAA_AAAA_AAAA_AAAAUL };
        yield return new object[] { InstructionsEnum.CLMULR, 0x8000_0000_0000_0000UL, 2UL, 2UL };
        yield return new object[] { InstructionsEnum.CLMULR, 0x1234_5678_9ABC_DEF0UL, 0x0FED_CBA9_8765_4321UL, 0x01C0_71B0_D110_A160UL };
    }

    [Theory]
    [MemberData(nameof(OpcodeRows))]
    public void CarryLessVariants_OpcodeStatusSurfaceAndLeafMetadata_AreRuntimeClosed(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind _)
    {
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal((ushort)opcode, GetIsaOpcodeValue(opcode));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarCarryLessChecksum", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap[mnemonic]);
        Assert.Equal("ExecutableScalarAlu", GetCloseToRtlEvidenceBoundary(opcode));
        Assert.True(GetCloseToRtlHasOpcodeAllocation(opcode));
        Assert.True(GetCloseToRtlIsExecutable(opcode));
        Assert.False(GetCloseToRtlCompilerHelperAllowed(opcode));
        Assert.False(GetCloseToRtlRequiresVmxProjection(opcode));
        Assert.True(GetCloseToRtlNoHiddenMultiOpEmission(opcode));
    }

    [Theory]
    [MemberData(nameof(OpcodeRows))]
    public void CarryLessVariants_ClassifierRegistryMaterializerAndInternalOp_AreScalarAlu(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedInternalOp)
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Value.Flags);
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
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
        Assert.False(scalar.IsMemoryOp);

        Assert.Equal(expectedInternalOp, new InternalOpBuilder()
            .Build(CreateInstructionIr(opcode, rd, rs1, rs2))
            .Kind);

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

    [Theory]
    [MemberData(nameof(OpcodeRows))]
    public void CarryLessVariants_DecoderEncoderIrAndProjector_UseCanonicalRegisterPayloadOnly(
        InstructionsEnum opcode,
        string _,
        int __,
        InternalOpKind ___)
    {
        Assert.True(__ > 0);
        Assert.True(Enum.IsDefined(typeof(InternalOpKind), ___));

        const byte rd = 9;
        const byte rs1 = 4;
        const byte rs2 = 10;
        const ulong pc = 0x8200UL;

        VLIW_Instruction encoded = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            rs2,
            predicateMask: 0xFF);
        Assert.Equal(0, encoded.Immediate);

        VLIW_Instruction[] rawSlots = CreateBundle(encoded);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: pc, bundleSerial: 182);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(rs2, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
        Assert.Null(ir.VectorPayload);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, microOp.OpCode);
        Assert.Equal(rd, microOp.DestRegID);
        Assert.Equal(rs1, microOp.Src1RegID);
        Assert.Equal(rs2, microOp.Src2RegID);
        Assert.False(microOp.UsesImmediate);

        VLIW_Instruction immediateAlias = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            rs2,
            predicateMask: 0xFF,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x8220, 183));
    }

    [Theory]
    [MemberData(nameof(SemanticsRows))]
    public void CarryLessVariants_ScalarAluDispatcherAndCloseToRtl_DefineXlen64WindowSemantics(
        InstructionsEnum opcode,
        ulong multiplicand,
        ulong multiplier,
        ulong expected)
    {
        ulong scalarAlu = ScalarAluOps.Compute(
            (uint)opcode,
            multiplicand,
            multiplier,
            immediate: 0);
        Assert.Equal(expected, scalarAlu);
        Assert.Equal(expected, ExecuteCloseToRtl(opcode, multiplicand, multiplier));

        var core = new Processor.CPU_Core(0);
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        core.WriteCommittedArch(vtId, rs1, multiplicand);
        core.WriteCommittedArch(vtId, rs2, multiplier);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 198,
                vtId);

        Assert.Equal(0UL, core.ReadArch(vtId, rd));
        RetireRecord record = Assert.Single(snapshot.RetireRecords);
        Assert.True(record.IsRegisterWrite);
        Assert.Equal(vtId, record.VtId);
        Assert.Equal(rd, record.ArchReg);
        Assert.Equal(expected, record.Value);
        Assert.False(snapshot.HasTypedEffect);
    }

    [Fact]
    public void CarryLessVariants_CloseToRtlGoldenVectors_ArePublishedAndMatchRuntime()
    {
        foreach (var vector in CloseToRtlClmulh.GetLocalGoldenVectors())
        {
            Assert.Equal(vector.Expected, CloseToRtlClmulh.Execute(vector.Multiplicand, vector.Multiplier));
            Assert.Equal(vector.Expected, ScalarAluOps.Compute((uint)InstructionsEnum.CLMULH, vector.Multiplicand, vector.Multiplier, 0));
        }

        foreach (var vector in CloseToRtlClmulr.GetLocalGoldenVectors())
        {
            Assert.Equal(vector.Expected, CloseToRtlClmulr.Execute(vector.Multiplicand, vector.Multiplier));
            Assert.Equal(vector.Expected, ScalarAluOps.Compute((uint)InstructionsEnum.CLMULR, vector.Multiplicand, vector.Multiplier, 0));
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.CLMULH, 2, ulong.MaxValue, ulong.MaxValue, 0x5555_5555_5555_5555UL)]
    [InlineData(InstructionsEnum.CLMULR, 3, ulong.MaxValue, ulong.MaxValue, 0xAAAA_AAAA_AAAA_AAAAUL)]
    public void CarryLessVariants_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong multiplicandValue,
        ulong multiplierValue,
        ulong expectedResult)
    {
        const ulong pc = 0x8300UL;
        const ushort multiplicandRegister = 5;
        const ushort multiplierRegister = 6;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, multiplicandRegister, multiplicandValue);
        core.WriteCommittedArch(vtId, multiplierRegister, multiplierValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)multiplicandRegister,
                rs2: (byte)multiplierRegister));

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

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
    }

    [Theory]
    [MemberData(nameof(OpcodeRows))]
    public void CarryLessVariants_WriteToX0_IsDiscardedAtRetire(
        InstructionsEnum opcode,
        string _,
        int __,
        InternalOpKind ___)
    {
        Assert.True(__ > 0);
        Assert.True(Enum.IsDefined(typeof(InternalOpKind), ___));

        const int vtId = 1;
        const ulong pc = 0x8400UL;
        const ushort multiplicandRegister = 8;
        const ushort multiplierRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, multiplicandRegister, 0x8000_0000_0000_0000UL);
        core.WriteCommittedArch(vtId, multiplierRegister, 2UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: (byte)multiplicandRegister,
                rs2: (byte)multiplierRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0x8000_0000_0000_0000UL, core.ReadArch(vtId, multiplicandRegister));
        Assert.Equal(2UL, core.ReadArch(vtId, multiplierRegister));
    }

    [Theory]
    [MemberData(nameof(OpcodeRows))]
    public void CarryLessVariants_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        string _,
        int __,
        InternalOpKind ___)
    {
        Assert.True(__ > 0);
        Assert.True(Enum.IsDefined(typeof(InternalOpKind), ___));

        const int vtId = 2;
        const ulong pc = 0x8500UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, 0x8000_0000_0000_0000UL);
        core.WriteCommittedArch(vtId, 6, 2UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: 5,
                rs2: 6,
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

        Assert.Equal(ExecuteCloseToRtl(opcode, 0x8000_0000_0000_0000UL, 2UL), core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void CarryLessVariants_AdjacentCrcAndMultiPrecisionRows_RemainFailClosed()
    {
        Assert.True(CloseToRtlCrc32.RequiresPolynomialAbi);
        Assert.True(CloseToRtlCrc32.RequiresReflectionAbi);
        Assert.True(CloseToRtlCrc64.RequiresPolynomialAbi);
        Assert.True(CloseToRtlCrc64.RequiresReflectionAbi);
        Assert.True(CloseToRtlAdc.RequiresCarryInAbi);
        Assert.True(CloseToRtlAdc.RequiresCarryOutAbi);
        Assert.True(CloseToRtlSbc.RequiresBorrowInAbi);
        Assert.True(CloseToRtlSbc.RequiresBorrowOutAbi);
        Assert.True(CloseToRtlAddc.RequiresCarryOutAbi);
        Assert.True(CloseToRtlSubc.RequiresBorrowOutAbi);

        foreach (string mnemonic in new[] { "CRC32", "CRC64", "ADC", "SBC", "ADDC", "SUBC" })
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim, mnemonic);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
        }
    }

    [Fact]
    public void CarryLessVariants_CompilerHelperAuthorityAndHiddenLowering_RemainClosed()
    {
        Assert.False(CloseToRtlClmulh.CompilerHelperAllowed);
        Assert.False(CloseToRtlClmulr.CompilerHelperAllowed);
        Assert.True(CloseToRtlClmulh.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlClmulr.NoHiddenMultiOpEmission);
        Assert.True(CloseToRtlClmulh.NoVmxFrontendIntegrationRequired);
        Assert.True(CloseToRtlClmulr.NoVmxFrontendIntegrationRequired);

        string compilerSource = ReadAllCompilerSource();
        Assert.DoesNotContain("CLMULH", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CLMULR", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileClMulH", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileClMulR", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitClMulH", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitClMulR", compilerSource, StringComparison.OrdinalIgnoreCase);
    }

    private static ushort GetIsaOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => IsaOpcodeValues.CLMULH,
            InstructionsEnum.CLMULR => IsaOpcodeValues.CLMULR,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static string GetCloseToRtlEvidenceBoundary(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.EvidenceBoundary,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.EvidenceBoundary,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static bool GetCloseToRtlHasOpcodeAllocation(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.HasOpcodeAllocation,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.HasOpcodeAllocation,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static bool GetCloseToRtlIsExecutable(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.IsExecutable,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.IsExecutable,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static bool GetCloseToRtlCompilerHelperAllowed(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.CompilerHelperAllowed,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.CompilerHelperAllowed,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static bool GetCloseToRtlRequiresVmxProjection(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.RequiresVmxProjection,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.RequiresVmxProjection,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static bool GetCloseToRtlNoHiddenMultiOpEmission(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.NoHiddenMultiOpEmission,
            InstructionsEnum.CLMULR => CloseToRtlClmulr.NoHiddenMultiOpEmission,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static ulong ExecuteCloseToRtl(
        InstructionsEnum opcode,
        ulong multiplicand,
        ulong multiplier) =>
        opcode switch
        {
            InstructionsEnum.CLMULH => CloseToRtlClmulh.Execute(multiplicand, multiplier),
            InstructionsEnum.CLMULR => CloseToRtlClmulr.Execute(multiplicand, multiplier),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null),
        };

    private static ScalarALUMicroOp DecodeAndMaterializeScalar(
        VLIW_Instruction instruction,
        int vtId)
    {
        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8600, bundleSerial: 184);
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
        byte rs2)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0
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
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }

    private static string ReadAllCompilerSource()
    {
        string compilerRoot = Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(compilerRoot, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
