using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase55CompletionReasonQualificationVmReadE0D2Tests
{
    [Fact]
    public void E0_SplitsExactAllowedAndDeniedFieldCoverage()
    {
        Assert.True(Phase55CompletionReasonQualificationVmReadE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.ExitReason, (ushort)VmcsField.ExitQualification]));
        Assert.True(Phase55CompletionReasonQualificationVmReadE0Contract.ExplicitlyDeniedFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestPhysicalAddress, (ushort)VmcsField.EptViolationQualification]));
        Assert.False(Phase55CompletionReasonQualificationVmReadE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase55CompletionReasonQualificationVmReadE0Contract.CompletionCreationAuthorized);
        Assert.False(Phase55CompletionReasonQualificationVmReadE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void ExactMapping_ProjectsOnlyOwnerApprovedReasonBoundTuple()
    {
        Processor.CPU_Core core = CreateCore();
        var owner = new NeutralCompletionReasonQualificationProjectionOwner(
            core.CanonicalCpuTranslationFaultCompletionProducer);
        ulong qualification = Encode(CpuInstructionTranslationFaultReason.AccessDenied,
            CpuInstructionTranslationAccessKind.ScalarStore, 8);
        NeutralCompletionReasonQualificationProjection projected = owner.Project(
            Snapshot(core, CpuInstructionTranslationFaultReason.AccessDenied, qualification));

        Assert.True(projected.IsProjected);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, projected.ExitReason);
        Assert.Equal(qualification, projected.ExitQualification);
    }

    [Theory]
    [InlineData(CpuInstructionTranslationFaultReason.UnmappedAddress)]
    [InlineData(CpuInstructionTranslationFaultReason.AddressOverflow)]
    public void ExactMapping_DeniesNeutralReasonsWithoutFrozenCompatibilityMeaning(
        CpuInstructionTranslationFaultReason reason)
    {
        Processor.CPU_Core core = CreateCore();
        var owner = new NeutralCompletionReasonQualificationProjectionOwner(
            core.CanonicalCpuTranslationFaultCompletionProducer);

        NeutralCompletionReasonQualificationProjection denied = owner.Project(
            Snapshot(core, reason, Encode(reason,
                CpuInstructionTranslationAccessKind.ScalarLoad, 8)));

        Assert.Equal(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonSemantic,
            denied.Decision);
        Assert.Equal(VmExitReason.None, denied.ExitReason);
        Assert.Equal(0UL, denied.ExitQualification);
    }

    [Fact]
    public void ExactMapping_DeniesMalformedQualificationWithoutZeroFallback()
    {
        Processor.CPU_Core core = CreateCore();
        var owner = new NeutralCompletionReasonQualificationProjectionOwner(
            core.CanonicalCpuTranslationFaultCompletionProducer);
        NeutralCompletionObservationSnapshot snapshot = Snapshot(
            core,
            CpuInstructionTranslationFaultReason.AccessDenied,
            Encode(CpuInstructionTranslationFaultReason.AccessDenied,
                CpuInstructionTranslationAccessKind.ScalarLoad, 8) | 1UL);

        NeutralCompletionReasonQualificationProjection denied = owner.Project(snapshot);

        Assert.Equal(NeutralCompletionCompatibilityProjectionDecision.DeniedQualificationSemantic,
            denied.Decision);
        Assert.False(denied.IsProjected);
        Assert.Equal(0UL, denied.ExitQualification);
    }

    [Fact]
    public void D2_ImmutableSpecHasExactCanonicalShapeAndNoRuntimeAuthority()
    {
        VirtualizationDecisionSpecV2 spec =
            Phase55CompletionReasonQualificationVmReadDecisionSpecV2.Instance;
        VmReadScalarDeliveryDecisionValidationResultV2 result =
            CompletionReasonQualificationVmReadDecisionValidatorV2.ValidateSpecShape(spec);

        Assert.True(result.IsExactPolicyShape, result.Reason);
        Assert.Equal(VirtualizationDecisionAuthorityPlaneV2.CompletionObservationReadProjection,
            spec.AuthorityPlane);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionSpecV2.RuntimeAuthorityGranted);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionSpecV2.ProductionCompositionAuthorized);
    }

    [Fact]
    public void D2_LaterAcceptanceBindsEarlierSpecAndRemainsGovernanceOnly()
    {
        VirtualizationDecisionAcceptanceRecordV2 record =
            Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.Record;

        Assert.Equal("9497d6152bee14e3743e99edc6c4c451b869ee8a", record.SpecCommitSha);
        Assert.Equal(
            VmReadScalarDeliveryDecisionValidationDecisionV2.AcceptedPolicyObject,
            Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2
                .ValidateRepositoryArtifact("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").Decision);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.CompletionCreationAuthorized);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.ProductionCompositionAuthorized);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.GuestPhysicalAddressAuthorized);
        Assert.False(Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2.EptViolationQualificationAuthorized);
    }

    [Theory]
    [InlineData(VmcsField.ExitReason, (ulong)VmExitReason.SecurityPolicyViolation)]
    [InlineData(VmcsField.ExitQualification, 0x0201_0100_0000_0000UL)]
    public void ProductionComposition_ReadsExactCommittedSnapshotAndUsesExistingScalarReceipt(
        VmcsField field,
        ulong expected)
    {
        Processor.CPU_Core core = CreateAccessDeniedFetchCore(enableProjection: true);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var composition = new CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(packet, lane));

        Assert.True(scheduler.TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)field, core.CurrentVirtualizationRestoreGeneration));
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);
        Assert.Equal(expected, receipt.Value);
        Assert.True(carrier.Execute(ref core));
        Assert.True(carrier.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(VmcsField.GuestPhysicalAddress)]
    [InlineData(VmcsField.EptViolationQualification)]
    public void ProductionComposition_ExplicitlyDeniesIncompleteFields(VmcsField field)
    {
        Processor.CPU_Core core = CreateAccessDeniedFetchCore(enableProjection: true);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var composition = new CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(packet, lane));

        Assert.False(scheduler.TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)field, core.CurrentVirtualizationRestoreGeneration));
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied,
            scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    [Fact]
    public void ProductionReceipt_IsInvalidAfterRestoreAndCannotBeConsumedTwice()
    {
        Processor.CPU_Core core = CreateAccessDeniedFetchCore(enableProjection: true);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var composition = new CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(packet, lane));
        Assert.True(scheduler.TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)VmcsField.ExitReason, core.CurrentVirtualizationRestoreGeneration));
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);

        Processor.CPU_Core.VectorContext saved = core.SaveVectorContext();
        core.RestoreVectorContext(saved);

        Assert.False(receipt.TryValidateSpeculative(core.CurrentVirtualizationRestoreGeneration));
        Assert.False(receipt.TryConsumeAtRetire(core.CurrentVirtualizationRestoreGeneration));
    }

    [Fact]
    public void ProductionConstruction_IsExplicitOptInAndCompletionFieldsDoNotFallThrough()
    {
        Assert.False(CpuCorePlatformContext.CreateFixed(
            new Processor.MultiBankMemoryArea(1, 0x1000),
            ProcessorMode.Emulation).EnableCompletionReasonQualificationVmRead);
        string materialization = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        Assert.Contains("TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead", materialization);
        Assert.Contains("GuestPhysicalAddress", materialization);
        Assert.Contains("EptViolationQualification", materialization);
    }

    [Fact]
    public void ProductionKillSwitch_IsExactIdempotentAndCannotReactivate()
    {
        Processor.CPU_Core core = CreateAccessDeniedFetchCore(enableProjection: true);

        Assert.True(core.HasActiveCompletionReasonQualificationVmReadProfile);
        Assert.True(core.DisableCompletionReasonQualificationVmReadProfile());
        Assert.False(core.HasActiveCompletionReasonQualificationVmReadProfile);
        Assert.True(core.DisableCompletionReasonQualificationVmReadProfile());
        Assert.False(core.HasActiveCompletionReasonQualificationVmReadProfile);
    }

    [Fact]
    public async Task PrepareVersusKillSwitch_InvalidatesEveryPreparedReceipt()
    {
        Processor.CPU_Core core = CreateAccessDeniedFetchCore(enableProjection: true);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var composition = new CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition(
            core.ArchitecturalCompletionCommitOwner,
            core.CanonicalCpuTranslationFaultCompletionProducer,
            enabled: true);
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(packet, lane));

        Task<bool> prepare = Task.Run(() =>
            scheduler.TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead(
                composition, lane, (ushort)VmcsField.ExitReason,
                core.CurrentVirtualizationRestoreGeneration));
        Task<bool> disable = Task.Run(composition.Disable);
        await Task.WhenAll(prepare, disable);

        Assert.True(await disable);
        Assert.False(composition.IsEnabled);
        if (scheduler.LastVmReadScalarDeliveryResult?.Receipt is { } receipt)
        {
            Assert.False(receipt.TryValidateSpeculative(core.CurrentVirtualizationRestoreGeneration));
            Assert.False(receipt.TryConsumeAtRetire(core.CurrentVirtualizationRestoreGeneration));
        }
    }

    private static Processor.CPU_Core CreateCore()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        return new Processor.CPU_Core(0,
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation));
    }

    private static Processor.CPU_Core CreateAccessDeniedFetchCore(bool enableProjection)
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        CpuInstructionTranslationPolicy policy = CpuInstructionTranslationPolicy.CreateBoundedRegions(
            new CpuInstructionTranslationScope(7, 3, 0),
            [new CpuInstructionTranslationRegion(0, 0, 0x400, true, false, false)]);
        var core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(
            memory, ProcessorMode.Emulation,
            cpuInstructionTranslationPolicy: policy,
            enableCompletionReasonQualificationVmRead: enableProjection));
        core.PrepareExecutionStart(0, 0);
        return core;
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
        CpuInstructionTranslationFaultReason reason,
        ulong qualification)
    {
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer =
            core.CanonicalCpuTranslationFaultCompletionProducer;
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
                NeutralAddressFact.Present(0, NeutralFaultAddressSemantic.VirtualAddress),
                NeutralAuxiliaryFact.Present(1, NeutralFaultAuxiliarySemantic.TranslationFault)));
    }

    private static ulong Encode(
        CpuInstructionTranslationFaultReason reason,
        CpuInstructionTranslationAccessKind access,
        ushort size) =>
        ((ulong)(byte)reason << 56) | ((ulong)(byte)access << 48) | ((ulong)size << 32);
}
