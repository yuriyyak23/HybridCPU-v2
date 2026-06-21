using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeStatusPhase07Tests
{
    [Fact]
    public void DscStatus_OpcodeDecoderRegistryAndProjectionPublishLane6Carrier()
    {
        Assert.Equal(246, (ushort)InstructionsEnum.DSC_STATUS);
        Assert.Equal((ushort)InstructionsEnum.DSC_STATUS, IsaOpcodeValues.DSC_STATUS);

        OpcodeInfo? maybeInfo = OpcodeRegistry.GetInfo((uint)InstructionsEnum.DSC_STATUS);
        Assert.True(maybeInfo.HasValue);
        OpcodeInfo info = maybeInfo.Value;
        Assert.Equal("DSC_STATUS", info.Mnemonic);
        Assert.Equal(InstructionClass.Memory, info.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, info.SerializationClass);
        Assert.Equal(
            (InstructionClass.Memory, SerializationClass.MemoryOrdered),
            InstructionClassifier.Classify(InstructionsEnum.DSC_STATUS));

        InstructionSupportStatus support =
            InstructionSupportStatusCatalog.GetStatus("DSC_STATUS");
        Assert.True(support.IsExecutableClaim);
        Assert.Equal("Lane6QueueControl", support.ExtensionName);

        var decoder = new VliwDecoderV4();
        VLIW_Instruction raw = CreateStatusInstruction(destinationRegister: 5, tokenRegister: 4);
        InstructionIR ir = decoder.Decode(in raw, slotIndex: 6);
        Assert.Equal(InstructionsEnum.DSC_STATUS, (InstructionsEnum)ir.CanonicalOpcode.Value);
        Assert.Equal(5, ir.Rd);
        Assert.Equal(4, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.False(ir.DmaStreamComputeDescriptorReference.HasValue);

        VLIW_Instruction dirtyRs2 = CreateStatusInstruction(destinationRegister: 5, tokenRegister: 4);
        dirtyRs2.Word1 = VLIW_Instruction.PackArchRegs(5, 4, 3);
        InvalidOpcodeException rs2Fault = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in dirtyRs2, slotIndex: 6));
        Assert.Contains("rs2", rs2Fault.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x0", rs2Fault.Message, StringComparison.OrdinalIgnoreCase);

        MicroOp registryMicroOp = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.DSC_STATUS,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.DSC_STATUS,
                Reg1ID = 5,
                Reg2ID = 4,
                Reg3ID = 0
            });
        Assert.IsType<DmaStreamComputeStatusMicroOp>(registryMicroOp);

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = raw;
        DecodedInstructionBundle decoded =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7100);
        BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(decoded);
        Assert.Equal(0b_0100_0000, legality.TypedSlotFacts.DmaStreamClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0b_0100_0000, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0, legality.TypedSlotFacts.FlexibleSlotMask);
        Assert.Equal(SlotClass.DmaStreamClass, legality.GetSlotLegality(6).SlotClass);
        Assert.False(legality.HasMemoryOps);
        Assert.True(legality.DependencySummary.HasValue);
        Assert.Equal(1UL << 4, legality.DependencySummary.Value.ReadRegisterMask);
        Assert.Equal(1UL << 5, legality.DependencySummary.Value.WriteRegisterMask);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        DmaStreamComputeStatusMicroOp projected =
            Assert.IsType<DmaStreamComputeStatusMicroOp>(carriers[6]);
        Assert.Equal(SlotClass.DmaStreamClass, projected.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, projected.Placement.PinningKind);
        Assert.Equal(6, projected.Placement.PinnedLaneId);
        Assert.False(projected.IsMemoryOp);
        Assert.True(projected.WritesRegister);
        Assert.Equal(new[] { 4 }, projected.ReadRegisters);
        Assert.Equal(new[] { 5 }, projected.WriteRegisters);
    }

    [Fact]
    public void DscStatus_ExecuteCaptureRetireWritebackObservesCommitPendingTokenOnlyAtRetire()
    {
        DmaStreamComputeDescriptor descriptor = CreateStatusOwnedDescriptor();
        Processor.CPU_Core core = CreateCoreWithStagedCopy(descriptor, out DmaStreamComputeMicroOp compute);
        ulong tokenId = compute.LastExecutionTokenHandle.TokenId;
        core.WriteCommittedArch(0, TokenRegister, tokenId);

        DmaStreamComputeStatusMicroOp status = CreateStatusMicroOp(descriptor);
        Assert.True(status.Execute(ref core));

        DmaStreamComputeStatusQueryResult result = status.LastStatusResult!;
        Assert.True(result.IsAccepted, result.Message);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, result.Snapshot!.State);
        Assert.Equal(1, result.Snapshot.StagedWriteCount);
        Assert.True(status.LastStatusReplayEvidence.IsComplete);
        Assert.False(status.UsedStreamEngineFallback);
        Assert.False(status.UsedDmaControllerFallback);
        Assert.Equal(1, compute.ActiveTokenCount);
        Assert.Equal(0UL, core.ReadArch(0, StatusRegister));
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));

        RetireRecord[] records = new RetireRecord[1];
        int recordCount = 0;
        status.EmitWriteBackRetireRecords(ref core, records, ref recordCount);
        Assert.Equal(1, recordCount);
        Assert.Equal(0UL, core.ReadArch(0, StatusRegister));

        core.RetireCoordinator.Retire(records.AsSpan(0, recordCount));

        ulong statusWord = core.ReadArch(0, StatusRegister);
        Assert.Equal((byte)DmaStreamComputeTokenState.CommitPending, statusWord & 0xFFUL);
        Assert.Equal(1UL, statusWord >> 32);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, compute.LastExecutionToken!.State);
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DscStatus_InvalidAndForeignTokensRejectAtRetireWithoutWriteback()
    {
        DmaStreamComputeDescriptor descriptor = CreateStatusOwnedDescriptor();
        Processor.CPU_Core core = CreateCoreWithStagedCopy(descriptor, out DmaStreamComputeMicroOp compute);

        core.WriteCommittedArch(0, TokenRegister, 0xBADUL);
        DmaStreamComputeStatusMicroOp invalid = CreateStatusMicroOp(descriptor);
        Assert.True(invalid.Execute(ref core));
        Assert.Equal(DmaStreamComputeStatusQueryRejectKind.InvalidToken, invalid.LastStatusResult!.RejectKind);
        Assert.Throws<InvalidOperationException>(() => EmitStatusRetire(ref core, invalid));
        Assert.Equal(0UL, core.ReadArch(0, StatusRegister));

        core.WriteCommittedArch(0, TokenRegister, compute.LastExecutionTokenHandle.TokenId);
        DmaStreamComputeStatusMicroOp foreign = CreateStatusMicroOp(
            descriptor,
            ownerDomainTag: descriptor.OwnerBinding.OwnerDomainTag ^ 0x40UL);
        Assert.True(foreign.Execute(ref core));
        Assert.Equal(DmaStreamComputeStatusQueryRejectKind.OwnerDomainMismatch, foreign.LastStatusResult!.RejectKind);
        Assert.Throws<DomainFaultException>(() => EmitStatusRetire(ref core, foreign));
        Assert.Equal(0UL, core.ReadArch(0, StatusRegister));
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, compute.LastExecutionToken!.State);
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DscStatus_ReplayRollbackEvidenceChangesAfterTokenCancelWithoutPublishingMemory()
    {
        DmaStreamComputeDescriptor descriptor = CreateStatusOwnedDescriptor();
        Processor.CPU_Core core = CreateCoreWithStagedCopy(descriptor, out DmaStreamComputeMicroOp compute);
        core.WriteCommittedArch(0, TokenRegister, compute.LastExecutionTokenHandle.TokenId);

        DmaStreamComputeStatusMicroOp before = CreateStatusMicroOp(descriptor);
        Assert.True(before.Execute(ref core));
        DmaStreamComputeStatusReplayEvidence beforeEvidence = before.LastStatusReplayEvidence;
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, beforeEvidence.TokenLifecycleEvidence.State);

        compute.LastExecutionToken!.Cancel(DmaStreamComputeTokenCancelReason.ReplayDiscard);

        DmaStreamComputeStatusMicroOp after = CreateStatusMicroOp(descriptor);
        Assert.True(after.Execute(ref core));
        DmaStreamComputeStatusReplayEvidence afterEvidence = after.LastStatusReplayEvidence;

        Assert.True(after.LastStatusResult!.IsAccepted);
        Assert.Equal(DmaStreamComputeTokenState.Canceled, afterEvidence.TokenLifecycleEvidence.State);
        Assert.Equal(DmaStreamComputeTokenCancelReason.ReplayDiscard, afterEvidence.TokenLifecycleEvidence.CancelReason);
        Assert.NotEqual(beforeEvidence.TokenLifecycleEvidence.EvidenceHash, afterEvidence.TokenLifecycleEvidence.EvidenceHash);
        Assert.False(afterEvidence.TokenLifecycleEvidence.HasCommitted);
        Assert.Equal(0, afterEvidence.TokenLifecycleEvidence.StagedWriteCount);
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DscStatus_DoesNotConsumeOutstandingTokenCapacity()
    {
        DmaStreamComputeDescriptor descriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore(
            new DmaStreamComputeTokenStoreOptions(
                activeTokenCapacity: 1,
                perDomainTokenQuota: 1));

        DmaStreamComputeIssueAdmissionResult first =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 1)));
        Assert.True(first.IsAccepted, first.Message);

        DmaStreamComputeStatusQueryResult status =
            store.QueryStatusByTokenId(first.Handle.TokenId, descriptor.OwnerBinding);
        Assert.True(status.IsAccepted, status.Message);
        Assert.Equal(1, store.ActiveTokenCount);

        DmaStreamComputeIssueAdmissionResult second =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 2)));
        Assert.False(second.IsAccepted);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.StoreCapacity, second.RejectKind);
        Assert.Equal(1, store.ActiveTokenCount);
    }

    [Fact]
    public void DscStatus_AdjacentQueueControlDsc2Lane7AndCompilerBoundariesStayClosed()
    {
        var decoder = new VliwDecoderV4();
        var wrongSlot = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        wrongSlot[5] = CreateStatusInstruction(destinationRegister: 5, tokenRegister: 4);
        InvalidOpcodeException laneFault = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(wrongSlot, bundleAddress: 0x7200));
        Assert.Contains("lane6", laneFault.Message, StringComparison.OrdinalIgnoreCase);

        string[] closedQueueControls =
        [
            "DSC_POLL",
            "DSC_WAIT",
            "DSC_CANCEL",
            "DSC_FENCE",
            "DSC_COMMIT"
        ];
        foreach (string mnemonic in closedQueueControls)
        {
            InstructionSupportStatus support = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, support.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, support.RuntimeEvidence);
            Assert.False(support.HasNumericOpcode);
            Assert.False(Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _));
        }

        Assert.Equal(IsaInstructionStatus.ParserOnly, InstructionSupportStatusCatalog.GetStatus("DSC2").Status);
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, InstructionSupportStatusCatalog.GetStatus("ACCEL_WAIT").Status);
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, InstructionSupportStatusCatalog.GetStatus("ACCEL_CANCEL").Status);

        string[] threadMethods = typeof(HybridCpuThreadCompilerContext)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .ToArray();
        Assert.Equal(
            [nameof(HybridCpuThreadCompilerContext.CompileDmaStreamCompute),
             nameof(HybridCpuThreadCompilerContext.CompileDmaStreamComputeDescriptor)],
            threadMethods
                .Where(name => name.Contains("DmaStreamCompute", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(threadMethods, name => name.Contains("Dsc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Queue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Status", StringComparison.OrdinalIgnoreCase));

        string repoRoot = ResolveRepoRoot();
        string compilerText = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.DoesNotContain("DSC_STATUS", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeStatus", compilerText, StringComparison.Ordinal);
    }

    private const ushort TokenRegister = 4;
    private const ushort StatusRegister = 5;
    private const ulong OwnerDomainTag = 0x20UL;

    private static VLIW_Instruction CreateStatusInstruction(
        byte destinationRegister,
        byte tokenRegister)
    {
        return InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DSC_STATUS,
            DataTypeEnum.UINT64,
            destinationRegister,
            tokenRegister,
            0);
    }

    private static Processor.CPU_Core CreateCoreWithStagedCopy(
        DmaStreamComputeDescriptor descriptor,
        out DmaStreamComputeMicroOp compute)
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeTelemetryTests.WriteMemory(0x1000, DmaStreamComputeTelemetryTests.Fill(0x11, 16));
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, DmaStreamComputeTelemetryTests.Fill(0xEE, 16));

        var core = new Processor.CPU_Core(0);
        compute = new DmaStreamComputeMicroOp(descriptor);
        Assert.True(compute.Execute(ref core));
        Assert.False(compute.LastExecutionTokenHandle.IsDefault);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, compute.LastExecutionToken!.State);
        return core;
    }

    private static DmaStreamComputeStatusMicroOp CreateStatusMicroOp(
        DmaStreamComputeDescriptor descriptor,
        ulong? ownerDomainTag = null)
    {
        var microOp = new DmaStreamComputeStatusMicroOp(
            StatusRegister,
            TokenRegister)
        {
            VirtualThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId,
            OwnerThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId,
            OwnerContextId = (int)descriptor.OwnerBinding.OwnerContextId,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.DmaStreamClass,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 6,
                DomainTag = ownerDomainTag ?? descriptor.OwnerBinding.OwnerDomainTag
            }
        };
        microOp.RefreshOwnerDependentMetadata();
        return microOp;
    }

    private static void EmitStatusRetire(
        ref Processor.CPU_Core core,
        DmaStreamComputeStatusMicroOp status)
    {
        RetireRecord[] records = new RetireRecord[1];
        int count = 0;
        status.EmitWriteBackRetireRecords(ref core, records, ref count);
    }

    private static DmaStreamComputeDescriptor CreateStatusOwnedDescriptor()
    {
        byte[] descriptorBytes =
            DmaStreamComputeTestDescriptorFactory.BuildDescriptor(
                DmaStreamComputeOperationKind.Copy);
        WriteUInt16(descriptorBytes, 60, 0);
        WriteUInt32(descriptorBytes, 64, 0);
        WriteUInt32(descriptorBytes, 68, 0);
        WriteUInt32(descriptorBytes, 72, 0);
        WriteUInt64(descriptorBytes, 80, OwnerDomainTag);

        DmaStreamComputeDescriptorReference reference =
            DmaStreamComputeTestDescriptorFactory.CreateReference(descriptorBytes);
        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes, reference),
                reference);
        Assert.True(validation.IsValid, validation.Message);
        return validation.RequireDescriptorForAdmission();
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    private static string ResolveRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory, "HybridCPU_ISE")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to resolve repository root for Phase 07 boundary test.");
    }
}
