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
using CloseToRtlAndn = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BooleanInvert.AndnInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxPhase01AndnExecutableTests
{
    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL };
        yield return new object[] { 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL };
        yield return new object[] { 0xFFFF_0000_FFFF_0000UL, 0x00FF_00FF_00FF_00FFUL, 0xFF00_0000_FF00_0000UL };
        yield return new object[] { 0x0123_4567_89AB_CDEFUL, 0x0000_FFFF_0000_FFFFUL, 0x0123_0000_89AB_0000UL };
    }

    [Fact]
    public void Andn_OpcodeStatusAndCloseToRtlObject_AreRuntimeClosed()
    {
        Assert.Equal(65, (int)InstructionsEnum.ANDN);
        Assert.Equal((ushort)InstructionsEnum.ANDN, IsaOpcodeValues.ANDN);
        Assert.Equal(InternalOpKind.AndN, InternalOpBuilder.MapToKind(IsaOpcodeValues.ANDN));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "ANDN",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarBitmanipCore", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("ANDN", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("ANDN", IsaV4Surface.OptionalExtensions);
        Assert.DoesNotContain("ANDN", IsaV4Surface.OptionalDisabledOpcodes);

        Assert.Equal("ANDN", CloseToRtlAndn.Mnemonic);
        Assert.Equal("rd, rs1, rs2", CloseToRtlAndn.OperandShape);
        Assert.Equal("ExecutableScalarAlu", CloseToRtlAndn.EvidenceBoundary);
        Assert.Equal(64, CloseToRtlAndn.XLen);
        Assert.Equal((ushort)InstructionsEnum.ANDN, CloseToRtlAndn.Opcode);
        Assert.True(CloseToRtlAndn.HasOpcodeAllocation);
        Assert.True(CloseToRtlAndn.IsExecutable);
        Assert.True(CloseToRtlAndn.WritesScalarRegister);
        Assert.False(CloseToRtlAndn.HasSideEffects);
        Assert.False(CloseToRtlAndn.CompilerHelperAllowed);
        Assert.False(CloseToRtlAndn.RequiresVmxProjection);
    }

    [Fact]
    public void Andn_ClassifierRegistryAndMaterializer_PublishBinaryScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.ANDN));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.ANDN);
        Assert.NotNull(info);
        Assert.Equal("ANDN", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.ANDN));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.ANDN);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ANDN));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.ANDN,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.ANDN,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.ANDN, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.ANDN,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.ANDN,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(rd, rs1, rs2));
        Assert.Equal(InternalOpKind.AndN, internalOp.Kind);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Fact]
    public void Andn_EncoderDecoderIrAndProjector_RequireCanonicalBinaryRegisterPayload()
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const byte rs2 = 10;

        VLIW_Instruction instruction = CreateScalarInstruction(rd, rs1, rs2);
        Assert.Equal((uint)InstructionsEnum.ANDN, instruction.OpCode);
        Assert.Equal(0, instruction.Immediate);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8200, bundleSerial: 120);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.ANDN, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(rs2, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Equal("ANDN", OpcodeRegistry.GetMnemonicOrHex((uint)InstructionsEnum.ANDN));

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.ANDN, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);

        decoder.DecodeInstructionBundle(CreateBundle(CreateScalarInstruction(rd, rs1, rs2: 0)), 0x8220, 121);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            rd,
            rs1,
            rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x8240, 122));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void Andn_ScalarAluCloseToRtlAndGoldenVectors_DefineXlen64Edges(
        ulong left,
        ulong right,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.ANDN,
            left,
            right,
            immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlAndn.Execute(left, right));
        Assert.Equal(expected, CloseToRtlAndn.EvaluateXLen64(left, right));

        foreach (var vector in CloseToRtlAndn.GetLocalGoldenVectors())
        {
            Assert.Equal(
                vector.Result,
                ScalarAluOps.Compute((uint)InstructionsEnum.ANDN, vector.Left, vector.Right, immediate: 0));
            Assert.Equal(vector.Result, CloseToRtlAndn.Execute(vector.Left, vector.Right));
        }
    }

    [Fact]
    public void Andn_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication()
    {
        const int vtId = 2;
        const ulong pc = 0x8300UL;
        const ushort sourceRegister = 5;
        const ushort maskRegister = 6;
        const ushort destinationRegister = 7;
        const ulong sourceValue = 0xFFFF_0000_FFFF_0000UL;
        const ulong maskValue = 0x00FF_00FF_00FF_00FFUL;
        const ulong expectedResult = 0xFF00_0000_FF00_0000UL;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, maskRegister, maskValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)maskRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.ANDN, decodeStatus.OpCode);
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

    [Fact]
    public void Andn_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x8400UL;
        const ushort sourceRegister = 8;
        const ushort maskRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, maskRegister, 0UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                rd: 0,
                rs1: (byte)sourceRegister,
                rs2: (byte)maskRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0xFFFF_FFFF_FFFF_FFFFUL, core.ReadArch(vtId, sourceRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, maskRegister));
    }

    [Fact]
    public void Andn_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        const ulong sourceValue = 0xFFFF_0000_FFFF_0000UL;
        const ulong maskValue = 0x00FF_00FF_00FF_00FFUL;
        const ulong expectedResult = 0xFF00_0000_FF00_0000UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, maskValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 128,
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
            bundleSerial: 128,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void Andn_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x8500UL;
        const ushort destinationRegister = 14;
        const ulong sourceValue = 0x0123_4567_89AB_CDEFUL;
        const ulong maskValue = 0x0000_FFFF_0000_FFFFUL;
        const ulong expectedResult = 0x0123_0000_89AB_0000UL;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, 6, maskValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
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

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Andn_CompilerAndVmxGates_RemainGenericNoEmission()
    {
        string compilerSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler"));
        Assert.DoesNotContain("InstructionsEnum.ANDN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ANDN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Andn", compilerSource, StringComparison.Ordinal);

        string vmxSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_ISE", "Core", "VMX"));
        Assert.DoesNotContain("InstructionsEnum.ANDN", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ANDN", vmxSource, StringComparison.Ordinal);

        OpcodeInfo info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.ANDN)!.Value;
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.NotEqual(InstructionClass.Vmx, info.InstructionClass);
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8600, bundleSerial: 129);
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
        byte rd,
        byte rs1,
        byte rs2)
    {
        return new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.ANDN,
            Class = InstructionClassifier.GetClass(InstructionsEnum.ANDN),
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.ANDN),
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
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.ANDN,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            rs2,
            predicateMask: 0xFF,
            immediate: immediate);
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }

    private static string ReadAllSource(string root)
    {
        if (!Directory.Exists(root))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
