using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureAuthorityDisciplineTests
{
    private const ulong HypercallGrantMask = 1UL << 46;

    [Fact]
    public void SecureGrantAuthority_ValidScalarShapeWithoutProvenanceIsDenied()
    {
        SecureGrantAuthorityResult result = ValidateGrant(
            new SecureGrantHandle(
                SecureGrantHandleKind.HypercallPolicy,
                HypercallGrantMask,
                ProvenanceHash: 0,
                Epoch: 7));

        Assert.Equal(SecureGrantAuthorityDecision.DeniedMissingProvenance, result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_ValidScalarShapeWithStaleEpochIsDenied()
    {
        SecureGrantAuthorityResult result = ValidateGrant(
            new SecureGrantHandle(
                SecureGrantHandleKind.HypercallPolicy,
                HypercallGrantMask,
                ProvenanceHash: 0xC0DE,
                Epoch: 6));

        Assert.Equal(SecureGrantAuthorityDecision.DeniedStaleEpoch, result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_GuestVisibleScalarCannotMaterializeHandle()
    {
        Assert.False(
            SecureGrantHandle.TryMaterializeFromGuestScalar(
                0x7000_0000_0000_0001,
                out SecureGrantHandle materialized));
        Assert.Equal(SecureGrantHandle.None, materialized);

        SecureGrantAuthorityResult result = ValidateGrant(
            ValidGrant(),
            source: SecureGrantMaterializationSource.GuestArchitecturalState,
            runtimeOwnerMaterialized: true);

        Assert.Equal(
            SecureGrantAuthorityDecision.DeniedGuestScalarMaterialization,
            result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_CompatibilityProjectionCannotSatisfySecureAuthority()
    {
        SecureGrantAuthorityResult result = ValidateGrant(
            ValidGrant(),
            source: SecureGrantMaterializationSource.CompatibilityProjection,
            capabilities: CreateCapabilities(CapabilityGrantScope.CompatibilityProjection),
            requiredScope: CapabilityGrantScope.CompatibilityProjection);

        Assert.Equal(
            SecureGrantAuthorityDecision.DeniedCompatibilityProjectionAuthority,
            result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_RevokedGrantFailsAdmission()
    {
        SecureGrantAuthorityResult result = ValidateGrant(
            ValidGrant(),
            grantRevoked: true);

        Assert.Equal(SecureGrantAuthorityDecision.DeniedRevokedGrant, result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_RequestOutsideAuthorityBoundsIsDenied()
    {
        SecureAuthorityBounds granted = Bounds(allowsHypercalls: false);
        SecureAuthorityBounds requested = Bounds(allowsHypercalls: true);

        SecureGrantAuthorityResult result = ValidateGrant(
            ValidGrant(),
            grantedBounds: granted,
            requestedBounds: requested);

        Assert.Equal(SecureGrantAuthorityDecision.DeniedAuthorityBounds, result.Decision);
    }

    [Fact]
    public void SecureGrantAuthority_HostRuntimeAuthorityPlusGuestForgedScalarIsDenied()
    {
        SecureGrantAuthorityResult result = ValidateGrant(
            ValidGrant(),
            source: SecureGrantMaterializationSource.GuestArchitecturalState,
            runtimeOwnerMaterialized: true,
            capabilities: CreateCapabilities(CapabilityGrantScope.DomainGranted));

        Assert.Equal(
            SecureGrantAuthorityDecision.DeniedGuestScalarMaterialization,
            result.Decision);
    }

    [Fact]
    public void SecureChildIntent_WideningIoHypercallOrDebugPolicyIsDenied()
    {
        SecureAuthorityBounds noIo = Bounds(allowsHypercalls: true, allowsDebug: true);
        SecureAuthorityBounds noHypercall = Bounds(allowsIo: true, allowsDebug: true);
        SecureAuthorityBounds noDebug = Bounds(allowsIo: true, allowsHypercalls: true);

        Assert.Equal(
            SecurePolicyDerivationDecision.ChildExpandsAuthority,
            CreateChildIntent(Bounds(allowsIo: true))
                .ValidateMonotonicDerivation(noIo, new SecureRevocationEpoch(7)));
        Assert.Equal(
            SecurePolicyDerivationDecision.ChildExpandsAuthority,
            CreateChildIntent(Bounds(allowsHypercalls: true))
                .ValidateMonotonicDerivation(noHypercall, new SecureRevocationEpoch(7)));
        Assert.Equal(
            SecurePolicyDerivationDecision.ChildExpandsAuthority,
            CreateChildIntent(Bounds(allowsDebug: true))
                .ValidateMonotonicDerivation(noDebug, new SecureRevocationEpoch(7)));
    }

    [Fact]
    public void SecureMigrationRestore_HandleWithoutProvenanceIsDenied()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitRestore(
                CreateMigration(),
                CreateMeasurement(epoch: 7),
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: new SecureGrantHandle(
                    SecureGrantHandleKind.MigrationPolicy,
                    LocalId: 0xA,
                    ProvenanceHash: 0,
                    Epoch: 7),
                measurementRevalidated: true,
                reattestationCompleted: false);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedGrantProvenanceMissing,
            result.Decision);
    }

    [Fact]
    public void SecureMigrationRestore_HandleWithStaleEpochIsDenied()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitRestore(
                CreateMigration(),
                CreateMeasurement(epoch: 7),
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: new SecureGrantHandle(
                    SecureGrantHandleKind.MigrationPolicy,
                    LocalId: 0xA,
                    ProvenanceHash: 0xB,
                    Epoch: 6),
                measurementRevalidated: true,
                reattestationCompleted: false,
                grantProvenanceValidated: true);

        Assert.Equal(SecureMigrationAdmissionDecision.DeniedStaleGrantEpoch, result.Decision);
    }

    [Fact]
    public void SecureMigrationRestore_ValidHandleStillRequiresRestoreTimeProvenanceValidation()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitRestore(
                CreateMigration(),
                CreateMeasurement(epoch: 7),
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: new SecureGrantHandle(
                    SecureGrantHandleKind.MigrationPolicy,
                    LocalId: 0xA,
                    ProvenanceHash: 0xB,
                    Epoch: 7),
                measurementRevalidated: true,
                reattestationCompleted: false,
                grantProvenanceValidated: false);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedGrantRestoreProvenanceRevalidation,
            result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_AbsentSecureComputeLeavesOrdinaryInstructionStreamUnchanged()
    {
        RuntimeBoundaryAdmissionResult result =
            new RuntimeBoundaryAdmissionService().Validate(
                new RuntimeBoundaryAdmissionRequest(
                    Context: CreateRuntimeContext(),
                    RootAuthority: CreateRoot(),
                    EvidencePolicy: new EvidencePolicyDescriptor(
                        allowCompatibilityAliases: false,
                        allowGuestArchitecturalState: true,
                        allowMigrationSerializableState: false),
                    Operation: new DomainRuntimeOperation(
                        DomainRuntimeOperationKind.EnterDomain,
                        DomainRuntimeOperationSource.RuntimeService,
                        requiresCapabilityGrant: false,
                        isProjectionOnly: false),
                    DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
                    CapabilityRequirement: CapabilityBoundaryRequirement.None,
                    EvidenceRequirement: EvidenceBoundaryRequirement.None,
                    SecureDescriptor: null,
                    SecureOperationClass: SecureDomainOperationClass.Ordinary,
                    SecureMeasurement: null,
                    SecureMemory: null));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void Sources_DoNotIntroduceVmxVmcsVmreadOrVmwriteAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Authority/SecureGrantHandle.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Authority/SecureGrantAuthorityPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Authority/SecureAuthorityBounds.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Authority/SecurePolicyDerivationRecord.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs");

        Assert.DoesNotContain("VMCS", source);
        Assert.DoesNotContain("Vmcs", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmRead", source);
        Assert.DoesNotContain("VmWrite", source);
    }

    private static SecureGrantAuthorityResult ValidateGrant(
        SecureGrantHandle handle,
        SecureGrantMaterializationSource source = SecureGrantMaterializationSource.NeutralRuntimeOwner,
        SecureAuthorityBounds? grantedBounds = null,
        SecureAuthorityBounds? requestedBounds = null,
        CapabilityDescriptorSet? capabilities = null,
        CapabilityGrantScope requiredScope = CapabilityGrantScope.DomainGranted,
        bool runtimeOwnerMaterialized = true,
        bool grantRevoked = false) =>
        SecureGrantAuthorityPolicy.Default.Validate(
            handle,
            source,
            grantedBounds ?? Bounds(allowsHypercalls: true),
            requestedBounds ?? Bounds(allowsHypercalls: true),
            SecureGrantEpochSet.Single(new SecureRevocationEpoch(7)),
            runtimeOwnerMaterialized,
            capabilities ?? CreateCapabilities(CapabilityGrantScope.DomainGranted),
            requiredScope,
            grantRevoked);

    private static SecureGrantHandle ValidGrant() =>
        new(
            SecureGrantHandleKind.HypercallPolicy,
            HypercallGrantMask,
            ProvenanceHash: 0xC0DE,
            Epoch: 7);

    private static SecureAuthorityBounds Bounds(
        bool allowsIo = false,
        bool allowsHypercalls = false,
        bool allowsDebug = false) =>
        new(
            allowsPrivateMemory: false,
            allowsSharedMemory: false,
            allowsIo: allowsIo,
            allowsDma: false,
            allowsHypercalls: allowsHypercalls,
            allowsDebug: allowsDebug,
            allowsMigration: false,
            allowsCompatibilityProjection: false);

    private static SecureChildDomainIntentDescriptor CreateChildIntent(
        SecureAuthorityBounds requestedBounds) =>
        new(
            parentDomainTag: 7,
            childDomainTag: 8,
            requestedSecurityLevel: SecureComputeSecurityLevel.Private,
            requestedBounds,
            derivation: new SecurePolicyDerivationRecord(
                ParentPolicyDigest: 0xA,
                ChildPolicyDigest: 0xB,
                ProvenanceHash: 0xC,
                ParentEpoch: 7,
                ChildEpoch: 7),
            state: SecureChildDomainIntentState.Declared);

    private static SecureMigrationDescriptor CreateMigration() =>
        new(
            SecureMigrationMode.PolicyCompatible,
            SecurePrivateMemoryMigrationPolicy.ReinitializeAfterRestore,
            new SecureRevocationEpoch(7),
            allowGuestVisibleEvidence: true,
            allowCompatibilityProjectionMetadata: false,
            SecureMeasurementRestorePolicy.Revalidate,
            SecureGrantRestorePolicy.Rederive);

    private static DomainMeasurementDescriptor CreateMeasurement(ulong epoch) =>
        new(
            new SecureMeasurementHandle(
                MeasurementId: 0x4D,
                ProvenanceHash: 0xA17E,
                Epoch: epoch),
            SecureMeasurementState.Materialized,
            SecureMeasurementDebugClass.Production,
            policyDigest: 0xC0DE,
            memoryDigest: 0,
            runtimeDigest: 0xC0FFEE,
            SecureEvidenceVisibilityClass.GuestVisible,
            creatorDomainTag: 7,
            parentMeasurementId: 0,
            policySourceHash: 0x51);

    private static CapabilityDescriptorSet CreateCapabilities(CapabilityGrantScope scope) =>
        new(new CapabilityGrantCollection(new[]
        {
            new CapabilityGrant(
                HypercallGrantMask,
                scope,
                isGranted: true,
                ownerDomainId: 7,
                delegationPolicy: CapabilityDelegationPolicy.NonDelegable,
                revocationPolicy: CapabilityRevocationPolicy.RuntimeRevocable,
                migrationClass: CapabilityMigrationClass.DomainLocal,
                evidenceVisibility: scope == CapabilityGrantScope.CompatibilityProjection
                    ? CapabilityEvidenceVisibility.GuestVisibleProjection
                    : CapabilityEvidenceVisibility.HostOnly,
                frontendProjectionPolicy: scope == CapabilityGrantScope.CompatibilityProjection
                    ? CapabilityFrontendProjectionPolicy.ProjectIfCompatible
                    : CapabilityFrontendProjectionPolicy.NeverProject),
        }));

    private static DomainRuntimeContext CreateRuntimeContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domainTag: 0,
            addressSpaceTag: 0);

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: true);

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
