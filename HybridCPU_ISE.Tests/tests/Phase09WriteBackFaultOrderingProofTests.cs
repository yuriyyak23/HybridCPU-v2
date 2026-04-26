using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class WriteBackFaultOrderingProofTests
    {
        [Fact]
        public void WriteBackFaultWinner_WhenLane7FaultIsOlderBySlotOrder_ThenYoungerLane4StoreDoesNotCommit()
        {
            const ulong storeAddress = 0x180UL;
            const ulong storeData = 0x8877_6655_4433_2211UL;

            InitializeCpuMainMemoryIdentityMap(0x1000);

            var core = new Processor.CPU_Core(0);
            WriteBackStage writeBack = CreateWriteBackStageWithYoungerDeferredStore(
                storeAddress,
                storeData);

            ScalarWriteBackLaneState lane7 = new();
            lane7.Clear(7);
            lane7.IsOccupied = true;
            lane7.PC = 0x4000;
            lane7.SlotIndex = 0;
            lane7.OwnerThreadId = 0;
            lane7.VirtualThreadId = 0;
            lane7.OwnerContextId = 0;
            lane7.HasFault = true;
            lane7.FaultAddress = 0xDEAD_BEEFUL;
            lane7.FaultIsWrite = true;
            writeBack.Lane7 = lane7;

            core.TestSetWriteBackStage(writeBack);

            YAKSys_Hybrid_CPU.Core.PageFaultException ex =
                Assert.Throws<YAKSys_Hybrid_CPU.Core.PageFaultException>(
                    () => core.TestRunWriteBackStage());

            Assert.Equal(0xDEAD_BEEFUL, ex.FaultAddress);
            Assert.True(ex.IsWrite);
            Assert.Equal(0UL, BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], storeAddress, 8), 0));

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void WriteBackFaultWinner_WhenLane4FaultIsYoungerBySlotOrder_ThenOlderLane7CsrEffectRetiresBeforeTrap()
        {
            var core = new Processor.CPU_Core(0);
            WriteBackStage writeBack = CreateWriteBackStageWithOlderLane7CsrEffect();

            ScalarWriteBackLaneState lane4 = new();
            lane4.Clear(4);
            lane4.IsOccupied = true;
            lane4.PC = 0x4400;
            lane4.SlotIndex = 4;
            lane4.OwnerThreadId = 0;
            lane4.VirtualThreadId = 0;
            lane4.OwnerContextId = 0;
            lane4.HasFault = true;
            lane4.FaultAddress = 0xBAAD_F00DUL;
            lane4.FaultIsWrite = false;
            writeBack.Lane4 = lane4;

            core.TestSetWriteBackStage(writeBack);

            YAKSys_Hybrid_CPU.Core.PageFaultException ex =
                Assert.Throws<YAKSys_Hybrid_CPU.Core.PageFaultException>(
                    () => core.TestRunWriteBackStage());

            Assert.Equal(0xBAAD_F00DUL, ex.FaultAddress);
            Assert.False(ex.IsWrite);
            Assert.Equal((byte)1, core.VectorConfig.FSP_Enabled);
            Assert.Equal(0UL, core.ReadArch(0, 9));

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void WriteBackFaultWinner_WhenOlderLane7ConditionalBranchRetiresRedirect_ThenYoungerLane4FaultIsSuppressed()
        {
            const ulong startPc = 0x4800;
            const ulong targetPc = 0x4840;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 2;
            writeBack.Lane7 = CreateTakenConditionalBranchLane(
                pc: startPc,
                targetPc: targetPc,
                slotIndex: 0);

            ScalarWriteBackLaneState youngerFaultLane = new();
            youngerFaultLane.Clear(4);
            youngerFaultLane.IsOccupied = true;
            youngerFaultLane.PC = startPc;
            youngerFaultLane.SlotIndex = 4;
            youngerFaultLane.OwnerThreadId = 0;
            youngerFaultLane.VirtualThreadId = 0;
            youngerFaultLane.OwnerContextId = 0;
            youngerFaultLane.HasFault = true;
            youngerFaultLane.FaultAddress = 0xBAAD_F00DUL;
            youngerFaultLane.FaultIsWrite = true;
            writeBack.Lane4 = youngerFaultLane;

            core.TestSetWriteBackStage(writeBack);
            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadCommittedPc(0));
            Assert.Equal(targetPc, core.ReadActiveLivePc());

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
            Assert.Equal(1UL, control.ExceptionYoungerSuppressCount);
        }

        [Fact]
        public void WriteBackFaultWinner_WhenOlderLane4FaultPrecedesYoungerLane7ConditionalBranch_ThenBranchDoesNotRedirect()
        {
            const ulong startPc = 0x4A00;
            const ulong targetPc = 0x4A40;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 2;

            ScalarWriteBackLaneState olderFaultLane = new();
            olderFaultLane.Clear(4);
            olderFaultLane.IsOccupied = true;
            olderFaultLane.PC = startPc;
            olderFaultLane.SlotIndex = 0;
            olderFaultLane.OwnerThreadId = 0;
            olderFaultLane.VirtualThreadId = 0;
            olderFaultLane.OwnerContextId = 0;
            olderFaultLane.HasFault = true;
            olderFaultLane.FaultAddress = 0xDEAD_BEEFUL;
            olderFaultLane.FaultIsWrite = false;
            writeBack.Lane4 = olderFaultLane;
            writeBack.Lane7 = CreateTakenConditionalBranchLane(
                pc: startPc,
                targetPc: targetPc,
                slotIndex: 7);

            core.TestSetWriteBackStage(writeBack);

            YAKSys_Hybrid_CPU.Core.PageFaultException ex =
                Assert.Throws<YAKSys_Hybrid_CPU.Core.PageFaultException>(
                    () => core.TestRunWriteBackStage());

            Assert.Equal(0xDEAD_BEEFUL, ex.FaultAddress);
            Assert.False(ex.IsWrite);
            Assert.Equal(startPc, core.ReadCommittedPc(0));
            Assert.Equal(startPc, core.ReadActiveLivePc());

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        private static WriteBackStage CreateWriteBackStageWithYoungerDeferredStore(
            ulong storeAddress,
            ulong storeData)
        {
            var storeOp = new StoreMicroOp
            {
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                OwnerContextId = 0,
                Address = storeAddress,
                Value = storeData,
                Size = 8,
                SrcRegID = 1
            };
            storeOp.InitializeMetadata();

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 2;

            ScalarWriteBackLaneState lane4 = new();
            lane4.Clear(4);
            lane4.IsOccupied = true;
            lane4.PC = 0x4100;
            lane4.SlotIndex = 4;
            lane4.OpCode = (uint)InstructionsEnum.Store;
            lane4.ResultValue = storeData;
            lane4.IsMemoryOp = true;
            lane4.MemoryAddress = storeAddress;
            lane4.MemoryData = storeData;
            lane4.IsLoad = false;
            lane4.MemoryAccessSize = 8;
            lane4.MicroOp = storeOp;
            lane4.OwnerThreadId = 0;
            lane4.VirtualThreadId = 0;
            lane4.OwnerContextId = 0;
            lane4.DefersStoreCommitToWriteBack = true;
            writeBack.Lane4 = lane4;

            return writeBack;
        }

        private static WriteBackStage CreateWriteBackStageWithOlderLane7CsrEffect()
        {
            var csrOp = new CsrReadWriteMicroOp
            {
                OpCode = (uint)InstructionsEnum.CSRRW,
                CSRAddress = (ulong)VectorCSR.VLIW_STEAL_ENABLE,
                SrcRegID = 1,
                DestRegID = 9,
                WritesRegister = true,
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                OwnerContextId = 0
            };
            csrOp.InitializeMetadata();

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 2;

            ScalarWriteBackLaneState lane7 = new();
            lane7.Clear(7);
            lane7.IsOccupied = true;
            lane7.PC = 0x4200;
            lane7.SlotIndex = 0;
            lane7.OpCode = (uint)InstructionsEnum.CSRRW;
            lane7.ResultValue = 0;
            lane7.WritesRegister = true;
            lane7.DestRegID = 9;
            lane7.MicroOp = csrOp;
            lane7.OwnerThreadId = 0;
            lane7.VirtualThreadId = 0;
            lane7.OwnerContextId = 0;
            lane7.GeneratedCsrEffect = CsrRetireEffect.Create(
                CsrStorageSurface.VectorPodPlane,
                (ushort)VectorCSR.VLIW_STEAL_ENABLE,
                readValue: 0,
                hasRegisterWriteback: true,
                destRegId: 9,
                hasCsrWrite: true,
                csrWriteValue: 1UL);
            writeBack.Lane7 = lane7;

            return writeBack;
        }

        private static ScalarWriteBackLaneState CreateTakenConditionalBranchLane(
            ulong pc,
            ulong targetPc,
            byte slotIndex)
        {
            var branchOp = new BranchMicroOp
            {
                OpCode = (uint)InstructionsEnum.BEQ,
                IsConditional = true,
                Reg1ID = 1,
                Reg2ID = 2,
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                OwnerContextId = 0
            };
            branchOp.InitializeMetadata();
            branchOp.ConditionMet = true;
            branchOp.CaptureResolvedRetireTargetAddress(targetPc);

            ScalarWriteBackLaneState lane = new();
            lane.Clear(7);
            lane.IsOccupied = true;
            lane.PC = pc;
            lane.SlotIndex = slotIndex;
            lane.OpCode = (uint)InstructionsEnum.BEQ;
            lane.ResultValue = targetPc;
            lane.WritesRegister = false;
            lane.MicroOp = branchOp;
            lane.OwnerThreadId = 0;
            lane.VirtualThreadId = 0;
            lane.OwnerContextId = 0;
            return lane;
        }

        private static void InitializeCpuMainMemoryIdentityMap(ulong size)
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);

            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: size,
                permissions: IOMMUAccessPermissions.ReadWrite);
        }
    }
}
