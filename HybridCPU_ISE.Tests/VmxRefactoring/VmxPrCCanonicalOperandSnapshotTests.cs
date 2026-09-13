using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrCCanonicalOperandSnapshotTests
{
    [Fact]
    public void CanonicalPipeline_CapturesFullRs1ValueOnceAfterE1_AndStillFaults()
    {
        VmxMicroOp vmx = CreateVmCall(rs1: 5, domainTag: 7);
        IssuePacketLane lane7 = CreateLane(vmx);
        BundleIssuePacket packet = CreateIssuePacket(lane7);
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.InitializePipeline();
        core.RetireCoordinator.Retire(RetireRecord.RegisterWrite(0, 5, 1));

        MethodInfo materialize = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core)
            .GetMethod(
                "ResolveMaterializedIssuePacketLane",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
        IssuePacketLane result = Assert.IsType<IssuePacketLane>(
            materialize.Invoke(core, new object[] { packet, (byte)7, (byte)0x80 }));

        Assert.True(result.IsOccupied);
        Assert.NotNull(vmx.VirtualizationAdmission);
        VirtualizationOperandSnapshot snapshot =
            Assert.IsType<VirtualizationOperandSnapshot>(vmx.VirtualizationOperandSnapshot);
        Assert.Equal(vmx.VirtualizationAdmission!.AttemptId, snapshot.AttemptId);
        Assert.Equal((byte)5, snapshot.Rs1Selector);
        Assert.Equal(1UL, snapshot.Rs1Value);
        Assert.Equal((byte)0, snapshot.Rs2Selector);
        Assert.Equal(0UL, snapshot.Rs2Value);
        Assert.Equal((byte)0, snapshot.RdSelector);
        Assert.Equal(7UL, snapshot.DomainTag);
        Assert.Equal(7, snapshot.SourceSlotId);
        Assert.Equal(7, snapshot.WorkingSlotId);
        Assert.NotEqual(0UL, snapshot.RestoreGeneration);
        Assert.NotEqual(0UL, snapshot.CaptureSequence);
        Assert.Equal(64, snapshot.OperandDigest.Length);
        Assert.False(snapshot.BackendExecutionAuthorized);
        Assert.False(snapshot.CompletionPublicationAuthorized);
        Assert.False(snapshot.RetirePublicationAuthorized);

        core.RetireCoordinator.Retire(RetireRecord.RegisterWrite(0, 5, 2));
        Assert.Equal(2UL, core.ReadArch(0, 5));
        Assert.Equal(1UL, snapshot.Rs1Value);

        Assert.True(vmx.Execute(ref core));
        Assert.True(vmx.CreateRetireEffect().IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, vmx.CreateRetireEffect().FailureReason);
    }

    [Fact]
    public void Materializer_DeniesZeroAdjacentHighBitAndInvalidRegisterShape()
    {
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.InvalidLeafValue, rs1Value: 0);
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.InvalidLeafValue, rs1Value: 2);
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.LeafHighBitsSet, rs1Value: 0x1_0001);
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.InvalidRegisterShape, rs1Value: 1, rs2: 1);
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.InvalidRegisterShape, rs1Value: 1, rd: 1);
        AssertCaptureDecision(VirtualizationOperandCaptureDecision.InvalidRegisterShape, rs1Value: 1, rs1: 0);
    }

    [Fact]
    public void Materializer_DeniesZeroDomainWrongSlotZeroRestoreAndDuplicateAttempt()
    {
        AssertCaptureDecision(
            VirtualizationOperandCaptureDecision.MissingDomainIdentity,
            rs1Value: 1,
            domainTag: 0);
        (VmxMicroOp displaced, SafetyVerifier.VirtualizationAdmissionCertificate displacedE1) =
            CreateAdmittedVmCall();
        displaced.Placement = displaced.Placement with { PinnedLaneId = 6 };
        VirtualizationOperandCaptureResult displacedResult =
            new VirtualizationOperandSnapshotMaterializer().CaptureAfterValidatedE1(
                displaced,
                displacedE1,
                1,
                1,
                Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot);
        Assert.Equal(VirtualizationOperandCaptureDecision.InvalidSlotIdentity, displacedResult.Decision);
        AssertCaptureDecision(
            VirtualizationOperandCaptureDecision.MissingRestoreGeneration,
            rs1Value: 1,
            restoreGeneration: 0);

        (VmxMicroOp vmx, SafetyVerifier.VirtualizationAdmissionCertificate e1) =
            CreateAdmittedVmCall();
        var materializer = new VirtualizationOperandSnapshotMaterializer();
        VirtualizationOperationOwnerSnapshot o1 =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandCaptureResult first =
            materializer.CaptureAfterValidatedE1(vmx, e1, 1, 1, o1);
        VirtualizationOperandCaptureResult duplicate =
            materializer.CaptureAfterValidatedE1(vmx, e1, 1, 1, o1);

        Assert.True(first.IsCaptured);
        Assert.Equal(VirtualizationOperandCaptureDecision.DuplicateAttempt, duplicate.Decision);
        Assert.Null(duplicate.Snapshot);
    }

    [Fact]
    public void SnapshotDigest_IsCanonicalAndSnapshotHasNoMutationSurface()
    {
        (VmxMicroOp vmx, SafetyVerifier.VirtualizationAdmissionCertificate e1) =
            CreateAdmittedVmCall();
        VirtualizationOperationOwnerSnapshot o1 =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandCaptureResult result =
            new VirtualizationOperandSnapshotMaterializer().CaptureAfterValidatedE1(
                vmx,
                e1,
                rs1Value: 1,
                restoreGeneration: 9,
                o1);
        VirtualizationOperandSnapshot snapshot = Assert.IsType<VirtualizationOperandSnapshot>(result.Snapshot);

        string recomputed = VirtualizationOperandSnapshotDigest.Compute(
            snapshot.AttemptId,
            snapshot.E1IssuerGeneration,
            snapshot.VirtualThreadId,
            snapshot.OwnerContextId,
            snapshot.DomainTag,
            snapshot.Rs1Selector,
            snapshot.Rs1Value,
            snapshot.Rs2Selector,
            snapshot.Rs2Value,
            snapshot.RdSelector,
            snapshot.SourceSlotId,
            snapshot.WorkingSlotId,
            snapshot.BundleIdentity,
            snapshot.ReplayEpoch,
            snapshot.CarrierIdentityDigest,
            snapshot.RestoreGeneration,
            snapshot.CaptureSequence,
            snapshot.OwnerPolicyDigest);

        Assert.Equal(snapshot.OperandDigest, recomputed);
        Assert.Empty(typeof(VirtualizationOperandSnapshot).GetConstructors());
        Assert.DoesNotContain(
            typeof(VirtualizationOperandSnapshot).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.SetMethod is not null);
    }

    [Fact]
    public void SnapshotValidation_DeniesStaleRestoreCarrierMutationAndDigestMismatch()
    {
        (VmxMicroOp vmx, SafetyVerifier.VirtualizationAdmissionCertificate e1) =
            CreateAdmittedVmCall();
        VirtualizationOperationOwnerSnapshot o1 =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandSnapshot snapshot = Assert.IsType<VirtualizationOperandSnapshot>(
            new VirtualizationOperandSnapshotMaterializer().CaptureAfterValidatedE1(
                vmx,
                e1,
                1,
                9,
                o1).Snapshot);
        vmx.AttachVirtualizationOperandSnapshot(snapshot);

        Assert.True(VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
            snapshot, vmx, e1, o1, 9).IsValidForE2Input);
        Assert.Equal(
            VirtualizationOperandValidationDecision.RestoreGenerationMismatch,
            VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                snapshot, vmx, e1, o1, 10).Decision);
        vmx.Rs1 = 6;
        Assert.Equal(
            VirtualizationOperandValidationDecision.IdentityMismatch,
            VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                snapshot, vmx, e1, o1, 9).Decision);

        (VmxMicroOp digestCarrier, SafetyVerifier.VirtualizationAdmissionCertificate digestE1) =
            CreateAdmittedVmCall();
        VirtualizationOperandSnapshot valid = Assert.IsType<VirtualizationOperandSnapshot>(
            new VirtualizationOperandSnapshotMaterializer().CaptureAfterValidatedE1(
                digestCarrier,
                digestE1,
                1,
                9,
                o1).Snapshot);
        ConstructorInfo constructor = Assert.Single(
            typeof(VirtualizationOperandSnapshot).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic));
        var malformed = Assert.IsType<VirtualizationOperandSnapshot>(constructor.Invoke(new object[]
        {
            valid.AttemptId,
            valid.E1IssuerGeneration,
            valid.VirtualThreadId,
            valid.OwnerContextId,
            valid.DomainTag,
            valid.Rs1Selector,
            valid.Rs1Value,
            valid.Rs2Selector,
            valid.Rs2Value,
            valid.RdSelector,
            valid.SourceSlotId,
            valid.WorkingSlotId,
            valid.BundleIdentity,
            valid.ReplayEpoch,
            valid.CarrierIdentityDigest,
            valid.RestoreGeneration,
            valid.CaptureSequence,
            valid.OwnerPolicyDigest,
            new string('0', 64),
        }));
        digestCarrier.AttachVirtualizationOperandSnapshot(malformed);
        Assert.Equal(
            VirtualizationOperandValidationDecision.DigestMismatch,
            VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                malformed, digestCarrier, digestE1, o1, 9).Decision);
    }

    private static void AssertCaptureDecision(
        VirtualizationOperandCaptureDecision expected,
        ulong rs1Value,
        byte rs1 = 5,
        byte rs2 = 0,
        byte rd = 0,
        ulong domainTag = 7,
        int sourceSlot = 7,
        int workingSlot = 7,
        ulong restoreGeneration = 1)
    {
        (VmxMicroOp vmx, SafetyVerifier.VirtualizationAdmissionCertificate e1) =
            CreateAdmittedVmCall(rs1, rs2, rd, domainTag, sourceSlot, workingSlot);
        VirtualizationOperandCaptureResult result =
            new VirtualizationOperandSnapshotMaterializer().CaptureAfterValidatedE1(
                vmx,
                e1,
                rs1Value,
                restoreGeneration,
                Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot);

        Assert.False(result.IsCaptured);
        Assert.Null(result.Snapshot);
        Assert.Equal(expected, result.Decision);
    }

    private static (VmxMicroOp, SafetyVerifier.VirtualizationAdmissionCertificate) CreateAdmittedVmCall(
        byte rs1 = 5,
        byte rs2 = 0,
        byte rd = 0,
        ulong domainTag = 7,
        int sourceSlot = 7,
        int workingSlot = 7)
    {
        var verifier = new SafetyVerifier();
        VmxMicroOp vmx = CreateVmCall(rs1, domainTag, rs2, rd);
        ReplayPhaseContext phase = CreateReplayPhase();
        SmtBundleMetadata4Way bundle = CreateBundleMetadata(domainTag);
        VirtualizationAdmissionIssueResult issue =
            verifier.IssueVirtualizationAdmissionAfterStageB(
                phase,
                bundle,
                vmx,
                sourceSlot,
                workingSlot);
        SafetyVerifier.VirtualizationAdmissionCertificate e1 =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(issue.Certificate);
        vmx.AttachVirtualizationAdmission(e1);
        return (vmx, e1);
    }

    private static VmxMicroOp CreateVmCall(
        byte rs1,
        ulong domainTag,
        byte rs2 = 0,
        byte rd = 0)
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = domainTag };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static ReplayPhaseContext CreateReplayPhase() =>
        new(
            isActive: true,
            epochId: 17,
            cachedPc: 0x4000,
            epochLength: 1,
            completedReplays: 0,
            validSlotCount: 0,
            stableDonorMask: 0,
            ReplayPhaseInvalidationReason.None);

    private static SmtBundleMetadata4Way CreateBundleMetadata(ulong domainTag) =>
        new(
            ownerVirtualThreadId: 0,
            ownerContextId: 42,
            ownerDomainTag: domainTag,
            bundleDomainXor: domainTag,
            bundleDomainSum: domainTag,
            operationCount: 1);

    private static IssuePacketLane CreateLane(VmxMicroOp vmx) =>
        new(
            physicalLaneIndex: 7,
            isOccupied: true,
            slotIndex: 7,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: IsaOpcodeValues.VMCALL,
            microOp: vmx,
            requiredSlotClass: SlotClass.SystemSingleton,
            pinningKind: SlotPinningKind.HardPinned,
            countsTowardScalarProjection: false);

    private static BundleIssuePacket CreateIssuePacket(IssuePacketLane lane7) =>
        new(
            pc: 0x7000,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0x80,
            scalarCandidateMask: 0,
            scalarIssueMask: 0,
            selectedSlotMask: 0x80,
            unmappedSelectedSlotMask: 0,
            preparedScalarMask: 0,
            refinedPreparedScalarMask: 0,
            advisoryScalarIssueWidth: 0,
            refinedAdvisoryScalarIssueWidth: 0,
            executionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared,
            shouldProbeClusterPath: false,
            usesIssuePacketAsExecutionSource: true,
            retainsReferenceSequentialPath: false,
            IssuePacketLane.CreateEmpty(0),
            IssuePacketLane.CreateEmpty(1),
            IssuePacketLane.CreateEmpty(2),
            IssuePacketLane.CreateEmpty(3),
            IssuePacketLane.CreateEmpty(4),
            IssuePacketLane.CreateEmpty(5),
            IssuePacketLane.CreateEmpty(6),
            lane7,
            BundleIssueFallbackInfo.CreateEmpty());
}
