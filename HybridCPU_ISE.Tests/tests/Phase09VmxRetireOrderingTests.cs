using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class AtomicExecutionSurfaceContractTests
    {
        [Fact]
        public void DecodeSurfaceContract_AllowsAtomicThroughAuthoritativeMainlineExecutionSurface()
        {
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x6400);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x6400,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x6400, bundleSerial: 81);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
            AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(slot.MicroOp);

            Assert.Equal(InstructionClass.Atomic, microOp.InstructionClass);
            Assert.Equal(SerializationClass.AtomicSerial, microOp.SerializationClass);
            Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.True(core.CanRouteDecodedSlotToExecutionSurfaceForTesting(slot));
            core.EnforceDecodedSlotExecutionSurfaceContractForTesting(slot, bundlePc: 0x6400);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = core.TestGetFSPScheduler()!.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.True(schedulerPhase.IsActive);

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        private static VLIW_Instruction[] CreateBundle(
            VLIW_Instruction slot0)
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[0] = slot0;
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

    public sealed class VmxExecutionSurfaceContractTests
    {
        [Fact]
        public void DecodeSurfaceContract_RejectsUnwiredVmxBeforeExecution()
        {
            var wiredCore = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            var unwiredCore = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            unwiredCore.SetVmxExecutionPlaneWiredForTesting(false);

            var vmxMicroOp = new VmxMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMXON,
                Instruction = VmxIrHelper.MakeVmx(InstructionsEnum.VMXON),
            };
            vmxMicroOp.RefreshWriteMetadata();

            var slot = DecodedBundleSlotDescriptor.Create(0, vmxMicroOp);

            Assert.True(wiredCore.CanRouteDecodedSlotToExecutionSurfaceForTesting(slot));
            Assert.False(unwiredCore.CanRouteDecodedSlotToExecutionSurfaceForTesting(slot));
            Assert.Throws<InvalidOperationException>(() => _ = unwiredCore.VmxUnit);
        }

        [Fact]
        public void DecodeSurfaceContract_ErrorTextUsesPublishedSlotOpcode_WhenCarrierOpcodeIsTampered()
        {
            var unwiredCore = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            unwiredCore.SetVmxExecutionPlaneWiredForTesting(false);

            var vmxMicroOp = new VmxMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMXON,
                Instruction = VmxIrHelper.MakeVmx(InstructionsEnum.VMXON),
            };
            vmxMicroOp.RefreshWriteMetadata();

            DecodedBundleSlotDescriptor slot = WithRuntimeHeader(
                DecodedBundleSlotDescriptor.Create(0, vmxMicroOp),
                virtualThreadId: 0,
                ownerThreadId: 0,
                opCode: (uint)InstructionsEnum.ORI);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => unwiredCore.EnforceDecodedSlotExecutionSurfaceContractForTesting(slot, bundlePc: 0x4400));

            Assert.Contains($"0x{(uint)InstructionsEnum.VMXON:X}", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain($"0x{(uint)InstructionsEnum.ORI:X}", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void IssuePacketSurfaceContract_RejectsUnwiredVmxAuxiliaryLaneInsteadOfDroppingIt()
        {
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4600);
            core.SetVmxExecutionPlaneWiredForTesting(false);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x4600,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, rs2: 3),
                    CreateScalarInstruction(InstructionsEnum.VMXON));

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x4600, bundleSerial: 82);

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleSlotDescriptor scalarSlot = transportFacts.Slots[0];
            DecodedBundleSlotDescriptor vmxSlot = transportFacts.Slots[1];

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
            Assert.IsType<VmxMicroOp>(vmxSlot.MicroOp);
            Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
            Assert.Equal((byte)0b0000_0011, handoff.IssuePacket.SelectedSlotMask);
            Assert.Equal((byte)0b0000_0010, handoff.IssuePacket.SelectedNonScalarSlotMask);
            Assert.True(handoff.IssuePacket.Lane0.IsOccupied);

            IssuePacketLane vmxLane = handoff.IssuePacket.Lane7;
            Assert.True(vmxLane.IsOccupied);
            Assert.Equal((byte)1, vmxLane.SlotIndex);
            Assert.IsType<VmxMicroOp>(vmxLane.MicroOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.EnforceIssueLaneExecutionSurfaceContractForTesting(vmxLane, handoff.PC));

            Assert.Contains("issue-packet execute materialization", ex.Message, StringComparison.Ordinal);
            Assert.Contains($"0x{(uint)InstructionsEnum.VMXON:X}", ex.Message, StringComparison.Ordinal);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = core.TestGetFSPScheduler()!.TestGetReplayPhaseContext();
            Assert.True(replayPhase.IsActive);
            Assert.True(schedulerPhase.IsActive);

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void IssuePacketSurfaceContract_ErrorTextUsesPublishedLaneOpcode_WhenLaneOpcodeIsTampered()
        {
            var unwiredCore = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            unwiredCore.SetVmxExecutionPlaneWiredForTesting(false);

            var vmxMicroOp = new VmxMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMXON,
                Instruction = VmxIrHelper.MakeVmx(InstructionsEnum.VMXON),
            };
            vmxMicroOp.RefreshWriteMetadata();

            IssuePacketLane tamperedLane = new(
                physicalLaneIndex: 7,
                isOccupied: true,
                slotIndex: 1,
                virtualThreadId: 0,
                ownerThreadId: 0,
                opCode: (uint)InstructionsEnum.ORI,
                microOp: vmxMicroOp,
                requiredSlotClass: SlotClass.SystemSingleton,
                pinningKind: SlotPinningKind.HardPinned,
                countsTowardScalarProjection: false);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => unwiredCore.EnforceIssueLaneExecutionSurfaceContractForTesting(tamperedLane, bundlePc: 0x4700));

            Assert.Contains($"0x{(uint)InstructionsEnum.ORI:X}", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain($"0x{(uint)InstructionsEnum.VMXON:X}", ex.Message, StringComparison.Ordinal);
        }

        private static DecodedBundleSlotDescriptor WithRuntimeHeader(
            in DecodedBundleSlotDescriptor slot,
            int virtualThreadId,
            int ownerThreadId,
            uint opCode)
        {
            return new DecodedBundleSlotDescriptor(
                slot.MicroOp,
                slot.SlotIndex,
                virtualThreadId,
                ownerThreadId,
                opCode,
                slot.ReadRegisters,
                slot.WriteRegisters,
                slot.WritesRegister,
                slot.IsMemoryOp,
                slot.IsControlFlow,
                slot.Placement,
                slot.MemoryBankIntent,
                slot.IsFspInjected,
                slot.IsEmptyOrNop);
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

    public sealed class VmxRetireOrderingRegressionTests
    {
        [Fact]
        public void RetireWindowFaultDecision_WinsBeforeDeferredVmExitEffect()
        {
            WriteBackStage writeBack = CreateVmExitCarrierWriteBackStage(virtualThreadId: 0);
            MemoryStage memory = CreateFaultedMemoryStage(laneIndex: 4, faultAddress: 0xDEAD_BEEFUL);
            ExecuteStage execute = new();
            execute.Clear();

            Assert.Equal((byte)(1 << 7), YAKSys_Hybrid_CPU.Processor.CPU_Core.ResolveRetireEligibleWriteBackLanesExcludingFaulted(writeBack));

            Assert.True(
                YAKSys_Hybrid_CPU.Processor.CPU_Core.TryResolveExceptionDeliveryDecisionForRetireWindow(
                    writeBack,
                    memory,
                    execute,
                    out PipelineStage winnerStage,
                    out byte winnerLaneIndex,
                    out bool shouldSuppressYoungerWork));

            Assert.Equal(PipelineStage.Memory, winnerStage);
            Assert.Equal((byte)4, winnerLaneIndex);
            Assert.False(shouldSuppressYoungerWork);
        }

        [Fact]
        public void VmExitRetireEffect_OnActiveVt_RedirectsLivePcAndFlushBoundary()
        {
            var core = CreateVmExitReadyCore(
                activeVtId: 0,
                guestVtId: 0,
                activePc: 0x1200,
                guestPc: 0x1200,
                guestSp: 0x3300,
                hostPc: 0x5000,
                hostSp: 0x5100);

            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(
                VmxRetireEffect.Control(
                    VmxOperationKind.VmxOff,
                    exitGuestContextOnRetire: true),
                virtualThreadId: 0);

            Assert.True(outcome.RedirectsControlFlow);
            Assert.True(outcome.FlushesPipeline);
            Assert.Equal(0x5000UL, outcome.RedirectTargetPc);
            Assert.Equal(0x5000UL, core.ReadCommittedPc(0));
            Assert.Equal(0x5000UL, core.ReadActiveLivePc());
            Assert.Equal(0x5100UL, core.ReadArch(0, 2));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal((ulong)VmExitReason.VmxOff, core.Csr.DirectRead(CsrAddresses.VmxExitReason));
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmExitCnt));
            Assert.Equal(0x1200L, core.Vmcs.ReadFieldValue(VmcsField.GuestPc).Value);
            Assert.Equal(0x3300L, core.Vmcs.ReadFieldValue(VmcsField.GuestSp).Value);
        }

        [Fact]
        public void VmExitRetireEffect_OnNonZeroInactiveVt_LeavesActiveFrontendUntouched()
        {
            var core = CreateVmExitReadyCore(
                activeVtId: 0,
                guestVtId: 2,
                activePc: 0x1000,
                guestPc: 0x2200,
                guestSp: 0x3300,
                hostPc: 0x6600,
                hostSp: 0x7700);
            ulong activeLivePcBefore = core.ReadActiveLivePc();

            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(
                VmxRetireEffect.Control(
                    VmxOperationKind.VmxOff,
                    exitGuestContextOnRetire: true),
                virtualThreadId: 2);

            Assert.True(outcome.RedirectsControlFlow);
            Assert.True(outcome.FlushesPipeline);
            Assert.Equal(0x6600UL, outcome.RedirectTargetPc);
            Assert.Equal(0x6600UL, core.ReadCommittedPc(2));
            Assert.Equal(0x7700UL, core.ReadArch(2, 2));
            Assert.Equal(activeLivePcBefore, core.ReadActiveLivePc());
            Assert.Equal(0x1000UL, core.ReadCommittedPc(0));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(2));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(0x2200L, core.Vmcs.ReadFieldValue(VmcsField.GuestPc).Value);
            Assert.Equal(0x3300L, core.Vmcs.ReadFieldValue(VmcsField.GuestSp).Value);
        }

        private static YAKSys_Hybrid_CPU.Processor.CPU_Core CreateVmExitReadyCore(
            int activeVtId,
            int guestVtId,
            ulong activePc,
            ulong guestPc,
            ulong guestSp,
            ulong hostPc,
            ulong hostSp)
        {
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(activePc, activeVtId);
            core.WriteCommittedPc(guestVtId, guestPc);
            core.WriteCommittedArch(guestVtId, 2, guestSp);
            core.WriteVirtualThreadPipelineState(guestVtId, PipelineState.GuestExecution);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1000 + (ulong)guestVtId);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)hostPc));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)hostSp));
            return core;
        }

        private static WriteBackStage CreateVmExitCarrierWriteBackStage(int virtualThreadId)
        {
            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
        writeBack.RetainsReferenceSequentialPath = false;
            writeBack.MaterializedPhysicalLaneCount = 1;

            ScalarWriteBackLaneState lane7 = new();
            lane7.Clear(7);
            lane7.IsOccupied = true;
            lane7.OpCode = (uint)InstructionsEnum.VMXOFF;
            lane7.OwnerThreadId = virtualThreadId;
            lane7.VirtualThreadId = virtualThreadId;
            lane7.GeneratedVmxEffect = VmxRetireEffect.Control(
                VmxOperationKind.VmxOff,
                exitGuestContextOnRetire: true);
            writeBack.Lane7 = lane7;

            return writeBack;
        }

        private static MemoryStage CreateFaultedMemoryStage(byte laneIndex, ulong faultAddress)
        {
            MemoryStage memory = new();
            memory.Clear();
            memory.Valid = true;
            memory.ActiveLaneIndex = laneIndex;
            memory.MaterializedPhysicalLaneCount = 1;

            ScalarMemoryLaneState lane = new();
            lane.Clear(laneIndex);
            lane.IsOccupied = true;
            lane.HasFault = true;
            lane.FaultAddress = faultAddress;
            memory.SetLane(laneIndex, lane);

            return memory;
        }
    }
}

