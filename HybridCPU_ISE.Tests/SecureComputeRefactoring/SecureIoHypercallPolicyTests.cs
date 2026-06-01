using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureIoHypercallPolicyTests
{
    private const ulong DmaGrantMask = 1UL << 45;
    private const ulong HypercallGrantMask = 1UL << 46;

    [Fact]
    public void SecureDomainAdmission_MissingIoAndHypercallPolicyDeniedOnlyForSecureOperations()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor();
        var policy = new SecureDomainAdmissionPolicy();

        SecureDomainAdmissionResult ordinary = policy.Admit(
            descriptor,
            SecureDomainOperationClass.Ordinary,
            measurement: null,
            memory: null);
        SecureDomainAdmissionResult secureIo = policy.Admit(
            descriptor,
            SecureDomainOperationClass.SecureIo,
            measurement: null,
            memory: null);
        SecureDomainAdmissionResult secureHypercall = policy.Admit(
            descriptor,
            SecureDomainOperationClass.SecureHypercall,
            measurement: null,
            memory: null);

        Assert.True(ordinary.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedMissingIoPolicy, secureIo.Decision);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedMissingHypercallPolicy, secureHypercall.Decision);
    }

    [Fact]
    public void SecureIo_DmaToPrivateMemoryDeniedAndSharedBufferRequiresTypedGrant()
    {
        var policy = SecureIoHypercallAdmissionPolicy.Default;
        SecureIoDomainDescriptor io = CreateIoPolicy(neutralOwner: true);
        SecureMemoryAccessRequest sharedWithoutGrant = SharedDmaAccess(CapabilityDescriptorSet.Empty);
        SecureMemoryAccessRequest sharedWithGrant = SharedDmaAccess(CreateCapabilities(DmaGrantMask));

        SecureIoHypercallAdmissionResult privateAccess = policy.AdmitIoDma(
            io,
            CreatePrivateMemory(),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.DmaWrite,
                Address: 0x1000,
                Length: 0x20,
                Origin: SecureMemoryAccessOrigin.IoDma,
                CapabilityRequirement: DmaRequirement(),
                Capabilities: CreateCapabilities(DmaGrantMask)),
            CompletionFence(),
            requireRetirePublication: false);
        SecureIoHypercallAdmissionResult missingGrant = policy.AdmitIoDma(
            io,
            CreateSharedMemory(),
            sharedWithoutGrant,
            CompletionFence(),
            requireRetirePublication: false);
        SecureIoHypercallAdmissionResult allowed = policy.AdmitIoDma(
            io,
            CreateSharedMemory(),
            sharedWithGrant,
            CompletionFence(),
            requireRetirePublication: false);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedPrivateMemoryAccess, privateAccess.Decision);
        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant, missingGrant.Decision);
        Assert.True(allowed.IsAllowed);
        Assert.Equal(SecureIoHypercallAdmissionDecision.AllowedIo, allowed.Decision);
    }

    [Fact]
    public void SecureIo_MissingNeutralOwnerAndPublicationFenceDeny()
    {
        var policy = SecureIoHypercallAdmissionPolicy.Default;
        SecureMemoryAccessRequest access = SharedDmaAccess(CreateCapabilities(DmaGrantMask));

        SecureIoHypercallAdmissionResult missingOwner = policy.AdmitIoDma(
            CreateIoPolicy(neutralOwner: false),
            CreateSharedMemory(),
            access,
            CompletionFence(),
            requireRetirePublication: false);
        SecureIoHypercallAdmissionResult missingCompletionFence = policy.AdmitIoDma(
            CreateIoPolicy(neutralOwner: true),
            CreateSharedMemory(),
            access,
            SecureCompletionPublicationFence.Denied,
            requireRetirePublication: false);
        SecureIoHypercallAdmissionResult completionOnly = policy.AdmitIoDma(
            CreateIoPolicy(neutralOwner: true),
            CreateSharedMemory(),
            access,
            CompletionFence(),
            requireRetirePublication: true);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedMissingIoOwner, missingOwner.Decision);
        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedCompletionFence, missingCompletionFence.Decision);
        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedRetireFence, completionOnly.Decision);
    }

    [Fact]
    public void SecureHypercall_RawPrivatePointerAndForgedOpaqueHandleDenied()
    {
        var policy = SecureIoHypercallAdmissionPolicy.Default;
        SecureHypercallDescriptor rawPrivatePointer = CreateHypercallPolicy(
            new SecureHypercallArgumentDescriptor(
                0,
                SecureHypercallArgumentClass.RawPrivatePointerDenied,
                SharedBufferId: 0,
                Grant: SecureGrantHandle.None));
        SecureHypercallDescriptor forgedOpaqueHandle = CreateHypercallPolicy(
            new SecureHypercallArgumentDescriptor(
                0,
                SecureHypercallArgumentClass.OpaqueHandle,
                SharedBufferId: 0,
                Grant: SecureGrantHandle.None));

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedRawPrivatePointer,
            policy.AdmitHypercall(
                rawPrivatePointer,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedForgedOpaqueHandle,
            policy.AdmitHypercall(
                forgedOpaqueHandle,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
    }

    [Fact]
    public void SecureHypercall_MissingBackendOwnerTypedGrantAndEvidenceDeny()
    {
        var policy = SecureIoHypercallAdmissionPolicy.Default;
        SecureHypercallDescriptor hypercall = CreateHypercallPolicy();

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedMissingNeutralBackendOwner,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: false,
                domainValidated: true).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: 0x10,
                CapabilityDescriptorSet.Empty,
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedEvidence,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicyDescriptor.FailClosed,
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
    }

    [Fact]
    public void SecureHypercall_SharedBufferArgumentRequiresIoPolicyAndCurrentGrant()
    {
        var policy = SecureIoHypercallAdmissionPolicy.Default;
        SecureHypercallDescriptor hypercall = CreateHypercallPolicy(
            new SecureHypercallArgumentDescriptor(
                0,
                SecureHypercallArgumentClass.ExplicitSharedBuffer,
                SharedBufferId: 1,
                Grant: new SecureGrantHandle(
                    SecureGrantHandleKind.IoPolicy,
                    LocalId: 1,
                    ProvenanceHash: 0x51,
                    Epoch: 7)));
        SecureHypercallDescriptor staleGrant = CreateHypercallPolicy(
            new SecureHypercallArgumentDescriptor(
                0,
                SecureHypercallArgumentClass.ExplicitSharedBuffer,
                SharedBufferId: 1,
                Grant: new SecureGrantHandle(
                    SecureGrantHandleKind.IoPolicy,
                    LocalId: 1,
                    ProvenanceHash: 0x51,
                    Epoch: 6)));

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedMissingIoOwner,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: false),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
            policy.AdmitHypercall(
                staleGrant,
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true).Decision);
    }

    [Fact]
    public void SecureHypercall_AdmittedDeniedDoesNotAuthorizeBackendSuccess()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(),
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsAdmittedDenied);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.True(result.CompletionPublicationAuthorized);
        Assert.True(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void SecureHypercall_CompletionFenceAloneDoesNotImplyRetirePublication()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(),
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                CompletionFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedRetireFence, result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureHypercall_BackendSuccessRemainsClosedEvenWhenDescriptorRequestsIt()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(allowBackendExecution: true),
                hypercallId: 0x10,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedBackendSuccessClosed, result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureIoHypercallPolicies_ContributeToMeasurementPolicyDigest()
    {
        SecureComputeDomainDescriptor baseline = CreateSecureDescriptor();
        SecureComputeDomainDescriptor withBoundaryPolicies = CreateSecureDescriptor(
            CreateIoPolicy(neutralOwner: true),
            CreateHypercallPolicy());

        Assert.NotEqual(
            DomainMeasurementDescriptor.ComputePolicyDigest(baseline),
            DomainMeasurementDescriptor.ComputePolicyDigest(withBoundaryPolicies));
    }

    [Fact]
    public void SecureIoHypercallSources_DoNotCreateVmxVmcsOrVmxCapsAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Io/SecureIoDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs");

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.Contains("DeniedBackendSuccessClosed", source);
    }

    private static SecureComputeDomainDescriptor CreateSecureDescriptor(
        SecureIoDomainDescriptor? ioPolicy = null,
        SecureHypercallDescriptor? hypercallPolicy = null) =>
        new(
            7,
            SecureComputeSecurityLevel.Private,
            measurementRequired: false,
            privateMemoryRequired: false,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            ioPolicy ?? SecureIoDomainDescriptor.Disabled,
            hypercallPolicy ?? SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);

    private static SecureHypercallDescriptor CreateHypercallPolicy(
        params SecureHypercallArgumentDescriptor[] arguments) =>
        CreateHypercallPolicy(false, arguments);

    private static SecureHypercallDescriptor CreateHypercallPolicy(
        bool allowBackendExecution,
        params SecureHypercallArgumentDescriptor[] arguments) =>
        new(
            neutralBackendOwnerRequired: true,
            allowBackendExecution,
            allowedHypercallIds: new[] { 0x10UL },
            requiredGrant: new SecureGrantHandle(
                SecureGrantHandleKind.HypercallPolicy,
                LocalId: HypercallGrantMask,
                ProvenanceHash: 0xA11,
                Epoch: 7),
            arguments,
            requireEvidenceApproval: true,
            requireCompletionFence: true,
            requireRetirePublicationRule: true);

    private static SecureIoDomainDescriptor CreateIoPolicy(bool neutralOwner) =>
        new(
            SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
            new[] { SharedBuffer() },
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: neutralOwner);

    private static SecureSharedBufferDescriptor SharedBuffer() =>
        new(
            BufferId: 1,
            Start: 0x2000,
            Length: 0x100,
            Direction: SecureSharedBufferDirection.DeviceToDomain,
            Grant: new SecureGrantHandle(
                SecureGrantHandleKind.IoPolicy,
                LocalId: 1,
                ProvenanceHash: 0x51,
                Epoch: 7),
            EvidenceClass: SecureEvidenceVisibilityClass.HostOwnedQuarantined,
            OwnerDomainTag: 7,
            LifetimeEpoch: 7);

    private static SecureMemoryDomainDescriptor CreatePrivateMemory() =>
        new(
            7,
            9,
            new SecureRevocationEpoch(7),
            new[]
            {
                new SecureMemoryRegionDescriptor(
                    SecureMemoryRegionClass.Private,
                    0x1000,
                    0x100,
                    SecureMemoryHostVisibility.Denied,
                    7),
            },
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant);

    private static SecureMemoryDomainDescriptor CreateSharedMemory() =>
        new(
            7,
            9,
            new SecureRevocationEpoch(7),
            new[]
            {
                new SecureMemoryRegionDescriptor(
                    SecureMemoryRegionClass.Shared,
                    0x2000,
                    0x100,
                    SecureMemoryHostVisibility.ExplicitShared,
                    7),
            },
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant);

    private static SecureMemoryAccessRequest SharedDmaAccess(CapabilityDescriptorSet capabilities) =>
        new(
            SecureMemoryAccessKind.DmaWrite,
            Address: 0x2000,
            Length: 0x40,
            Origin: SecureMemoryAccessOrigin.IoDma,
            CapabilityRequirement: DmaRequirement(),
            Capabilities: capabilities);

    private static CapabilityBoundaryRequirement DmaRequirement() =>
        CapabilityBoundaryRequirement.TypedGrant(DmaGrantMask, CapabilityGrantScope.DomainGranted);

    private static CapabilityDescriptorSet CreateCapabilities(ulong capabilityMask) =>
        new(new CapabilityGrantCollection(new[]
        {
            new CapabilityGrant(
                capabilityMask,
                CapabilityGrantScope.DomainGranted,
                true,
                7,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.NeverProject),
        }));

    private static EvidencePolicyDescriptor EvidencePolicy() =>
        new(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

    private static SecureCompletionPublicationFence CompletionFence() =>
        new(
            SecureCompletionFenceState.CompletionAllowed,
            SecureRetirePublicationRule.CompletionFenceRequired);

    private static SecureCompletionPublicationFence RetireFence() =>
        new(
            SecureCompletionFenceState.RetireAllowed,
            SecureRetirePublicationRule.ExplicitRetireFenceRequired);

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
