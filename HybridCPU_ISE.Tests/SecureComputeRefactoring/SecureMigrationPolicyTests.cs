using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureMigrationPolicyTests
{
    [Fact]
    public void SecureCheckpointPayloads_RejectHostEvidenceVmcsCompatibilityAndRawSecrets()
    {
        var policy = SecureMigrationAdmissionPolicy.Default;
        SecureMigrationDescriptor migration = CreateMigration();

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
            policy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.HostOwnedEvidence).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedVmcsProjectionAuthority,
            policy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.VmcsProjectionMetadata).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedCompatibilityMetadataAuthority,
            policy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.CompatibilityProjectionMetadata).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedRawSecret,
            policy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.RawSealingKey).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedActiveHostPointer,
            policy.AdmitCheckpointPayload(
                migration,
                SecureCheckpointPayloadClass.ActiveHostPointer).Decision);
    }

    [Theory]
    [InlineData(
        SecureCheckpointPayloadClass.HostOwnedEvidence,
        SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.SchedulerEvidence,
        SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.BackendBindingEvidence,
        SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.NativeTokenEvidence,
        SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.VmcsProjectionMetadata,
        SecureMigrationAdmissionDecision.DeniedVmcsProjectionAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.CompatibilityProjectionMetadata,
        SecureMigrationAdmissionDecision.DeniedCompatibilityMetadataAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.RawMeasurementSecret,
        SecureMigrationAdmissionDecision.DeniedRawSecret)]
    [InlineData(
        SecureCheckpointPayloadClass.ActiveHostPointer,
        SecureMigrationAdmissionDecision.DeniedActiveHostPointer)]
    [InlineData(
        SecureCheckpointPayloadClass.RawSealingKey,
        SecureMigrationAdmissionDecision.DeniedRawSecret)]
    public void SecureMigrationAdmissionPolicy_ForbiddenAuthorityPayloadsRemainDenied(
        SecureCheckpointPayloadClass payloadClass,
        SecureMigrationAdmissionDecision expectedDecision)
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitCheckpointPayload(
                CreateMigration(),
                payloadClass);

        Assert.Equal(expectedDecision, result.Decision);
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void SecureCheckpointPayloads_RejectDebugTraceAsGuestState()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitCheckpointPayload(
                CreateMigration(),
                SecureCheckpointPayloadClass.DebugTrace);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedDebugTraceAsGuestState,
            result.Decision);
    }

    [Fact]
    public void PrivateMemoryPayload_IsDeniedWithoutCompleteSealedEncryptedContract()
    {
        var policy = SecureMigrationAdmissionPolicy.Default;
        SecureMigrationDescriptor migrationWithoutPrivatePayload = CreateMigration(
            privateMemoryPolicy: SecurePrivateMemoryMigrationPolicy.Denied);
        SecureMigrationDescriptor sealedPrivateMigration = CreateMigration(
            privateMemoryPolicy: SecurePrivateMemoryMigrationPolicy.SealedEncryptedPayloadRequired);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            policy.AdmitCheckpointPayload(
                migrationWithoutPrivatePayload,
                SecureCheckpointPayloadClass.SecurePrivateMemory,
                CompleteSealedContract()).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            policy.AdmitCheckpointPayload(
                sealedPrivateMigration,
                SecureCheckpointPayloadClass.SecurePrivateMemory,
                new SecurePrivateMemorySealedPayloadContract(
                    HasSealedPayload: true,
                    HasEncryptedPayload: true,
                    HasNeutralKeyOwner: false,
                    HasEvidencePolicy: true,
                    HasRestoreValidationProof: true)).Decision);

        SecureMigrationAdmissionResult allowed = policy.AdmitCheckpointPayload(
            sealedPrivateMigration,
            SecureCheckpointPayloadClass.SecurePrivateMemory,
            CompleteSealedContract());

        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureRestore_RejectsWrongRestorePolicyAndEpochRollback()
    {
        var policy = SecureMigrationAdmissionPolicy.Default;
        DomainMeasurementDescriptor measurement = CreateMeasurement(epoch: 7);
        SecureMigrationDescriptor deniedRestorePolicy = CreateMigration(
            measurementPolicy: SecureMeasurementRestorePolicy.Denied);
        SecureMigrationDescriptor staleMigration = CreateMigration(policyEpoch: 6);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedRestorePolicy,
            policy.AdmitRestore(
                deniedRestorePolicy,
                measurement,
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: true,
                reattestationCompleted: false).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPolicyEpochRollback,
            policy.AdmitRestore(
                staleMigration,
                measurement,
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: true,
                reattestationCompleted: false).Decision);
    }

    [Fact]
    public void SecureRestore_RejectsStaleGrantEpoch()
    {
        var policy = SecureMigrationAdmissionPolicy.Default;
        SecureGrantHandle staleGrant = new(
            SecureGrantHandleKind.MigrationPolicy,
            LocalId: 0xA,
            ProvenanceHash: 0xB,
            Epoch: 6);

        SecureMigrationAdmissionResult result = policy.AdmitRestore(
            CreateMigration(),
            CreateMeasurement(epoch: 7),
            memory: null,
            expectedPolicyEpoch: new SecureRevocationEpoch(7),
            restoredGrant: staleGrant,
            measurementRevalidated: true,
            reattestationCompleted: false);

        Assert.Equal(SecureMigrationAdmissionDecision.DeniedStaleGrantEpoch, result.Decision);
    }

    [Fact]
    public void SecureRestore_RequiresRevalidationOrReattestationWhenPolicySaysSo()
    {
        var policy = SecureMigrationAdmissionPolicy.Default;
        DomainMeasurementDescriptor measurement = CreateMeasurement(epoch: 7);
        SecureMigrationDescriptor revalidateMigration = CreateMigration(
            measurementPolicy: SecureMeasurementRestorePolicy.Revalidate);
        SecureMigrationDescriptor reattestMigration = CreateMigration(
            mode: SecureMigrationMode.ReattestRequired,
            measurementPolicy: SecureMeasurementRestorePolicy.Reattest);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedMeasurementRevalidationRequired,
            policy.AdmitRestore(
                revalidateMigration,
                measurement,
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: false,
                reattestationCompleted: false).Decision);
        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedMeasurementReattestationRequired,
            policy.AdmitRestore(
                reattestMigration,
                measurement,
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: true,
                reattestationCompleted: false).Decision);

        SecureMigrationAdmissionResult allowed = policy.AdmitRestore(
            reattestMigration,
            measurement,
            memory: null,
            expectedPolicyEpoch: new SecureRevocationEpoch(7),
            restoredGrant: SecureGrantHandle.None,
            measurementRevalidated: true,
            reattestationCompleted: true);

        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void SecureRestore_RejectsStaleMeasurementEpoch()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitRestore(
                CreateMigration(),
                CreateMeasurement(epoch: 6),
                memory: null,
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: true,
                reattestationCompleted: false);

        Assert.Equal(SecureMigrationAdmissionDecision.DeniedStaleMeasurementEpoch, result.Decision);
    }

    [Fact]
    public void SecureRestore_PrivateMemoryIsDeniedWhenPolicyDoesNotAllowMigration()
    {
        SecureMigrationAdmissionResult result =
            SecureMigrationAdmissionPolicy.Default.AdmitRestore(
                CreateMigration(privateMemoryPolicy: SecurePrivateMemoryMigrationPolicy.Denied),
                CreateMeasurement(epoch: 7),
                CreatePrivateMemory(),
                expectedPolicyEpoch: new SecureRevocationEpoch(7),
                restoredGrant: SecureGrantHandle.None,
                measurementRevalidated: true,
                reattestationCompleted: false);

        Assert.Equal(
            SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            result.Decision);
    }

    [Fact]
    public void SecureCheckpointPayloadPolicy_RejectsCompatibilityMetadataPointersAndSealingKeys()
    {
        SecureCheckpointPayloadPolicy policy = SecureCheckpointPayloadPolicy.FailClosed;

        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority,
            policy.Classify(SecureCheckpointPayloadClass.CompatibilityProjectionMetadata));
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedActiveHostPointer,
            policy.Classify(SecureCheckpointPayloadClass.ActiveHostPointer));
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedRawSealingKey,
            policy.Classify(SecureCheckpointPayloadClass.RawSealingKey));
    }

    [Theory]
    [InlineData(
        SecureCheckpointPayloadClass.HostOwnedEvidence,
        SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.SchedulerEvidence,
        SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.BackendBindingEvidence,
        SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.NativeTokenEvidence,
        SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.VmcsProjectionMetadata,
        SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.CompatibilityProjectionMetadata,
        SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.RawMeasurementSecret,
        SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret)]
    [InlineData(
        SecureCheckpointPayloadClass.ActiveHostPointer,
        SecureCheckpointPayloadDecision.DeniedActiveHostPointer)]
    [InlineData(
        SecureCheckpointPayloadClass.RawSealingKey,
        SecureCheckpointPayloadDecision.DeniedRawSealingKey)]
    public void SecureCheckpointPayloadPolicy_ForbiddenAuthorityPayloadsAreNeverSerializable(
        SecureCheckpointPayloadClass payloadClass,
        SecureCheckpointPayloadDecision expectedDecision)
    {
        SecureCheckpointPayloadDecision decision =
            SecureCheckpointPayloadPolicy.FailClosed.Classify(payloadClass);

        Assert.Equal(expectedDecision, decision);
        Assert.NotEqual(SecureCheckpointPayloadDecision.Allowed, decision);
    }

    [Fact]
    public void SecureCheckpointPayloadPolicy_AllPayloadClassesHaveStableClassification()
    {
        IReadOnlyDictionary<SecureCheckpointPayloadClass, SecureCheckpointPayloadDecision> expected = new Dictionary<SecureCheckpointPayloadClass, SecureCheckpointPayloadDecision>
        {
            [SecureCheckpointPayloadClass.GuestVisibleState] = SecureCheckpointPayloadDecision.Allowed,
            [SecureCheckpointPayloadClass.SecurePolicyDescriptor] = SecureCheckpointPayloadDecision.Allowed,
            [SecureCheckpointPayloadClass.SecureSharedMemory] = SecureCheckpointPayloadDecision.Allowed,
            [SecureCheckpointPayloadClass.SecurePrivateMemory] = SecureCheckpointPayloadDecision.DeniedPrivateMemoryWithoutSealedPayload,
            [SecureCheckpointPayloadClass.HostOwnedEvidence] = SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.SchedulerEvidence] = SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.BackendBindingEvidence] = SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.NativeTokenEvidence] = SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.DebugTrace] = SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState,
            [SecureCheckpointPayloadClass.VmcsProjectionMetadata] = SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority,
            [SecureCheckpointPayloadClass.RawMeasurementSecret] = SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret,
            [SecureCheckpointPayloadClass.CompatibilityProjectionMetadata] = SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority,
            [SecureCheckpointPayloadClass.ActiveHostPointer] = SecureCheckpointPayloadDecision.DeniedActiveHostPointer,
            [SecureCheckpointPayloadClass.RawSealingKey] = SecureCheckpointPayloadDecision.DeniedRawSealingKey,
        };

        Assert.Equal(
            Enum.GetValues<SecureCheckpointPayloadClass>().OrderBy(static payloadClass => payloadClass),
            expected.Keys.OrderBy(static payloadClass => payloadClass));

        foreach ((SecureCheckpointPayloadClass payloadClass, SecureCheckpointPayloadDecision expectedDecision) in expected)
        {
            Assert.Equal(
                expectedDecision,
                SecureCheckpointPayloadPolicy.FailClosed.Classify(payloadClass));
        }
    }

    [Fact]
    public void SecureMigrationAdmissionPolicy_AllPayloadClassesHaveStableCheckpointAdmissionOutcome()
    {
        IReadOnlyDictionary<SecureCheckpointPayloadClass, SecureMigrationAdmissionDecision> expected = new Dictionary<SecureCheckpointPayloadClass, SecureMigrationAdmissionDecision>
        {
            [SecureCheckpointPayloadClass.GuestVisibleState] = SecureMigrationAdmissionDecision.Allowed,
            [SecureCheckpointPayloadClass.SecurePolicyDescriptor] = SecureMigrationAdmissionDecision.Allowed,
            [SecureCheckpointPayloadClass.SecureSharedMemory] = SecureMigrationAdmissionDecision.Allowed,
            [SecureCheckpointPayloadClass.SecurePrivateMemory] = SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
            [SecureCheckpointPayloadClass.HostOwnedEvidence] = SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.SchedulerEvidence] = SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.BackendBindingEvidence] = SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.NativeTokenEvidence] = SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
            [SecureCheckpointPayloadClass.DebugTrace] = SecureMigrationAdmissionDecision.DeniedDebugTraceAsGuestState,
            [SecureCheckpointPayloadClass.VmcsProjectionMetadata] = SecureMigrationAdmissionDecision.DeniedVmcsProjectionAuthority,
            [SecureCheckpointPayloadClass.RawMeasurementSecret] = SecureMigrationAdmissionDecision.DeniedRawSecret,
            [SecureCheckpointPayloadClass.CompatibilityProjectionMetadata] = SecureMigrationAdmissionDecision.DeniedCompatibilityMetadataAuthority,
            [SecureCheckpointPayloadClass.ActiveHostPointer] = SecureMigrationAdmissionDecision.DeniedActiveHostPointer,
            [SecureCheckpointPayloadClass.RawSealingKey] = SecureMigrationAdmissionDecision.DeniedRawSecret,
        };

        Assert.Equal(
            Enum.GetValues<SecureCheckpointPayloadClass>().OrderBy(static payloadClass => payloadClass),
            expected.Keys.OrderBy(static payloadClass => payloadClass));

        SecureMigrationDescriptor migration = CreateMigration();
        foreach ((SecureCheckpointPayloadClass payloadClass, SecureMigrationAdmissionDecision expectedDecision) in expected)
        {
            Assert.Equal(
                expectedDecision,
                SecureMigrationAdmissionPolicy.Default.AdmitCheckpointPayload(migration, payloadClass).Decision);
        }
    }

    [Fact]
    public void SecureMigrationSources_DoNotCreateVmcsVmreadOrCompatibilityMetadataAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Migration/SecureMigrationDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Migration/SecureMigrationAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs");

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.Contains("DeniedCompatibilityMetadataAuthority", source);
        Assert.Contains("DeniedVmcsProjectionAuthority", source);
        Assert.Contains("DeniedHostOwnedEvidence", source);
        Assert.Contains("DeniedRawMeasurementSecret", source);
        Assert.Contains("DeniedActiveHostPointer", source);
        Assert.Contains("DeniedRawSealingKey", source);
        Assert.Contains("SecureCheckpointPayloadClass.HostOwnedEvidence", source);
        Assert.Contains("SecureCheckpointPayloadClass.SchedulerEvidence", source);
        Assert.Contains("SecureCheckpointPayloadClass.BackendBindingEvidence", source);
        Assert.Contains("SecureCheckpointPayloadClass.NativeTokenEvidence", source);
        Assert.Contains("SecureCheckpointPayloadClass.VmcsProjectionMetadata", source);
        Assert.Contains("SecureCheckpointPayloadClass.CompatibilityProjectionMetadata", source);
        Assert.Contains("SecureCheckpointPayloadClass.RawMeasurementSecret", source);
        Assert.Contains("SecureCheckpointPayloadClass.ActiveHostPointer", source);
        Assert.Contains("SecureCheckpointPayloadClass.RawSealingKey", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("RuntimeOwnedPublication", source);
        Assert.DoesNotContain("TrapCompletionRoute", source);
        Assert.DoesNotContain("CompletionRecord", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("CanSerialize", source);
        Assert.DoesNotContain("Serialize(", source);
        Assert.DoesNotContain("Serializable", source);
    }

    private static SecureMigrationDescriptor CreateMigration(
        SecureMigrationMode mode = SecureMigrationMode.PolicyCompatible,
        SecurePrivateMemoryMigrationPolicy privateMemoryPolicy =
            SecurePrivateMemoryMigrationPolicy.ReinitializeAfterRestore,
        ulong policyEpoch = 7,
        SecureMeasurementRestorePolicy measurementPolicy = SecureMeasurementRestorePolicy.Revalidate,
        SecureGrantRestorePolicy grantPolicy = SecureGrantRestorePolicy.Rederive) =>
        new(
            mode,
            privateMemoryPolicy,
            new SecureRevocationEpoch(policyEpoch),
            allowGuestVisibleEvidence: true,
            allowCompatibilityProjectionMetadata: false,
            measurementPolicy,
            grantPolicy);

    private static SecurePrivateMemorySealedPayloadContract CompleteSealedContract() =>
        new(
            HasSealedPayload: true,
            HasEncryptedPayload: true,
            HasNeutralKeyOwner: true,
            HasEvidencePolicy: true,
            HasRestoreValidationProof: true);

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
                    0x1000,
                    SecureMemoryHostVisibility.Denied,
                    7),
            });

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
