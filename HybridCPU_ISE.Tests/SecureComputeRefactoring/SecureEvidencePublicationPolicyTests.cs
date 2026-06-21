using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureEvidencePublicationPolicyTests
{
    [Fact]
    public void CompatibilityAliasEvidence_IsDeniedUnlessEveryExplicitPolicyAllowsIt()
    {
        const ulong aliasBit = 1UL << 5;
        var policy = SecureEvidencePublicationPolicy.Default;
        var secureAliasPolicy = new SecureEvidencePolicy(
            allowGuestVisibleEvidence: false,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: true,
            allowDebugEvidence: false);
        var neutralAliasPolicy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);
        var compatibilityPolicy = new SecureCompatibilityProjectionPolicy(
            allowReadOnlyAliases: true,
            allowedAliasMask: aliasBit);

        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedNeutralEvidencePolicy,
            policy.AdmitCompatibilityAliasEvidence(
                secureAliasPolicy,
                EvidencePolicyDescriptor.FailClosed,
                compatibilityPolicy,
                aliasBit).Decision);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedSecureEvidencePolicy,
            policy.AdmitCompatibilityAliasEvidence(
                SecureEvidencePolicy.FailClosed,
                neutralAliasPolicy,
                compatibilityPolicy,
                aliasBit).Decision);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedCompatibilityAliasPolicy,
            policy.AdmitCompatibilityAliasEvidence(
                secureAliasPolicy,
                neutralAliasPolicy,
                SecureCompatibilityProjectionPolicy.DenyAll,
                aliasBit).Decision);

        SecureEvidencePublicationResult allowed = policy.AdmitCompatibilityAliasEvidence(
            secureAliasPolicy,
            neutralAliasPolicy,
            compatibilityPolicy,
            aliasBit);

        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void CompletionPublication_IsDeniedBeforeFence()
    {
        var policy = SecureEvidencePublicationPolicy.Default;

        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedCompletionFence,
            policy.AdmitCompletionPublication(null).Decision);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedCompletionFence,
            policy.AdmitCompletionPublication(SecureCompletionPublicationFence.Denied).Decision);

        SecureEvidencePublicationResult allowed = policy.AdmitCompletionPublication(
            new SecureCompletionPublicationFence(
                SecureCompletionFenceState.CompletionAllowed,
                SecureRetirePublicationRule.CompletionFenceRequired));

        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void CompletionFence_DoesNotImplyRetireSideEffectPublication()
    {
        var policy = SecureEvidencePublicationPolicy.Default;
        var completionOnlyFence = new SecureCompletionPublicationFence(
            SecureCompletionFenceState.CompletionAllowed,
            SecureRetirePublicationRule.CompletionFenceRequired);

        SecureEvidencePublicationResult completion =
            policy.AdmitCompletionPublication(completionOnlyFence);
        SecureEvidencePublicationResult retire =
            policy.AdmitRetirePublication(completionOnlyFence);

        Assert.True(completion.IsAllowed);
        Assert.Equal(SecureEvidencePublicationDecision.DeniedRetireFence, retire.Decision);
    }

    [Fact]
    public void RetirePublication_RequiresExplicitRetireFence()
    {
        var policy = SecureEvidencePublicationPolicy.Default;
        var retireStateWithoutRule = new SecureCompletionPublicationFence(
            SecureCompletionFenceState.RetireAllowed,
            SecureRetirePublicationRule.CompletionFenceRequired);
        var explicitRetireFence = new SecureCompletionPublicationFence(
            SecureCompletionFenceState.RetireAllowed,
            SecureRetirePublicationRule.ExplicitRetireFenceRequired);

        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedRetireFence,
            policy.AdmitRetirePublication(retireStateWithoutRule).Decision);
        Assert.True(policy.AdmitRetirePublication(explicitRetireFence).IsAllowed);
    }

    [Theory]
    [InlineData(SecureCompletionFenceState.Missing, SecureRetirePublicationRule.Denied, false, false)]
    [InlineData(SecureCompletionFenceState.Pending, SecureRetirePublicationRule.Denied, false, false)]
    [InlineData(SecureCompletionFenceState.CompletionAllowed, SecureRetirePublicationRule.CompletionFenceRequired, true, false)]
    [InlineData(SecureCompletionFenceState.CompletionAllowed, SecureRetirePublicationRule.ExplicitRetireFenceRequired, true, false)]
    [InlineData(SecureCompletionFenceState.RetireAllowed, SecureRetirePublicationRule.CompletionFenceRequired, true, false)]
    [InlineData(SecureCompletionFenceState.RetireAllowed, SecureRetirePublicationRule.ExplicitRetireFenceRequired, true, true)]
    public void SecureCompletionPublicationFence_MatrixKeepsCompletionAndRetireSeparate(
        SecureCompletionFenceState state,
        SecureRetirePublicationRule retireRule,
        bool expectedCompletion,
        bool expectedRetire)
    {
        var fence = new SecureCompletionPublicationFence(state, retireRule);
        var policy = SecureEvidencePublicationPolicy.Default;

        Assert.Equal(expectedCompletion, fence.CanPublishCompletion);
        Assert.Equal(expectedRetire, fence.CanPublishRetire);
        Assert.Equal(
            expectedCompletion,
            policy.AdmitCompletionPublication(fence).IsAllowed);
        Assert.Equal(
            expectedRetire,
            policy.AdmitRetirePublication(fence).IsAllowed);
    }

    [Fact]
    public void Lane6Lane7SidebandEvidence_RespectsVisibilityClassAndPolicy()
    {
        const ulong aliasBit = 1UL << 6;
        var policy = SecureEvidencePublicationPolicy.Default;
        var secureGuestPolicy = new SecureEvidencePolicy(
            allowGuestVisibleEvidence: true,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: false,
            allowDebugEvidence: false);
        var secureAliasPolicy = new SecureEvidencePolicy(
            allowGuestVisibleEvidence: false,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: true,
            allowDebugEvidence: false);
        var neutralGuestPolicy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);
        var neutralAliasPolicy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);
        var compatibilityPolicy = new SecureCompatibilityProjectionPolicy(
            allowReadOnlyAliases: true,
            allowedAliasMask: aliasBit);
        var guestVisibleLane6 = new SecureComputeEvidenceSidebandEnvelope(
            SecureComputeEvidenceSidebandClass.GuestVisible,
            domainTag: 7,
            evidenceHash: 0x600D);
        var compatibilityLane7 = new SecureComputeEvidenceSidebandEnvelope(
            SecureComputeEvidenceSidebandClass.CompatibilityAlias,
            domainTag: 7,
            evidenceHash: 0x700D);
        var hostOwnedLane7 = new SecureComputeEvidenceSidebandEnvelope(
            SecureComputeEvidenceSidebandClass.HostOwnedQuarantined,
            domainTag: 7,
            evidenceHash: 0xBAD);

        Assert.True(policy.AdmitSidebandEnvelope(
            guestVisibleLane6,
            secureGuestPolicy,
            neutralGuestPolicy).IsAllowed);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedSidebandVisibility,
            policy.AdmitSidebandEnvelope(
                compatibilityLane7,
                secureAliasPolicy,
                neutralAliasPolicy).Decision);
        Assert.True(policy.AdmitSidebandEnvelope(
            compatibilityLane7,
            secureAliasPolicy,
            neutralAliasPolicy,
            compatibilityPolicy,
            aliasBit).IsAllowed);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedHostOwnedEvidence,
            policy.AdmitSidebandEnvelope(
                hostOwnedLane7,
                secureGuestPolicy,
                neutralGuestPolicy).Decision);
    }

    [Fact]
    public void EvidenceVisibility_DoesNotCreateMigrationCompletionOrCheckpointAuthority()
    {
        var policy = SecureEvidencePublicationPolicy.Default;
        var secureGuestPolicy = new SecureEvidencePolicy(
            allowGuestVisibleEvidence: true,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: false,
            allowDebugEvidence: false);
        var secureDebugPolicy = new SecureEvidencePolicy(
            allowGuestVisibleEvidence: false,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: false,
            allowDebugEvidence: true);
        var neutralGuestPolicy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

        Assert.True(policy.AdmitGuestVisibleEvidence(
            secureGuestPolicy,
            neutralGuestPolicy,
            SecureEvidenceVisibilityClass.GuestVisible).IsAllowed);
        Assert.True(policy.AdmitGuestVisibleEvidence(
            secureDebugPolicy,
            neutralGuestPolicy,
            SecureEvidenceVisibilityClass.DebugOnly).IsAllowed);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedCompletionFence,
            policy.AdmitCompletionPublication(null).Decision);
        Assert.False(secureGuestPolicy.CanSerializeAcrossMigration(
            neutralGuestPolicy,
            SecureEvidenceVisibilityClass.GuestVisible));
        Assert.False(secureDebugPolicy.CanSerializeAcrossMigration(
            neutralGuestPolicy,
            SecureEvidenceVisibilityClass.DebugOnly));
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedHostOwnedEvidence,
            policy.AdmitGuestVisibleEvidence(
                secureGuestPolicy,
                neutralGuestPolicy,
                SecureEvidenceVisibilityClass.HostOwnedQuarantined).Decision);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedRecomputedEvidence,
            policy.AdmitGuestVisibleEvidence(
                secureGuestPolicy,
                neutralGuestPolicy,
                SecureEvidenceVisibilityClass.RecomputedAfterRestore).Decision);
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState,
            SecureCheckpointPayloadPolicy.FailClosed.Classify(
                SecureCheckpointPayloadClass.DebugTrace));
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,
            SecureCheckpointPayloadPolicy.FailClosed.Classify(
                SecureCheckpointPayloadClass.HostOwnedEvidence));
        Assert.Equal(
            SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret,
            SecureCheckpointPayloadPolicy.FailClosed.Classify(
                SecureCheckpointPayloadClass.RawMeasurementSecret));
    }

    [Fact]
    public void ReplayOrRollback_CannotReuseStaleEvidenceEpoch()
    {
        var policy = SecureEvidencePublicationPolicy.Default;
        DomainMeasurementDescriptor currentMeasurement = CreateMeasurement(
            SecureMeasurementState.Materialized,
            epoch: 7);
        DomainMeasurementDescriptor staleMeasurement = CreateMeasurement(
            SecureMeasurementState.Materialized,
            epoch: 6);
        DomainMeasurementDescriptor revokedMeasurement = CreateMeasurement(
            SecureMeasurementState.Revoked,
            epoch: 7);

        Assert.True(policy.AdmitReplayOrRestoreEvidence(
            currentMeasurement,
            new SecureRevocationEpoch(7)).IsAllowed);
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedStaleEvidenceEpoch,
            policy.AdmitReplayOrRestoreEvidence(
                staleMeasurement,
                new SecureRevocationEpoch(7)).Decision);
        Assert.Equal(
            SecureComputeMigrationReplayViolation.EpochRollback,
            new SecureComputeMigrationReplayContract().Validate(
                epochRollback: staleMeasurement.MeasurementEpoch < currentMeasurement.MeasurementEpoch,
                vmcsProjectionAuthority: false,
                compatibilityMetadataAuthority: false,
                privateMemoryWithoutSealedPayload: false));
        Assert.Equal(
            SecureEvidencePublicationDecision.DeniedStaleEvidenceEpoch,
            policy.AdmitReplayOrRestoreEvidence(
                revokedMeasurement,
                new SecureRevocationEpoch(7)).Decision);
    }

    [Fact]
    public void SecureEvidencePublicationSources_DoNotCreateVmcsVmxCapsOrVmreadAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePublicationPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionPublicationFence.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Sideband/EvidenceTransport/SecureComputeEvidenceSidebandEnvelope.cs");

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
    }

    private static DomainMeasurementDescriptor CreateMeasurement(
        SecureMeasurementState state,
        ulong epoch) =>
        new(
            new SecureMeasurementHandle(
                MeasurementId: 0x4D,
                ProvenanceHash: 0xA17E,
                Epoch: epoch),
            state,
            SecureMeasurementDebugClass.Production,
            policyDigest: 0xC0DE,
            memoryDigest: 0,
            runtimeDigest: 0xC0FFEE,
            SecureEvidenceVisibilityClass.GuestVisible,
            creatorDomainTag: 7,
            parentMeasurementId: 0,
            policySourceHash: 0x51);

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
