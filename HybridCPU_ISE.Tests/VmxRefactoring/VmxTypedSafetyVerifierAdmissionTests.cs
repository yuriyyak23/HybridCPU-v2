using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxTypedSafetyVerifierAdmissionTests
{
    [Fact]
    public void SafetyVerifier_IssuesOpaqueAttemptBoundCertificateWithNoBackendAuthority()
    {
        var verifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 17, cachedPc: 0x4000);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VmxMicroOp vmx = CreateVmCall();

        VirtualizationAdmissionIssueResult issue =
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, vmx, sourceSlotId: 7, selectedLane: 7);

        Assert.True(issue.IsIssued);
        SafetyVerifier.VirtualizationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(issue.Certificate);
        Assert.Equal(VirtualizationAdmissionIssueDecision.IssuedForFaultOnlyTransport, issue.Decision);
        Assert.Equal((ushort)IsaOpcodeValues.VMCALL, certificate.Opcode);
        Assert.Equal(1U, certificate.SchemaVersion);
        Assert.Equal(VmxOperationKind.VmCall, certificate.Operation);
        Assert.Equal(0, certificate.VirtualThreadId);
        Assert.Equal(42, certificate.OwnerContextId);
        Assert.Equal(7, certificate.SourceSlotId);
        Assert.Equal(7, certificate.WorkingSlotId);
        Assert.Equal(17UL, certificate.ReplayEpoch);
        Assert.Equal(certificate.ReplayEpoch, certificate.AttemptEpoch);
        Assert.Equal(certificate.IssuerGeneration, certificate.ReplayGeneration);
        Assert.NotEqual(0UL, certificate.AttemptId);
        Assert.NotEqual(0UL, certificate.BundleIdentity);
        Assert.NotEqual(0UL, certificate.CarrierIdentityDigest);

        Assert.False(certificate.HasAcceptedNumericLeaf);
        Assert.False(certificate.HasMaterializedAddressSpaceIdentity);
        Assert.False(certificate.HasMaterializedDescriptorIdentity);
        Assert.False(certificate.HasCapabilityGrantIdentity);
        Assert.False(certificate.HasEvidencePolicyIdentity);
        Assert.False(certificate.HasRestoreGeneration);
        Assert.False(certificate.BackendExecutionAuthorized);
        Assert.False(certificate.CompletionPublicationAuthorized);
        Assert.False(certificate.RetirePublicationAuthorized);

        VirtualizationAdmissionValidationResult validation =
            verifier.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
        Assert.True(validation.IsValidForFaultOnlyTransport);
    }

    [Fact]
    public void Certificate_HasNoPublicOrParameterlessConstructionPath()
    {
        Type type = typeof(SafetyVerifier.VirtualizationAdmissionCertificate);

        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(
            type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic),
            constructor => constructor.GetParameters().Length == 0);
    }

    [Fact]
    public void Validation_DeniesForeignIssuerMutationAndInvalidatedGeneration()
    {
        var issuer = new SafetyVerifier();
        var foreignVerifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 23, cachedPc: 0x5000);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VmxMicroOp vmx = CreateVmCall();
        SafetyVerifier.VirtualizationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
                issuer.IssueVirtualizationAdmissionAfterStageB(phase, bundle, vmx, 7, 7).Certificate);

        VirtualizationAdmissionValidationResult foreign =
            foreignVerifier.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
        Assert.Equal(VirtualizationAdmissionValidationDecision.IssuerMismatch, foreign.Decision);

        vmx.Rs1 = 9;
        VirtualizationAdmissionValidationResult mutated =
            issuer.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
        Assert.Equal(VirtualizationAdmissionValidationDecision.OpcodeOrOperationMismatch, mutated.Decision);

        vmx.Rs1 = 0;
        issuer.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
        VirtualizationAdmissionValidationResult invalidated =
            issuer.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
        Assert.Equal(VirtualizationAdmissionValidationDecision.IssuerGenerationMismatch, invalidated.Decision);
    }

    [Fact]
    public void Validation_DeniesCopiedInstanceAndCrossDomainReuse()
    {
        var verifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 27, cachedPc: 0x5A00);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VmxMicroOp vmx = CreateVmCall();
        SafetyVerifier.VirtualizationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
                verifier.IssueVirtualizationAdmissionAfterStageB(
                    phase,
                    bundle,
                    vmx,
                    7,
                    7).Certificate);

        FieldInfo sealField = typeof(SafetyVerifier.VirtualizationAdmissionCertificate)
            .GetField("_issuerSeal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ConstructorInfo constructor = Assert.Single(
            typeof(SafetyVerifier.VirtualizationAdmissionCertificate)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        var copied = Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
            constructor.Invoke(new object[]
            {
                sealField.GetValue(certificate)!,
                certificate.IssuerGeneration,
                certificate.AttemptId,
                certificate.Opcode,
                certificate.Operation,
                certificate.VirtualThreadId,
                certificate.OwnerContextId,
                certificate.DomainTag,
                certificate.SourceSlotId,
                certificate.WorkingSlotId,
                certificate.BundleIdentity,
                certificate.ReplayEpoch,
                certificate.CarrierIdentityDigest,
            }));
        Assert.Equal(
            VirtualizationAdmissionValidationDecision.IssuanceNotLive,
            verifier.ValidateVirtualizationAdmission(
                phase,
                bundle,
                vmx,
                7,
                7,
                copied).Decision);

        vmx.Placement = new SlotPlacementMetadata
        {
            RequiredSlotClass = SlotClass.SystemSingleton,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = 7,
            DomainTag = 0xAA55,
        };
        Assert.Equal(
            VirtualizationAdmissionValidationDecision.DomainMismatch,
            verifier.ValidateVirtualizationAdmission(
                phase,
                bundle,
                vmx,
                7,
                7,
            certificate).Decision);
    }

    [Fact]
    public void Validation_DeniesCrossVirtualThreadBundleAndReplayReuse()
    {
        var verifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 28, cachedPc: 0x5C00);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VmxMicroOp vmx = CreateVmCall();
        SafetyVerifier.VirtualizationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
                verifier.IssueVirtualizationAdmissionAfterStageB(
                    phase,
                    bundle,
                    vmx,
                    7,
                    7).Certificate);

        ReplayPhaseContext differentReplay = CreateReplayPhase(
            epochId: 29,
            cachedPc: 0x5C00);
        Assert.Equal(
            VirtualizationAdmissionValidationDecision.BundleOrReplayMismatch,
            verifier.ValidateVirtualizationAdmission(
                differentReplay,
                bundle,
                vmx,
                7,
                7,
                certificate).Decision);

        SmtBundleMetadata4Way differentBundle = new(
            ownerVirtualThreadId: 0,
            ownerContextId: 42,
            ownerDomainTag: 0,
            bundleDomainXor: 1,
            bundleDomainSum: 1,
            operationCount: 1);
        Assert.Equal(
            VirtualizationAdmissionValidationDecision.BundleOrReplayMismatch,
            verifier.ValidateVirtualizationAdmission(
                phase,
                differentBundle,
                vmx,
                7,
                7,
                certificate).Decision);

        vmx.VirtualThreadId = 1;
        vmx.OwnerThreadId = 1;
        Assert.Equal(
            VirtualizationAdmissionValidationDecision.VirtualThreadMismatch,
            verifier.ValidateVirtualizationAdmission(
                phase,
                bundle,
                vmx,
                7,
                7,
                certificate).Decision);
    }

    [Fact]
    public void Issuance_DeniesInstructionCarrierOpcodeMismatch()
    {
        var verifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 25, cachedPc: 0x5800);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VmxMicroOp vmx = CreateVmCall();
        vmx.Instruction = vmx.Instruction! with
        {
            CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMREAD),
        };

        Assert.Equal(
            VirtualizationAdmissionIssueDecision.UnknownFrozenOpcode,
            verifier.IssueVirtualizationAdmissionAfterStageB(
                phase,
                bundle,
                vmx,
                7,
                7).Decision);
        Assert.Null(vmx.VirtualizationAdmission);
    }

    [Fact]
    public void Issuance_DeniesCrossOwnerWrongLaneAndDuplicateAttempt()
    {
        var verifier = new SafetyVerifier();
        ReplayPhaseContext phase = CreateReplayPhase(epochId: 29, cachedPc: 0x6000);
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();

        VmxMicroOp wrongOwner = CreateVmCall();
        wrongOwner.OwnerThreadId = 1;
        Assert.Equal(
            VirtualizationAdmissionIssueDecision.OwnerVirtualThreadMismatch,
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, wrongOwner, 7, 7).Decision);

        VmxMicroOp wrongLane = CreateVmCall();
        Assert.Equal(
            VirtualizationAdmissionIssueDecision.WorkingSlotMismatch,
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, wrongLane, 7, 6).Decision);

        VmxMicroOp duplicate = CreateVmCall();
        VirtualizationAdmissionIssueResult first =
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, duplicate, 7, 7);
        duplicate.AttachVirtualizationAdmission(
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(first.Certificate));
        Assert.Equal(
            VirtualizationAdmissionIssueDecision.DuplicateAttempt,
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, duplicate, 7, 7).Decision);
    }

    [Fact]
    public void CanonicalIssuePacketLane7_TransportsCertificateButExecutionAndRetireRemainFaultOnly()
    {
        VmxMicroOp vmx = CreateVmCall();
        IssuePacketLane lane7 = new(
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
        BundleIssuePacket packet = CreateIssuePacket(lane7);
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.InitializePipeline();
        MethodInfo materialize = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core)
            .GetMethod(
                "ResolveMaterializedIssuePacketLane",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        IssuePacketLane materialized = Assert.IsType<IssuePacketLane>(
            materialize.Invoke(core, new object[] { packet, (byte)7, (byte)0x80 }));
        Assert.True(materialized.IsOccupied);
        Assert.Same(vmx, materialized.MicroOp);
        Assert.NotNull(vmx.VirtualizationAdmission);
        Assert.False(vmx.VirtualizationAdmission.BackendExecutionAuthorized);

        Assert.True(vmx.Execute(ref core));
        VmxRetireEffect effect = vmx.CreateRetireEffect();
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);

        VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(effect, 0);
        Assert.True(outcome.Faulted);
        Assert.False(outcome.HasRegisterWriteback);
    }

    [Fact]
    public void CanonicalMaterialization_DeniesNonCanonicalSourceSlotWithoutMintingCertificate()
    {
        var scheduler = new MicroOpScheduler();
        VmxMicroOp vmx = CreateVmCall();
        IssuePacketLane displacedLane = new(
            physicalLaneIndex: 7,
            isOccupied: true,
            slotIndex: 6,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: IsaOpcodeValues.VMCALL,
            microOp: vmx,
            requiredSlotClass: SlotClass.SystemSingleton,
            pinningKind: SlotPinningKind.HardPinned,
            countsTowardScalarProjection: false);

        Assert.False(
            scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
                CreateIssuePacket(displacedLane),
                displacedLane));
        Assert.Null(vmx.VirtualizationAdmission);
    }

    [Fact]
    public void CompatibilityDecodeBooleans_DoNotMintCertificate()
    {
        var boundary = new VmxCompatDecodeBoundary();
        VmxCompatDecodeResult decode = boundary.Decode(new VmxCompatDecodeRequest(
            Opcode: IsaOpcodeValues.VMCALL,
            Rd: 0,
            Rs1: 0,
            Rs2: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true));
        VmxMicroOp vmx = CreateVmCall();

        Assert.True(decode.IsAllowed);
        Assert.Null(vmx.VirtualizationAdmission);
    }

    [Fact]
    public void PublicRuntimeLegalityInterface_DoesNotExposeE1IssuanceOrValidation()
    {
        string[] publicMethods = typeof(IRuntimeLegalityService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("IssueVirtualizationAdmissionAfterStageB", publicMethods);
        Assert.DoesNotContain("ValidateVirtualizationAdmission", publicMethods);
    }

    private static VmxMicroOp CreateVmCall()
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static ReplayPhaseContext CreateReplayPhase(ulong epochId, ulong cachedPc) =>
        new(
            isActive: true,
            epochId,
            cachedPc,
            epochLength: 1,
            completedReplays: 0,
            validSlotCount: 0,
            stableDonorMask: 0,
            ReplayPhaseInvalidationReason.None);

    private static SmtBundleMetadata4Way CreateBundleMetadata() =>
        new(
            ownerVirtualThreadId: 0,
            ownerContextId: 42,
            ownerDomainTag: 0,
            bundleDomainXor: 0,
            bundleDomainSum: 0,
            operationCount: 0);

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
