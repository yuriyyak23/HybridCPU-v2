using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase57CompletionSecondStageVmReadE0D2Tests
{
    [Fact]
    public void E0_AdmitsOnlyExactGpaAndSecondStageQualificationFields()
    {
        Assert.True(Phase57CompletionSecondStageVmReadE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestPhysicalAddress, (ushort)VmcsField.EptViolationQualification]));
        Assert.False(Phase57CompletionSecondStageVmReadE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase57CompletionSecondStageVmReadE0Contract.CompletionCreationAuthorized);
        Assert.False(Phase57CompletionSecondStageVmReadE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void ExactMapper_PreservesPresentZeroGpaAndMapsNeutralAuxiliaryExplicitly()
    {
        Processor.CPU_Core core = CreateCore();
        var owner = new NeutralCompletionSecondStageProjectionOwner(
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer);

        NeutralCompletionSecondStageProjection mapped = owner.Project(Snapshot(
            core,
            gpa: 0,
            CpuInstructionTranslationFaultReason.SecondStageViolation,
            CpuSecondStageFaultKind.NotPresent,
            CpuInstructionTranslationAccessKind.ScalarLoad,
            accessSize: 8,
            pageWalkLevel: 1));

        Assert.True(mapped.IsProjected, mapped.Reason);
        Assert.Equal(0UL, mapped.GuestPhysicalAddress);
        Assert.Equal((1UL << 0) | (1UL << 7) | (1UL << 12),
            mapped.EptViolationQualification);
    }

    [Fact]
    public void ExactMapper_DeniesReasonAuxiliaryMismatchWithoutFallback()
    {
        Processor.CPU_Core core = CreateCore();
        var owner = new NeutralCompletionSecondStageProjectionOwner(
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer);

        NeutralCompletionSecondStageProjection denied = owner.Project(Snapshot(
            core,
            gpa: 0x3000,
            CpuInstructionTranslationFaultReason.SecondStageViolation,
            CpuSecondStageFaultKind.Misconfiguration,
            CpuInstructionTranslationAccessKind.InstructionFetch,
            accessSize: 256,
            pageWalkLevel: 2));

        Assert.Equal(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonSemantic,
            denied.Decision);
        Assert.Equal(0UL, denied.GuestPhysicalAddress);
        Assert.Equal(0UL, denied.EptViolationQualification);
    }

    [Fact]
    public void D2_ImmutableSpecHasExactCanonicalShapeAndNoRuntimeAuthority()
    {
        VirtualizationDecisionSpecV2 spec =
            Phase57CompletionSecondStageVmReadDecisionSpecV2.Instance;
        VmReadScalarDeliveryDecisionValidationResultV2 result =
            CompletionSecondStageVmReadDecisionValidatorV2.ValidateSpecShape(spec);

        Assert.True(result.IsExactPolicyShape, result.Reason);
        Assert.False(Phase57CompletionSecondStageVmReadDecisionSpecV2.RuntimeAuthorityGranted);
        Assert.False(Phase57CompletionSecondStageVmReadDecisionSpecV2.ProductionCompositionAuthorized);
    }

    [Fact]
    public void D2_LaterAcceptanceBindsEarlierImmutableSpecAndGrantsNoRuntimeAuthority()
    {
        VirtualizationDecisionAcceptanceRecordV2 record =
            Phase57CompletionSecondStageVmReadDecisionAcceptanceV2.Record;

        Assert.Equal("0865929ae2ab5ee95b0912eebd63ae7af2494f4c", record.SpecCommitSha);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.AcceptedPolicyObject,
            Phase57CompletionSecondStageVmReadDecisionAcceptanceV2
                .ValidateRepositoryArtifact("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").Decision);
        Assert.False(Phase57CompletionSecondStageVmReadDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase57CompletionSecondStageVmReadDecisionAcceptanceV2.CompletionCreationAuthorized);
        Assert.False(Phase57CompletionSecondStageVmReadDecisionAcceptanceV2.ProductionCompositionAuthorized);
    }

    [Theory]
    [InlineData(VmcsField.GuestPhysicalAddress, 0UL)]
    [InlineData(VmcsField.EptViolationQualification, 0x1081UL)]
    public void ProductionComposition_ReadsOnlyExactCommittedSecondStageSnapshot(
        VmcsField field,
        ulong expected)
    {
        Processor.CPU_Core core = CreateCore();
        CommitSecondStage(core, gpa: 0);
        var composition = new CompletionSecondStageVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            CreatePacket(lane), lane));

        Assert.True(scheduler.TryPrepareCompletionSecondStageVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)field, core.CurrentVirtualizationRestoreGeneration));
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);
        Assert.Equal(expected, receipt.Value);
        Assert.True(carrier.Execute(ref core));
        Assert.True(carrier.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ProductionReceipt_IsRevokedByRestoreBeforeArchitecturalConsumption()
    {
        Processor.CPU_Core core = CreateCore();
        CommitSecondStage(core, gpa: 0x3000);
        var composition = new CompletionSecondStageVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            CreatePacket(lane), lane));
        Assert.True(scheduler.TryPrepareCompletionSecondStageVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)VmcsField.GuestPhysicalAddress,
            core.CurrentVirtualizationRestoreGeneration));
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);

        Processor.CPU_Core.VectorContext saved = core.SaveVectorContext();
        core.RestoreVectorContext(saved);

        Assert.False(receipt.TryValidateSpeculative(core.CurrentVirtualizationRestoreGeneration));
        Assert.False(receipt.TryConsumeAtRetire(core.CurrentVirtualizationRestoreGeneration));
    }

    [Fact]
    public void ProductionConstruction_IsSeparateExplicitOptInDefaultDisabled()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        CpuCorePlatformContext defaults = CpuCorePlatformContext.CreateFixed(
            memory, ProcessorMode.Emulation);
        Assert.False(defaults.EnableCompletionSecondStageVmRead);
        Assert.False(defaults.EnableCompletionReasonQualificationVmRead);
        CpuCorePlatformContext exact = CpuCorePlatformContext.CreateFixed(
            memory, ProcessorMode.Emulation,
            enableCompletionSecondStageVmRead: true);
        Assert.True(exact.EnableCompletionSecondStageVmRead);
        Assert.False(exact.EnableCompletionReasonQualificationVmRead);
    }

    [Fact]
    public void MachineStatusAndEvidence_CloseOnlyExactPhase57Boundary()
    {
        string status = ActiveVmxConformanceHelpers.ReadProjectSource(
            "docs/ref2/VirtualizationActivationPlan/VirtualizationActivationStatusV1.json");
        string evidence = ActiveVmxConformanceHelpers.ReadProjectSource(
            "docs/ref2/VirtualizationActivationPlan/evidence/2026-08-13-phase57-exact-completion-second-stage-vmread-clean-evidence.json");
        Assert.Contains("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence", status);
        Assert.Contains("875b472abe8286ad72c7157c075f2866e4e75f15", status);
        Assert.Contains("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation", status);
        Assert.Contains("\"NonSelfReferential\": true", evidence);
        Assert.Contains("\"WorktreeVmx\": \"556/556\"", evidence);
        Assert.DoesNotContain("VMX fully activated", evidence);
    }

    private static Processor.CPU_Core CreateCore()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        return new Processor.CPU_Core(0,
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation));
    }

    private static void CommitSecondStage(Processor.CPU_Core core, ulong gpa)
    {
        NeutralCompletionObservationSnapshot snapshot = Snapshot(
            core,
            gpa,
            CpuInstructionTranslationFaultReason.SecondStageViolation,
            CpuSecondStageFaultKind.NotPresent,
            CpuInstructionTranslationAccessKind.ScalarLoad,
            accessSize: 8,
            pageWalkLevel: 1);
        ArchitecturalCompletionCommitResult result =
            core.ArchitecturalCompletionCommitOwner.CommitAtCanonicalPreciseFaultBoundary(
                core.CanonicalCpuSecondStageTranslationFaultCompletionProducer,
                new ArchitecturalCompletionCandidate(
                    snapshot.Scope.DomainId,
                    snapshot.Scope.ContextId,
                    snapshot.Scope.VirtualThreadId,
                    snapshot.AttemptId,
                    snapshot.EventId,
                    snapshot.Facts,
                    snapshot.TranslationProvenance));
        Assert.True(result.IsCommitted, result.Reason);
    }

    private static VmxMicroOp CreateVmRead()
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMREAD,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 3,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMREAD),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 3, Rs1 = 1, Rs2 = 0, Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = 7 };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static IssuePacketLane CreateLane(VmxMicroOp vmx) => new(
        7, true, 7, 0, 0, unchecked((ushort)vmx.OpCode), vmx,
        SlotClass.SystemSingleton, SlotPinningKind.HardPinned,
        countsTowardScalarProjection: false);

    private static BundleIssuePacket CreatePacket(IssuePacketLane lane7) => new(
        0x7000, DecodeMode.ClusterPreparedMode, 0x80, 0, 0, 0x80, 0, 0, 0, 0, 0,
        RuntimeClusterAdmissionExecutionMode.ClusterPrepared, false, true, false,
        IssuePacketLane.CreateEmpty(0), IssuePacketLane.CreateEmpty(1),
        IssuePacketLane.CreateEmpty(2), IssuePacketLane.CreateEmpty(3),
        IssuePacketLane.CreateEmpty(4), IssuePacketLane.CreateEmpty(5),
        IssuePacketLane.CreateEmpty(6), lane7, BundleIssueFallbackInfo.CreateEmpty());

    private static ReplayPhaseContext Replay() => new(
        true, 17, 0x4000, 1, 1, 1, 0x80, ReplayPhaseInvalidationReason.None);

    private static NeutralCompletionObservationSnapshot Snapshot(
        Processor.CPU_Core core,
        ulong gpa,
        CpuInstructionTranslationFaultReason reason,
        CpuSecondStageFaultKind kind,
        CpuInstructionTranslationAccessKind access,
        ushort accessSize,
        byte pageWalkLevel)
    {
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer =
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer;
        ulong qualification =
            ((ulong)(byte)reason << 56) |
            ((ulong)(byte)access << 48) |
            ((ulong)accessSize << 32);
        return new(
            new CompletionObservationScope(7, 3, 0),
            CompletionGeneration: 2,
            CompletionIdentity: 11,
            producer.OwnerIdentity,
            producer.OwnerEpoch,
            AttemptId: 13,
            EventId: 17,
            NeutralArchitecturalCompletionClass.TranslationFault,
            CompletionDigest: new string('a', 64),
            CanonicalOrderSequence: 19,
            CommitSequence: 23,
            RestoreGeneration: 1,
            new NeutralArchitecturalCompletionFacts(
                NeutralArchitecturalCompletionClass.TranslationFault,
                NeutralScalarFact.Present((ulong)(byte)reason),
                NeutralScalarFact.Present(qualification),
                NeutralAddressFact.Present(gpa, NeutralFaultAddressSemantic.GuestPhysicalAddress),
                NeutralAuxiliaryFact.Present(
                    CpuSecondStageNeutralAuxiliary.Encode(access, accessSize, kind, pageWalkLevel),
                    NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation)),
            NeutralTranslationProvenance.Present(29, 31, 37, 41, 43));
    }
}
