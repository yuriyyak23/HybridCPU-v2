using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureDebugAttestationVisibilityPhase16Tests
{
    [Fact]
    public void DebugTraceVisibility_IsDebugOnlyAndCreatesNoAuthority()
    {
        SecureDebugAttestationVisibilityResult result = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.DebugTrace,
                DebugDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.DebugOnly, SecureMeasurementDebugClass.MeasuredDebug),
                NeutralGuestEvidence()));

        Assert.Equal(SecureDebugAttestationVisibilityDecision.VisibilityClassified, result.Decision);
        Assert.Equal(SecureEvidenceVisibilityClass.DebugOnly, result.VisibilityClass);
        Assert.True(result.DebugOnly);
        Assert.False(result.GuestVisible);
        AssertNoAuthority(result);
    }

    [Fact]
    public void DebugTrace_CannotBecomeMigrationPayloadGuestStateCompletionOrRetire()
    {
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedMigrationAuthority,
            Policy.Classify(new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.DebugTrace,
                DebugDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.DebugOnly, SecureMeasurementDebugClass.MeasuredDebug),
                NeutralGuestEvidence(),
                RequestsMigrationPayload: true)).Decision);
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedCompletionPublication,
            Policy.Classify(new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.DebugTrace,
                DebugDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.DebugOnly, SecureMeasurementDebugClass.MeasuredDebug),
                NeutralGuestEvidence(),
                RequestsCompletionPublication: true)).Decision);
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedRetirePublication,
            Policy.Classify(new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.DebugTrace,
                DebugDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.DebugOnly, SecureMeasurementDebugClass.MeasuredDebug),
                NeutralGuestEvidence(),
                RequestsRetirePublication: true)).Decision);
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState,
            SecureCheckpointPayloadPolicy.FailClosed.Classify(SecureCheckpointPayloadClass.DebugTrace));
    }

    [Fact]
    public void AttestationReport_CanBeGuestVisibleOnlyUnderEvidencePolicyAndNeverVmreadOrActivationAuthority()
    {
        SecureDebugAttestationVisibilityResult allowed = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence()));
        SecureDebugAttestationVisibilityResult vmread = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence(),
                RequestsVmreadValueSource: true));
        SecureDebugAttestationVisibilityResult activation = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence(),
                RequestsActivationEvidence: true));

        Assert.True(allowed.IsAllowed);
        Assert.True(allowed.GuestVisible);
        Assert.True(allowed.AttestationOnly);
        AssertNoAuthority(allowed);
        Assert.Equal(SecureDebugAttestationVisibilityDecision.DeniedVmreadAuthority, vmread.Decision);
        Assert.Equal(SecureDebugAttestationVisibilityDecision.DeniedActivationEvidence, activation.Decision);
        AssertNoAuthority(vmread);
        AssertNoAuthority(activation);
    }

    [Fact]
    public void AttestationReport_RejectsHostOwnedRecomputedAndMissingEvidencePolicies()
    {
        SecureDebugAttestationVisibilityResult hostOwned = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.HostOwnedQuarantined),
                NeutralGuestEvidence()));
        SecureDebugAttestationVisibilityResult recomputed = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.RecomputedAfterRestore),
                NeutralGuestEvidence()));
        SecureDebugAttestationVisibilityResult missingNeutral = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.AttestationReport,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralEvidencePolicy: null));

        Assert.Equal(SecureDebugAttestationVisibilityDecision.DeniedHostOwnedEvidence, hostOwned.Decision);
        Assert.Equal(SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy, recomputed.Decision);
        Assert.Equal(SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy, missingNeutral.Decision);
        AssertNoAuthority(hostOwned);
        AssertNoAuthority(recomputed);
        AssertNoAuthority(missingNeutral);
    }

    [Fact]
    public void TelemetrySnapshot_IsHostOnlyAndCannotSatisfyBackendOwnerProof()
    {
        SecureDebugAttestationVisibilityResult classified = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence()));
        SecureDebugAttestationVisibilityResult backendOwnerProof = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence(),
                RequestsBackendOwnerProof: true));

        Assert.True(classified.IsAllowed);
        Assert.True(classified.HostOnly);
        Assert.False(classified.GuestVisible);
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedBackendOwnerProof,
            backendOwnerProof.Decision);
        AssertNoAuthority(classified);
        AssertNoAuthority(backendOwnerProof);
    }

    [Fact]
    public void HostInspectionMetadata_CannotBypassPrivateMemoryPolicy()
    {
        SecureComputeDomainDescriptor descriptor = GuestVisibleDescriptor(
            hostInspectionPolicy: new SecureHostInspectionPolicy(
                SecureHostInspectionMode.MetadataOnly,
                allowPrivateMemoryInspection: false));

        SecureDebugAttestationVisibilityResult metadata = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.HostInspectionMetadata,
                descriptor,
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence()));
        SecureDebugAttestationVisibilityResult privateInspection = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.HostInspectionMetadata,
                descriptor,
                Measurement(SecureEvidenceVisibilityClass.GuestVisible),
                NeutralGuestEvidence(),
                RequestsPrivateMemoryInspection: true));

        Assert.True(metadata.IsAllowed);
        Assert.True(metadata.HostOnly);
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedPrivateMemoryInspection,
            privateInspection.Decision);
        AssertNoAuthority(metadata);
        AssertNoAuthority(privateInspection);
    }

    [Fact]
    public void CompatibilityAliasVisibility_RequiresProjectionPolicyButIsNotVmreadAuthority()
    {
        SecureComputeDomainDescriptor descriptor = GuestVisibleDescriptor(
            evidencePolicy: new SecureEvidencePolicy(
                allowGuestVisibleEvidence: false,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: true,
                allowDebugEvidence: false),
            compatibilityPolicy: new SecureCompatibilityProjectionPolicy(
                allowReadOnlyAliases: true,
                allowedAliasMask: 1));
        SecureDebugAttestationVisibilityResult allowed = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.CompatibilityAliasEvidence,
                descriptor,
                Measurement(SecureEvidenceVisibilityClass.CompatibilityAlias),
                NeutralAliasEvidence()));
        SecureDebugAttestationVisibilityResult deniedProjection = Policy.Classify(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.CompatibilityAliasEvidence,
                GuestVisibleDescriptor(),
                Measurement(SecureEvidenceVisibilityClass.CompatibilityAlias),
                NeutralAliasEvidence()));

        Assert.True(allowed.IsAllowed);
        Assert.True(allowed.GuestVisible);
        Assert.Equal(SecureEvidenceVisibilityClass.CompatibilityAlias, allowed.VisibilityClass);
        Assert.Equal(
            SecureDebugAttestationVisibilityDecision.DeniedCompatibilityProjectionPolicy,
            deniedProjection.Decision);
        AssertNoAuthority(allowed);
        AssertNoAuthority(deniedProjection);
    }

    [Fact]
    public void DebugAttestationVisibilitySources_DoNotCreateVmxVmreadMigrationPublicationBackendOrCompilerAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Debug/SecureDebugPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Debug/SecureDebugAttestationVisibilityPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/HostInspection/SecureHostInspectionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Measurement/DomainMeasurementDescriptor.cs");

        Assert.Contains("DeniedMigrationAuthority", source);
        Assert.Contains("DeniedVmreadAuthority", source);
        Assert.Contains("DeniedActivationEvidence", source);
        Assert.Contains("DeniedBackendOwnerProof", source);
        Assert.Contains("DeniedPrivateMemoryInspection", source);
        Assert.Contains("DeniedCompletionPublication", source);
        Assert.Contains("DeniedRetirePublication", source);
        Assert.Contains("CreatesRuntimeAuthority: false", source);
        Assert.Contains("CreatesVmreadAuthority: false", source);
        Assert.Contains("CreatesMigrationAuthority: false", source);
        Assert.Contains("CreatesActivationEvidence: false", source);
        Assert.Contains("CreatesBackendOwnerProof: false", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("SecureComputeControlledEmission", source);
    }

    private static SecureDebugAttestationVisibilityPolicy Policy =>
        SecureDebugAttestationVisibilityPolicy.FailClosed;

    private static SecureComputeDomainDescriptor DebugDescriptor() =>
        GuestVisibleDescriptor(
            evidencePolicy: new SecureEvidencePolicy(
                allowGuestVisibleEvidence: false,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: false,
                allowDebugEvidence: true),
            debugPolicy: new SecureDebugPolicy(
                SecureDebugMode.MeasuredDebugOnly,
                changesMeasurementClass: true));

    private static SecureComputeDomainDescriptor GuestVisibleDescriptor(
        SecureEvidencePolicy? evidencePolicy = null,
        SecureDebugPolicy? debugPolicy = null,
        SecureHostInspectionPolicy? hostInspectionPolicy = null,
        SecureCompatibilityProjectionPolicy? compatibilityPolicy = null) =>
        new(
            7,
            SecureComputeSecurityLevel.Measured,
            measurementRequired: true,
            privateMemoryRequired: false,
            hostInspectionPolicy ?? SecureHostInspectionPolicy.DenyAll,
            evidencePolicy ?? new SecureEvidencePolicy(
                allowGuestVisibleEvidence: true,
                allowMigrationSerializableEvidence: false,
                allowCompatibilityAliasEvidence: false,
                allowDebugEvidence: false),
            new SecureMigrationDescriptor(
                SecureMigrationMode.PolicyCompatible,
                SecurePrivateMemoryMigrationPolicy.Denied,
                new SecureRevocationEpoch(7),
                allowGuestVisibleEvidence: false,
                allowCompatibilityProjectionMetadata: false),
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            debugPolicy ?? SecureDebugPolicy.Denied,
            compatibilityPolicy ?? SecureCompatibilityProjectionPolicy.DenyAll);

    private static DomainMeasurementDescriptor Measurement(
        SecureEvidenceVisibilityClass evidenceClass,
        SecureMeasurementDebugClass debugClass = SecureMeasurementDebugClass.Production,
        SecureMeasurementState state = SecureMeasurementState.Materialized,
        ulong epoch = 7) =>
        new(
            new SecureMeasurementHandle(
                MeasurementId: 0x4D,
                ProvenanceHash: 0xA17E,
                Epoch: epoch),
            state,
            debugClass,
            policyDigest: 0xC0DE,
            memoryDigest: 0,
            runtimeDigest: 0xC0FFEE,
            evidenceClass,
            creatorDomainTag: 7,
            parentMeasurementId: 0,
            policySourceHash: 0x51);

    private static EvidencePolicyDescriptor NeutralGuestEvidence() =>
        new(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

    private static EvidencePolicyDescriptor NeutralAliasEvidence() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);

    private static void AssertNoAuthority(SecureDebugAttestationVisibilityResult result)
    {
        Assert.False(result.CreatesRuntimeAuthority);
        Assert.False(result.CreatesVmreadAuthority);
        Assert.False(result.CreatesMigrationAuthority);
        Assert.False(result.CreatesActivationEvidence);
        Assert.False(result.CreatesBackendOwnerProof);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
        Assert.False(result.CreatesAnyAuthority);
    }

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
