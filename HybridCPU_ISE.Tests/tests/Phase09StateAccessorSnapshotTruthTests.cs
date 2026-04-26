using System;
using HybridCPU_ISE;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09StateAccessorSnapshotTruthTests
{
    [Fact]
    public void GetVirtualThreadLivePcs_WhenActiveLivePcDiffersFromCommittedPc_ReturnsLiveOnlyForActiveVt()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x2000, activeVtId: 2);
        core.ActiveVirtualThreadId = 2;
        core.WriteVirtualThreadPipelineState(2, PipelineState.Task);
        core.WriteCommittedPc(0, 0x1000);
        core.WriteCommittedPc(1, 0x1100);
        core.WriteCommittedPc(2, 0x1200);
        core.WriteCommittedPc(3, 0x1300);
        core.WriteActiveLivePc(0x2200);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        (ulong[] livePcs, CoreStateSnapshot snapshot) =
            (service.GetVirtualThreadLivePcs(0), service.GetCoreState(0));

        Assert.Equal(new ulong[] { 0x1000, 0x1100, 0x2200, 0x1300 }, livePcs);
        Assert.Equal(livePcs, snapshot.VirtualThreadLivePcs);
        Assert.Equal(0x1200UL, snapshot.VirtualThreadCommittedPcs[2]);
    }

    [Fact]
    public void StallAccessors_WhenDecodeBankPendingStallsActiveVt_ProjectSameTruthAsCoreSnapshot()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = CreateCoreAfterDecodeBankPendingStall();
            IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

            (bool[] stalled, string[] stallReasons, CoreStateSnapshot snapshot) = (
                service.GetVirtualThreadStalled(0),
                service.GetVirtualThreadStallReasons(0),
                service.GetCoreState(0));

            Assert.Equal(stalled, snapshot.VirtualThreadStalled);
            Assert.Equal(stallReasons, snapshot.VirtualThreadStallReasons);
            Assert.True(stalled[0]);
            Assert.False(stalled[1]);
            Assert.Equal("Memory Wait", stallReasons[0]);
            Assert.Equal("None", stallReasons[1]);
        });
    }

    [Fact]
    public void GetVirtualThreadRegisters_WhenCommittedArchStateIsSeeded_MatchesActiveCoreSnapshot()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x3000, activeVtId: 1);
        core.ActiveVirtualThreadId = 1;
        core.WriteVirtualThreadPipelineState(1, PipelineState.Task);
        core.WriteCommittedArch(1, 5, 0x1234);
        core.WriteCommittedArch(1, 7, 0x5566);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        (ulong[] registers, CoreStateSnapshot snapshot) =
            (service.GetVirtualThreadRegisters(0, 1), service.GetCoreState(0));

        Assert.Equal(32, registers.Length);
        Assert.Equal(0UL, registers[0]);
        Assert.Equal(0x1234UL, registers[5]);
        Assert.Equal(0x5566UL, registers[7]);
        Assert.Equal(0UL, registers[6]);
        Assert.Equal(registers, snapshot.ActiveVirtualThreadRegisters);
    }

    [Fact]
    public void GetStackFlagsSnapshot_WhenPackedFlagStateIsSeeded_ProjectsSingleAuthoritativeTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.CoreFlagsRegister.Zero_Flag = true;
        core.CoreFlagsRegister.Sign_Flag = true;
        core.CoreFlagsRegister.OverFlow_Flag = true;
        core.CoreFlagsRegister.Parity_Flag = true;
        core.CoreFlagsRegister.EnableInterrupt_Flag = true;
        core.CoreFlagsRegister.IOPrivilege_Flag = true;
        core.CoreFlagsRegister.OSKernel_Flag = true;
        core.CoreFlagsRegister.Direction_Flag = true;
        core.CoreFlagsRegister.Jump_Flag = true;
        core.CoreFlagsRegister.JumpOutsideVLIW_Flag = true;
        core.CoreFlagsRegister.Offset_VLIWOpCode = 7;
        core.CoreFlagsRegister.VLIW_Begin_MemPtr = 0x4000;
        core.CoreFlagsRegister.VLIW_End_MemPtr = 0x4100;
        core.Call_Callback_Addresses.Add(0xAAAA);
        core.Interrupt_Callback_Addresses.Add(0xBBBB);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        StackFlagsSnapshot snapshot = service.GetStackFlagsSnapshot(0);

        Assert.Equal(1, snapshot.CallStackDepth);
        Assert.Equal(0xAAAAUL, snapshot.CallStackTop);
        Assert.Equal(1, snapshot.InterruptStackDepth);
        Assert.Equal(0xBBBBUL, snapshot.InterruptStackTop);
        Assert.True(snapshot.AluZero);
        Assert.True(snapshot.AluSign);
        Assert.True(snapshot.AluOverflow);
        Assert.True(snapshot.AluParity);
        Assert.True(snapshot.BaseEnableInterrupt);
        Assert.True(snapshot.BaseIoPrivilege);
        Assert.True(snapshot.BaseOsKernel);
        Assert.True(snapshot.CommonDirection);
        Assert.True(snapshot.CommonJump);
        Assert.True(snapshot.CommonJumpOutsideVliw);
        Assert.Equal((byte)7, snapshot.EilpOffsetVliwOpcode);
    }

    [Fact]
    public void GetCoreState_WhenPowerControlCsrsAreSeeded_ProjectsCanonicalPowerTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.Csr.Write(
            CsrAddresses.MpowerState,
            PowerControlCsr.EncodeState(Processor.CPU_Core.CorePowerState.C1_Halt),
            PrivilegeLevel.Machine);
        core.Csr.Write(
            CsrAddresses.MperfLevel,
            PowerControlCsr.EncodeState(Processor.CPU_Core.CorePowerState.P1_HighPerformance),
            PrivilegeLevel.Machine);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(Processor.CPU_Core.CorePowerState.C1_Halt, snapshot.CurrentPowerState);
        Assert.Equal(
            (uint)Processor.CPU_Core.CorePowerState.P1_HighPerformance,
            snapshot.CurrentPerformanceLevel);
    }

    private static Processor.CPU_Core CreateCoreAfterDecodeBankPendingStall()
    {
        return ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4E00, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.TestInitializeFSPScheduler();

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40));

            int pendingBankId = ResolveDecodedMemoryBankIntent(rawSlots, bundleAddress: 0x4E00);

            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
            scheduler.ClearSmtScoreboard();

            int slotIndex = scheduler.SetSmtScoreboardPendingTyped(
                targetId: pendingBankId,
                virtualThreadId: 0,
                currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad,
                bankId: pendingBankId);
            Assert.True(slotIndex >= 0);

            core.TestStageFetchedBundleForDecode(rawSlots, pc: 0x4E00);
            core.ExecutePipelineCycle();
            return core;
        });
    }

    private static int ResolveDecodedMemoryBankIntent(
        VLIW_Instruction[] rawSlots,
        ulong bundleAddress)
    {
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: bundleAddress, bundleSerial: 1);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]));

        return microOp.MemoryBankId;
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] occupiedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < occupiedSlots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = occupiedSlots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
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
            VirtualThreadId = virtualThreadId
        };
    }
}

