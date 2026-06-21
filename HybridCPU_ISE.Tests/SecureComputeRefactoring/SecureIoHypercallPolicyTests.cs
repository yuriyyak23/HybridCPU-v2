using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureIoHypercallPolicyTests
{
    private const ulong DmaGrantMask = 1UL << 45;
    private const ulong HypercallGrantMask = 1UL << 46;
    private static ulong ProductionHypercallId =>
        SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf.Value;

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
        Assert.True(allowed.IsPolicyAdmissionOnly);
        Assert.Equal(SecureIoHypercallAdmissionDecision.AllowedIo, allowed.Decision);
        Assert.False(allowed.BackendExecutionAuthorized);
        Assert.False(allowed.CompletionPublicationAuthorized);
        Assert.False(allowed.RetirePublicationAuthorized);
    }

    [Fact]
    public void SecureIo_FencePresenceDoesNotAuthorizeCompletionOrRetirePublication()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitIoDma(
                CreateIoPolicy(neutralOwner: true),
                CreateSharedMemory(),
                SharedDmaAccess(CreateCapabilities(DmaGrantMask)),
                RetireFence(),
                requireRetirePublication: true);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsPolicyAdmissionOnly);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
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
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: false),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true,
                validatedDomainTag: 7).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedMissingTypedGrant,
            policy.AdmitHypercall(
                staleGrant,
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true,
                validatedDomainTag: 7).Decision);
    }

    [Fact]
    public void SecureHypercall_SharedBufferRequiresCurrentOwnerLifetimeEvidenceAndBufferGrant()
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

        SecureIoHypercallAdmissionResult missingValidatedOwner = AdmitSharedBufferHypercall(
            policy,
            hypercall,
            CreateIoPolicy(neutralOwner: true),
            validatedDomainTag: 0);
        SecureIoHypercallAdmissionResult wrongOwner = AdmitSharedBufferHypercall(
            policy,
            hypercall,
            CreateIoPolicy(
                neutralOwner: true,
                sharedBuffer: SharedBuffer(ownerDomainTag: 8)),
            validatedDomainTag: 7);
        SecureIoHypercallAdmissionResult staleLifetime = AdmitSharedBufferHypercall(
            policy,
            hypercall,
            CreateIoPolicy(
                neutralOwner: true,
                sharedBuffer: SharedBuffer(lifetimeEpoch: 6)),
            validatedDomainTag: 7);
        SecureIoHypercallAdmissionResult staleBufferGrant = AdmitSharedBufferHypercall(
            policy,
            hypercall,
            CreateIoPolicy(
                neutralOwner: true,
                sharedBuffer: SharedBuffer(grantEpoch: 6)),
            validatedDomainTag: 7);
        SecureIoHypercallAdmissionResult deniedEvidence = AdmitSharedBufferHypercall(
            policy,
            hypercall,
            CreateIoPolicy(
                neutralOwner: true,
                sharedBuffer: SharedBuffer(
                    evidenceClass: SecureEvidenceVisibilityClass.Denied)),
            validatedDomainTag: 7);

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            missingValidatedOwner.Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            wrongOwner.Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            staleLifetime.Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            staleBufferGrant.Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            deniedEvidence.Decision);
    }

    [Fact]
    public void SecureHypercall_SharedBufferArgumentRequiresExplicitSharedBufferPolicy()
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
        SecureIoDomainDescriptor deniedIoPolicy = CreateIoPolicy(
            neutralOwner: true,
            dmaPolicy: SecureIoDmaPolicy.Denied);
        SecureIoDomainDescriptor missingSharedBuffer = new(
            SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
            Array.Empty<SecureSharedBufferDescriptor>(),
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: true);

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                deniedIoPolicy,
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true,
                validatedDomainTag: 7).Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding,
            policy.AdmitHypercall(
                hypercall,
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                missingSharedBuffer,
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true,
                validatedDomainTag: 7).Decision);
    }

    [Fact]
    public void SecureIo_SharedDmaRequiresExplicitSharedBufferPolicyNotJustMaterializedBuffer()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitIoDma(
                CreateIoPolicy(
                    neutralOwner: true,
                    dmaPolicy: SecureIoDmaPolicy.Denied),
                CreateSharedMemory(),
                SharedDmaAccess(CreateCapabilities(DmaGrantMask)),
                CompletionFence(),
                requireRetirePublication: false);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedSharedBufferBinding, result.Decision);
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void SecureHypercall_AdmittedDeniedDoesNotAuthorizeBackendCompletionOrRetirePublication()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(),
                hypercallId: ProductionHypercallId,
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
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void SecureHypercall_CompletionFenceAloneDoesNotImplyRetirePublication()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(),
                hypercallId: ProductionHypercallId,
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
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true);

        Assert.Equal(SecureIoHypercallAdmissionDecision.DeniedBackendSuccessClosed, result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
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
        Assert.DoesNotContain("VmCall", source);
        Assert.DoesNotContain("Lane6", source);
        Assert.DoesNotContain("Lane7", source);
        Assert.Contains("DeniedBackendSuccessClosed", source);
        Assert.Contains("IsPolicyAdmissionOnly", source);
        Assert.Contains("TryFindCurrentSharedBuffer", source);
        Assert.DoesNotContain("AllowsSharedBuffer(", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);
        Assert.DoesNotContain("completion.CompletionPublicationAuthorized", source);
        Assert.DoesNotContain("completion.RetirePublicationAuthorized", source);
        Assert.DoesNotContain("publicationFence?.CanPublishCompletion == true", source);
        Assert.DoesNotContain("publicationFence?.CanPublishRetire == true", source);
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
            allowedHypercallIds: new[] { ProductionHypercallId },
            requiredGrant: new SecureGrantHandle(
                SecureGrantHandleKind.HypercallPolicy,
                LocalId: HypercallGrantMask,
                ProvenanceHash: 0xA11,
                Epoch: 7),
            arguments,
            requireEvidenceApproval: true,
            requireCompletionFence: true,
            requireRetirePublicationRule: true);

    private static SecureIoDomainDescriptor CreateIoPolicy(
        bool neutralOwner,
        SecureIoDmaPolicy dmaPolicy = SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
        SecureSharedBufferDescriptor? sharedBuffer = null) =>
        new(
            dmaPolicy,
            new[] { sharedBuffer ?? SharedBuffer() },
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: neutralOwner);

    private static SecureSharedBufferDescriptor SharedBuffer(
        ulong ownerDomainTag = 7,
        ulong lifetimeEpoch = 7,
        ulong grantEpoch = 7,
        SecureEvidenceVisibilityClass evidenceClass =
            SecureEvidenceVisibilityClass.HostOwnedQuarantined) =>
        new(
            BufferId: 1,
            Start: 0x2000,
            Length: 0x100,
            Direction: SecureSharedBufferDirection.DeviceToDomain,
            Grant: new SecureGrantHandle(
                SecureGrantHandleKind.IoPolicy,
                LocalId: 1,
                ProvenanceHash: 0x51,
                Epoch: grantEpoch),
            EvidenceClass: evidenceClass,
            OwnerDomainTag: ownerDomainTag,
            LifetimeEpoch: lifetimeEpoch);

    private static SecureIoHypercallAdmissionResult AdmitSharedBufferHypercall(
        SecureIoHypercallAdmissionPolicy policy,
        SecureHypercallDescriptor hypercall,
        SecureIoDomainDescriptor ioPolicy,
        ulong validatedDomainTag) =>
        policy.AdmitHypercall(
            hypercall,
            hypercallId: ProductionHypercallId,
            CreateCapabilities(HypercallGrantMask),
            EvidencePolicy(),
            ioPolicy,
            new SecureRevocationEpoch(7),
            RetireFence(),
            neutralBackendOwnerMaterialized: true,
            domainValidated: true,
            validatedDomainTag: validatedDomainTag);

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
