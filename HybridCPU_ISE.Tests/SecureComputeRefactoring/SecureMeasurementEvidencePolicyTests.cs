using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureMeasurementEvidencePolicyTests
{
    [Fact]
    public void RuntimeBoundaryAdmission_MaterializedMeasurementAllowsMeasuredSecureEnter()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(measurementRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(descriptor, memory);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                memory,
                measurement,
                SecureDomainOperationClass.EnterSecureDomain));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_StaleMeasurementEpochDeniesSecureEnter()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(measurementRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(
            descriptor,
            memory,
            measurementEpoch: 6);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                memory,
                measurement,
                SecureDomainOperationClass.EnterSecureDomain));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("epoch", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_MissingMeasurementDoesNotAffectOrdinaryOperation()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(measurementRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateBoundaryRequest(
                descriptor,
                memory,
                measurement: null,
                SecureDomainOperationClass.Ordinary));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void SecureMeasurementPolicy_DebugDomainRequiresMeasuredDebugClass()
    {
        var debugPolicy = new SecureDebugPolicy(
            SecureDebugMode.MeasuredDebugOnly,
            changesMeasurementClass: true);
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            measurementRequired: true,
            debugPolicy: debugPolicy);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor productionMeasurement = CreateMeasurement(
            descriptor,
            memory,
            debugClass: SecureMeasurementDebugClass.Production);
        DomainMeasurementDescriptor debugMeasurement = CreateMeasurement(
            descriptor,
            memory,
            debugClass: SecureMeasurementDebugClass.MeasuredDebug);
        var policy = new SecureMeasurementAdmissionPolicy();

        SecureMeasurementAdmissionResult denied = policy.AdmitMeasurement(
            descriptor,
            productionMeasurement,
            memory);
        SecureMeasurementAdmissionResult allowed = policy.AdmitMeasurement(
            descriptor,
            debugMeasurement,
            memory);

        Assert.Equal(SecureMeasurementAdmissionDecision.DeniedDebugClassMismatch, denied.Decision);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureMeasurementPolicy_DebugDeniedPolicyRejectsDebugEvidence()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            measurementRequired: true,
            evidencePolicy: new SecureEvidencePolicy(
                allowGuestVisibleEvidence: false,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: false,
                allowDebugEvidence: true),
            debugPolicy: SecureDebugPolicy.Denied);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(
            descriptor,
            memory,
            debugClass: SecureMeasurementDebugClass.MeasuredDebug,
            evidenceClass: SecureEvidenceVisibilityClass.DebugOnly);

        SecureMeasurementAdmissionResult result =
            new SecureMeasurementAdmissionPolicy().AdmitAttestationPublication(
                descriptor,
                measurement,
                memory,
                new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false),
                new SecureCompletionPublicationFence(
                    SecureCompletionFenceState.CompletionAllowed,
                    SecureRetirePublicationRule.CompletionFenceRequired));

        Assert.Equal(SecureMeasurementAdmissionDecision.DeniedDebugClassMismatch, result.Decision);
    }

    [Fact]
    public void SecureMeasurementPolicy_AttestationCannotExposeHostOwnedEvidence()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            measurementRequired: true,
            evidencePolicy: new SecureEvidencePolicy(
                allowGuestVisibleEvidence: true,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: false,
                allowDebugEvidence: false));
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(
            descriptor,
            memory,
            evidenceClass: SecureEvidenceVisibilityClass.HostOwnedQuarantined);

        SecureMeasurementAdmissionResult result =
            new SecureMeasurementAdmissionPolicy().AdmitAttestationPublication(
                descriptor,
                measurement,
                memory,
                new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false),
                new SecureCompletionPublicationFence(
                    SecureCompletionFenceState.CompletionAllowed,
                    SecureRetirePublicationRule.CompletionFenceRequired));

        Assert.Equal(SecureMeasurementAdmissionDecision.DeniedEvidenceVisibility, result.Decision);
    }

    [Fact]
    public void SecureMeasurementPolicy_AttestationGuestVisibleRequiresEvidencePolicyAndFence()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            measurementRequired: true,
            evidencePolicy: new SecureEvidencePolicy(
                allowGuestVisibleEvidence: true,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: false,
                allowDebugEvidence: false));
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(
            descriptor,
            memory,
            evidenceClass: SecureEvidenceVisibilityClass.GuestVisible);
        var policy = new SecureMeasurementAdmissionPolicy();

        SecureMeasurementAdmissionResult missingFence = policy.AdmitAttestationPublication(
            descriptor,
            measurement,
            memory,
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            SecureCompletionPublicationFence.Denied);
        SecureMeasurementAdmissionResult neutralEvidenceDenied = policy.AdmitAttestationPublication(
            descriptor,
            measurement,
            memory,
            EvidencePolicyDescriptor.FailClosed,
            new SecureCompletionPublicationFence(
                SecureCompletionFenceState.CompletionAllowed,
                SecureRetirePublicationRule.CompletionFenceRequired));
        SecureMeasurementAdmissionResult allowed = policy.AdmitAttestationPublication(
            descriptor,
            measurement,
            memory,
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            new SecureCompletionPublicationFence(
                SecureCompletionFenceState.CompletionAllowed,
                SecureRetirePublicationRule.CompletionFenceRequired));

        Assert.Equal(SecureMeasurementAdmissionDecision.DeniedPublicationFence, missingFence.Decision);
        Assert.Equal(SecureMeasurementAdmissionDecision.DeniedEvidenceVisibility, neutralEvidenceDenied.Decision);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureCheckpointPolicy_RejectsRawMeasurementSecretsAndHostEvidence()
    {
        var policy = SecureCheckpointPayloadPolicy.FailClosed;

        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret,
            policy.Classify(SecureCheckpointPayloadClass.RawMeasurementSecret));
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            policy.Classify(SecureCheckpointPayloadClass.HostOwnedEvidence));
    }

    [Fact]
    public void SecureMeasurementDescriptor_RevalidatesOrReattestsOnRestoreWhenPolicyRequiresIt()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(measurementRequired: true);
        SecureMemoryDomainDescriptor memory = CreateMeasuredMemory();
        DomainMeasurementDescriptor measurement = CreateMeasurement(
            descriptor,
            memory,
            evidenceClass: SecureEvidenceVisibilityClass.RecomputedAfterRestore);
        var migration = new SecureMigrationDescriptor(
            SecureMigrationMode.ReattestRequired,
            SecurePrivateMemoryMigrationPolicy.Denied,
            new SecureRevocationEpoch(7),
            allowGuestVisibleEvidence: false,
            allowCompatibilityProjectionMetadata: false);

        Assert.True(measurement.MustRevalidateOnRestore(migration));
    }

    [Fact]
    public void SecureMeasurementSources_DoNotCreateVmcsVmxCapsOrVmreadAttestationAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Measurement/DomainMeasurementDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Measurement/SecureMeasurementAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs");

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
    }

    private static RuntimeBoundaryAdmissionRequest CreateBoundaryRequest(
        SecureComputeDomainDescriptor descriptor,
        SecureMemoryDomainDescriptor memory,
        DomainMeasurementDescriptor? measurement,
        SecureDomainOperationClass operationClass)
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
                domainTag: descriptor.DomainTag,
                addressSpaceTag: memory.AddressSpaceTag),
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
            SecureOperationClass: operationClass,
            SecureMeasurement: measurement,
            SecureMemory: memory,
            SecureMemoryAccess: null);
    }

    private static SecureComputeDomainDescriptor CreateSecureDescriptor(
        bool measurementRequired,
        SecureEvidencePolicy? evidencePolicy = null,
        SecureDebugPolicy? debugPolicy = null,
        SecureCompatibilityProjectionPolicy? compatibilityPolicy = null,
        SecureMigrationDescriptor? migrationPolicy = null) =>
        new(
            7,
            SecureComputeSecurityLevel.Measured,
            measurementRequired,
            false,
            SecureHostInspectionPolicy.DenyAll,
            evidencePolicy ?? SecureEvidencePolicy.FailClosed,
            migrationPolicy ?? SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            debugPolicy ?? SecureDebugPolicy.Denied,
            compatibilityPolicy ?? SecureCompatibilityProjectionPolicy.DenyAll);

    private static SecureMemoryDomainDescriptor CreateMeasuredMemory() =>
        new(
            7,
            9,
            new SecureRevocationEpoch(7),
            new[]
            {
                new SecureMemoryRegionDescriptor(
                    SecureMemoryRegionClass.Measured,
                    0x3000,
                    0x100,
                    SecureMemoryHostVisibility.MetadataOnly,
                    7),
            },
            SecureMemoryDmaPolicy.Denied);

    private static DomainMeasurementDescriptor CreateMeasurement(
        SecureComputeDomainDescriptor descriptor,
        SecureMemoryDomainDescriptor memory,
        SecureMeasurementState state = SecureMeasurementState.Materialized,
        ulong measurementEpoch = 7,
        SecureMeasurementDebugClass debugClass = SecureMeasurementDebugClass.Production,
        SecureEvidenceVisibilityClass evidenceClass = SecureEvidenceVisibilityClass.HostOwnedQuarantined) =>
        new(
            new SecureMeasurementHandle(
                MeasurementId: 0x4D,
                ProvenanceHash: 0xA17E,
                Epoch: measurementEpoch),
            state,
            debugClass,
            DomainMeasurementDescriptor.ComputePolicyDigest(descriptor),
            DomainMeasurementDescriptor.ComputeMemoryDigest(memory),
            0xC0DE,
            evidenceClass,
            descriptor.DomainTag,
            0,
            0x51);

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
