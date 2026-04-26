using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DecodeIssueCarrierTailTests
{
    [Fact]
    public void DecodeIssueFacts_UseLiveAdmissionAndExecutionState_WhenCarrierFactsAreTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6000);

        var microOp = new LoadMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.Load,
            Address = 0x240,
            BaseRegID = 1,
            DestRegID = 3,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        Assert.Same(microOp, canonicalSlot.MicroOp);

        microOp.VirtualThreadId = 0;
        microOp.OwnerThreadId = 2;
        microOp.OpCode = (uint)InstructionsEnum.AMOADD_W;
        microOp.Address = 0x840;
        microOp.BaseRegID = 11;
        microOp.DestRegID = 13;
        microOp.WritesRegister = true;
        microOp.InitializeMetadata();

        DecodedBundleSlotDescriptor tamperedSlot = WithIssueFacts(
            canonicalSlot,
            virtualThreadId: 3,
            ownerThreadId: 1,
            opCode: (uint)InstructionsEnum.ADDI,
            readRegisters: new[] { 1, 2 },
            writeRegisters: new[] { 4 },
            writesRegister: false,
            isMemoryOp: false,
            isControlFlow: true,
            memoryBankIntent: 99);

        var runtimeFacts = core.TestReadDecodedSlotRuntimeIssueFacts(tamperedSlot);

        Assert.Equal(microOp.OpCode, runtimeFacts.OpCode);
        Assert.Equal(microOp.AdmissionMetadata.IsMemoryOp, runtimeFacts.IsMemoryOp);
        Assert.Equal(microOp.AdmissionMetadata.IsControlFlow, runtimeFacts.IsControlFlow);
        Assert.Equal(microOp.AdmissionMetadata.WritesRegister, runtimeFacts.WritesRegister);
        Assert.Equal(microOp.VirtualThreadId, runtimeFacts.VirtualThreadId);
        Assert.Equal(microOp.MemoryBankId, runtimeFacts.MemoryBankIntent);
        Assert.Equal(microOp.AdmissionMetadata.ReadRegisters, runtimeFacts.ReadRegisters);
        Assert.Equal(microOp.AdmissionMetadata.WriteRegisters, runtimeFacts.WriteRegisters);

        Assert.NotEqual((uint)InstructionsEnum.ADDI, runtimeFacts.OpCode);
        Assert.False(runtimeFacts.IsControlFlow);
        Assert.True(runtimeFacts.IsMemoryOp);
        Assert.True(runtimeFacts.WritesRegister);
        Assert.NotEqual(3, runtimeFacts.VirtualThreadId);
        Assert.NotEqual(99, runtimeFacts.MemoryBankIntent);
        Assert.DoesNotContain(1, runtimeFacts.ReadRegisters);
        Assert.DoesNotContain(4, runtimeFacts.WriteRegisters);
    }

    [Fact]
    public void ForegroundSkip_UsesLiveControlFlowAndVirtualThread_WhenCarrierFactsAreTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7000);

        var controlFallback = new NopMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.JAL,
            IsControlFlow = true
        };
        controlFallback.RefreshAdmissionMetadata();

        DecodedBundleSlotDescriptor canonicalSlot = DecodedBundleSlotDescriptor.Create(0, controlFallback);
        DecodedBundleSlotDescriptor tamperedSlot = WithIssueFacts(
            canonicalSlot,
            virtualThreadId: 1,
            ownerThreadId: 1,
            opCode: (uint)InstructionsEnum.ADDI,
            readRegisters: canonicalSlot.ReadRegisters,
            writeRegisters: canonicalSlot.WriteRegisters,
            writesRegister: canonicalSlot.WritesRegister,
            isMemoryOp: canonicalSlot.IsMemoryOp,
            isControlFlow: false,
            memoryBankIntent: canonicalSlot.MemoryBankIntent);

        bool shouldSkip = core.TestShouldSkipDecodedSlotForForegroundIssue(tamperedSlot);

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ForegroundSkip_UsesLiveEmptyState_WhenCarrierEmptyFlagIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7100);

        var microOp = new LoadMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.Load,
            Address = 0x280,
            BaseRegID = 2,
            DestRegID = 5,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        DecodedBundleSlotDescriptor canonicalSlot = DecodedBundleSlotDescriptor.Create(0, microOp);
        DecodedBundleSlotDescriptor tamperedSlot = WithEmptyState(canonicalSlot, isEmptyOrNop: true);

        bool shouldSkip = core.TestShouldSkipDecodedSlotForForegroundIssue(tamperedSlot);

        Assert.False(shouldSkip);
    }

    [Fact]
    public void CandidateView_UsesLiveEmptyState_WhenCarrierEmptyFlagIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7200);

        var microOp = new LoadMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.Load,
            Address = 0x2C0,
            BaseRegID = 3,
            DestRegID = 7,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithEmptyState(canonicalSlot, isEmptyOrNop: true);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);
        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                transportFacts.PC,
                tamperedSlots,
                clusterPreparation,
                runtimePreparation);

        Assert.Equal((byte)1, candidateView.ValidNonEmptyMask);
    }

    [Fact]
    public void ClusterFallbackDiagnostics_UseLiveEmptyState_WhenCarrierEmptyFlagIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7250);

        var microOp = new LoadMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.Load,
            Address = 0x2E0,
            BaseRegID = 6,
            DestRegID = 9,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithEmptyState(canonicalSlot, isEmptyOrNop: true);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);

        Assert.Equal((byte)1, clusterPreparation.FallbackDiagnosticsMask);
    }

    [Fact]
    public void InjectableGapCount_UsesLiveEmptyState_WhenCarrierEmptyFlagIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7300);

        var microOp = new LoadMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.Load,
            Address = 0x300,
            BaseRegID = 4,
            DestRegID = 8,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithEmptyState(canonicalSlot, isEmptyOrNop: true);

        int gapCount = core.TestCountInjectableGaps(tamperedSlots);

        Assert.Equal(7, gapCount);
    }

    [Fact]
        public void MemoryClusteringTelemetry_UsesLiveMemoryFacts_WhenCarrierFactsAreTampered()
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
                () =>
            {
                var core = new Processor.CPU_Core(0);
                core.PrepareExecutionStart(0x7400);

                var load0 = new LoadMicroOp
                {
                    VirtualThreadId = 0,
                    OwnerThreadId = 0,
                    OpCode = (uint)InstructionsEnum.Load,
                    Address = 0x180,
                    BaseRegID = 1,
                    DestRegID = 6,
                    WritesRegister = true
                };
                load0.InitializeMetadata();

                var load1 = new LoadMicroOp
                {
                    VirtualThreadId = 0,
                    OwnerThreadId = 0,
                    OpCode = (uint)InstructionsEnum.Load,
                    Address = 0x1C0,
                    BaseRegID = 2,
                    DestRegID = 7,
                    WritesRegister = true
                };
                load1.InitializeMetadata();

                DecodedBundleSlotDescriptor slot0 = DecodedBundleSlotDescriptor.Create(0, load0);
                DecodedBundleSlotDescriptor slot1 = DecodedBundleSlotDescriptor.Create(1, load1);
                DecodedBundleSlotDescriptor tamperedSlot0 = WithIssueFacts(
                    slot0,
                    virtualThreadId: 1,
                    ownerThreadId: 1,
                    opCode: (uint)InstructionsEnum.ADDI,
                    readRegisters: slot0.ReadRegisters,
                    writeRegisters: slot0.WriteRegisters,
                    writesRegister: slot0.WritesRegister,
                    isMemoryOp: false,
                    isControlFlow: false,
                    memoryBankIntent: 5);
                DecodedBundleSlotDescriptor tamperedSlot1 = WithIssueFacts(
                    slot1,
                    virtualThreadId: 2,
                    ownerThreadId: 2,
                    opCode: (uint)InstructionsEnum.ANDI,
                    readRegisters: slot1.ReadRegisters,
                    writeRegisters: slot1.WriteRegisters,
                    writesRegister: slot1.WritesRegister,
                    isMemoryOp: false,
                    isControlFlow: false,
                    memoryBankIntent: 9);

                bool hasClusteringEvent = core.TestHasMemoryClusteringEvent(
                    new[] { tamperedSlot0, tamperedSlot1 });

                Assert.True(hasClusteringEvent);
            });
        }

    [Fact]
    public void MemoryClusteringTelemetry_IgnoresDescriptorOnlyFalsePositives_WhenLiveOpsAreNotMemory()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7500);

        var scalar0 = new WritableGenericMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.ADDI,
            DestRegID = 5,
            WritesRegister = true,
            PublishedWriteRegisters = new[] { 5 }
        };
        scalar0.RefreshAdmissionMetadata();

        var scalar1 = new WritableGenericMicroOp
        {
            VirtualThreadId = 0,
            OwnerThreadId = 0,
            OpCode = (uint)InstructionsEnum.ORI,
            DestRegID = 6,
            WritesRegister = true,
            PublishedWriteRegisters = new[] { 6 }
        };
        scalar1.RefreshAdmissionMetadata();

        DecodedBundleSlotDescriptor slot0 = DecodedBundleSlotDescriptor.Create(0, scalar0);
        DecodedBundleSlotDescriptor slot1 = DecodedBundleSlotDescriptor.Create(1, scalar1);
        DecodedBundleSlotDescriptor tamperedSlot0 = WithIssueFacts(
            slot0,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: slot0.OpCode,
            readRegisters: slot0.ReadRegisters,
            writeRegisters: slot0.WriteRegisters,
            writesRegister: slot0.WritesRegister,
            isMemoryOp: true,
            isControlFlow: false,
            memoryBankIntent: 0);
        DecodedBundleSlotDescriptor tamperedSlot1 = WithIssueFacts(
            slot1,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: slot1.OpCode,
            readRegisters: slot1.ReadRegisters,
            writeRegisters: slot1.WriteRegisters,
            writesRegister: slot1.WritesRegister,
            isMemoryOp: true,
            isControlFlow: false,
            memoryBankIntent: 0);

        bool hasClusteringEvent = core.TestHasMemoryClusteringEvent(
            new[] { tamperedSlot0, tamperedSlot1 });

        Assert.False(hasClusteringEvent);
    }

    private static DecodedBundleSlotDescriptor WithIssueFacts(
        in DecodedBundleSlotDescriptor slot,
        int virtualThreadId,
        int ownerThreadId,
        uint opCode,
        IReadOnlyList<int> readRegisters,
        IReadOnlyList<int> writeRegisters,
        bool writesRegister,
        bool isMemoryOp,
        bool isControlFlow,
        int memoryBankIntent)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            virtualThreadId,
            ownerThreadId,
            opCode,
            readRegisters,
            writeRegisters,
            writesRegister,
            isMemoryOp,
            isControlFlow,
            slot.Placement,
            memoryBankIntent,
            slot.IsFspInjected,
            slot.IsEmptyOrNop);
    }

    private static DecodedBundleSlotDescriptor WithEmptyState(
        in DecodedBundleSlotDescriptor slot,
        bool isEmptyOrNop)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            slot.VirtualThreadId,
            slot.OwnerThreadId,
            slot.OpCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
            slot.WritesRegister,
            slot.IsMemoryOp,
            slot.IsControlFlow,
            slot.Placement,
            slot.MemoryBankIntent,
            slot.IsFspInjected,
            isEmptyOrNop);
    }

    private sealed class WritableGenericMicroOp : GenericMicroOp
    {
        public IReadOnlyList<int> PublishedWriteRegisters
        {
            set => WriteRegisters = value ?? Array.Empty<int>();
        }
    }

}
