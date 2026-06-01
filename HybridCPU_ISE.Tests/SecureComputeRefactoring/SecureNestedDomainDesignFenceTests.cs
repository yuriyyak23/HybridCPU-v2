using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureNestedDomainDesignFenceTests
{
    [Fact]
    public void SecureNestedChildIntent_MissingNeutralOwnerIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            hasNeutralChildIntentOwner: false);

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedMissingChildIntentOwner,
            result.Decision);
        Assert.False(result.BackendSuccessAuthorized);
    }

    [Fact]
    public void SecureNestedChildIntent_MissingParentSecureDescriptorIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(provideParentDescriptor: false);

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedMissingParentSecureDescriptor,
            result.Decision);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("hypercall")]
    [InlineData("debug")]
    public void SecureNestedChildIntent_ChildPolicyWiderThanParentIsDenied(string authority)
    {
        SecureAuthorityBounds parent = authority switch
        {
            "io" => Bounds(allowsHypercalls: true, allowsDebug: true),
            "hypercall" => Bounds(allowsIo: true, allowsDebug: true),
            _ => Bounds(allowsIo: true, allowsHypercalls: true),
        };
        SecureAuthorityBounds child = authority switch
        {
            "io" => Bounds(allowsIo: true),
            "hypercall" => Bounds(allowsHypercalls: true),
            _ => Bounds(allowsDebug: true),
        };

        SecureNestedDomainAdmissionResult result = Admit(
            parentBounds: parent,
            childIntent: ChildIntent(child));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedChildPolicyExpansion,
            result.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_CompatibilityProjectionWiderThanParentIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            parentBounds: Bounds(allowsCompatibilityProjection: false),
            childIntent: ChildIntent(Bounds(allowsCompatibilityProjection: true)));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedChildCompatibilityProjectionExpansion,
            result.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_MigrationPayloadWiderThanParentIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            parentBounds: Bounds(allowsMigration: false),
            childIntent: ChildIntent(Bounds(allowsMigration: true)));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedChildMigrationPayloadExpansion,
            result.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_HostEvidenceLeakageIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            parentHostEvidenceExposedToChild: true);

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedHostEvidenceLeakage,
            result.Decision);
    }

    [Fact]
    public void SecureNestedProjection_CannotOpenMoreThanParentAllowed()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            nestedProjectionExceedsParent: true);

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedNestedProjectionExpansion,
            result.Decision);
    }

    [Fact]
    public void SecureNestedCheckpoint_RejectsVmcs12AndVmcs02Authority()
    {
        var policy = SecureNestedDomainAdmissionPolicy.Default;

        SecureNestedDomainAdmissionResult vmcs12 =
            policy.AdmitCheckpointPayload(SecureNestedCheckpointPayloadClass.Vmcs12Authority);
        SecureNestedDomainAdmissionResult vmcs02 =
            policy.AdmitCheckpointPayload(SecureNestedCheckpointPayloadClass.Vmcs02Authority);

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedNestedVmcsAuthority,
            vmcs12.Decision);
        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedNestedVmcsAuthority,
            vmcs02.Decision);
    }

    [Fact]
    public void SecureNestedShadowVmcs_RemainsCompatibilityBridgeOnly()
    {
        var policy = SecureNestedDomainAdmissionPolicy.Default;

        SecureNestedDomainAdmissionResult bridge =
            policy.AdmitCheckpointPayload(
                SecureNestedCheckpointPayloadClass.ShadowVmcsCompatibilityBridge);
        SecureNestedDomainAdmissionResult mutableAuthority =
            policy.AdmitCheckpointPayload(
                SecureNestedCheckpointPayloadClass.ShadowVmcsCompatibilityBridge,
                shadowVmcsStoresMutableAuthority: true);

        Assert.True(bridge.IsAllowed);
        Assert.False(bridge.BackendSuccessAuthorized);
        Assert.False(bridge.MutableNestedStateAuthorized);
        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedMutableShadowVmcsAuthority,
            mutableAuthority.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_StaleParentOrChildEpochFailsAdmission()
    {
        SecureNestedDomainAdmissionResult staleParent = Admit(
            childIntent: ChildIntent(
                Bounds(),
                new SecurePolicyDerivationRecord(
                    ParentPolicyDigest: 0xA,
                    ChildPolicyDigest: 0xB,
                    ProvenanceHash: 0xC,
                    ParentEpoch: 6,
                    ChildEpoch: 7)));
        SecureNestedDomainAdmissionResult staleChild = Admit(
            childIntent: ChildIntent(
                Bounds(),
                new SecurePolicyDerivationRecord(
                    ParentPolicyDigest: 0xA,
                    ChildPolicyDigest: 0xB,
                    ProvenanceHash: 0xC,
                    ParentEpoch: 7,
                    ChildEpoch: 6)));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedStaleParentChildEpoch,
            staleParent.Decision);
        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedStaleParentChildEpoch,
            staleChild.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_MissingDerivationProvenanceIsDenied()
    {
        SecureNestedDomainAdmissionResult result = Admit(
            childIntent: ChildIntent(
                Bounds(),
                new SecurePolicyDerivationRecord(
                    ParentPolicyDigest: 0xA,
                    ChildPolicyDigest: 0xB,
                    ProvenanceHash: 0,
                    ParentEpoch: 7,
                    ChildEpoch: 7)));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedChildIntentProvenanceMissing,
            result.Decision);
    }

    [Fact]
    public void SecureNestedChildIntent_ValidSubsetIsDesignFenceOnly()
    {
        SecureNestedDomainAdmissionResult result = Admit();

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.AllowedDesignFence,
            result.Decision);
        Assert.False(result.BackendSuccessAuthorized);
        Assert.False(result.MutableNestedStateAuthorized);
    }

    [Fact]
    public void Phase9SecureNestedSources_DoNotIntroduceIsaOrVmxAuthorityBackend()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs");

        Assert.Contains("DeniedNestedVmcsAuthority", source);
        Assert.Contains("DeniedMutableShadowVmcsAuthority", source);
        Assert.DoesNotContain("capability register", source);
        Assert.DoesNotContain("tagged memory", source);
        Assert.DoesNotContain("LoadStore", source);
        Assert.DoesNotContain("Decoder", source);
        Assert.DoesNotContain("Encoder", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("AllowBackendExecution = true", source);
    }

    private static SecureNestedDomainAdmissionResult Admit(
        SecureComputeDomainDescriptor? parentDescriptor = null,
        bool provideParentDescriptor = true,
        SecureAuthorityBounds? parentBounds = null,
        SecureChildDomainIntentDescriptor? childIntent = null,
        bool hasNeutralChildIntentOwner = true,
        bool parentHostEvidenceExposedToChild = false,
        bool childHostEvidenceExposedToParent = false,
        bool nestedProjectionExceedsParent = false,
        SecureNestedCheckpointPayloadClass checkpointPayloadClass =
            SecureNestedCheckpointPayloadClass.NeutralChildIntentDescriptor,
        bool shadowVmcsStoresMutableAuthority = false)
    {
        parentDescriptor = provideParentDescriptor
            ? parentDescriptor ?? ParentDescriptor()
            : null;
        parentBounds ??= Bounds();
        childIntent ??= ChildIntent(Bounds());

        return SecureNestedDomainAdmissionPolicy.Default.Admit(
            new SecureNestedDomainAdmissionRequest(
                parentDescriptor,
                parentBounds,
                childIntent,
                CurrentEpoch: new SecureRevocationEpoch(7),
                hasNeutralChildIntentOwner,
                parentHostEvidenceExposedToChild,
                childHostEvidenceExposedToParent,
                nestedProjectionExceedsParent,
                checkpointPayloadClass,
                shadowVmcsStoresMutableAuthority));
    }

    private static SecureComputeDomainDescriptor ParentDescriptor() =>
        new(
            domainTag: 7,
            securityLevel: SecureComputeSecurityLevel.Private,
            measurementRequired: false,
            privateMemoryRequired: false,
            hostInspectionPolicy: SecureHostInspectionPolicy.DenyAll,
            evidenceVisibilityPolicy: SecureEvidencePolicy.FailClosed,
            migrationPolicy: SecureMigrationDescriptor.Disabled,
            ioPolicy: SecureIoDomainDescriptor.Disabled,
            hypercallPolicy: SecureHypercallDescriptor.Disabled,
            debugPolicy: SecureDebugPolicy.Denied,
            compatibilityProjectionPolicy: SecureCompatibilityProjectionPolicy.DenyAll);

    private static SecureChildDomainIntentDescriptor ChildIntent(
        SecureAuthorityBounds requestedBounds,
        SecurePolicyDerivationRecord? derivation = null) =>
        new(
            parentDomainTag: 7,
            childDomainTag: 8,
            requestedSecurityLevel: SecureComputeSecurityLevel.Private,
            requestedBounds,
            derivation ?? new SecurePolicyDerivationRecord(
                ParentPolicyDigest: 0xA,
                ChildPolicyDigest: 0xB,
                ProvenanceHash: 0xC,
                ParentEpoch: 7,
                ChildEpoch: 7),
            state: SecureChildDomainIntentState.Declared);

    private static SecureAuthorityBounds Bounds(
        bool allowsPrivateMemory = false,
        bool allowsSharedMemory = false,
        bool allowsIo = false,
        bool allowsDma = false,
        bool allowsHypercalls = false,
        bool allowsDebug = false,
        bool allowsMigration = false,
        bool allowsCompatibilityProjection = false) =>
        new(
            allowsPrivateMemory,
            allowsSharedMemory,
            allowsIo,
            allowsDma,
            allowsHypercalls,
            allowsDebug,
            allowsMigration,
            allowsCompatibilityProjection);

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
