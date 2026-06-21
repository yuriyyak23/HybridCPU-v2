using System;
using System.Linq;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcPhase08AStatusTests
{
    private const int TokenRegister = 9;
    private const int StatusRegister = 10;

    [Fact]
    public void A_StatusOpcodeStatusClassifierAndRegistry_AreRuntimeOwned()
    {
        InstructionSupportStatus status =
            InstructionSupportStatusCatalog.GetStatus("ACCEL_STATUS");

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("Lane7L7SDC", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Equal((ushort)266, (ushort)InstructionsEnum.ACCEL_STATUS);
        Assert.Equal((ushort)266, IsaOpcodeValues.ACCEL_STATUS);
        Assert.Equal("ACCEL_STATUS", OpcodeRegistry.GetMnemonicOrHex((uint)InstructionsEnum.ACCEL_STATUS));
        Assert.True(OpcodeRegistry.IsSystemDeviceCommandOpcode((uint)InstructionsEnum.ACCEL_STATUS));
        Assert.Contains("ACCEL_STATUS", IsaV4Surface.SystemDeviceCommandOpcodes);
        Assert.Contains("ACCEL_STATUS", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("ACCEL_STATUS", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.ACCEL_STATUS));
        Assert.Equal(SerializationClass.CsrOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.ACCEL_STATUS));

        MicroOp microOp = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.ACCEL_STATUS,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.ACCEL_STATUS,
                Reg1ID = StatusRegister,
                Reg2ID = TokenRegister,
                Reg3ID = 0
            });
        AcceleratorStatusMicroOp carrier = Assert.IsType<AcceleratorStatusMicroOp>(microOp);
        Assert.Equal(SystemDeviceCommandKind.Status, carrier.CommandKind);
        Assert.Equal(new[] { TokenRegister }, carrier.ReadRegisters);
        Assert.Equal(new[] { StatusRegister }, carrier.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.Placement.PinningKind);
        Assert.Equal((byte)7, carrier.Placement.PinnedLaneId);
        Assert.False(carrier.IsControlFlow);
        Assert.False(carrier.IsMemoryOp);
        Assert.False(carrier.UsedArithmeticExecutionPlane);
        Assert.False(carrier.UsedLegacyCustomAcceleratorFallback);
    }

    [Fact]
    public void A_StatusDecoderIrProjectionAndLegality_AreHardPinnedLane7()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = CreateStatusInstruction(StatusRegister, TokenRegister);

        DecodedInstructionBundle decoded =
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x8A00, bundleSerial: 8);
        InstructionIR instruction = decoded.GetDecodedSlot(7).RequireInstruction();

        Assert.Equal(InstructionsEnum.ACCEL_STATUS, instruction.CanonicalOpcode);
        Assert.Equal(InstructionClass.System, instruction.Class);
        Assert.Equal(SerializationClass.CsrOrdered, instruction.SerializationClass);
        Assert.Equal(StatusRegister, instruction.Rd);
        Assert.Equal(TokenRegister, instruction.Rs1);
        Assert.Equal(0, instruction.Rs2);
        Assert.Null(instruction.AcceleratorCommandDescriptor);
        Assert.False(instruction.AcceleratorCommandDescriptorReference.HasValue);

        BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(decoded);
        Assert.Equal(0b_1000_0000, legality.TypedSlotFacts.SystemSingletonMask);
        Assert.Equal(0, legality.TypedSlotFacts.BranchControlMask);
        Assert.Equal(0b_1000_0000, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0, legality.TypedSlotFacts.FlexibleSlotMask);
        Assert.Equal(new[] { TokenRegister }, BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction));
        Assert.True(BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction));
        Assert.Equal(new[] { StatusRegister }, BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister: true));

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        AcceleratorStatusMicroOp projected = Assert.IsType<AcceleratorStatusMicroOp>(carriers[7]);
        Assert.Equal(new[] { TokenRegister }, projected.ReadRegisters);
        Assert.Equal(new[] { StatusRegister }, projected.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, projected.AdmissionMetadata.Placement.RequiredSlotClass);
    }

    [Fact]
    public void A_StatusConflictsWithBranchControlPressureOnAliasedLane7()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = CreateBranchInstruction();
        rawSlots[7] = CreateStatusInstruction(StatusRegister, TokenRegister);

        DecodedInstructionBundle decoded =
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x8A40, bundleSerial: 9);
        BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(decoded);
        DecodedBundleDependencySummary summary =
            legality.DependencySummary ?? throw new InvalidOperationException("Expected dependency summary.");

        Assert.Equal(0b_0000_0001, legality.TypedSlotFacts.BranchControlMask);
        Assert.Equal(0b_1000_0000, legality.TypedSlotFacts.SystemSingletonMask);
        Assert.NotEqual(0UL, summary.ControlConflictMask);
        SlotHazardQueryResult branchHazards =
            summary.QuerySlotHazards(slotIndex: 0, scalarGroupMask: 0);
        Assert.True((branchHazards.HardRejectPeers & 0b_1000_0000) != 0);
        Assert.Equal(HazardEffectKind.SystemBarrier, branchHazards.DominantEffectKind);
    }

    [Fact]
    public void A_StatusRejectsWrongLaneDirtyRs2AndDescriptorSideband()
    {
        var wrongLane = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        wrongLane[6] = CreateStatusInstruction(StatusRegister, TokenRegister);
        InvalidOpcodeException wrongLaneEx = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(wrongLane, bundleAddress: 0x8A80));
        Assert.Contains("lane7", wrongLaneEx.Message, StringComparison.OrdinalIgnoreCase);

        var dirtyRs2 = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        dirtyRs2[7] = CreateStatusInstruction(StatusRegister, TokenRegister, rs2: 3);
        InvalidOpcodeException dirtyRs2Ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(dirtyRs2, bundleAddress: 0x8A90));
        Assert.Contains("rs2", dirtyRs2Ex.Message, StringComparison.OrdinalIgnoreCase);

        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var sideband = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        sideband[7] = CreateStatusInstruction(StatusRegister, TokenRegister);
        InvalidOpcodeException sidebandEx = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                sideband,
                L7SdcNativeCarrierValidationTests.CreateAnnotations(
                    7,
                    descriptor,
                    L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0x8AA0));
        Assert.Contains("ACCEL_SUBMIT", sidebandEx.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AcceleratorCommandDescriptor), sidebandEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_StatusMaterializerRejectsNonZeroRs2()
    {
        DecodeProjectionFaultException ex = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.ACCEL_STATUS,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.ACCEL_STATUS,
                    Reg1ID = StatusRegister,
                    Reg2ID = TokenRegister,
                    Reg3ID = 3
                }));

        Assert.Contains("ACCEL_STATUS", ex.Message, StringComparison.Ordinal);
        Assert.Contains("x0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_StatusExecuteCaptureAndRetireWritesPackedStatusOnlyThroughRuntime()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(0x1000, L7SdcPhase07TestFactory.Fill(0x42, 0x40));
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x7B, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            var core = new Processor.CPU_Core(0);
            var submit = new AcceleratorSubmitMicroOp(TokenRegister, L7SdcTestDescriptorFactory.ParseValidDescriptor());
            Assert.True(submit.Execute(ref core));
            Assert.True(submit.LastSubmitAdmission!.IsAccepted, submit.LastSubmitAdmission.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, submit.LastSubmitAdmission.Token!.State);
            RetireAndApply(ref core, submit);
            ulong tokenHandle = core.ReadArch(0, TokenRegister);
            Assert.NotEqual(0UL, tokenHandle);

            var status = new AcceleratorStatusMicroOp(StatusRegister, TokenRegister);
            Assert.True(status.Execute(ref core));
            Assert.Equal(SystemDeviceCommandKind.Status, status.LastCommandResult!.CommandKind);
            Assert.True(status.LastTokenLookup!.IsAllowed, status.LastTokenLookup.Message);
            Assert.True(status.TryGetPrimaryWriteBackResult(out ulong capturedStatus));
            Assert.Equal(0UL, core.ReadArch(0, StatusRegister));
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            RetireAndApply(ref core, status);
            Assert.Equal(capturedStatus, core.ReadArch(0, StatusRegister));
            AcceleratorTokenStatusWord unpacked =
                AcceleratorTokenStatusWord.Unpack(core.ReadArch(0, StatusRegister));
            Assert.Equal(AcceleratorTokenState.DeviceComplete, unpacked.State);
            Assert.Equal(AcceleratorTokenFaultCode.None, unpacked.FaultCode);
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.False(status.UsedArithmeticExecutionPlane);
            Assert.False(status.UsedLegacyCustomAcceleratorFallback);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void A_StatusInvalidHandleHasExplicitNoResultRetireSemantics()
    {
        var core = new Processor.CPU_Core(0);
        var status = new AcceleratorStatusMicroOp(StatusRegister, TokenRegister);

        Assert.True(status.Execute(ref core));
        Assert.True(status.LastTokenLookup!.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.InvalidHandle, status.LastCommandResult!.RegisterAbi.FaultCode);
        Assert.False(status.LastCommandResult.RegisterAbi.WritesRegister);
        Assert.False(status.TryGetPrimaryWriteBackResult(out ulong captured));
        Assert.Equal(0UL, captured);
        RetireAndApply(ref core, status, expectedRecords: 0);
        Assert.Equal(0UL, core.ReadArch(0, StatusRegister));
    }

    [Fact]
    public void A_StatusRollbackAndReplayDoNotPublishBeforeRetire()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(0x1000, L7SdcPhase07TestFactory.Fill(0x55, 0x40));
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0xE1, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            var core = new Processor.CPU_Core(0);
            var submit = new AcceleratorSubmitMicroOp(TokenRegister, L7SdcTestDescriptorFactory.ParseValidDescriptor());
            Assert.True(submit.Execute(ref core));
            RetireAndApply(ref core, submit);

            var rolledBack = new AcceleratorStatusMicroOp(StatusRegister, TokenRegister);
            Assert.True(rolledBack.Execute(ref core));
            Assert.True(rolledBack.TryGetPrimaryWriteBackResult(out ulong firstCapture));
            Assert.Equal(0UL, core.ReadArch(0, StatusRegister));

            var replayed = new AcceleratorStatusMicroOp(StatusRegister, TokenRegister);
            Assert.True(replayed.Execute(ref core));
            Assert.True(replayed.TryGetPrimaryWriteBackResult(out ulong replayCapture));
            Assert.Equal(firstCapture, replayCapture);
            RetireAndApply(ref core, replayed);

            Assert.Equal(replayCapture, core.ReadArch(0, StatusRegister));
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void A_UnselectedResultAndResetRemainReservedAndUnallocated()
    {
        foreach (string mnemonic in new[] { "ACCEL_GET_RESULT", "ACCEL_RESET" })
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _));
            Assert.DoesNotContain(mnemonic, IsaV4Surface.SystemDeviceCommandOpcodes);
        }

        // VMX Phase 2 owns 267/268 as INVEPT/INVVPID and VMX Phase 5a owns
        // 270/271 as VMSAVEX/VMRESTX; keep this sentinel on
        // currently unallocated raw slots so the Lane7 reserved-command proof
        // does not conflict with the frozen VMX ABI.
        foreach (ushort rawOpcode in new ushort[] { 272, 273 })
        {
            Assert.False(OpcodeRegistry.IsSystemDeviceCommandOpcode(rawOpcode));
            Assert.False(OpcodeRegistry.GetInfo(rawOpcode).HasValue);
            var raw = CreateStatusInstruction(StatusRegister, TokenRegister);
            raw.OpCode = rawOpcode;
            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => new VliwDecoderV4().Decode(in raw, slotIndex: 7));
            Assert.Contains("outside the canonical ISA v4 opcode space", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_StatusCompilerBoundaryKeepsCarrierOnlySubmitSurface()
    {
        string[] lane7Methods = typeof(HybridCpuThreadCompilerContext)
            .GetMethods(System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .Where(static name => name.Contains("Accelerator", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([nameof(HybridCpuThreadCompilerContext.CompileAcceleratorSubmit)], lane7Methods);
        Assert.DoesNotContain(lane7Methods, name => name.Contains("Status", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lane7Methods, name => name.Contains("GetResult", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lane7Methods, name => name.Contains("Reset", StringComparison.OrdinalIgnoreCase));
    }

    private static VLIW_Instruction CreateStatusInstruction(
        int rd,
        int rs1,
        int rs2 = 0)
    {
        VLIW_Instruction instruction =
            L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_STATUS);
        instruction.Word1 = VLIW_Instruction.PackArchRegs(
            (byte)rd,
            (byte)rs1,
            (byte)rs2);
        return instruction;
    }

    private static VLIW_Instruction CreateBranchInstruction()
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.BEQ,
            Word1 = VLIW_Instruction.PackArchRegs(0, 1, 2),
            Immediate = 4,
            Src2Pointer = 0
        };
    }

    private static void RetireAndApply(
        ref Processor.CPU_Core core,
        SystemDeviceCommandMicroOp microOp,
        int expectedRecords = 1)
    {
        RetireRecord[] records = new RetireRecord[2];
        int recordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, records, ref recordCount);
        Assert.Equal(expectedRecords, recordCount);
        if (recordCount != 0)
        {
            Assert.All(records.Take(recordCount), record => Assert.True(record.IsRegisterWrite));
            core.RetireCoordinator.Retire(records.AsSpan(0, recordCount));
        }
    }
}
