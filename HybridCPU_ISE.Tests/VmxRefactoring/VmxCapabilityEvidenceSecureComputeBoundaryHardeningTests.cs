using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests
{
    [Fact]
    public void VmxCapsProjection_PublishesOnlyKnownCompatibilityCapsNotSecureEvidenceBits()
    {
        const ulong knownCompatibilityCaps =
            VmxV2InstructionCaps.VmCall |
            VmxV2InstructionCaps.VmFunc;
        const ulong hypotheticalSecureComputeBit = 1UL << 63;
        const ulong hypotheticalAttestationEvidenceBit = 1UL << 62;
        const ulong forbiddenSecureBits =
            hypotheticalSecureComputeBit |
            hypotheticalAttestationEvidenceBit;

        var descriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: knownCompatibilityCaps | forbiddenSecureBits,
            runtimeEnabledCaps: knownCompatibilityCaps | forbiddenSecureBits,
            domainGrantedCaps: knownCompatibilityCaps | forbiddenSecureBits);
        var projection = new VmxCapsProjection();

        Assert.Equal(knownCompatibilityCaps, projection.Read(descriptorSet));
        Assert.False(
            CapabilityDescriptorSetSchema.VmxCompatibility.ContainsOnlyKnownCompatibilityCaps(
                forbiddenSecureBits));
        Assert.Equal(
            0UL,
            CapabilityDescriptorSetSchema.VmxCompatibility.FilterCompatibilityCaps(
                forbiddenSecureBits));
        Assert.False(projection.CanPublishCapability(descriptorSet, forbiddenSecureBits));
        Assert.All(
            CapabilityDescriptorSetSchema.VmxCompatibilityBits.ToArray(),
            entry =>
            {
                Assert.DoesNotContain("Secure", entry.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Evidence", entry.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Attestation", entry.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Measurement", entry.Name, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Theory]
    [InlineData(SecureBackendOwnerSource.CompatibilityProjection)]
    [InlineData(SecureBackendOwnerSource.VmxFrontend)]
    [InlineData(SecureBackendOwnerSource.VmcsProjection)]
    [InlineData(SecureBackendOwnerSource.VmxCapsProjection)]
    [InlineData(SecureBackendOwnerSource.ShadowVmcsProjection)]
    public void SecureBackendOwnerGate_DeniesCompatibilityCapabilityAndVmcsSources(
        SecureBackendOwnerSource source)
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateBackendOwnerRequest(owner: CreateBackendOwner(source)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedNonNeutralAuthoritySource,
            result.Decision);
        Assert.False(result.ProofChainAccepted);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureBackendOwnerGate_CompleteNeutralProofRemainsEvidenceOnly()
    {
        SecureBackendOwnerAdmissionResult proofOnly =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateBackendOwnerRequest());
        SecureBackendOwnerAdmissionResult executionAttempt =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateBackendOwnerRequest(requestsBackendExecution: true));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
            proofOnly.Decision);
        Assert.True(proofOnly.ProofChainAccepted);
        Assert.False(proofOnly.BackendExecutionAuthorized);

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedBackendExecutionClosed,
            executionAttempt.Decision);
        Assert.False(executionAttempt.ProofChainAccepted);
        Assert.False(executionAttempt.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureComputeCompatibilityMatrix_DeniesSecureEvidenceAndPublicationAuthority()
    {
        var policy = new SecureComputeCompatibilityBoundaryMatrixPolicy();

        SecureComputeCompatibilityMatrixResult secureEvidenceRead =
            policy.AdmitVmRead(
                CreateReadRequest(
                    field: (VmcsField)0x5EC0,
                    SecureComputeCompatibilityFieldClass.SecureMeasurementEvidenceDebugMigration,
                    owner: SecureComputeProjectionOwnerKind.SecureEvidencePolicy));
        SecureComputeCompatibilityMatrixResult missingVisibility =
            policy.AdmitVmRead(
                CreateReadRequest(
                    VmcsField.GuestPc,
                    secureVisibilityAllowed: false));
        SecureComputeCompatibilityMatrixResult write =
            policy.AdmitVmWrite(secureSensitiveField: true);
        SecureComputeCompatibilityMatrixResult caps =
            policy.AdmitVmxCapsDescriptorMaterialization(
                attemptsSecureDescriptorMaterialization: true);
        SecureComputeCompatibilityMatrixResult checkpoint =
            policy.AdmitVmcsCheckpointAuthority(containsSecureMetadata: true);
        SecureComputeCompatibilityMatrixResult backend =
            policy.AdmitProjectionCompletion(attemptsBackendSuccess: true);

        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedSecureSensitiveField,
            secureEvidenceRead.Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoSecureVisibility,
            missingVisibility.Decision);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedWriteMutation, write.Decision);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedVmxCapsAuthority, caps.Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedVmcsCheckpointAuthority,
            checkpoint.Decision);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedBackendSuccess, backend.Decision);

        foreach (SecureComputeCompatibilityMatrixResult result in new[]
                 {
                     secureEvidenceRead,
                     missingVisibility,
                     write,
                     caps,
                     checkpoint,
                     backend,
                 })
        {
            Assert.False(result.ValueAvailable);
            Assert.False(result.BackendSuccessAuthorized);
            Assert.False(result.MutationAuthorized);
        }
    }

    [Fact]
    public void RuntimeBoundaryAdmission_DoesNotBecomeSecureBackendExecutionAuthority()
    {
        var service = new RuntimeBoundaryAdmissionService();
        SecureComputeDomainDescriptor activeDescriptor = CreateSecureDescriptor(
            domainTag: 81,
            measurementRequired: false,
            privateMemoryRequired: false);

        RuntimeBoundaryAdmissionResult absentSecureDescriptor =
            service.Validate(
                CreateRuntimeRequest(
                    secureCompute: null,
                    secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));
        RuntimeBoundaryAdmissionResult disabledSecureDescriptor =
            service.Validate(
                CreateRuntimeRequest(
                    secureCompute: SecureComputeDomainDescriptor.Disabled,
                    secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));
        RuntimeBoundaryAdmissionResult ordinaryProjectionWithActiveDescriptor =
            service.Validate(
                CreateRuntimeRequest(
                    activeDescriptor,
                    SecureDomainOperationClass.Ordinary));
        SecureBackendOwnerAdmissionResult backendExecutionAttempt =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateBackendOwnerRequest(requestsBackendExecution: true));

        Assert.False(absentSecureDescriptor.IsAllowed);
        Assert.False(disabledSecureDescriptor.IsAllowed);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
            absentSecureDescriptor.Decision);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied,
            disabledSecureDescriptor.Decision);
        Assert.True(ordinaryProjectionWithActiveDescriptor.IsAllowed);
        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedBackendExecutionClosed,
            backendExecutionAttempt.Decision);
        Assert.False(backendExecutionAttempt.BackendExecutionAuthorized);
    }

    [Fact]
    public void Phase11AuthoritySeparationSources_DoNotContainExecutionPublicationShortcuts()
    {
        string source = ReadProjectSource(
            "CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Backend",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Migration",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Publication",
            "CloseToRTL/Core/Virtualization/SecureCompute/Compatibility",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs");

        Assert.Contains("AllowedProofOnlyNoExecution", source);
        Assert.Contains("DeniedBackendExecutionClosed", source);
        Assert.Contains("DeniedVmxCapsAuthority", source);
        Assert.Contains("DeniedVmcsCheckpointAuthority", source);
        Assert.Contains("DeniedBackendSuccess", source);

        foreach (string forbidden in new[]
                 {
                     "BackendExecutionAuthorized: true",
                     "AllowBackendExecution = true",
                     "VmxCaps.Secure",
                     "VmcsManager",
                     "IVmcsManager",
                     "VmxExecutionUnit",
                     "ReadFieldValue(",
                     "WriteFieldValue(",
                     "CompletionRecord.FromCompatibilityExit",
                     "CompletionRecord.TryFromCompatibilityExit",
                     "TrapCompletionRouteDescriptor.RuntimeOwnedPublication",
                     "VmxRetireEffect.InterceptExit",
                     "VmxRetireEffect.VmCall",
                     "VmxRetireEffect.VmFunc",
                 })
        {
            Assert.DoesNotContain(forbidden, source);
        }
    }

    private static SecureComputeCompatibilityReadMatrixRequest CreateReadRequest(
        VmcsField field,
        SecureComputeCompatibilityFieldClass fieldClass =
            SecureComputeCompatibilityFieldClass.OrdinaryReadOnlyProjection,
        SecureComputeProjectionOwnerKind owner =
            SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
        SecureComputeProjectionOwnerKind? expectedOwner = null,
        bool hasNeutralOwner = true,
        bool hasReadOnlySource = true,
        bool secureVisibilityAllowed = true,
        bool migrationClassified = true,
        bool conformanceProven = true) =>
        new(
            FieldId: (ulong)field,
            FieldClass: fieldClass,
            SchemaOwner: owner,
            ExpectedOwner: expectedOwner ?? owner,
            HasNeutralOwner: hasNeutralOwner,
            HasReadOnlySource: hasReadOnlySource,
            SecureVisibilityAllowed: secureVisibilityAllowed,
            MigrationClassified: migrationClassified,
            ConformanceProven: conformanceProven);

    private static SecureBackendOwnerAdmissionRequest CreateBackendOwnerRequest(
        SecureBackendOwnerDescriptor? owner = null,
        bool requestsBackendExecution = false) =>
        new(
            owner ?? CreateBackendOwner(),
            SecureBackendRfcAdrState.Approved,
            new SecureRevocationEpoch(11),
            requestsBackendExecution);

    private static SecureBackendOwnerDescriptor CreateBackendOwner(
        SecureBackendOwnerSource source = SecureBackendOwnerSource.NeutralRuntimeService) =>
        new(
            OwnerId: 0x11CE,
            Source: source,
            PolicyDigest: 0xC0DE_0011,
            ProofDigest: 0xE711_0011,
            Epoch: new SecureRevocationEpoch(11),
            Materialized: true,
            GrantProofValidated: true,
            EvidenceProofValidated: true,
            CompletionFenceValidated: true,
            RetireFenceValidated: true,
            NegativeTestsPresent: true);

    private static RuntimeBoundaryAdmissionRequest CreateRuntimeRequest(
        SecureComputeDomainDescriptor? secureCompute,
        SecureDomainOperationClass secureOperationClass)
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;

        return new RuntimeBoundaryAdmissionRequest(
            Context: CreateContext(secureCompute, domainTag: secureCompute?.DomainTag ?? 81),
            RootAuthority: new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot,
                authorityEpoch: 11,
                grantedCapabilityMask: capability,
                allowCompatibilityFrontendActivation: true,
                allowAuthoritativeStateMutation: false),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: false,
                allowMigrationSerializableState: false),
            Operation: DomainRuntimeOperation.FromCompatibilityFrontend(
                DomainRuntimeOperationKind.ReadCompatibilityProjection,
                requiresCapabilityGrant: true,
                isProjectionOnly: true),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.CompatibilityAlias),
            SecureDescriptor: secureCompute,
            SecureOperationClass: secureOperationClass,
            SecureMeasurement: null,
            SecureMemory: null);
    }

    private static DomainRuntimeContext CreateContext(
        SecureComputeDomainDescriptor? secureCompute,
        ulong domainTag)
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;

        return new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: capability,
                runtimeEnabledCaps: capability,
                domainGrantedCaps: capability),
            secureCompute: secureCompute,
            domainTag: domainTag,
            addressSpaceTag: 11);
    }

    private static SecureComputeDomainDescriptor CreateSecureDescriptor(
        ulong domainTag,
        bool measurementRequired,
        bool privateMemoryRequired) =>
        new(
            domainTag,
            SecureComputeSecurityLevel.Measured,
            measurementRequired,
            privateMemoryRequired,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        return string.Concat(relativePaths.SelectMany(path =>
        {
            string fullPath = Path.Combine(
                projectRoot,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                return new[] { File.ReadAllText(fullPath) };
            }

            if (Directory.Exists(fullPath))
            {
                return Directory
                    .GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Where(static sourcePath =>
                        !sourcePath.Contains(
                            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase) &&
                        !sourcePath.Contains(
                            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(static sourcePath => sourcePath, StringComparer.Ordinal)
                    .Select(File.ReadAllText);
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
