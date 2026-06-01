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
