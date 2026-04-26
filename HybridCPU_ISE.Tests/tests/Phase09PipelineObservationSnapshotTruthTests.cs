using CpuInterfaceBridge;
using HybridCPU_ISE;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using BridgeDecodedBundleStateOwnerKind = CpuInterfaceBridge.DecodedBundleStateOwnerKind;
using BridgeDecodedBundleStateKind = CpuInterfaceBridge.DecodedBundleStateKind;
using BridgeDecodedBundleStateOrigin = CpuInterfaceBridge.DecodedBundleStateOrigin;
using RuntimeDecodedBundleStateOwnerKind = YAKSys_Hybrid_CPU.Core.DecodedBundleStateOwnerKind;
using RuntimeDecodedBundleStateKind = YAKSys_Hybrid_CPU.Core.DecodedBundleStateKind;
using RuntimeDecodedBundleStateOrigin = YAKSys_Hybrid_CPU.Core.DecodedBundleStateOrigin;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09PipelineObservationSnapshotTruthTests
{
    [Fact]
    public void ObservationServiceSnapshot_WhenCanonicalDecodeIsPublished_ReportsTypedObservationTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6100);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x6100, bundleSerial: 91);
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(RuntimeDecodedBundleStateOwnerKind.BaseRuntimePublication, snapshot.DecodedBundleStateOwnerKind);
        Assert.Equal(runtimeState.StateEpoch, snapshot.DecodedBundleStateEpoch);
        Assert.Equal(runtimeState.StateVersion, snapshot.DecodedBundleStateVersion);
        Assert.Equal(RuntimeDecodedBundleStateKind.Canonical, snapshot.DecodedBundleStateKind);
        Assert.Equal(RuntimeDecodedBundleStateOrigin.CanonicalDecode, snapshot.DecodedBundleStateOrigin);
        Assert.Equal(0x6100UL, snapshot.DecodedBundlePc);
        Assert.Equal(transportFacts.ValidMask, snapshot.DecodedBundleValidMask);
        Assert.Equal(transportFacts.NopMask, snapshot.DecodedBundleNopMask);
        Assert.Equal(!core.GetCurrentDecodedInstructionBundle().IsEmpty, snapshot.DecodedBundleHasCanonicalDecode);
        Assert.Equal(!core.GetCurrentBundleLegalityDescriptor().IsEmpty, snapshot.DecodedBundleHasCanonicalLegality);
        Assert.Equal(core.GetCurrentDecodedInstructionBundle().HasDecodeFault, snapshot.DecodedBundleHasDecodeFault);
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenFspDerivedIssuePlanIsActive_ReportsForegroundDerivedObservationTruth()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x61C0);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
            var annotations = new VliwBundleAnnotations(
                new InstructionSlotMetadata[0],
                new BundleMetadata { FspBoundary = false });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x61C0, bundleSerial: 193);
            scheduler.Nominate(1, candidate);

            DecodedBundleTransportFacts baseTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleRuntimeState baseRuntimeState = core.TestReadCurrentDecodedBundleRuntimeState();

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts foregroundTransportFacts =
                core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            DecodedBundleRuntimeState foregroundRuntimeState =
                core.TestReadCurrentForegroundDecodedBundleRuntimeState();
            IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);
            CoreStateSnapshot snapshot = service.GetCoreState(0);

            Assert.Equal(RuntimeDecodedBundleStateKind.Canonical, baseTransportFacts.StateKind);
            Assert.Equal(RuntimeDecodedBundleStateOrigin.CanonicalDecode, baseTransportFacts.StateOrigin);
            Assert.Equal(RuntimeDecodedBundleStateOwnerKind.DerivedIssuePlanPublication, snapshot.DecodedBundleStateOwnerKind);
            Assert.Equal(baseRuntimeState.StateEpoch, snapshot.DecodedBundleStateEpoch);
            Assert.Equal(foregroundRuntimeState.StateVersion, snapshot.DecodedBundleStateVersion);
            Assert.True(snapshot.DecodedBundleStateVersion > baseRuntimeState.StateVersion);
            Assert.Equal(RuntimeDecodedBundleStateKind.ForegroundMutated, snapshot.DecodedBundleStateKind);
            Assert.Equal(RuntimeDecodedBundleStateOrigin.FspPacking, snapshot.DecodedBundleStateOrigin);
            Assert.Equal(0x61C0UL, snapshot.DecodedBundlePc);
            Assert.Equal(foregroundTransportFacts.ValidMask, snapshot.DecodedBundleValidMask);
            Assert.Equal(foregroundTransportFacts.NopMask, snapshot.DecodedBundleNopMask);
            Assert.True(snapshot.DecodedBundleHasCanonicalDecode);
            Assert.True(snapshot.DecodedBundleHasCanonicalLegality);
            Assert.False(snapshot.DecodedBundleHasDecodeFault);
            Assert.Same(candidate, foregroundTransportFacts.Slots[0].MicroOp);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenReplayBundleIsLoaded_ReportsReplayObservationTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6200);

        core.TestLoadReplayDecodedBundleSlotCarrier(
            0x6200,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6));
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(RuntimeDecodedBundleStateOwnerKind.BaseRuntimePublication, snapshot.DecodedBundleStateOwnerKind);
        Assert.Equal(runtimeState.StateEpoch, snapshot.DecodedBundleStateEpoch);
        Assert.Equal(runtimeState.StateVersion, snapshot.DecodedBundleStateVersion);
        Assert.Equal(RuntimeDecodedBundleStateKind.Replay, snapshot.DecodedBundleStateKind);
        Assert.Equal(RuntimeDecodedBundleStateOrigin.ReplayBundleLoad, snapshot.DecodedBundleStateOrigin);
        Assert.Equal(0x6200UL, snapshot.DecodedBundlePc);
        Assert.Equal(transportFacts.ValidMask, snapshot.DecodedBundleValidMask);
        Assert.Equal(transportFacts.NopMask, snapshot.DecodedBundleNopMask);
        Assert.Equal(!core.GetCurrentDecodedInstructionBundle().IsEmpty, snapshot.DecodedBundleHasCanonicalDecode);
        Assert.Equal(!core.GetCurrentBundleLegalityDescriptor().IsEmpty, snapshot.DecodedBundleHasCanonicalLegality);
        Assert.Equal(core.GetCurrentDecodedInstructionBundle().HasDecodeFault, snapshot.DecodedBundleHasDecodeFault);
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenDecodeFallbackTrapIsPublished_ReportsDecodeFaultObservationTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6280);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x6280);
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(RuntimeDecodedBundleStateOwnerKind.BaseRuntimePublication, snapshot.DecodedBundleStateOwnerKind);
        Assert.Equal(runtimeState.StateEpoch, snapshot.DecodedBundleStateEpoch);
        Assert.Equal(runtimeState.StateVersion, snapshot.DecodedBundleStateVersion);
        Assert.Equal(RuntimeDecodedBundleStateKind.DecodeFault, snapshot.DecodedBundleStateKind);
        Assert.Equal(RuntimeDecodedBundleStateOrigin.DecodeFallbackTrap, snapshot.DecodedBundleStateOrigin);
        Assert.Equal(0x6280UL, snapshot.DecodedBundlePc);
        Assert.Equal(transportFacts.ValidMask, snapshot.DecodedBundleValidMask);
        Assert.Equal(transportFacts.NopMask, snapshot.DecodedBundleNopMask);
        Assert.Equal(!core.GetCurrentDecodedInstructionBundle().IsEmpty, snapshot.DecodedBundleHasCanonicalDecode);
        Assert.Equal(!core.GetCurrentBundleLegalityDescriptor().IsEmpty, snapshot.DecodedBundleHasCanonicalLegality);
        Assert.Equal(core.GetCurrentDecodedInstructionBundle().HasDecodeFault, snapshot.DecodedBundleHasDecodeFault);
    }

    [Fact]
    public void IseCoreStateService_WhenDecodeObservationIsPublished_MapsTypedObservationTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x6300);

        core.TestLoadReplayDecodedBundleSlotCarrier(
            0x6300,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 7, src1Reg: 8, src2Reg: 9));
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();

        var service = new IseCoreStateService(
            ObservationServiceTestFactory.CreateSingleCoreService(core));

        CpuInterfaceBridge.CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(BridgeDecodedBundleStateOwnerKind.BaseRuntimePublication, snapshot.DecodedBundleStateOwnerKind);
        Assert.Equal(runtimeState.StateEpoch, snapshot.DecodedBundleStateEpoch);
        Assert.Equal(runtimeState.StateVersion, snapshot.DecodedBundleStateVersion);
        Assert.Equal(BridgeDecodedBundleStateKind.Replay, snapshot.DecodedBundleStateKind);
        Assert.Equal(BridgeDecodedBundleStateOrigin.ReplayBundleLoad, snapshot.DecodedBundleStateOrigin);
        Assert.Equal(0x6300UL, snapshot.DecodedBundlePc);
        Assert.Equal(transportFacts.ValidMask, snapshot.DecodedBundleValidMask);
        Assert.Equal(transportFacts.NopMask, snapshot.DecodedBundleNopMask);
        Assert.False(snapshot.DecodedBundleHasCanonicalDecode);
        Assert.False(snapshot.DecodedBundleHasCanonicalLegality);
        Assert.False(snapshot.DecodedBundleHasDecodeFault);
    }

    [Fact]
    public void WriteBackTrace_WhenReplayObservationIsPublished_EmitsObservationSnapshotFields()
    {
        var sink = new TraceSink(TraceFormat.JSON, "phase09-pipeline-observation.json");
        sink.SetEnabled(true);
        sink.SetLevel(TraceLevel.Full);

        TraceSink? originalTraceSink = Processor.TraceSink;
        try
        {
            Processor.TraceSink = sink;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x6380);

            core.TestLoadReplayDecodedBundleSlotCarrier(
                0x6380,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 10, src1Reg: 11, src2Reg: 12));
            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();
            core.TestEmitPhaseTimelineSample(stalled: true, stallReason: PipelineStallKind.MemoryWait);

            FullStateTraceEvent evt = Assert.Single(sink.GetThreadTrace(0));

            Assert.Equal(RuntimeDecodedBundleStateOwnerKind.BaseRuntimePublication, evt.DecodedBundleStateOwnerKind);
            Assert.Equal(runtimeState.StateEpoch, evt.DecodedBundleStateEpoch);
            Assert.Equal(runtimeState.StateVersion, evt.DecodedBundleStateVersion);
            Assert.Equal(RuntimeDecodedBundleStateKind.Replay, evt.DecodedBundleStateKind);
            Assert.Equal(RuntimeDecodedBundleStateOrigin.ReplayBundleLoad, evt.DecodedBundleStateOrigin);
            Assert.Equal(0x6380UL, evt.DecodedBundlePc);
            Assert.Equal(transportFacts.ValidMask, evt.DecodedBundleValidMask);
            Assert.Equal(transportFacts.NopMask, evt.DecodedBundleNopMask);
            Assert.False(evt.DecodedBundleHasCanonicalDecode);
            Assert.False(evt.DecodedBundleHasCanonicalLegality);
            Assert.False(evt.DecodedBundleHasDecodeFault);
        }
        finally
        {
            Processor.TraceSink = originalTraceSink;
        }
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
