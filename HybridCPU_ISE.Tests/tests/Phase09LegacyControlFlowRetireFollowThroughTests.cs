using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class LegacyControlFlowRetireFollowThroughTests
    {
        [Fact]
        public void DecodedJalr_OnActiveVt0_CommitsThroughWriteBackWindow_UpdatesCommittedAndLivePcAndRetireCounters()
        {
            const ulong startPc = 0x1200;
            const ulong targetPc = 0x4400;
            const ulong baseValue = 0x43E1;
            const ulong linkValue = startPc + 4;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 2, baseValue);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.JALR,
                    rd: 5,
                    rs1: 2,
                    immediate: 0x001F));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);
            core.TestRunExecuteStageFromCurrentDecodeState();
            core.TestRunMemoryStageFromCurrentExecuteState();
            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadCommittedPc(0));
            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(linkValue, core.ReadArch(0, 5));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DecodedJalr_OnActiveNonZeroVt_CommitsPerVtWithoutAffectingOthers()
        {
            const int activeVtId = 2;
            const ulong activePc = 0x2200;
            const ulong targetPc = 0x6600;
            const ulong untouchedVt0Pc = 0x1000;
            const ulong baseValue = 0x65E1;
            const ulong linkValue = activePc + 4;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(activePc, activeVtId);
            core.WriteCommittedPc(0, untouchedVt0Pc);
            core.WriteCommittedPc(activeVtId, activePc);
            core.WriteCommittedArch(activeVtId, 2, baseValue);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.JALR,
                    rd: 5,
                    rs1: 2,
                    immediate: 0x001F));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, activePc);
            core.TestRunExecuteStageFromCurrentDecodeState();
            core.TestRunMemoryStageFromCurrentExecuteState();
            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadCommittedPc(activeVtId));
            Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));
            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(linkValue, core.ReadArch(activeVtId, 5));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DecodedBeq_OnActiveVt0_RetiresLane7RedirectThroughWriteBackWindow()
        {
            const ulong startPc = 0x1800;
            const ulong targetPc = 0x3A00;
            const ushort branchImmediate = 0x2200;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 1, 0x55UL);
            core.WriteCommittedArch(0, 2, 0x55UL);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rd: 0,
                    rs1: 1,
                    rs2: 2,
                    targetPc: targetPc,
                    immediate: branchImmediate));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);

            var decodeStatus = core.TestReadDecodeStageStatus();
            Assert.True(decodeStatus.Valid);
            Assert.Equal((uint)InstructionsEnum.BEQ, decodeStatus.OpCode);
            Assert.False(decodeStatus.IsMemoryOp);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStage = core.GetExecuteStage();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.UsesExplicitPacketLanes);
            Assert.True(executeStage.Lane7.IsOccupied);
            BranchMicroOp executeBranch = Assert.IsType<BranchMicroOp>(executeStage.Lane7.MicroOp);
            Assert.True(executeBranch.IsConditional);
            Assert.True(executeBranch.ConditionMet);
            Assert.True(executeStage.Lane7.ResultReady);
            Assert.Equal(targetPc, executeStage.Lane7.ResultValue);
            Assert.Equal(startPc + 256, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            core.TestRunMemoryStageFromCurrentExecuteState();
            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(targetPc, core.ReadCommittedPc(0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
            Assert.Equal(1UL, control.Lane7ConditionalBranchExecuteCompletionCount);
            Assert.Equal(1UL, control.Lane7ConditionalBranchRedirectCount);
        }

        [Fact]
        public void DecodedBeq_WithoutRawTargetPointer_RetiresCanonicalPcRelativeRedirectThroughWriteBack()
        {
            const ulong startPc = 0x2400;
            const ushort branchImmediate = 0x0040;
            const ulong targetPc = startPc + branchImmediate;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 3, 0x77UL);
            core.WriteCommittedArch(0, 4, 0x77UL);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rd: 0,
                    rs1: 3,
                    rs2: 4,
                    immediate: branchImmediate));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);
            core.TestRunExecuteStageFromCurrentDecodeState();

            Assert.Equal(startPc + 256, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            core.TestRunMemoryStageFromCurrentExecuteState();
            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(targetPc, core.ReadCommittedPc(0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
            Assert.Equal(1UL, control.Lane7ConditionalBranchExecuteCompletionCount);
            Assert.Equal(1UL, control.Lane7ConditionalBranchRedirectCount);
        }

        [Fact]
        public void DecodedBneNotTaken_RetiresLane7BranchWithoutPcWrite()
        {
            const ulong startPc = 0x2460;
            const ushort branchImmediate = 0x0020;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 3, 0x77UL);
            core.WriteCommittedArch(0, 4, 0x77UL);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.BNE,
                    rd: 0,
                    rs1: 3,
                    rs2: 4,
                    immediate: branchImmediate));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);
            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStage = core.GetExecuteStage();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.UsesExplicitPacketLanes);
            Assert.True(executeStage.Lane7.IsOccupied);
            BranchMicroOp executeBranch = Assert.IsType<BranchMicroOp>(executeStage.Lane7.MicroOp);
            Assert.False(executeBranch.ConditionMet);
            Assert.Equal(startPc + 256, executeStage.Lane7.ResultValue);
            Assert.Equal(startPc + 256, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            core.TestRunMemoryStageFromCurrentExecuteState();
            core.TestRunWriteBackStage();

            Assert.Equal(startPc + 256, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
            Assert.Equal(1UL, control.Lane7ConditionalBranchExecuteCompletionCount);
            Assert.Equal(0UL, control.Lane7ConditionalBranchRedirectCount);
        }

        [Fact]
        public void DecodedJalr_PreservesCanonicalBranchMicroOpAuthorityAtDecodeStage()
        {
            const ulong startPc = 0x2680;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.JALR,
                    rd: 5,
                    rs1: 2,
                    immediate: 0x0020));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);

            var decodeStatus = core.TestReadDecodeStageStatus();
            Assert.True(decodeStatus.Valid);
            Assert.Equal((uint)InstructionsEnum.JALR, decodeStatus.OpCode);
            Assert.False(decodeStatus.IsMemoryOp);

            BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(core.TestReadDecodeStageMicroOp());
            Assert.False(microOp.IsConditional);
            Assert.Equal((ushort)5, microOp.DestRegID);
            Assert.Equal((ushort)2, microOp.Reg1ID);

            RuntimeClusterAdmissionHandoff handoff = core.TestReadDecodeStageAdmissionHandoff();
            Assert.Equal(0, handoff.CandidateView.ScalarCandidateMask);
            Assert.Equal(0, handoff.CandidateView.PreparedScalarMask);
            Assert.Equal(0x01, handoff.CandidateView.AuxiliaryCandidateMask);
            Assert.Equal(0x01, handoff.CandidateView.AuxiliaryReservationMask);
            Assert.Equal(RuntimeClusterAdmissionDecisionKind.AdvisoryAuxiliaryOnly, handoff.DecisionDraft.DecisionKind);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, handoff.DecisionDraft.ExecutionMode);
            Assert.Equal(0, handoff.IssuePacket.ScalarIssueMask);
            Assert.Equal(0x01, handoff.IssuePacket.SelectedSlotMask);
            BranchMicroOp lane7Branch = Assert.IsType<BranchMicroOp>(handoff.IssuePacket.Lane7.MicroOp);
            Assert.False(lane7Branch.IsConditional);
            Assert.Equal((ushort)5, lane7Branch.DestRegID);
            Assert.Equal((ushort)2, lane7Branch.Reg1ID);
            Assert.Equal((byte)0, handoff.IssuePacket.Lane7.SlotIndex);
            Assert.False(handoff.IssuePacket.Lane7.CountsTowardScalarProjection);
        }

        [Fact]
        public void DecodedJalr_RetiresThroughWriteBackWindow_WithoutLegacyDirectRetireContour()
        {
            const ulong startPc = 0x2C80;
            const ulong baseValue = 0x4101;
            const ulong targetPc = 0x4120;
            const ulong linkValue = startPc + 4;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 2, baseValue);

            VLIW_Instruction[] rawSlots =
                CreateBundle(CreateControlInstruction(
                    InstructionsEnum.JALR,
                    rd: 5,
                    rs1: 2,
                    immediate: 0x0020));

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, startPc);

            BranchMicroOp decodeBranch = Assert.IsType<BranchMicroOp>(core.TestReadDecodeStageMicroOp());
            Assert.False(decodeBranch.IsConditional);

            RuntimeClusterAdmissionHandoff handoff = core.TestReadDecodeStageAdmissionHandoff();
            Assert.Equal(0, handoff.CandidateView.ScalarCandidateMask);
            Assert.Equal(0x01, handoff.CandidateView.AuxiliaryCandidateMask);
            Assert.Equal(0, handoff.IssuePacket.ScalarIssueMask);
            BranchMicroOp issuePacketBranch = Assert.IsType<BranchMicroOp>(handoff.IssuePacket.Lane7.MicroOp);
            Assert.False(issuePacketBranch.IsConditional);
            Assert.False(handoff.IssuePacket.Lane7.CountsTowardScalarProjection);
            Assert.Equal((byte)0, handoff.IssuePacket.Lane7.SlotIndex);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStage = core.GetExecuteStage();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.UsesExplicitPacketLanes);
            Assert.True(executeStage.Lane7.IsOccupied);
            BranchMicroOp executeBranch = Assert.IsType<BranchMicroOp>(executeStage.Lane7.MicroOp);
            Assert.False(executeBranch.IsConditional);
            Assert.True(executeStage.Lane7.ResultReady);
            Assert.Equal(linkValue, executeStage.Lane7.ResultValue);
            Assert.NotEqual(targetPc, core.ReadActiveLivePc());

            core.TestRunMemoryStageFromCurrentExecuteState();
            var memoryStage = core.GetMemoryStage();
            Assert.True(memoryStage.Valid);
            Assert.True(memoryStage.UsesExplicitPacketLanes);
            Assert.True(memoryStage.Lane7.IsOccupied);
            Assert.True(memoryStage.Lane7.ResultReady);
            Assert.Equal(linkValue, memoryStage.Lane7.ResultValue);

            core.TestRunWriteBackStage();

            Assert.Equal(targetPc, core.ReadCommittedPc(0));
            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(linkValue, core.ReadArch(0, 5));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void SeededSingleLaneBeqMicroOp_TakenBranch_RetiresThroughWriteBackOwnedRedirect()
        {
            const ulong startPc = 0x2F00;
            const ushort immediate = 0x0040;
            const ulong targetPc = startPc + immediate;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 1, 0x55UL);
            core.WriteCommittedArch(0, 2, 0x55UL);

            var branchMicroOp = new BranchMicroOp
            {
                OpCode = (uint)InstructionsEnum.BEQ,
                IsConditional = true,
                Reg1ID = 1,
                Reg2ID = 2,
                OwnerThreadId = 0,
                VirtualThreadId = 0
            };
            branchMicroOp.ApplyCanonicalRuntimeRelativeTargetProjection(unchecked((short)immediate));
            branchMicroOp.InitializeMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rs1: 1,
                    rs2: 2,
                    immediate: immediate),
                branchMicroOp,
                isBranchOp: true,
                reg2Id: 1,
                reg3Id: 2,
                pc: startPc);

            var executeStage = core.GetExecuteStage();
            Assert.True(executeStage.Valid);
            Assert.True(branchMicroOp.ConditionMet);
            Assert.True(executeStage.ResultReady);
            Assert.Equal(targetPc, executeStage.ResultValue);
            Assert.Equal(startPc, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(targetPc, core.ReadActiveLivePc());
            Assert.Equal(targetPc, core.ReadCommittedPc(0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void SeededSingleLaneBneMicroOp_NotTakenBranch_RetiresWithoutPcWrite()
        {
            const ulong startPc = 0x2F80;
            const ushort immediate = 0x0020;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 1, 0x55UL);
            core.WriteCommittedArch(0, 2, 0x55UL);

            var branchMicroOp = new BranchMicroOp
            {
                OpCode = (uint)InstructionsEnum.BNE,
                IsConditional = true,
                Reg1ID = 1,
                Reg2ID = 2,
                OwnerThreadId = 0,
                VirtualThreadId = 0
            };
            branchMicroOp.ApplyCanonicalRuntimeRelativeTargetProjection(unchecked((short)immediate));
            branchMicroOp.InitializeMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                CreateControlInstruction(
                    InstructionsEnum.BNE,
                    rs1: 1,
                    rs2: 2,
                    immediate: immediate),
                branchMicroOp,
                isBranchOp: true,
                reg2Id: 1,
                reg3Id: 2,
                pc: startPc);

            var executeStage = core.GetExecuteStage();
            Assert.True(executeStage.Valid);
            Assert.False(branchMicroOp.ConditionMet);
            Assert.True(executeStage.ResultReady);
            Assert.Equal(startPc + 256, executeStage.ResultValue);
            Assert.Equal(startPc, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(startPc, core.ReadActiveLivePc());
            Assert.Equal(startPc, core.ReadCommittedPc(0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void WriteBackRetireWindow_WhenOlderLane7ConditionalBranchSharesWindowWithYoungerScalarLane_RetiresBothThenRedirects()
        {
            const ulong startPc = 0x3400;
            const ulong targetPc = 0x3440;
            const ulong expectedScalarResult = 0x1234;

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.WriteCommittedPc(0, startPc);
            core.WriteCommittedArch(0, 9, 0xAAAA_BBBB_CCCC_DDDDUL);

            var scalarOp = new ScalarALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.Addition,
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                OwnerContextId = 0,
                DestRegID = 9,
                Src1RegID = 1,
                Src2RegID = 2,
                WritesRegister = true
            };
            scalarOp.InitializeMetadata();

            var branchOp = new BranchMicroOp
            {
                OpCode = (uint)InstructionsEnum.BEQ,
                IsConditional = true,
                Reg1ID = 3,
                Reg2ID = 4,
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                OwnerContextId = 0
            };
            branchOp.ApplyCanonicalRuntimeRelativeTargetProjection(0x40);
            branchOp.InitializeMetadata();
            branchOp.ConditionMet = true;
            branchOp.CaptureResolvedRetireTargetAddress(targetPc);

            ScalarWriteBackLaneState scalarLane = new();
            scalarLane.Clear(0);
            scalarLane.IsOccupied = true;
            scalarLane.PC = startPc;
            scalarLane.SlotIndex = 1;
            scalarLane.OpCode = (uint)InstructionsEnum.Addition;
            scalarLane.ResultValue = expectedScalarResult;
            scalarLane.WritesRegister = true;
            scalarLane.DestRegID = 9;
            scalarLane.MicroOp = scalarOp;
            scalarLane.OwnerThreadId = 0;
            scalarLane.VirtualThreadId = 0;
            scalarLane.OwnerContextId = 0;

            ScalarWriteBackLaneState branchLane = new();
            branchLane.Clear(7);
            branchLane.IsOccupied = true;
            branchLane.PC = startPc;
            branchLane.SlotIndex = 0;
            branchLane.OpCode = (uint)InstructionsEnum.BEQ;
            branchLane.ResultValue = targetPc;
            branchLane.WritesRegister = false;
            branchLane.MicroOp = branchOp;
            branchLane.OwnerThreadId = 0;
            branchLane.VirtualThreadId = 0;
            branchLane.OwnerContextId = 0;

            WriteBackStage writeBackStage = new();
            writeBackStage.Clear();
            writeBackStage.Valid = true;
            writeBackStage.ActiveLaneIndex = 7;
            writeBackStage.UsesExplicitPacketLanes = true;
            writeBackStage.MaterializedScalarLaneCount = 1;
            writeBackStage.MaterializedPhysicalLaneCount = 2;
            writeBackStage.SetLane(0, scalarLane);
            writeBackStage.SetLane(7, branchLane);

            core.TestSetWriteBackStage(writeBackStage);
            core.TestRunWriteBackStage();

            Assert.Equal(expectedScalarResult, core.ReadArch(0, 9));
            Assert.Equal(targetPc, core.ReadCommittedPc(0));
            Assert.Equal(targetPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(2UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void CompatDecoder_LegacyJumpIfAbove_ReportsCanonicalUnsignedBranchAtDecodeBoundary()
        {
            IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
            VLIW_Instruction instruction = CreateControlInstruction(
                InstructionsEnum.JumpIfAbove,
                rd: 9,
                rs1: 1,
                rs2: 2,
                targetPc: 0x4800);

            InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);

            Assert.Equal(InstructionsEnum.BLTU, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.ControlFlow, ir.Class);
            Assert.False(YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(ir));
            Assert.Equal(new[] { 2, 1 }, YAKSys_Hybrid_CPU.Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(ir));
            Assert.True(ir.HasAbsoluteAddressing);
            Assert.Equal(0x4800L, ir.Imm);
        }

        private static VLIW_Instruction[] CreateBundle(
            VLIW_Instruction slot0)
        {
            var rawSlots = new VLIW_Instruction[YAKSys_Hybrid_CPU.Core.BundleMetadata.BundleSlotCount];
            rawSlots[0] = slot0;
            return rawSlots;
        }

        private static VLIW_Instruction CreateControlInstruction(
            InstructionsEnum opcode,
            byte rd = 0,
            byte rs1 = 0,
            byte rs2 = 0,
            ulong targetPc = 0,
            ushort immediate = 0)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
                Src2Pointer = targetPc,
                Immediate = immediate,
                StreamLength = 0,
                Stride = 0,
                VirtualThreadId = 0
            };
        }
    }
}
