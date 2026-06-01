using System;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcPhase08ExecutableTests
{
    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void Phase08_CurrentL7SdcCommandsPublishExecutableStatusOnlyInsideScopedContour(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("Lane7L7SDC", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((InstructionClass.System, expectedSerialization), InstructionClassifier.Classify(opcode));
        Assert.Equal(expectedCarrierType, InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            L7SdcPhase03TestCases.CreateRegisterResultContext(opcode)).GetType());
    }

    [Theory]
    [InlineData("ACCEL_GET_RESULT")]
    [InlineData("ACCEL_RESET")]
    public void Phase08A_UnselectedCommandsRemainReservedAndUnallocated(string mnemonic)
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

    [Fact]
    public void Phase08_DecoderProjectionAndMaterializerPublishDescriptorBackedSubmitMicroOp()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);
        rawSlots[7].Word1 = VLIW_Instruction.PackArchRegs(
            9,
            VLIW_Instruction.NoArchReg,
            VLIW_Instruction.NoArchReg);

        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            rawSlots,
            L7SdcNativeCarrierValidationTests.CreateAnnotations(
                7,
                descriptor,
                L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()),
            bundleAddress: 0x8080,
            bundleSerial: 80);
        InstructionIR instruction = decoded.GetDecodedSlot(7).RequireInstruction();

        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, instruction.CanonicalOpcode);
        Assert.Equal(9, instruction.Rd);
        Assert.Same(descriptor, instruction.AcceleratorCommandDescriptor);
        Assert.Equal(descriptor.DescriptorReference, instruction.AcceleratorCommandDescriptorReference);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(carriers[7]);

        Assert.Same(descriptor, submit.CommandDescriptor);
        Assert.True(submit.WritesRegister);
        Assert.Equal(new[] { 9 }, submit.WriteRegisters);
        Assert.Empty(submit.ReadRegisters);
        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, submit.Placement.PinningKind);
        Assert.Equal(7, submit.Placement.PinnedLaneId);
        Assert.False(submit.UsedLegacyCustomAcceleratorFallback);
        Assert.False(submit.UsedArithmeticExecutionPlane);
    }

    [Fact]
    public void Phase08_SubmitPollWaitFenceExecuteCaptureAndRetireThroughStagedCommit()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(0x1000, L7SdcPhase07TestFactory.Fill(0x24, 0x40));
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0xD7, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            AcceleratorCommandDescriptor descriptor =
                L7SdcTestDescriptorFactory.ParseValidDescriptor();
            var core = new Processor.CPU_Core(0);

            var submit = new AcceleratorSubmitMicroOp(9, descriptor);
            Assert.True(submit.Execute(ref core));
            Assert.NotNull(submit.LastSubmitAdmission);
            Assert.True(submit.LastSubmitAdmission!.IsAccepted, submit.LastSubmitAdmission.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, submit.LastSubmitAdmission.Token!.State);
            Assert.Equal(1, submit.LastCommandResult!.BackendTickResult!.StagedWriteCount);
            Assert.False(submit.LastCommandResult.BackendTickResult.CanPublishArchitecturalMemory);
            Assert.True(submit.TryGetPrimaryWriteBackResult(out ulong capturedTokenHandle));
            Assert.NotEqual(0UL, capturedTokenHandle);
            RetireAndApply(ref core, submit);
            Assert.Equal(capturedTokenHandle, core.ReadArch(0, 9));
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            var poll = new AcceleratorPollMicroOp(10, 9);
            Assert.True(poll.Execute(ref core));
            Assert.True(poll.LastTokenLookup!.IsAllowed, poll.LastTokenLookup.Message);
            RetireAndApply(ref core, poll);
            AcceleratorTokenStatusWord pollStatus =
                AcceleratorTokenStatusWord.Unpack(core.ReadArch(0, 10));
            Assert.Equal(AcceleratorTokenState.DeviceComplete, pollStatus.State);
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            var wait = new AcceleratorWaitMicroOp(11, 9);
            Assert.True(wait.Execute(ref core));
            Assert.True(wait.LastTokenLookup!.IsAllowed, wait.LastTokenLookup.Message);
            RetireAndApply(ref core, wait);
            AcceleratorTokenStatusWord waitStatus =
                AcceleratorTokenStatusWord.Unpack(core.ReadArch(0, 11));
            Assert.Equal(AcceleratorTokenState.DeviceComplete, waitStatus.State);

            var fence = new AcceleratorFenceMicroOp(12, 9);
            Assert.True(fence.Execute(ref core));
            Assert.True(fence.LastCommandResult!.FenceResult!.Succeeded, fence.LastCommandResult.FenceResult.Message);
            Assert.False(fence.LastCommandResult.FenceResult.CanPublishArchitecturalMemory);
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            RetireAndApply(ref core, fence);
            Assert.NotNull(fence.LastCommandResult!.CommitResult);
            Assert.True(fence.LastCommandResult.CommitResult!.Succeeded, fence.LastCommandResult.CommitResult.Message);
            Assert.Equal(1, fence.LastCommandResult.FenceResult!.CommittedCount);
            Assert.Equal(AcceleratorTokenState.Committed, submit.LastSubmitAdmission.Token.State);
            Assert.NotEqual(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.Equal(1UL, (core.ReadArch(0, 12) >> 32) & 0xFFFFUL);

            Assert.False(submit.UsedLegacyCustomAcceleratorFallback);
            Assert.False(poll.UsedLegacyCustomAcceleratorFallback);
            Assert.False(wait.UsedLegacyCustomAcceleratorFallback);
            Assert.False(fence.UsedLegacyCustomAcceleratorFallback);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase08_QueryCapsAndCancelStayControlPlaneOnly()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            var core = new Processor.CPU_Core(0);

            var query = new AcceleratorQueryCapsMicroOp(5);
            Assert.True(query.Execute(ref core));
            RetireAndApply(ref core, query);
            ulong caps = core.ReadArch(0, 5);
            Assert.NotEqual(0UL, caps);
            Assert.Equal(1UL, caps & 0x1UL);

            var cancel = new AcceleratorCancelMicroOp(6, 4);
            Assert.True(cancel.Execute(ref core));
            Assert.True(cancel.LastTokenLookup!.IsRejected);
            Assert.False(cancel.LastCommandResult!.RegisterAbi.WritesRegister);
            RetireAndApply(ref core, cancel, expectedRecords: 0);
            Assert.Equal(0UL, core.ReadArch(0, 6));
            Assert.False(query.UsedArithmeticExecutionPlane);
            Assert.False(cancel.UsedArithmeticExecutionPlane);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase08_DescriptorlessSubmitRemainsAdjacentFailClosed()
    {
        var core = new Processor.CPU_Core(0);
        var submit = new AcceleratorSubmitMicroOp(9);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => submit.Execute(ref core));

        Assert.Contains("descriptorless raw factory execution remains fail-closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(submit.UsedLegacyCustomAcceleratorFallback);
        Assert.False(submit.UsedArithmeticExecutionPlane);
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
