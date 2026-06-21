using System;
using System.IO;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class Lane6DscContractClosureTests
{
    [Fact]
    public void Lane6Dsc_RawRegistryReferenceOnlyAndUnguardedMaterialization_FailClosed()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.DmaStreamCompute,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32
        };

        DecodeProjectionFaultException rawFactory = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.DmaStreamCompute, context));
        Assert.Contains("guard-accepted descriptor sideband", rawFactory.Message, StringComparison.Ordinal);
        Assert.Contains("not the canonical lane6 descriptor path", rawFactory.Message, StringComparison.Ordinal);

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = CreateNativeDscInstruction();
        InvalidOpcodeException rawDecode = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x5100));
        Assert.Contains("typed decoded sideband", rawDecode.Message, StringComparison.OrdinalIgnoreCase);

        MicroOp?[] carriers = DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
            rawSlots,
            CreateReferenceOnlyDecodedBundle(descriptor.DescriptorReference));
        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(carriers[6]);
        Assert.Contains("without the guard-accepted descriptor payload", trap.TrapReason, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotType<DmaStreamComputeMicroOp>(trap);

        DmaStreamComputeOwnerGuardContext staleContext =
            descriptor.OwnerGuardDecision.RuntimeOwnerContext with
            {
                OwnerDomainTag = descriptor.OwnerBinding.OwnerDomainTag ^ 0x100UL,
                ActiveDomainCertificate = descriptor.OwnerBinding.OwnerDomainTag ^ 0x100UL
            };
        DmaStreamComputeOwnerGuardDecision staleGuard =
            new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
                descriptor.OwnerBinding,
                staleContext);

        InvalidOperationException unguardedMaterialization = Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor with { OwnerGuardDecision = staleGuard }));
        Assert.Contains("accepted owner/domain guard", unguardedMaterialization.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lane6Dsc_AcceptedCarrierIsHardPinnedLane6AndOtherSlotsCannotDecodeOrAdmit()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();

        for (int slot = 0; slot < BundleMetadata.BundleSlotCount; slot++)
        {
            if (slot == 6)
            {
                continue;
            }

            var wrongSlotRaw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            wrongSlotRaw[slot] = CreateNativeDscInstruction();

            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => new VliwDecoderV4().DecodeInstructionBundle(
                    wrongSlotRaw,
                    CreateDescriptorAnnotations(slot, descriptor),
                    bundleAddress: 0x5200 + (ulong)slot));

            Assert.Contains("lane6", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"Slot {slot}", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = CreateNativeDscInstruction();
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            rawSlots,
            CreateDescriptorAnnotations(6, descriptor),
            bundleAddress: 0x5300,
            bundleSerial: 9);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        DmaStreamComputeMicroOp carrier = Assert.IsType<DmaStreamComputeMicroOp>(carriers[6]);
        Assert.Equal(SlotClass.DmaStreamClass, carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.Placement.PinningKind);
        Assert.Equal((byte)6, carrier.Placement.PinnedLaneId);
        Assert.Equal(SlotClass.DmaStreamClass, carrier.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)6, carrier.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Equal((byte)0b_0100_0000, SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass));

        DmaStreamComputePressurePolicy policy = DmaStreamComputePressurePolicy.Default;
        DmaStreamComputePressureSnapshot pressure = DmaStreamComputePressureSnapshot.Permissive(policy);

        DmaStreamComputeIssueAdmissionResult wrongSlotAdmission =
            new DmaStreamComputeTokenStore().TryAllocateAtIssueAdmission(
                new DmaStreamComputeIssueAdmissionRequest(
                    carrier,
                    descriptor.OwnerGuardDecision,
                    new DmaStreamComputeIssueAdmissionMetadata(
                        issuingPc: 0x5300,
                        bundleId: 9,
                        slotIndex: 5,
                        laneIndex: 6,
                        issueCycle: 1,
                        replayEpoch: 0),
                    policy,
                    pressure));
        Assert.False(wrongSlotAdmission.IsAccepted);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.InvalidIssuePlacement, wrongSlotAdmission.RejectKind);

        DmaStreamComputeIssueAdmissionResult wrongLaneAdmission =
            new DmaStreamComputeTokenStore().TryAllocateAtIssueAdmission(
                new DmaStreamComputeIssueAdmissionRequest(
                    carrier,
                    descriptor.OwnerGuardDecision,
                    new DmaStreamComputeIssueAdmissionMetadata(
                        issuingPc: 0x5300,
                        bundleId: 9,
                        slotIndex: 6,
                        laneIndex: 5,
                        issueCycle: 1,
                        replayEpoch: 0),
                    policy,
                    pressure));
        Assert.False(wrongLaneAdmission.IsAccepted);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.InvalidIssuePlacement, wrongLaneAdmission.RejectKind);

        DmaStreamComputeIssueAdmissionResult lane6Admission =
            new DmaStreamComputeTokenStore().TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    carrier,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(
                        issuingPc: 0x5300,
                        bundleId: 9,
                        issueCycle: 1,
                        replayEpoch: 0)));
        Assert.True(lane6Admission.IsAccepted, lane6Admission.Message);
        Assert.Equal(6, lane6Admission.Entry!.Metadata.SlotIndex);
        Assert.Equal(6, lane6Admission.Entry.Metadata.LaneIndex);
    }

    [Fact]
    public void Lane6Dsc_DirectExecutionAndProjectionPathsDoNotUseFallbackExecution()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);

        Processor.MainMemoryArea previousMainMemory = Processor.MainMemory;
        var previousMemorySubsystem = Processor.Memory;
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x10000);
        Processor.Memory = null;
        byte[] original = { 0x21, 0x43, 0x65, 0x87 };

        try
        {
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x1000, new byte[16]));
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x2000, new byte[16]));
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x9000, original));
            var core = new Processor.CPU_Core(0);

            Assert.True(microOp.Execute(ref core));

            Assert.NotNull(microOp.LastExecutionResult);
            Assert.True(microOp.LastExecutionResult!.IsStoreTracked);
            Assert.Equal(DmaStreamComputeTokenState.CommitPending, microOp.LastExecutionToken!.State);
            Assert.False(microOp.TryGetPrimaryWriteBackResult(out ulong value));
            Assert.Equal(0UL, value);

            byte[] observed = new byte[original.Length];
            Assert.True(Processor.MainMemory.TryReadPhysicalRange(0x9000, observed));
            Assert.Equal(original, observed);
        }
        finally
        {
            Processor.MainMemory = previousMainMemory;
            Processor.Memory = previousMemorySubsystem;
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string projectionAndCarrierSource = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "DmaStreamComputeMicroOp.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Decoder", "DecodedBundleTransportProjector.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Decoder", "VliwDecoderV4.cs")));

        Assert.DoesNotContain("StreamEngine.Execute(", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("YAKSys_Hybrid_CPU.Execution.StreamEngine.Execute", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorALU.", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ScalarALUMicroOp", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeRuntime.ExecuteToCommitPending", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamAcceleratorBackend", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.DMAController", projectionAndCarrierSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DMAController.", projectionAndCarrierSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Lane6Dsc_DescriptorTokenTelemetryAndReplayEvidenceAreNotExecutionAuthority()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        DmaStreamComputeDescriptorReference reference =
            DmaStreamComputeTestDescriptorFactory.CreateReference(descriptorBytes);
        DmaStreamComputeValidationResult validation = DmaStreamComputeDescriptorParser.Parse(
            descriptorBytes,
            DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes, reference),
            reference,
            telemetry);

        Assert.True(validation.IsValid, validation.Message);
        DmaStreamComputeDescriptor descriptor = validation.RequireDescriptorForAdmission();
        DmaStreamComputeTelemetrySnapshot parsedSnapshot = telemetry.Snapshot();
        Assert.Equal(1, parsedSnapshot.DescriptorAccepted);
        Assert.True(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        var microOp = new DmaStreamComputeMicroOp(descriptor);
        Assert.True(microOp.ReplayEvidence.IsComplete);
        Assert.Equal(DmaStreamComputeTokenEvidenceKind.NotCreated, microOp.ReplayEvidence.TokenLifecycleEvidence.EvidenceKind);

        var store = new DmaStreamComputeTokenStore();
        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(
                        issuingPc: 0x5400,
                        bundleId: 0x54,
                        issueCycle: 2,
                        replayEpoch: 1),
                    telemetry));

        Assert.True(admission.IsAccepted, admission.Message);
        DmaStreamComputeTokenLifecycleEvidence tokenEvidence =
            admission.Token!.ExportLifecycleEvidence();
        DmaStreamComputeReplayEvidence envelope = microOp.ExportReplayEvidence(
            tokenEvidence,
            DmaStreamComputeLanePlacementEvidence.MaterializedLane6(
                selectedLane: 6,
                freeLaneMask: 0b_0100_0000,
                stableDonorMask: 0,
                replayActive: false));
        Assert.True(envelope.IsComplete);

        Processor.MainMemoryArea previousMainMemory = Processor.MainMemory;
        var previousMemorySubsystem = Processor.Memory;
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x10000);
        Processor.Memory = null;
        byte[] original = { 0xA5, 0x5A, 0xC3, 0x3C };

        try
        {
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x1000, new byte[16]));
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x2000, new byte[16]));
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x9000, original));
            var core = new Processor.CPU_Core(0);

            Assert.True(microOp.Execute(ref core));

            Assert.Equal(original, ReadMemory(0x9000, original.Length));
            DmaStreamComputeTelemetrySnapshot finalSnapshot = telemetry.Snapshot();
            Assert.Equal(1, finalSnapshot.DescriptorAccepted);
            Assert.Equal(1, finalSnapshot.ComputeAccepted);
            Assert.Equal(0, finalSnapshot.ComputeCommitted);
        }
        finally
        {
            Processor.MainMemory = previousMainMemory;
            Processor.Memory = previousMemorySubsystem;
        }
    }

    private static VLIW_Instruction CreateNativeDscInstruction() =>
        new()
        {
            OpCode = (uint)InstructionsEnum.DmaStreamCompute,
            DataType = 0,
            PredicateMask = 0,
            Immediate = 0,
            DestSrc1Pointer = 0,
            Src2Pointer = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

    private static VliwBundleAnnotations CreateDescriptorAnnotations(
        int slotIndex,
        DmaStreamComputeDescriptor descriptor)
    {
        InstructionSlotMetadata[] metadata = CreateDefaultInstructionSlotMetadata();
        metadata[slotIndex] = new InstructionSlotMetadata(
            VtId.Create(0),
            SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = descriptor
        };

        return new VliwBundleAnnotations(metadata);
    }

    private static DecodedInstructionBundle CreateReferenceOnlyDecodedBundle(
        DmaStreamComputeDescriptorReference descriptorReference)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.DmaStreamCompute,
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.MemoryOrdered,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
            DmaStreamComputeDescriptorReference = descriptorReference
        };

        return new DecodedInstructionBundle(
            bundleAddress: 0x5120,
            bundleSerial: 1,
            slots: new[] { DecodedInstruction.CreateOccupied(6, instruction) });
    }

    private static InstructionSlotMetadata[] CreateDefaultInstructionSlotMetadata()
    {
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        return metadata;
    }

    private static byte[] ReadMemory(ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
        return bytes;
    }
}
