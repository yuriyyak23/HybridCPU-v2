using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureMemoryDomainPolicyTests
{
    private const ulong DmaGrantMask = 1UL << 45;

    [Fact]
    public void RuntimeBoundaryAdmission_PrivateMemoryRequiredDeniesMaterializedPolicyWithoutPrivateRegion()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            privateMemoryRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            SharedRegion());

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                memory,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory,
                secureMemoryAccess: null));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("private memory policy", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecureMemoryPolicy_PrivateMemoryIsNotHostReadable()
    {
        SecureMemoryAdmissionResult result = new SecureMemoryAdmissionPolicy().Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, PrivateRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.HostRead,
                Address: 0x1000,
                Length: 0x80));

        Assert.False(result.IsAllowed);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedPrivateHostRead, result.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_MissingUnmaterializedOrStaleDescriptorDenied()
    {
        var policy = new SecureMemoryAdmissionPolicy();
        var access = new SecureMemoryAccessRequest(
            SecureMemoryAccessKind.RuntimeTouch,
            Address: 0x4000,
            Length: 0x20);
        var staleRegion = new SecureMemoryRegionDescriptor(
            SecureMemoryRegionClass.RuntimeMutable,
            0x4000,
            0x100,
            SecureMemoryHostVisibility.Denied,
            PolicyEpoch: 6,
            SecureRuntimeMutableDirtyPolicy.TrackDirtyPages,
            SecureRuntimeMutableMigrationClass.SealedPayloadRequired);

        SecureMemoryAdmissionResult missing = policy.Admit(null, access);
        SecureMemoryAdmissionResult unmaterialized = policy.Admit(
            SecureMemoryDomainDescriptor.Disabled,
            access);
        SecureMemoryAdmissionResult stale = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, staleRegion),
            access);

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedMissingDescriptor, missing.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedUnmaterializedDescriptor, unmaterialized.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedStalePolicyEpoch, stale.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_SharedMemoryIsExplicitOnlyForHostInspection()
    {
        var policy = new SecureMemoryAdmissionPolicy();

        SecureMemoryAdmissionResult implicitShared = policy.Admit(
            CreateMemoryDescriptor(
                SecureMemoryDmaPolicy.Denied,
                new SecureMemoryRegionDescriptor(
                    SecureMemoryRegionClass.Shared,
                    0x2000,
                    0x100,
                    SecureMemoryHostVisibility.MetadataOnly,
                    7)),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.HostRead,
                Address: 0x2000,
                Length: 0x20));

        SecureMemoryAdmissionResult explicitShared = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, SharedRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.HostRead,
                Address: 0x2000,
                Length: 0x20));

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedHostInspection, implicitShared.Decision);
        Assert.True(explicitShared.IsAllowed);
    }

    [Fact]
    public void SecureMemoryPolicy_DmaToPrivateMemoryDenied()
    {
        SecureMemoryAdmissionResult result = new SecureMemoryAdmissionPolicy().Admit(
            CreateMemoryDescriptor(
                SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
                PrivateRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.DmaWrite,
                Address: 0x1000,
                Length: 0x40,
                Origin: SecureMemoryAccessOrigin.IoDma,
                CapabilityRequirement: TypedDmaGrantRequirement(),
                Capabilities: CreateCapabilitiesWithDmaGrant()));

        Assert.False(result.IsAllowed);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedPrivateDma, result.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_DmaToPrivateMemoryDeniedThroughHypercallArgumentPath()
    {
        SecureMemoryAdmissionResult result = new SecureMemoryAdmissionPolicy().Admit(
            CreateMemoryDescriptor(
                SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
                PrivateRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.DmaRead,
                Address: 0x1000,
                Length: 0x20,
                Origin: SecureMemoryAccessOrigin.HypercallArgument,
                CapabilityRequirement: TypedDmaGrantRequirement(),
                Capabilities: CreateCapabilitiesWithDmaGrant()));

        Assert.False(result.IsAllowed);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedPrivateDma, result.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_DmaToExplicitSharedBufferRequiresMemoryIoPolicyAndTypedGrant()
    {
        SecureMemoryDomainDescriptor deniedPolicy = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            SharedRegion());
        SecureMemoryDomainDescriptor allowedPolicy = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
            SharedRegion());
        SecureIoDomainDescriptor allowedIoPolicy = CreateSecureIoPolicy(
            SharedBuffer(SecureSharedBufferDirection.DeviceToDomain));
        SecureIoDomainDescriptor wrongDirectionIoPolicy = CreateSecureIoPolicy(
            SharedBuffer(SecureSharedBufferDirection.DomainToDevice));
        var accessWithoutGrant = new SecureMemoryAccessRequest(
            SecureMemoryAccessKind.DmaWrite,
            Address: 0x2000,
            Length: 0x40,
            Origin: SecureMemoryAccessOrigin.IoDma,
            CapabilityRequirement: TypedDmaGrantRequirement(),
            Capabilities: CapabilityDescriptorSet.Empty);
        var accessWithGrant = accessWithoutGrant with
        {
            Capabilities = CreateCapabilitiesWithDmaGrant(),
        };
        var policy = new SecureMemoryAdmissionPolicy();

        SecureMemoryAdmissionResult missingMemoryPolicy = policy.Admit(
            deniedPolicy,
            accessWithGrant,
            allowedIoPolicy);
        SecureMemoryAdmissionResult missingIoPolicy = policy.Admit(
            allowedPolicy,
            accessWithGrant,
            SecureIoDomainDescriptor.Disabled);
        SecureMemoryAdmissionResult wrongDirection = policy.Admit(
            allowedPolicy,
            accessWithGrant,
            wrongDirectionIoPolicy);
        SecureMemoryAdmissionResult missingGrant = policy.Admit(
            allowedPolicy,
            accessWithoutGrant,
            allowedIoPolicy);
        SecureMemoryAdmissionResult allowed = policy.Admit(
            allowedPolicy,
            accessWithGrant,
            allowedIoPolicy);

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedRequiresExplicitPolicy, missingMemoryPolicy.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, missingIoPolicy.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, wrongDirection.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedDmaRequiresTypedGrant, missingGrant.Decision);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureMemoryPolicy_SharedDmaRequiresCurrentSharedBufferGrantEpoch()
    {
        SecureMemoryDomainDescriptor memory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
            SharedRegion());
        var access = new SecureMemoryAccessRequest(
            SecureMemoryAccessKind.DmaWrite,
            Address: 0x2000,
            Length: 0x40,
            Origin: SecureMemoryAccessOrigin.IoDma,
            CapabilityRequirement: TypedDmaGrantRequirement(),
            Capabilities: CreateCapabilitiesWithDmaGrant());

        SecureMemoryAdmissionResult result = new SecureMemoryAdmissionPolicy().Admit(
            memory,
            access,
            CreateSecureIoPolicy(SharedBuffer(
                SecureSharedBufferDirection.DeviceToDomain,
                grantEpoch: 6)));

        Assert.False(result.IsAllowed);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, result.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_SharedDmaRequiresOwnerLifetimeAndEvidenceClass()
    {
        SecureMemoryDomainDescriptor memory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
            SharedRegion());
        var access = new SecureMemoryAccessRequest(
            SecureMemoryAccessKind.DmaWrite,
            Address: 0x2000,
            Length: 0x40,
            Origin: SecureMemoryAccessOrigin.IoDma,
            CapabilityRequirement: TypedDmaGrantRequirement(),
            Capabilities: CreateCapabilitiesWithDmaGrant());
        var policy = new SecureMemoryAdmissionPolicy();

        SecureMemoryAdmissionResult wrongOwner = policy.Admit(
            memory,
            access,
            CreateSecureIoPolicy(SharedBuffer(SecureSharedBufferDirection.DeviceToDomain, ownerDomainTag: 8)));
        SecureMemoryAdmissionResult staleLifetime = policy.Admit(
            memory,
            access,
            CreateSecureIoPolicy(SharedBuffer(SecureSharedBufferDirection.DeviceToDomain, lifetimeEpoch: 6)));
        SecureMemoryAdmissionResult deniedEvidence = policy.Admit(
            memory,
            access,
            CreateSecureIoPolicy(SharedBuffer(
                SecureSharedBufferDirection.DeviceToDomain,
                evidenceClass: SecureEvidenceVisibilityClass.Denied)));

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, wrongOwner.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, staleLifetime.Decision);
        Assert.Equal(SecureMemoryAdmissionDecision.DeniedSharedBufferBinding, deniedEvidence.Decision);
    }

    [Fact]
    public void SecureMemoryPolicy_MeasurementRequiresMeasuredRegion()
    {
        var policy = new SecureMemoryAdmissionPolicy();

        SecureMemoryAdmissionResult privateRegion = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, PrivateRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.Measurement,
                Address: 0x1000,
                Length: 0x20));
        SecureMemoryAdmissionResult measuredRegion = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, MeasuredRegion()),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.Measurement,
                Address: 0x3000,
                Length: 0x20));

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedMeasuredRegionMissing, privateRegion.Decision);
        Assert.True(measuredRegion.IsAllowed);
    }

    [Fact]
    public void SecureMemoryPolicy_MeasuredAdmissionDoesNotSatisfyPrivateDomainActivation()
    {
        SecureMemoryDomainDescriptor measuredMemory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            MeasuredRegion());

        SecureMemoryAdmissionResult measuredAdmission = new SecureMemoryAdmissionPolicy().Admit(
            measuredMemory,
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.Measurement,
                Address: 0x3000,
                Length: 0x20));
        RuntimeBoundaryAdmissionResult privateDomainAdmission =
            new RuntimeBoundaryAdmissionService().Validate(
                CreateBoundaryRequest(
                    CreateSecureDescriptor(privateMemoryRequired: true),
                    measuredMemory,
                    secureOperationClass: SecureDomainOperationClass.EnterSecureDomain,
                    secureMemoryAccess: null));

        Assert.True(measuredAdmission.IsAllowed);
        Assert.False(privateDomainAdmission.IsAllowed);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
            privateDomainAdmission.Decision);
        Assert.Contains(
            "private memory policy",
            privateDomainAdmission.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecureMemoryDescriptor_RuntimeMutableMemoryCarriesEpochDirtyAndMigrationClassification()
    {
        SecureMemoryRegionDescriptor runtimeMutable = RuntimeMutableRegion();
        SecureMemoryDomainDescriptor descriptor = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            runtimeMutable);

        Assert.True(descriptor.HasRuntimeMutableMemory);
        Assert.True(runtimeMutable.IsRuntimeMutable);
        Assert.True(runtimeMutable.IsCurrentFor(descriptor.PolicyEpoch));
        Assert.Equal(SecureRuntimeMutableDirtyPolicy.TrackDirtyPages, runtimeMutable.RuntimeDirtyPolicy);
        Assert.Equal(SecureRuntimeMutableMigrationClass.SealedPayloadRequired, runtimeMutable.RuntimeMigrationClass);
        Assert.True(runtimeMutable.HasRuntimeMutableClassification);
    }

    [Fact]
    public void SecureMemoryPolicy_RuntimeMutableTouchRequiresDirtyAndMigrationClassification()
    {
        var unclassified = new SecureMemoryRegionDescriptor(
            SecureMemoryRegionClass.RuntimeMutable,
            0x4000,
            0x100,
            SecureMemoryHostVisibility.Denied,
            7);
        var classified = RuntimeMutableRegion();
        var policy = new SecureMemoryAdmissionPolicy();

        SecureMemoryAdmissionResult missingClassification = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, unclassified),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.RuntimeTouch,
                Address: 0x4000,
                Length: 0x20));
        SecureMemoryAdmissionResult allowed = policy.Admit(
            CreateMemoryDescriptor(SecureMemoryDmaPolicy.Denied, classified),
            new SecureMemoryAccessRequest(
                SecureMemoryAccessKind.RuntimeTouch,
                Address: 0x4000,
                Length: 0x20));

        Assert.Equal(SecureMemoryAdmissionDecision.DeniedRuntimeMutableClassification, missingClassification.Decision);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureMemoryPolicy_SealedPrivatePayloadContractIsValidationNotRawKeyOrTagAuthority()
    {
        var migration = new SecureMigrationDescriptor(
            SecureMigrationMode.PolicyCompatible,
            SecurePrivateMemoryMigrationPolicy.SealedEncryptedPayloadRequired,
            new SecureRevocationEpoch(7),
            allowGuestVisibleEvidence: false,
            allowCompatibilityProjectionMetadata: false);
        var completeContract = new SecurePrivateMemorySealedPayloadContract(
            HasSealedPayload: true,
            HasEncryptedPayload: true,
            HasNeutralKeyOwner: true,
            HasEvidencePolicy: true,
            HasRestoreValidationProof: true);
        var rawKeyContract = completeContract with
        {
            ContainsRawSealingKey = true,
        };
        var migrationPolicy = SecureMigrationAdmissionPolicy.Default;

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            migrationPolicy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.SecurePrivateMemory).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            migrationPolicy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.SecurePrivateMemory,
                rawKeyContract).Decision);
        Assert.True(migrationPolicy.AdmitCheckpointPayload(
            migration,
            SecureCheckpointPayloadClass.SecurePrivateMemory,
            completeContract).IsAllowed);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedRawSecret,
            migrationPolicy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.RawSealingKey,
                completeContract).Decision);
        Assert.Equal(
            SecureComputeMigrationReplayViolation.PrivateMemoryWithoutSealedPayload,
            new SecureComputeMigrationReplayContract().Validate(
                epochRollback: false,
                vmcsProjectionAuthority: false,
                compatibilityMetadataAuthority: false,
                privateMemoryWithoutSealedPayload: true));
    }

    [Fact]
    public void RuntimeBoundaryAdmission_RoutesSecureMemoryAccessOnlyAfterDescriptorActivation()
    {
        SecureComputeDomainDescriptor activeDescriptor = CreateSecureDescriptor(
            privateMemoryRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            PrivateRegion());

        RuntimeBoundaryAdmissionResult ordinary = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                activeDescriptor,
                memory,
                secureOperationClass: SecureDomainOperationClass.Ordinary,
                secureMemoryAccess: new SecureMemoryAccessRequest(
                    SecureMemoryAccessKind.HostRead,
                    Address: 0x1000,
                    Length: 0x20)));
        RuntimeBoundaryAdmissionResult secure = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                activeDescriptor,
                memory,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory,
                secureMemoryAccess: new SecureMemoryAccessRequest(
                    SecureMemoryAccessKind.HostRead,
                    Address: 0x1000,
                    Length: 0x20)));

        Assert.True(ordinary.IsAllowed);
        Assert.False(secure.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, secure.Decision);
        Assert.Contains("host readable", secure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_SecureMemoryMustMatchDomainAndAddressSpaceTags()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            privateMemoryRequired: true);
        SecureMemoryDomainDescriptor wrongDomain = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            8,
            9,
            PrivateRegion());
        SecureMemoryDomainDescriptor wrongAddressSpace = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.Denied,
            7,
            10,
            PrivateRegion());

        RuntimeBoundaryAdmissionResult domainMismatch = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                wrongDomain,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory,
                secureMemoryAccess: null));
        RuntimeBoundaryAdmissionResult addressSpaceMismatch = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                wrongAddressSpace,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory,
                secureMemoryAccess: null,
                contextAddressSpaceTag: 9));

        Assert.False(domainMismatch.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, domainMismatch.Decision);
        Assert.Contains("domain tag", domainMismatch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(addressSpaceMismatch.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, addressSpaceMismatch.Decision);
        Assert.Contains("address-space", addressSpaceMismatch.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_PassesSecureIoPolicyToSharedDmaAdmission()
    {
        SecureIoDomainDescriptor ioPolicy = CreateSecureIoPolicy(
            SharedBuffer(SecureSharedBufferDirection.DeviceToDomain));
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            privateMemoryRequired: false,
            ioPolicy: ioPolicy);
        SecureMemoryDomainDescriptor memory = CreateMemoryDescriptor(
            SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant,
            SharedRegion());

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                memory,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory,
                secureMemoryAccess: new SecureMemoryAccessRequest(
                    SecureMemoryAccessKind.DmaWrite,
                    Address: 0x2000,
                    Length: 0x40,
                    Origin: SecureMemoryAccessOrigin.IoDma,
                    CapabilityRequirement: TypedDmaGrantRequirement(),
                    Capabilities: CreateCapabilitiesWithDmaGrant())));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void SecureMemoryConformance_HostEvidenceNonLeakContractRejectsGuestVisibleHostEvidence()
    {
        var contract = new SecureComputeHostEvidenceNonLeakContract();
        var hostOwnedEnvelope = new SecureComputeEvidenceSidebandEnvelope(
            SecureComputeEvidenceSidebandClass.HostOwnedQuarantined,
            domainTag: 7,
            evidenceHash: 0x1234);

        Assert.False(hostOwnedEnvelope.CanExposeToGuest);
        Assert.Equal(
            SecureComputeEvidenceLeakViolation.HostEvidenceGuestVisible,
            contract.Validate(
                hostEvidenceGuestVisible: true,
                schedulerEvidenceSerialized: false,
                backendBindingEvidenceSerialized: false,
                nativeTokenEvidenceSerialized: false,
                debugTraceGuestState: false));
        Assert.Equal(
            SecureComputeEvidenceLeakViolation.None,
            contract.Validate(
                hostEvidenceGuestVisible: hostOwnedEnvelope.CanExposeToGuest,
                schedulerEvidenceSerialized: false,
                backendBindingEvidenceSerialized: false,
                nativeTokenEvidenceSerialized: false,
                debugTraceGuestState: false));
    }

    [Fact]
    public void OrdinaryLoadStoreFetchSources_DoNotRequireSecureMemoryPolicy()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Services/CPU_Core.System.cs",
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx",
            "HybridCPU_ISE/CloseToRTL/Core/Decoder/DecodedBundleTransportProjector.cs");

        Assert.DoesNotContain("SecureMemory", source);
        Assert.DoesNotContain("SecureMemoryAdmission", source);
        Assert.DoesNotContain("SecureMemoryAccess", source);
    }

    [Fact]
    public void SecureMemorySources_DoNotCreateVmcsBackedTranslationAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Memory/SecureMemoryDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs");

        Assert.Contains("SecureRuntimeMutableDirtyPolicy", source);
        Assert.Contains("SecureRuntimeMutableMigrationClass", source);
        Assert.Contains("DeniedRuntimeMutableClassification", source);
        Assert.DoesNotContain("VmcsField.GuestCr3", source);
        Assert.DoesNotContain("VmcsField.EptPointer", source);
        Assert.DoesNotContain("VmcsField.Vpid", source);
        Assert.DoesNotContain("VmcsField.NptPointer", source);
        Assert.DoesNotContain("VmcsField.HostCr3", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        foreach (string token in ForbiddenMemoryIsaAndTagTokens())
        {
            Assert.DoesNotContain(token, source);
        }
    }

    private static RuntimeBoundaryAdmissionRequest CreateBoundaryRequest(
        SecureComputeDomainDescriptor descriptor,
        SecureMemoryDomainDescriptor memory,
        SecureDomainOperationClass secureOperationClass,
        SecureMemoryAccessRequest? secureMemoryAccess,
        ulong? contextDomainTag = null,
        ulong? contextAddressSpaceTag = null)
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;

        return new RuntimeBoundaryAdmissionRequest(
            Context: new DomainRuntimeContext(
                execution: new ExecutionDomainDescriptor(),
                memory: new MemoryDomainDescriptor(),
                io: new IoDomainDescriptor(),
                capabilities: new CapabilityDescriptorSet(
                    globalHardwareCaps: capability,
                    runtimeEnabledCaps: capability,
                    domainGrantedCaps: capability),
                secureCompute: descriptor,
                domainTag: contextDomainTag ?? descriptor.DomainTag,
                addressSpaceTag: contextAddressSpaceTag ?? memory.AddressSpaceTag),
            RootAuthority: new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot,
                authorityEpoch: 1,
                grantedCapabilityMask: capability,
                allowCompatibilityFrontendActivation: true,
                allowAuthoritativeStateMutation: true),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState),
            SecureDescriptor: null,
            SecureOperationClass: secureOperationClass,
            SecureMeasurement: null,
            SecureMemory: memory,
            SecureMemoryAccess: secureMemoryAccess);
    }

    private static SecureComputeDomainDescriptor CreateSecureDescriptor(
        bool privateMemoryRequired,
        SecureIoDomainDescriptor? ioPolicy = null) =>
        new(
            7,
            SecureComputeSecurityLevel.Private,
            measurementRequired: false,
            privateMemoryRequired,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            ioPolicy ?? SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);

    private static SecureMemoryDomainDescriptor CreateMemoryDescriptor(
        SecureMemoryDmaPolicy dmaPolicy,
        params SecureMemoryRegionDescriptor[] regions) =>
        CreateMemoryDescriptor(dmaPolicy, 7, 9, regions);

    private static SecureMemoryDomainDescriptor CreateMemoryDescriptor(
        SecureMemoryDmaPolicy dmaPolicy,
        ulong domainTag,
        ulong addressSpaceTag,
        params SecureMemoryRegionDescriptor[] regions) =>
        new(
            domainTag,
            addressSpaceTag,
            new SecureRevocationEpoch(7),
            regions,
            dmaPolicy);

    private static SecureMemoryRegionDescriptor PrivateRegion() =>
        new(
            SecureMemoryRegionClass.Private,
            0x1000,
            0x100,
            SecureMemoryHostVisibility.Denied,
            7);

    private static SecureMemoryRegionDescriptor SharedRegion() =>
        new(
            SecureMemoryRegionClass.Shared,
            0x2000,
            0x100,
            SecureMemoryHostVisibility.ExplicitShared,
            7);

    private static SecureMemoryRegionDescriptor MeasuredRegion() =>
        new(
            SecureMemoryRegionClass.Measured,
            0x3000,
            0x100,
            SecureMemoryHostVisibility.MetadataOnly,
            7);

    private static SecureMemoryRegionDescriptor RuntimeMutableRegion() =>
        new(
            SecureMemoryRegionClass.RuntimeMutable,
            0x4000,
            0x100,
            SecureMemoryHostVisibility.Denied,
            7,
            SecureRuntimeMutableDirtyPolicy.TrackDirtyPages,
            SecureRuntimeMutableMigrationClass.SealedPayloadRequired);

    private static SecureIoDomainDescriptor CreateSecureIoPolicy(
        params SecureSharedBufferDescriptor[] sharedBuffers) =>
        new(
            SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
            sharedBuffers,
            requireCompletionFence: true);

    private static SecureSharedBufferDescriptor SharedBuffer(
        SecureSharedBufferDirection direction,
        ulong ownerDomainTag = 7,
        ulong lifetimeEpoch = 7,
        SecureEvidenceVisibilityClass evidenceClass = SecureEvidenceVisibilityClass.HostOwnedQuarantined,
        ulong grantEpoch = 7) =>
        new(
            BufferId: 1,
            Start: 0x2000,
            Length: 0x100,
            Direction: direction,
            Grant: new SecureGrantHandle(
                SecureGrantHandleKind.IoPolicy,
                LocalId: 1,
                ProvenanceHash: 0x51,
                Epoch: grantEpoch),
            EvidenceClass: evidenceClass,
            OwnerDomainTag: ownerDomainTag,
            LifetimeEpoch: lifetimeEpoch);

    private static CapabilityBoundaryRequirement TypedDmaGrantRequirement() =>
        CapabilityBoundaryRequirement.TypedGrant(DmaGrantMask, CapabilityGrantScope.DomainGranted);

    private static CapabilityDescriptorSet CreateCapabilitiesWithDmaGrant() =>
        new(new CapabilityGrantCollection(new[]
        {
            new CapabilityGrant(
                DmaGrantMask,
                CapabilityGrantScope.DomainGranted,
                true,
                7,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.NeverProject),
        }));

    private static string[] ForbiddenMemoryIsaAndTagTokens() =>
        new[]
        {
            "MemoryTag",
            "TagBit",
            "TaggedMemory",
            "CapabilityTag",
            "HardwareTag",
            "TagStorage",
            "TagMigrationPayload",
            "CapabilityLoad",
            "CapabilityStore",
            "CapabilityFetch",
            "CapabilityAwareLoad",
            "CapabilityAwareStore",
            "CapabilityAwareFetch",
            "LOAD_CAP",
            "STORE_CAP",
            "FETCH_CAP",
            "CapabilityOperand",
            "CapabilityRegister",
            "CHERI",
            "TagProvenance",
            "ProvisionalTag",
            "ProvenanceCheckpoint",
        };

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path =>
        {
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            if (Directory.Exists(fullPath))
            {
                return string.Concat(Directory
                    .EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            }

            throw new FileNotFoundException("Unable to locate source path.", fullPath);
        }));
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
