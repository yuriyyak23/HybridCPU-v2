using System;
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
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase03;

public sealed class ScalarSystemCounterRdcycleExecutableTests
{
    [Fact]
    public void Rdcycle_OpcodeValueAndSupportStatus_AreStableAndRuntimeClosed()
    {
        Assert.Equal(53, (int)InstructionsEnum.CZERO_EQZ);
        Assert.Equal(54, (int)InstructionsEnum.CLZ);
        Assert.Equal(56, (int)InstructionsEnum.SH1ADD);
        Assert.Equal(57, (int)InstructionsEnum.RDCYCLE);
        Assert.Equal((ushort)InstructionsEnum.RDCYCLE, IsaOpcodeValues.RDCYCLE);
        Assert.Null(OpcodeRegistry.GetInfo(55u));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "RDCYCLE",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarSystemCounter", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("RDCYCLE", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("RDCYCLE", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("RDCYCLE", IsaV4Surface.OptionalDisabledOpcodes);
        Assert.Equal("CSR_SERIAL", IsaV4Surface.PipelineClassMap["RDCYCLE"]);
    }

    [Fact]
    public void Rdcycle_ClassifierRegistryAndMaterializer_PublishCsrCounterMicroOp()
    {
        const byte rd = 7;

        Assert.Equal(
            (InstructionClass.Csr, SerializationClass.CsrOrdered),
            InstructionClassifier.Classify(InstructionsEnum.RDCYCLE));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.RDCYCLE);
        Assert.NotNull(info);
        Assert.Equal("RDCYCLE", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.System, info.Value.Category);
        Assert.Equal(1, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.None, info.Value.Flags);
        Assert.Equal(InstructionClass.Csr, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.CsrOrdered, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.RDCYCLE));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.RDCYCLE);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.RDCYCLE));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.RDCYCLE,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.RDCYCLE,
                Reg1ID = rd,
                Reg2ID = 0,
                Reg3ID = 0,
                HasImmediate = true,
                Immediate = 0,
            });

        CsrReadCounterMicroOp counter = Assert.IsType<CsrReadCounterMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.RDCYCLE, counter.OpCode);
        Assert.Equal((ulong)CsrAddresses.Cycle, counter.CSRAddress);
        Assert.Equal(rd, counter.DestRegID);
        Assert.Empty(counter.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, counter.WriteRegisters);
        Assert.Equal(InstructionClass.Csr, counter.InstructionClass);
        Assert.Equal(SerializationClass.CsrOrdered, counter.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, counter.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, counter.Placement.PinningKind);
        Assert.Equal((byte)7, counter.Placement.PinnedLaneId);

        InternalOp internalOp = new InternalOpBuilder().Build(
            CreateInstructionIr(InstructionsEnum.RDCYCLE, rd));
        Assert.Equal(InternalOpKind.CsrReadCounter, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Csr, internalOp.Category);
        Assert.Equal(CsrAddresses.Cycle, internalOp.CsrTarget);
        Assert.True(internalOp.IsSerializing);
        Assert.True(internalOp.ForbidsSmtInjection);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.RDCYCLE,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.RDCYCLE,
                    Reg1ID = rd,
                    Reg2ID = 0,
                    Reg3ID = 0,
                    HasImmediate = true,
                    Immediate = 1,
                }));

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.RDCYCLE,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.RDCYCLE,
                    Reg1ID = rd,
                    Reg2ID = 1,
                    Reg3ID = 0,
                    HasImmediate = true,
                    Immediate = 0,
                }));
    }

    [Fact]
    public void Rdcycle_DecoderIrAndProjector_UseCanonicalRdOnlyPayload()
    {
        const byte rd = 9;
        const ulong pc = 0x6D00UL;

        VLIW_Instruction[] rawSlots = CreateBundleAtSlot(
            7,
            CreateCounterInstruction(rd: rd));

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: pc, bundleSerial: 73);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(7).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.RDCYCLE, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.Csr, ir.Class);
        Assert.Equal(SerializationClass.CsrOrdered, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(0, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Equal(CsrAddresses.Cycle, ir.CsrAddress);
        Assert.False(ir.HasAbsoluteAddressing);
        Assert.Null(ir.VectorPayload);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        CsrReadCounterMicroOp microOp = Assert.IsType<CsrReadCounterMicroOp>(carrierBundle[7]);
        Assert.Equal((uint)InstructionsEnum.RDCYCLE, microOp.OpCode);
        Assert.Equal((ulong)CsrAddresses.Cycle, microOp.CSRAddress);
        Assert.Equal(rd, microOp.DestRegID);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, microOp.WriteRegisters);

        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(
                CreateBundleAtSlot(
                    7,
                    CreateCounterInstruction(rd: rd, rs1: 1)),
                0x6D20,
                74));

        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(
                CreateBundleAtSlot(
                    7,
                    CreateCounterInstruction(rd: rd, immediate: 1)),
                0x6D40,
                75));
    }

    [Fact]
    public void Rdcycle_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication()
    {
        const int vtId = 2;
        const ulong pc = 0x6E00UL;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;
        const ulong expectedCounterValue = 0x1234_5678_9ABC_DEF0UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
        core.Csr.HardwareWrite(CsrAddresses.Cycle, expectedCounterValue);

        VLIW_Instruction[] rawSlots =
            CreateBundleAtSlot(
                7,
                CreateCounterInstruction(
                    rd: (byte)destinationRegister,
                    virtualThreadId: (byte)vtId));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.RDCYCLE, decodeStatus.OpCode);
        Assert.False(decodeStatus.IsVectorOp);
        Assert.False(decodeStatus.IsMemoryOp);

        core.TestRunExecuteStageFromCurrentDecodeState();

        var executeStatus = core.TestReadExecuteStageStatus();
        Assert.True(executeStatus.Valid);
        Assert.True(executeStatus.ResultReady);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedCounterValue, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(pc, core.ReadCommittedPc(vtId));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.ScalarLanesRetired);
        Assert.Equal(1UL, control.NonScalarLanesRetired);
    }

    [Fact]
    public void Rdcycle_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x6E80UL;
        const ulong expectedCounterValue = 0xCAFE_BABE_F00D_1234UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.Csr.HardwareWrite(CsrAddresses.Cycle, expectedCounterValue);

        VLIW_Instruction[] rawSlots =
            CreateBundleAtSlot(
                7,
                CreateCounterInstruction(rd: 0, virtualThreadId: (byte)vtId));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(expectedCounterValue, core.Csr.DirectRead(CsrAddresses.Cycle));
    }

    [Fact]
    public void Rdcycle_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const ulong expectedCounterValue = 0x1010_2020_3030_4040UL;

        var core = new Processor.CPU_Core(0);
        var csrFile = new CsrFile();
        csrFile.HardwareWrite(CsrAddresses.Cycle, expectedCounterValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4(csrFile: csrFile);
        InstructionIR instruction = CreateInstructionIr(InstructionsEnum.RDCYCLE, rd);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 97,
                vtId);

        Assert.Equal(0UL, core.ReadArch(vtId, rd));
        RetireRecord record = Assert.Single(snapshot.RetireRecords);
        Assert.True(record.IsRegisterWrite);
        Assert.Equal(vtId, record.VtId);
        Assert.Equal(rd, record.ArchReg);
        Assert.Equal(expectedCounterValue, record.Value);
        Assert.False(snapshot.HasTypedEffect);

        RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
            ref core,
            dispatcher,
            instruction,
            state,
            bundleSerial: 97,
            vtId);

        Assert.Equal(expectedCounterValue, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void Rdcycle_CycleCsrFollowsPipelineCycleCounter()
    {
        const ulong pc = ulong.MaxValue;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: 0);

        ulong before = core.GetPipelineControl().CycleCount;
        core.ExecutePipelineCycle();

        ulong after = core.GetPipelineControl().CycleCount;
        Assert.Equal(before + 1, after);
        Assert.Equal(after, core.Csr.DirectRead(CsrAddresses.Cycle));
    }

    [Fact]
    public void Rdcycle_ReplayBoundaryAndRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x6F80UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        const ulong expectedCounterValue = 0x5555_AAAA_7777_CCCCUL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
        core.Csr.HardwareWrite(CsrAddresses.Cycle, expectedCounterValue);

        VLIW_Instruction instruction =
            CreateCounterInstruction(
                rd: (byte)destinationRegister,
                virtualThreadId: (byte)vtId);
        CsrReadCounterMicroOp microOp = DecodeAndMaterializeCounter(instruction, vtId);
        Assert.Equal(InternalOpKind.CsrReadCounter, new InternalOpBuilder()
            .Build(CreateInstructionIr(InstructionsEnum.RDCYCLE, (byte)destinationRegister))
            .Kind);

        HybridCPU_ISE.Core.ReplayToken rollbackToken = microOp.CreateRollbackToken(vtId);
        rollbackToken.CaptureRegisterState(ref core, [(int)destinationRegister]);

        core.TestRunDecodeStageWithFetchedBundle(CreateBundleAtSlot(7, instruction), pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedCounterValue, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Rdcycle_UnselectedTimerHintForms_RemainFailClosed()
    {
        string[] closed =
        [
            "RDTIME",
            "RDINSTRET",
            "PAUSE"
        ];

        foreach (string mnemonic in closed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
        }
    }

    [Fact]
    public void Rdcycle_CompilerEmissionOpensCycleCounterWhileAdjacentContoursRemainClosed()
    {
        string[] scalarClosed =
        [
            "POPCNT",
            "RDTIME",
            "RDINSTRET",
            "PAUSE",
            "CRC32",
            "CRC64",
            "ADC",
            "SBC",
            "ADDC",
            "SUBC",
            "SFENCE.VMA",
            "DCACHE_CLEAN",
            "ICACHE_INVAL",
        ];

        foreach (string mnemonic in scalarClosed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim, mnemonic);
        }

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerSystemCounterAbiContract", compilerSource, StringComparison.Ordinal);

        string compilerEmissionSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.Contains("InstructionsEnum.RDCYCLE", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("ReadSystemCycleCounter", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("ScalarSystemCounter", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ScalarSystemCounter", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileRdCycle", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitRdCycle", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.RDTIME", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.RDINSTRET", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileRdTime", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitRdTime", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileRdInstret", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitRdInstret", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReadSystemTimeCounter", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReadRetiredInstructionCounter", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.PAUSE", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompilePause", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitPause", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
    }

    private static CsrReadCounterMicroOp DecodeAndMaterializeCounter(
        VLIW_Instruction instruction,
        int vtId)
    {
        VLIW_Instruction[] rawSlots = CreateBundleAtSlot(7, instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7000, bundleSerial: 78);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        CsrReadCounterMicroOp microOp = Assert.IsType<CsrReadCounterMicroOp>(carrierBundle[7]);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static InstructionIR CreateInstructionIr(
        InstructionsEnum opcode,
        byte rd)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = 0,
            Rs2 = 0,
            Imm = 0,
            CsrAddress = opcode == InstructionsEnum.RDCYCLE
                ? CsrAddresses.Cycle
                : null
        };
    }

    private static VLIW_Instruction[] CreateBundleAtSlot(
        int slotIndex,
        VLIW_Instruction instruction)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;
        return rawSlots;
    }

    private static VLIW_Instruction CreateCounterInstruction(
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.RDCYCLE,
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

}
