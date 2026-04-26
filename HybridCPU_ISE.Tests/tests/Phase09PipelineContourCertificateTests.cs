using CpuInterfaceBridge;
using HybridCPU_ISE;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using RuntimePipelineContourCertificate = YAKSys_Hybrid_CPU.Core.PipelineContourCertificate;
using RuntimePipelineContourKind = YAKSys_Hybrid_CPU.Core.PipelineContourKind;
using RuntimePipelineContourOwner = YAKSys_Hybrid_CPU.Core.PipelineContourOwner;
using RuntimePipelineContourVisibilityStage = YAKSys_Hybrid_CPU.Core.PipelineContourVisibilityStage;
using BridgePipelineContourKind = CpuInterfaceBridge.PipelineContourKind;
using BridgePipelineContourOwner = CpuInterfaceBridge.PipelineContourOwner;
using BridgePipelineContourVisibilityStage = CpuInterfaceBridge.PipelineContourVisibilityStage;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09PipelineContourCertificateTests
{
    [Fact]
    public void ObservationServiceSnapshot_WhenCanonicalDecodeIsPublished_ReportsDecodeContourCertificate()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7100);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x7100, bundleSerial: 11);
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        AssertContourCertificate(
            snapshot.DecodePublicationCertificate,
            RuntimePipelineContourKind.DecodePublication,
            RuntimePipelineContourOwner.DecodedBundleTransportPublication,
            RuntimePipelineContourVisibilityStage.Decode,
            0x7100UL,
            transportFacts.ValidNonEmptyMask);
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenFspDerivedIssuePlanIsActive_PreservesCanonicalDecodeContourCertificate()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x7140);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
            var annotations = new VliwBundleAnnotations(
                new InstructionSlotMetadata[0],
                new BundleMetadata { FspBoundary = false });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x7140, bundleSerial: 13);
            DecodedBundleTransportFacts canonicalTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
            scheduler.Nominate(1, candidate);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts foregroundTransportFacts =
                core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);
            CoreStateSnapshot snapshot = service.GetCoreState(0);

            AssertContourCertificate(
                snapshot.DecodePublicationCertificate,
                RuntimePipelineContourKind.DecodePublication,
                RuntimePipelineContourOwner.DecodedBundleTransportPublication,
                RuntimePipelineContourVisibilityStage.Decode,
                0x7140UL,
                canonicalTransportFacts.ValidNonEmptyMask);
            Assert.NotEqual(
                foregroundTransportFacts.ValidNonEmptyMask,
                snapshot.DecodePublicationCertificate.SlotMask);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenSingleLaneMicroOpCompletes_ReportsExecuteContourCertificate()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7180);

        var microOp = MicroOpTestHelper.CreateScalarALU(0, destReg: 5, src1Reg: 6, src2Reg: 7);
        VLIW_Instruction instruction = CreateScalarInstruction(InstructionsEnum.Addition, rd: 5, rs1: 6, rs2: 7);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: 5,
            reg2Id: 6,
            reg3Id: 7,
            pc: 0x7180);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        AssertContourCertificate(
            snapshot.ExecuteCompletionCertificate,
            RuntimePipelineContourKind.ExecuteCompletion,
            RuntimePipelineContourOwner.SingleLaneMicroOpExecution,
            RuntimePipelineContourVisibilityStage.Execute,
            0x7180UL,
            0x01);
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenExplicitPacketCompletes_ReportsExecuteContourCertificateMask()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7200);

        core.TestExecuteExplicitPacketLanes(
            (0, MicroOpTestHelper.CreateScalarALU(0, destReg: 8, src1Reg: 9, src2Reg: 10), 0x7200UL, 0),
            (1, MicroOpTestHelper.CreateScalarALU(0, destReg: 11, src1Reg: 12, src2Reg: 13), 0x7200UL, 0));
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        AssertContourCertificate(
            snapshot.ExecuteCompletionCertificate,
            RuntimePipelineContourKind.ExecuteCompletion,
            RuntimePipelineContourOwner.ExplicitPacketExecution,
            RuntimePipelineContourVisibilityStage.Execute,
            0x7200UL,
            0x03);
    }

    [Fact]
    public void ObservationServiceSnapshot_WhenWriteBackRetireWindowCommits_ReportsRetireContourCertificate()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7280);

        core.TestRetireLegacyScalarStoreThroughWriteBack(
            pc: 0x7280,
            address: 0x100,
            data: 0x55,
            accessSize: 8,
            vtId: 0);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        CoreStateSnapshot snapshot = service.GetCoreState(0);

        AssertContourCertificate(
            snapshot.RetireVisibilityCertificate,
            RuntimePipelineContourKind.RetireVisibility,
            RuntimePipelineContourOwner.WriteBackRetireWindow,
            RuntimePipelineContourVisibilityStage.WriteBack,
            0x7280UL,
            0x01);
    }

    [Fact]
    public void RuntimeObservation_WhenCanonicalConditionalBranchCompletesInExecute_RetiresRedirectOnlyFromWriteBackWindow()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7300);
        core.WriteCommittedPc(0, 0x7300);
        core.WriteCommittedArch(0, 1, 0x44);
        core.WriteCommittedArch(0, 2, 0x44);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rs1: 1,
                    rs2: 2,
                    immediate: 0x40));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc: 0x7300);
        core.TestRunExecuteStageFromCurrentDecodeState();

        CoreStateSnapshot executeSnapshot =
            ObservationServiceTestFactory.CreateSingleCoreService(core).GetCoreState(0);

        Assert.Equal(0x7300UL, core.ReadCommittedPc(0));
        Assert.Equal(0x7400UL, core.ReadActiveLivePc());
        AssertContourCertificate(
            executeSnapshot.ExecuteCompletionCertificate,
            RuntimePipelineContourKind.ExecuteCompletion,
            RuntimePipelineContourOwner.ExplicitPacketExecution,
            RuntimePipelineContourVisibilityStage.Execute,
            0x7300UL,
            0x80);
        Assert.False(executeSnapshot.RetireVisibilityCertificate.IsPublished);
        Assert.Equal(RuntimePipelineContourKind.RetireVisibility, executeSnapshot.RetireVisibilityCertificate.Kind);
        Assert.Equal(RuntimePipelineContourOwner.None, executeSnapshot.RetireVisibilityCertificate.Owner);
        Assert.Equal(RuntimePipelineContourVisibilityStage.None, executeSnapshot.RetireVisibilityCertificate.VisibilityStage);

        core.TestRunMemoryStageFromCurrentExecuteState();
        core.TestRunWriteBackStage();

        CoreStateSnapshot retireSnapshot =
            ObservationServiceTestFactory.CreateSingleCoreService(core).GetCoreState(0);

        Assert.Equal(0x7340UL, core.ReadCommittedPc(0));
        Assert.Equal(0x7340UL, core.ReadActiveLivePc());
        AssertContourCertificate(
            retireSnapshot.RetireVisibilityCertificate,
            RuntimePipelineContourKind.RetireVisibility,
            RuntimePipelineContourOwner.WriteBackRetireWindow,
            RuntimePipelineContourVisibilityStage.WriteBack,
            0x7300UL,
            0x80);
    }

    [Fact]
    public void RuntimeObservation_WhenCanonicalJalrRetires_ReportsWriteBackRetireContourCertificate()
    {
        const ulong startPc = 0x2C80;
        const ulong baseValue = 0x4101;
        const ulong targetPc = 0x4120;
        const ulong linkValue = startPc + 4;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc);
        core.WriteCommittedPc(0, startPc);
        core.WriteCommittedArch(0, 2, baseValue);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateControlInstruction(InstructionsEnum.JALR, rd: 5, rs1: 2, immediate: 0x20));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc: startPc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryStageFromCurrentExecuteState();
        core.TestRunWriteBackStage();

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.ScalarLanesRetired);
        Assert.Equal(1UL, control.NonScalarLanesRetired);
        Assert.Equal(linkValue, core.ReadArch(0, 5));
        Assert.Equal(targetPc, core.ReadCommittedPc(0));
        Assert.Equal(targetPc, core.ReadActiveLivePc());

        Processor.CPU_Core.PipelineObservationSnapshot observation = core.GetPipelineObservationSnapshot();

        AssertContourCertificate(
            observation.RetireVisibilityCertificate,
            RuntimePipelineContourKind.RetireVisibility,
            RuntimePipelineContourOwner.WriteBackRetireWindow,
            RuntimePipelineContourVisibilityStage.WriteBack,
            startPc,
            0x80);
    }

    [Fact]
    public void IseCoreStateService_WhenContourCertificatesArePublished_MapsTypedContourTruth()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x7380);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 8));
        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x7380, bundleSerial: 12);
        core.TestExecuteExplicitPacketLanes(
            (0, MicroOpTestHelper.CreateScalarALU(0, destReg: 14, src1Reg: 15, src2Reg: 16), 0x7380UL, 0),
            (1, MicroOpTestHelper.CreateScalarALU(0, destReg: 17, src1Reg: 18, src2Reg: 19), 0x7380UL, 0));
        core.TestRetireLegacyScalarStoreThroughWriteBack(
            pc: 0x7380,
            address: 0x180,
            data: 0x1234,
            accessSize: 8,
            vtId: 0);

        var service = new IseCoreStateService(
            ObservationServiceTestFactory.CreateSingleCoreService(core));

        CpuInterfaceBridge.CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.True(snapshot.DecodePublicationCertificate.IsPublished);
        Assert.Equal(BridgePipelineContourKind.DecodePublication, snapshot.DecodePublicationCertificate.Kind);
        Assert.Equal(BridgePipelineContourOwner.DecodedBundleTransportPublication, snapshot.DecodePublicationCertificate.Owner);
        Assert.Equal(BridgePipelineContourVisibilityStage.Decode, snapshot.DecodePublicationCertificate.VisibilityStage);

        Assert.True(snapshot.ExecuteCompletionCertificate.IsPublished);
        Assert.Equal(BridgePipelineContourKind.ExecuteCompletion, snapshot.ExecuteCompletionCertificate.Kind);
        Assert.Equal(BridgePipelineContourOwner.ExplicitPacketExecution, snapshot.ExecuteCompletionCertificate.Owner);
        Assert.Equal(BridgePipelineContourVisibilityStage.Execute, snapshot.ExecuteCompletionCertificate.VisibilityStage);
        Assert.Equal(0x03, snapshot.ExecuteCompletionCertificate.SlotMask);

        Assert.True(snapshot.RetireVisibilityCertificate.IsPublished);
        Assert.Equal(BridgePipelineContourKind.RetireVisibility, snapshot.RetireVisibilityCertificate.Kind);
        Assert.Equal(BridgePipelineContourOwner.WriteBackRetireWindow, snapshot.RetireVisibilityCertificate.Owner);
        Assert.Equal(BridgePipelineContourVisibilityStage.WriteBack, snapshot.RetireVisibilityCertificate.VisibilityStage);
        Assert.Equal(0x01, snapshot.RetireVisibilityCertificate.SlotMask);
    }

    private static void AssertContourCertificate(
        RuntimePipelineContourCertificate certificate,
        RuntimePipelineContourKind kind,
        RuntimePipelineContourOwner owner,
        RuntimePipelineContourVisibilityStage visibilityStage,
        ulong pc,
        byte slotMask)
    {
        Assert.True(
            certificate.IsPublished,
            $"Expected published contour {kind}/{owner}/{visibilityStage} at PC 0x{pc:X} mask 0x{slotMask:X2}, " +
            $"but observed IsPublished={certificate.IsPublished}, Kind={certificate.Kind}, Owner={certificate.Owner}, " +
            $"VisibilityStage={certificate.VisibilityStage}, Pc=0x{certificate.Pc:X}, SlotMask=0x{certificate.SlotMask:X2}.");
        Assert.Equal(kind, certificate.Kind);
        Assert.Equal(owner, certificate.Owner);
        Assert.Equal(visibilityStage, certificate.VisibilityStage);
        Assert.Equal(pc, certificate.Pc);
        Assert.Equal(slotMask, certificate.SlotMask);
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

    private static VLIW_Instruction CreateControlInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ulong targetPc = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
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
            VirtualThreadId = virtualThreadId
        };
    }
}
