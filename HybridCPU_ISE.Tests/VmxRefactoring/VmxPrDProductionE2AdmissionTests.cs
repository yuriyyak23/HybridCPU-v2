using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrDProductionE2AdmissionTests
{
    [Fact]
    public void SafetyVerifier_IssuesOneExactD2BoundE2_WithoutBackendOrPublicationAuthority()
    {
        Fixture fixture = CreateFixture();
        VirtualizationE2Result issued = fixture.Verifier.IssueVirtualizationE2(fixture.Request);
        SafetyVerifier.VirtualizationOperationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(issued.Certificate);

        Assert.True(issued.IsIssued);
        Assert.True(fixture.Verifier.ValidateVirtualizationE2(certificate, fixture.RestoreOwner).IsLive);
        Assert.Equal(VirtualizationE2State.Issued, fixture.Verifier.GetVirtualizationE2State(certificate));
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, certificate.DecisionId);
        Assert.Equal(Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, certificate.SpecDigest);
        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.Record.AcceptanceDigest, certificate.AcceptanceDigest);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, certificate.OwnerId);
        Assert.Equal("HybridCPU.VMCALL.Runtime.v1", certificate.OperationNamespace);
        Assert.Equal("PROBE_NO_STATE_V1", certificate.OperationId);
        Assert.Equal((ushort)1, certificate.NumericLeaf);
        Assert.Equal(fixture.Operand.OperandDigest, certificate.OperandDigest);
        Assert.Equal(fixture.Lease.GrantIdentity, certificate.CapabilityGrantIdentity);
        Assert.Equal(fixture.Lease.Generation, certificate.CapabilityGeneration);
        Assert.Equal(1UL, certificate.RootAuthorityEpoch);
        Assert.Equal(1UL, certificate.RestoreGeneration);
        Assert.False(certificate.HasAddressSpaceIdentity);
        Assert.False(certificate.HasEvidenceIdentity);
        Assert.False(certificate.BackendExecutionAuthorized);
        Assert.False(certificate.CompletionPublicationAuthorized);
        Assert.False(certificate.RetirePublicationAuthorized);
        Assert.Equal(64, certificate.CertificateDigest.Length);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, fixture.Carrier.CreateRetireEffect().FailureReason);
    }

    [Fact]
    public void E2_DeniesDuplicateMissingForeignDomainAndUnexpectedAddressSpace()
    {
        Fixture fixture = CreateFixture();
        Assert.True(fixture.Verifier.IssueVirtualizationE2(fixture.Request).IsIssued);
        Assert.Equal(
            VirtualizationE2Decision.DuplicateAttempt,
            fixture.Verifier.IssueVirtualizationE2(fixture.Request).Decision);
        Assert.Equal(
            VirtualizationE2Decision.MissingInput,
            fixture.Verifier.IssueVirtualizationE2(fixture.Request with { Operand = null }).Decision);

        Fixture foreignDomain = CreateFixture();
        Assert.Equal(
            VirtualizationE2Decision.DomainIdentityMismatch,
            foreignDomain.Verifier.IssueVirtualizationE2(
                foreignDomain.Request with
                {
                    DomainContext = CreateDomainContext(8, foreignDomain.Grant),
                }).Decision);

        Fixture addressSpace = CreateFixture();
        Assert.Equal(
            VirtualizationE2Decision.DomainIdentityMismatch,
            addressSpace.Verifier.IssueVirtualizationE2(
                addressSpace.Request with
                {
                    DomainContext = CreateDomainContext(7, addressSpace.Grant, addressSpaceTag: 9),
                }).Decision);
    }

    [Fact]
    public void E2_DeniesRevokedOrForeignCapabilityAndWrongRootPolicy()
    {
        Fixture revoked = CreateFixture();
        revoked.CapabilityOwner.RevokeAll();
        Assert.Equal(
            VirtualizationE2Decision.CapabilityLeaseNotLive,
            revoked.Verifier.IssueVirtualizationE2(revoked.Request).Decision);

        Fixture foreign = CreateFixture();
        var foreignOwner = new RuntimeCapabilityGrantOwner();
        RuntimeCapabilityGrantLease foreignLease = foreignOwner.Issue(foreign.Grant);
        Assert.Equal(
            VirtualizationE2Decision.CapabilityLeaseNotLive,
            foreign.Verifier.IssueVirtualizationE2(
                foreign.Request with { CapabilityLease = foreignLease }).Decision);

        Fixture frontendRoot = CreateFixture();
        RootAuthorityDescriptor wrongRoot = new(
            RootAuthorityClass.CompatibilityFrontend, 1,
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false);
        Assert.Equal(
            VirtualizationE2Decision.RootAuthorityMismatch,
            frontendRoot.Verifier.IssueVirtualizationE2(
                frontendRoot.Request with { RootAuthority = wrongRoot }).Decision);

        Fixture mutationRoot = CreateFixture();
        RootAuthorityDescriptor overpowered = new(
            RootAuthorityClass.RuntimeRoot, 1,
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: true);
        Assert.Equal(
            VirtualizationE2Decision.RootAuthorityMismatch,
            mutationRoot.Verifier.IssueVirtualizationE2(
                mutationRoot.Request with { RootAuthority = overpowered }).Decision);
    }

    [Fact]
    public void E2_ValidationFailsClosedAfterCapabilityRevocationRestoreOrExplicitRevocation()
    {
        Fixture capability = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate capabilityE2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                capability.Verifier.IssueVirtualizationE2(capability.Request).Certificate);
        capability.CapabilityOwner.RevokeAll();
        Assert.Equal(
            VirtualizationE2Decision.CapabilityLeaseNotLive,
            capability.Verifier.ValidateVirtualizationE2(capabilityE2, capability.RestoreOwner).Decision);

        Fixture restore = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate restoreE2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                restore.Verifier.IssueVirtualizationE2(restore.Request).Certificate);
        restore.RestoreOwner.AdvanceAfterRestore();
        Assert.Equal(
            VirtualizationE2Decision.RestoreGenerationMismatch,
            restore.Verifier.ValidateVirtualizationE2(restoreE2, restore.RestoreOwner).Decision);

        Fixture revoked = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate revokedE2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                revoked.Verifier.IssueVirtualizationE2(revoked.Request).Certificate);
        Assert.True(revoked.Verifier.RevokeVirtualizationE2(revokedE2));
        Assert.False(revoked.Verifier.RevokeVirtualizationE2(revokedE2));
        Assert.Equal(VirtualizationE2State.Revoked, revoked.Verifier.GetVirtualizationE2State(revokedE2));
        Assert.Equal(
            VirtualizationE2Decision.Revoked,
            revoked.Verifier.ValidateVirtualizationE2(revokedE2, revoked.RestoreOwner).Decision);
    }

    [Fact]
    public void E2_ValidationRechecksLiveE1AndCanonicalCarrierIdentity()
    {
        Fixture staleE1 = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate staleE2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                staleE1.Verifier.IssueVirtualizationE2(staleE1.Request).Certificate);
        staleE1.Verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
        Assert.Equal(
            VirtualizationE2Decision.InvalidE1,
            staleE1.Verifier.ValidateVirtualizationE2(staleE2, staleE1.RestoreOwner).Decision);

        Fixture mutated = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate mutatedE2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                mutated.Verifier.IssueVirtualizationE2(mutated.Request).Certificate);
        mutated.Carrier.Rs1 = 6;
        Assert.Equal(
            VirtualizationE2Decision.InvalidE1,
            mutated.Verifier.ValidateVirtualizationE2(mutatedE2, mutated.RestoreOwner).Decision);
    }

    [Fact]
    public void E2_IsNonForgeableAndLegacyBooleanSubstrateRemainsDenied()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);
        Assert.Empty(typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate).GetConstructors());
        Assert.DoesNotContain(
            typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.SetMethod is not null);
        Assert.DoesNotContain(
            typeof(SafetyVerifier).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.ReturnType == typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate));

        VirtualizationOperationAdmissionResult legacy = new SafetyVerifier()
            .EvaluateVirtualizationOperationAdmission(new(
                default,
                default,
                CanonicalRuntimeLeafCaptured: true,
                CapabilityGrantIdentityPresent: true,
                EvidencePolicyIdentityPresent: true,
                AddressSpaceIdentityPresent: true,
                RestoreGenerationPresent: true));
        Assert.False(legacy.IsIssued);
        Assert.Null(legacy.Certificate);
    }

    [Fact]
    public void E2_CommonAdmissionDeniesSecureComputeAndWrongCapabilityPolicy()
    {
        Fixture secure = CreateFixture();
        SecureComputeDomainDescriptor descriptor = new(
            domainTag: 7,
            SecureComputeSecurityLevel.Measured,
            measurementRequired: false,
            privateMemoryRequired: false,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);
        DomainRuntimeContext secureContext = new(
            secure.Request.DomainContext!.Execution,
            memory: null,
            io: null,
            secure.Request.DomainContext.Capabilities,
            descriptor,
            domainTag: 7,
            addressSpaceTag: 0);
        Assert.Equal(
            VirtualizationE2Decision.CommonRuntimeAdmissionDenied,
            secure.Verifier.IssueVirtualizationE2(
                secure.Request with { DomainContext = secureContext }).Decision);

        CapabilityGrant projectedGrant = new(
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            CapabilityGrantScope.DomainGranted,
            isGranted: true,
            ownerDomainId: 7,
            CapabilityDelegationPolicy.NonDelegable,
            CapabilityRevocationPolicy.RuntimeRevocable,
            CapabilityMigrationClass.DomainLocal,
            CapabilityEvidenceVisibility.HostOnly,
            CapabilityFrontendProjectionPolicy.ProjectIfCompatible);
        Fixture projected = CreateFixture(projectedGrant);
        Assert.Equal(
            VirtualizationE2Decision.CapabilityPolicyMismatch,
            projected.Verifier.IssueVirtualizationE2(projected.Request).Decision);
    }

    private static Fixture CreateFixture(CapabilityGrant? suppliedGrant = null)
    {
        var verifier = new SafetyVerifier();
        VmxMicroOp carrier = CreateVmCall();
        ReplayPhaseContext phase = CreateReplayPhase();
        SmtBundleMetadata4Way bundle = CreateBundleMetadata();
        VirtualizationAdmissionIssueResult e1Result =
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, carrier, 7, 7);
        SafetyVerifier.VirtualizationAdmissionCertificate e1 =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(e1Result.Certificate);
        carrier.AttachVirtualizationAdmission(e1);
        VirtualizationOperationOwnerSnapshot owner =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandSnapshot operand = Assert.IsType<VirtualizationOperandSnapshot>(
            new VirtualizationOperandSnapshotMaterializer()
                .CaptureAfterValidatedE1(carrier, e1, 1, 1, owner).Snapshot);
        carrier.AttachVirtualizationOperandSnapshot(operand);

        CapabilityGrant grant = suppliedGrant ?? CreateGrant();
        var capabilityOwner = new RuntimeCapabilityGrantOwner();
        RuntimeCapabilityGrantLease lease = capabilityOwner.Issue(grant);
        DomainRuntimeContext context = CreateDomainContext(7, grant);
        RootAuthorityDescriptor root = new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false);
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(7);
        Assert.True(lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact));
        VirtualizationE2IssueRequest request = new(
            phase, bundle, carrier, 7, 7, e1, owner, operand, context, root,
            capabilityOwner, lease, restoreOwner, lifecycleGate);
        return new(
            verifier, carrier, new YAKSys_Hybrid_CPU.Processor.CPU_Core(0), operand,
            grant, capabilityOwner, lease, restoreOwner, lifecycleGate, request);
    }

    private static CapabilityGrant CreateGrant() => new(
        RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
        CapabilityGrantScope.DomainGranted,
        isGranted: true,
        ownerDomainId: 7,
        CapabilityDelegationPolicy.NonDelegable,
        CapabilityRevocationPolicy.RuntimeRevocable,
        CapabilityMigrationClass.DomainLocal,
        CapabilityEvidenceVisibility.HostOnly,
        CapabilityFrontendProjectionPolicy.NeverProject);

    private static DomainRuntimeContext CreateDomainContext(
        ulong domainTag,
        CapabilityGrant grant,
        ulong addressSpaceTag = 0) => new(
            new ExecutionDomainDescriptor(
                domainTag,
                new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                schedulingBudget: null,
                extension: null,
                compatibilityProjectionEnabled: false),
            memory: null,
            io: null,
            new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
            secureCompute: null,
            domainTag,
            addressSpaceTag);

    private static VmxMicroOp CreateVmCall()
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 5,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 5,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = 7 };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static ReplayPhaseContext CreateReplayPhase() => new(
        isActive: true, epochId: 17, cachedPc: 0x4000, epochLength: 1,
        completedReplays: 0, validSlotCount: 0, stableDonorMask: 0,
        ReplayPhaseInvalidationReason.None);

    private static SmtBundleMetadata4Way CreateBundleMetadata() => new(
        ownerVirtualThreadId: 0, ownerContextId: 42, ownerDomainTag: 7,
        bundleDomainXor: 7, bundleDomainSum: 7, operationCount: 1);

    private sealed record Fixture(
        SafetyVerifier Verifier,
        VmxMicroOp Carrier,
        YAKSys_Hybrid_CPU.Processor.CPU_Core Core,
        VirtualizationOperandSnapshot Operand,
        CapabilityGrant Grant,
        RuntimeCapabilityGrantOwner CapabilityOwner,
        RuntimeCapabilityGrantLease Lease,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        DomainHypercallLifecycleGate LifecycleGate,
        VirtualizationE2IssueRequest Request);
}
