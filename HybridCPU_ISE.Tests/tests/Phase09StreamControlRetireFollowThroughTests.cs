using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class StreamControlRetireFollowThroughTests
    {
        private static void SeedSchedulerClassTemplate(MicroOpScheduler scheduler)
        {
            var capacityState = new SlotClassCapacityState();
            capacityState.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);
        }

        [Fact]
        public void StreamWaitDecodeSurfaceContract_RemainsRoutableToMainlineRetireBoundary()
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x3600);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_WAIT));

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x3600, bundleSerial: 83);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(slot.MicroOp);

            Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
            Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.True(core.CanRouteDecodedSlotToExecutionSurfaceForTesting(slot));
        }

        [Fact]
        public void StreamWaitRetireFollowThrough_Lane7_RetiresThroughWriteBack()
        {
            var core = new Processor.CPU_Core(0);
            var op = new StreamControlMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT
            };
            op.InitializeMetadata();

            core.TestRetireExplicitLane7SingletonMicroOp(op, pc: 0x3400, vtId: 2);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
            Assert.Equal(1UL, control.RetireCycleCount);
        }

        [Fact]
        public void StreamWaitRetireFollowThrough_Lane7_PublishesSerializingReplayBoundary()
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x1000);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x1000,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var op = new StreamControlMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT
            };
            op.InitializeMetadata();

            core.TestRetireExplicitLane7SingletonMicroOp(op, pc: 0x3500, vtId: 1);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
        }

        [Fact]
        public void StreamSetupDecodeSurfaceContract_RejectsUnsupportedBeforeExecution_AndPublishesTrapInvalidation()
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x3700);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x3700,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_SETUP));

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x3700, bundleSerial: 84);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(slot.MicroOp);

            Assert.Equal(InstructionClass.System, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
            Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
            Assert.False(core.CanRouteDecodedSlotToExecutionSurfaceForTesting(slot));

            UnsupportedExecutionSurfaceException ex =
                Assert.Throws<UnsupportedExecutionSurfaceException>(
                    () => core.EnforceDecodedSlotExecutionSurfaceContractForTesting(slot, bundlePc: 0x3700));

            Assert.Contains("StreamControl", ex.Message, StringComparison.Ordinal);
            Assert.Contains($"0x{(uint)InstructionsEnum.STREAM_SETUP:X}", ex.Message, StringComparison.Ordinal);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void IssuePacketSurfaceContract_RejectsUnsupportedStreamStartLane7InsteadOfNoOpSuccess()
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x3800);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x3800,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, rs2: 3),
                    CreateScalarInstruction(InstructionsEnum.STREAM_START));

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x3800, bundleSerial: 85);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor scalarSlot = transportFacts.Slots[0];
            DecodedBundleSlotDescriptor streamSlot = transportFacts.Slots[1];

            ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
                transportFacts.PC,
                transportFacts.Slots,
                transportFacts.AdmissionPrep,
                transportFacts.DependencySummary);
            RuntimeClusterAdmissionPreparation runtimePreparation =
                RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
            RuntimeClusterAdmissionCandidateView candidateView =
                RuntimeClusterAdmissionCandidateView.Create(
                    transportFacts.PC,
                    transportFacts.Slots,
                    clusterPreparation,
                    runtimePreparation);
            RuntimeClusterAdmissionDecisionDraft decisionDraft =
                RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                    candidateView,
                    clusterPreparedModeEnabled: true)
                .BindToCurrentSlot(0);
            RuntimeClusterAdmissionHandoff handoff = RuntimeClusterAdmissionHandoff.Create(
                transportFacts.PC,
                transportFacts.Slots,
                clusterPreparation,
                candidateView,
                decisionDraft);

            Assert.True(core.CanRouteDecodedSlotToExecutionSurfaceForTesting(scalarSlot));
            Assert.IsType<StreamControlMicroOp>(streamSlot.MicroOp);
            Assert.False(core.CanRouteDecodedSlotToExecutionSurfaceForTesting(streamSlot));
            Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
            Assert.Equal((byte)0b0000_0011, handoff.IssuePacket.SelectedSlotMask);
            Assert.Equal((byte)0b0000_0010, handoff.IssuePacket.SelectedNonScalarSlotMask);
            Assert.True(handoff.IssuePacket.Lane0.IsOccupied);

            IssuePacketLane streamLane = handoff.IssuePacket.Lane7;
            Assert.True(streamLane.IsOccupied);
            Assert.Equal((byte)1, streamLane.SlotIndex);
            Assert.IsType<StreamControlMicroOp>(streamLane.MicroOp);

            UnsupportedExecutionSurfaceException ex =
                Assert.Throws<UnsupportedExecutionSurfaceException>(
                    () => core.EnforceIssueLaneExecutionSurfaceContractForTesting(streamLane, handoff.PC));

            Assert.Contains("StreamControl", ex.Message, StringComparison.Ordinal);
            Assert.Contains($"0x{(uint)InstructionsEnum.STREAM_START:X}", ex.Message, StringComparison.Ordinal);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
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
            ushort immediate = 0)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
                Src2Pointer = immediate,
                StreamLength = 0,
                Stride = 0,
                VirtualThreadId = 0
            };
        }
    }
}

